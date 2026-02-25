using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Application.Abstractions.Observability;
using MyApp.Domain.Common;
using MyApp.Presentation.Observability.Options;
using MyApp.Presentation.Observability.Redaction;
using System.Diagnostics;
using System.Text;

namespace MyApp.Presentation.Observability.Middleware;

public sealed class BodyOnErrorLoggingMiddleware
{
    public const string ItemKey = "__BodyLogKey";

    private readonly RequestDelegate _next;
    private readonly ILogger<BodyOnErrorLoggingMiddleware> _logger;
    private readonly IFailedHttpPayloadStore _store;

    // snapshot bez alokacji per-request
    private volatile Snapshot _snapshot;

    public BodyOnErrorLoggingMiddleware(
        RequestDelegate next,
        ILogger<BodyOnErrorLoggingMiddleware> logger,
        IFailedHttpPayloadStore store,
        IOptionsMonitor<BodyLoggingOptions> options)
    {
        _next = next;
        _logger = logger;
        _store = store;

        _snapshot = Snapshot.From(options.CurrentValue);
        options.OnChange((o, _) => _snapshot = Snapshot.From(o));
    }

    public async Task Invoke(HttpContext ctx)
    {
        var s = _snapshot;

        if (!s.Enabled || s.Mode == BodyLoggingMode.Off)
        {
            await _next(ctx);
            return;
        }

        // Request capture gate
        var mayHaveBodyMethod =
            HttpMethods.IsPost(ctx.Request.Method) ||
            HttpMethods.IsPut(ctx.Request.Method) ||
            HttpMethods.IsPatch(ctx.Request.Method);

        var limitBytes = s.MaxBytes;
        var reqContentType = ctx.Request.ContentType;
        var reqLen = ctx.Request.ContentLength;

        var allowReqType = IsAllowedContentType(reqContentType, s.ContentTypesAllowList);

        var canCaptureRequest =
            limitBytes > 0 &&
            mayHaveBodyMethod &&
            allowReqType &&
            (
                (reqLen is not null && reqLen.Value <= s.MaxRequestContentLengthToCapture) ||
                (reqLen is null && s.AllowUnknownContentLength)
            );

        if (canCaptureRequest)
        {
            try { ctx.Request.EnableBuffering(); } catch { /* ignore */ }
        }

        // Response capture (tee)
        var originalBody = ctx.Response.Body;
        TeeLimitedBufferingStream? tee = null;

        if (limitBytes > 0)
        {
            tee = new TeeLimitedBufferingStream(originalBody, limitBytes);
            ctx.Response.Body = tee;
        }

        try
        {
            await _next(ctx);
        }
        finally
        {
            // zawsze przywróć body (nawet gdy _next rzuci wyjątek)
            ctx.Response.Body = originalBody;
        }

        // policy + decyzja po _next (status i content-type są już ustawione)
        var policy = BodyLogPolicyHttpContext.Get(ctx);

        var doCapture =
            policy == BodyLogPolicy.Force ||
            (policy == BodyLogPolicy.Default && ShouldCaptureForMode(s.Mode, ctx.Response.StatusCode));

        if (policy == BodyLogPolicy.Suppress)
            doCapture = false;

        if (!doCapture)
        {
            if (tee is not null) await tee.DisposeAsync();
            return;
        }

        // bodies (lazy)
        string? requestBody = null;
        string? responseBody = null;

        if (tee is not null)
        {
            try
            {
                // czytaj PRZED dispose
                responseBody = tee.BufferedCount > 0 ? tee.ReadBufferedAsString() : null;
            }
            catch
            {
                // ignore
            }

            await tee.DisposeAsync();
        }

        if (canCaptureRequest)
            requestBody = await ReadRequestBodySafe(ctx, limitBytes);

        // allow-list content-type na etapie ekspozycji/storu
        if (!IsAllowedContentType(reqContentType, s.ContentTypesAllowList))
            requestBody = null;

        if (!IsAllowedContentType(ctx.Response.ContentType, s.ContentTypesAllowList))
            responseBody = null;

        // redaction JSON (PII / secrets)
        requestBody = RedactIfJson(requestBody, reqContentType, s.JsonDenyPaths);
        responseBody = RedactIfJson(responseBody, ctx.Response.ContentType, s.JsonDenyPaths);

        // log (zawsze, gdy doCapture)
        _logger.LogWarning(
            LogEvents.BodyCaptured,
            "HTTP payload captured. Status={Status} Policy={Policy} RequestBody={RequestBody} ResponseBody={ResponseBody}",
            ctx.Response.StatusCode,
            policy,
            requestBody,
            responseBody);

        // store (tylko gdy włączony)
        if (s.StoreModeIsNone)
            return;

        try
        {
            var correlationId =
                CorrelationIdMiddleware.TryGet(ctx)
                ?? ctx.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();

            var requestId = ctx.TraceIdentifier;
            var traceId = Activity.Current?.TraceId.ToString();
            var spanId = Activity.Current?.SpanId.ToString();

            var keyBase = !string.IsNullOrWhiteSpace(correlationId)
                ? correlationId!
                : traceId ?? requestId;

            var key = $"{s.StoreKeyPrefix}:{keyBase}:{requestId}";

            var userId =
                ctx.User?.FindFirst("sub")?.Value
                ?? ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // domyślnie bez nagłówków (PII risk)
            IReadOnlyDictionary<string, string>? headers = null;

            var payload = new FailedHttpPayload(
                Key: key,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                CorrelationId: correlationId,
                TraceId: traceId,
                SpanId: spanId,
                RequestId: requestId,
                Method: ctx.Request.Method,
                Path: ctx.Request.Path.Value ?? string.Empty,
                StatusCode: ctx.Response.StatusCode,
                RequestContentType: reqContentType,
                ResponseContentType: ctx.Response.ContentType,
                UserId: userId,
                RemoteIp: ctx.Connection.RemoteIpAddress?.ToString(),
                Headers: headers,
                RequestBody: requestBody,
                ResponseBody: responseBody);

            var ttl = TimeSpan.FromMinutes(Math.Max(1, s.StoreTtlMinutes));

            await _store.TryStoreAsync(payload, ttl, ctx.RequestAborted);

            ctx.Items[ItemKey] = key;

            _logger.LogWarning(
                "HTTP payload stored. Key={Key} Status={Status} {Method} {Path}",
                key,
                ctx.Response.StatusCode,
                ctx.Request.Method,
                ctx.Request.Path.Value);
        }
        catch
        {
            // store nigdy nie może psuć requestu
        }
    }

    private static bool ShouldCaptureForMode(BodyLoggingMode mode, int statusCode) =>
        mode switch
        {
            BodyLoggingMode.Always => true,
            BodyLoggingMode.OnServerError => statusCode >= 500,
            BodyLoggingMode.OnError => statusCode >= 400,
            _ => false
        };

    private static bool IsAllowedContentType(string? contentType, string[] allowList)
    {
        if (allowList is null || allowList.Length == 0)
            return MayHaveJsonContentType(contentType);

        if (string.IsNullOrWhiteSpace(contentType)) return false;
        var ct = contentType.Split(';')[0].Trim();

        return allowList.Any(x => string.Equals(x, ct, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MayHaveJsonContentType(string? contentType)
    {
        var ct = contentType ?? "";
        return ct.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || ct.Contains("application/problem+json", StringComparison.OrdinalIgnoreCase);
    }

    private static string? RedactIfJson(string? body, string? contentType, string[] denyPaths)
    {
        if (string.IsNullOrWhiteSpace(body)) return body;
        if (!MayHaveJsonContentType(contentType)) return body;

        var deny = new HashSet<string>(denyPaths ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        return JsonBodyRedactor.RedactIfJson(body, deny);
    }

    private static async Task<string?> ReadRequestBodySafe(HttpContext ctx, int limitBytes)
    {
        try
        {
            if (!ctx.Request.Body.CanSeek)
                return null;

            // kluczowe: czytamy od początku
            ctx.Request.Body.Position = 0;

            using var sr = new StreamReader(
                ctx.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);

            var text = await ReadTextLimited(sr, limitBytes);

            ctx.Request.Body.Position = 0;

            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch
        {
            if (ctx.Request.Body.CanSeek)
                ctx.Request.Body.Position = 0;

            return null;
        }
    }

    private static async Task<string> ReadTextLimited(TextReader tr, int limitBytes)
    {
        if (limitBytes <= 0) return string.Empty;

        var sb = new StringBuilder(capacity: Math.Min(limitBytes, 4096));
        var buf = new char[1024];
        var totalBytes = 0;

        while (true)
        {
            var read = await tr.ReadAsync(buf, 0, buf.Length);
            if (read <= 0) break;

            sb.Append(buf, 0, read);
            totalBytes += Encoding.UTF8.GetByteCount(buf, 0, read);

            if (totalBytes >= limitBytes)
            {
                sb.Append("…(truncated)");
                break;
            }
        }

        return sb.ToString();
    }

    private sealed record class Snapshot(
        bool Enabled,
        int MaxBytes,
        BodyLoggingMode Mode,
        long MaxRequestContentLengthToCapture,
        bool AllowUnknownContentLength,
        string[] ContentTypesAllowList,
        string[] JsonDenyPaths,
        string StoreMode,
        int StoreTtlMinutes,
        string StoreKeyPrefix)
    {
        public bool StoreModeIsNone =>
            string.Equals(StoreMode, "None", StringComparison.OrdinalIgnoreCase);

        public static Snapshot From(BodyLoggingOptions o)
        {
            // hard-guard na “nieskończone” wartości z configu
            var max = o.MaxBytes;
            if (max < 0) max = 0;
            if (max > 1024 * 1024) max = 1024 * 1024;

            // Store.Mode może być enumem lub stringiem -> Convert.ToString działa dla obu
            var storeMode = Convert.ToString(o.Store?.Mode) ?? "None";

            var keyPrefix = o.Store?.KeyPrefix;
            if (string.IsNullOrWhiteSpace(keyPrefix))
                keyPrefix = "failed-http";

            var ttl = o.Store?.TtlMinutes ?? 60;
            if (ttl < 1) ttl = 1;

            return new Snapshot(
                Enabled: o.Enabled,
                MaxBytes: max,
                Mode: o.Mode,
                MaxRequestContentLengthToCapture: Math.Max(0, o.MaxRequestContentLengthToCapture),
                AllowUnknownContentLength: o.AllowUnknownContentLength,
                ContentTypesAllowList: o.ContentTypesAllowList ?? Array.Empty<string>(),
                JsonDenyPaths: o.JsonDenyPaths ?? Array.Empty<string>(),
                StoreMode: storeMode,
                StoreTtlMinutes: ttl,
                StoreKeyPrefix: keyPrefix!);
        }
    }

    private sealed class TeeLimitedBufferingStream : Stream
    {
        private readonly Stream _inner;
        private readonly int _limitBytes;
        private readonly MemoryStream _buffer;
        private bool _truncated;

        public TeeLimitedBufferingStream(Stream inner, int limitBytes)
        {
            _inner = inner;
            _limitBytes = Math.Max(0, limitBytes);
            _buffer = new MemoryStream(capacity: Math.Min(_limitBytes, 4096));
        }

        public int BufferedCount => (int)_buffer.Length;

        public string ReadBufferedAsString()
        {
            if (_buffer.Length == 0)
                return string.Empty;

            if (_buffer.TryGetBuffer(out var seg) && seg.Array is not null)
            {
                var s = Encoding.UTF8.GetString(seg.Array, seg.Offset, seg.Count);
                return _truncated ? s + "…(truncated)" : s;
            }

            var s2 = Encoding.UTF8.GetString(_buffer.ToArray());
            return _truncated ? s2 + "…(truncated)" : s2;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            CopyToLocalBuffer(buffer.AsSpan(offset, count));
            _inner.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            CopyToLocalBuffer(buffer.AsSpan(offset, count));
            await _inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

#if NET8_0_OR_GREATER
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            CopyToLocalBuffer(buffer);
            _inner.Write(buffer);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            CopyToLocalBuffer(buffer.Span);
            return _inner.WriteAsync(buffer, cancellationToken);
        }
#endif

        private void CopyToLocalBuffer(ReadOnlySpan<byte> data)
        {
            if (_limitBytes <= 0) return;
            if (data.Length <= 0) return;

            var remaining = _limitBytes - (int)_buffer.Length;
            if (remaining <= 0)
            {
                _truncated = true;
                return;
            }

            var toCopy = Math.Min(remaining, data.Length);
            if (toCopy < data.Length) _truncated = true;

#if NET8_0_OR_GREATER
            _buffer.Write(data.Slice(0, toCopy));
#else
            // fallback
            var tmp = data.Slice(0, toCopy).ToArray();
            _buffer.Write(tmp, 0, tmp.Length);
#endif
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _buffer.Dispose();
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _buffer.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
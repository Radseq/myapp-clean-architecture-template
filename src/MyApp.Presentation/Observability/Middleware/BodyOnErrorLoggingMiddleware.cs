using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Application.Abstractions.Observability;
using MyApp.Presentation.Observability.Options;
using MyApp.Presentation.Observability.Redaction;
using System.Diagnostics;
using System.Text;

namespace MyApp.Presentation.Observability.Middleware;

public sealed class BodyOnErrorLoggingMiddleware(
    RequestDelegate next,
    ILogger<BodyOnErrorLoggingMiddleware> logger,
    IOptions<BodyLoggingOptions> options,
    IFailedHttpPayloadStore store)
{
    public const string ItemKey = "__BodyLogKey";

    public async Task Invoke(HttpContext ctx)
    {
        var opt = options.Value;
        if (!opt.Enabled || opt.Mode == BodyLoggingMode.Off)
        {
            await next(ctx);
            return;
        }

        // Request buffering (tylko jeśli to ma sens i jest bezpieczne)
        var mayHaveBody = HttpMethods.IsPost(ctx.Request.Method)
                          || HttpMethods.IsPut(ctx.Request.Method)
                          || HttpMethods.IsPatch(ctx.Request.Method);

        var contentLen = ctx.Request.ContentLength;
        var requestContentType = ctx.Request.ContentType;

        var allowReqType = IsAllowedContentType(requestContentType, opt.ContentTypesAllowList);

        var enableReqBuffering =
            mayHaveBody
            && allowReqType
            && (
                contentLen is not null && contentLen <= opt.MaxRequestContentLengthToCapture
                || contentLen is null && opt.AllowUnknownContentLength
            );

        if (enableReqBuffering)
            ctx.Request.EnableBuffering();

        // Response capture – bufferujemy zawsze do limitu, a decyzję o storowaniu podejmujemy na końcu.
        // Dzięki temu nie tracimy body w przypadku późnego ustawienia StatusCode.
        var original = ctx.Response.Body;
        await using var capture = new LimitedCaptureStream(original, maxBytes: opt.MaxBytes, logger);
        ctx.Response.Body = capture;

        try
        {
            await next(ctx);
        }
        finally
        {
            ctx.Response.Body = original;

            var status = ctx.Response?.StatusCode ?? 0;
            if (ShouldStore(opt, status))
            {

                // key: prefix + correlation (if any) + requestId (unikalność per request)
                var correlationId =
                    CorrelationIdMiddleware.TryGet(ctx)
                    ?? ctx.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();

                var requestId = ctx.TraceIdentifier;
                var keyBase = !string.IsNullOrWhiteSpace(correlationId)
                    ? correlationId!
                    : Activity.Current?.TraceId.ToString() ?? requestId;

                var key = $"{opt.Store.KeyPrefix}:{keyBase}:{requestId}";

                // request body (limited)
                var reqBody = enableReqBuffering
                    ? await ReadRequestBodyLimited(ctx, opt)
                    : null;

                // response body (limited)
                var respBody = capture.GetCapturedText();

                // respektuj allow-list content-type na etapie zapisu do store
                if (!IsAllowedContentType(requestContentType, opt.ContentTypesAllowList))
                    reqBody = null;

                if (!IsAllowedContentType(ctx.Response.ContentType, opt.ContentTypesAllowList))
                    respBody = null;

                // redakcja JSON
                reqBody = RedactIfJson(reqBody, requestContentType, opt);
                respBody = RedactIfJson(respBody, ctx.Response.ContentType, opt);

                var traceId = Activity.Current?.TraceId.ToString();
                var spanId = Activity.Current?.SpanId.ToString();

                var userId =
                    ctx.User?.FindFirst("sub")?.Value
                    ?? ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                IReadOnlyDictionary<string, string>? headers = null; // domyślnie: bez nagłówków w payload (PII risk)

                var payload = new FailedHttpPayload(
                    Key: key,
                    CreatedAtUtc: DateTimeOffset.UtcNow,
                    CorrelationId: correlationId,
                    TraceId: traceId,
                    SpanId: spanId,
                    RequestId: requestId,
                    Method: ctx.Request.Method,
                    Path: ctx.Request.Path.Value ?? string.Empty,
                    StatusCode: status,
                    RequestContentType: requestContentType,
                    ResponseContentType: ctx.Response.ContentType,
                    UserId: userId,
                    RemoteIp: ctx.Connection.RemoteIpAddress?.ToString(),
                    Headers: headers,
                    RequestBody: reqBody,
                    ResponseBody: respBody);

                var ttl = TimeSpan.FromMinutes(Math.Max(1, opt.Store.TtlMinutes));

                await store.TryStoreAsync(payload, ttl, ctx.RequestAborted);

                ctx.Items[ItemKey] = key;

                logger.LogWarning(
                    "HTTP error payload captured. Key={Key} Status={StatusCode} {Method} {Path}",
                    key,
                    status,
                    ctx.Request.Method,
                    ctx.Request.Path.Value);
            }
        }
    }

    private static bool ShouldStore(BodyLoggingOptions opt, int statusCode) =>
        opt.Mode switch
        {
            BodyLoggingMode.Always => true,
            BodyLoggingMode.OnServerError => statusCode >= 500,
            BodyLoggingMode.OnError => statusCode >= 400,
            _ => false
        };

    private static bool IsAllowedContentType(string? contentType, string[] allow)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        var ct = contentType.Split(';')[0].Trim();
        return allow.Any(x => string.Equals(x, ct, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string?> ReadRequestBodyLimited(HttpContext ctx, BodyLoggingOptions opt)
    {
        try
        {
            if (!ctx.Request.Body.CanSeek) return null;

            ctx.Request.Body.Position = 0;

            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var buffer = new char[Math.Max(0, opt.MaxBytes)];
            if (buffer.Length == 0) return null;

            var read = await reader.ReadAsync(buffer, 0, buffer.Length);

            ctx.Request.Body.Position = 0;

            return read <= 0 ? null : new string(buffer, 0, read);
        }
        catch
        {
            return null;
        }
    }

    private static string? RedactIfJson(string? body, string? contentType, BodyLoggingOptions opt)
    {
        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(contentType))
            return body;

        var ct = contentType.Split(';')[0].Trim();
        if (!string.Equals(ct, "application/json", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(ct, "application/problem+json", StringComparison.OrdinalIgnoreCase))
            return body;

        var deny = new HashSet<string>(opt.JsonDenyPaths ?? [], StringComparer.OrdinalIgnoreCase);
        return JsonBodyRedactor.RedactIfJson(body, deny);
    }

    private sealed class LimitedCaptureStream : Stream
    {
        private readonly Stream _inner;
        private readonly int _maxBytes;
        private readonly ILogger _logger;

        private readonly MemoryStream _buffer;
        private bool _limitReached;

        public LimitedCaptureStream(Stream inner, int maxBytes, ILogger logger)
        {
            _inner = inner;
            _maxBytes = Math.Max(0, maxBytes);
            _logger = logger;
            _buffer = new MemoryStream(capacity: Math.Min(_maxBytes, 4096));
        }

        public string? GetCapturedText()
        {
            try
            {
                if (_buffer.Length == 0) return null;
                return Encoding.UTF8.GetString(_buffer.ToArray());
            }
            catch
            {
                return null;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Capture(buffer.AsSpan(offset, count));
            _inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Capture(buffer);
            _inner.Write(buffer);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Capture(buffer.AsSpan(offset, count));
            await _inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Capture(buffer.Span);
            return _inner.WriteAsync(buffer, cancellationToken);
        }

        private void Capture(ReadOnlySpan<byte> data)
        {
            if (_maxBytes <= 0) return;
            if (_limitReached) return;

            try
            {
                var remaining = _maxBytes - (int)_buffer.Length;
                if (remaining <= 0)
                {
                    _limitReached = true;
                    return;
                }

                var toWrite = Math.Min(remaining, data.Length);
                _buffer.Write(data[..toWrite]);

                if (toWrite < data.Length)
                    _limitReached = true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Body capture failed.");
                _limitReached = true;
            }
        }

        // Stream plumbing
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => _inner.SetLength(value);

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
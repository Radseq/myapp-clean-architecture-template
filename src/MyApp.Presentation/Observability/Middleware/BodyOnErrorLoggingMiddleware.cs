using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Domain.Common;
using MyApp.Presentation.Observability.Options;
using System.Diagnostics;
using System.Text;

namespace MyApp.Presentation.Observability.Middleware;

public sealed class BodyOnErrorLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BodyOnErrorLoggingMiddleware> _logger;

    // snapshot bez alokacji per-request
    private volatile Snapshot _snapshot;

    public BodyOnErrorLoggingMiddleware(
        RequestDelegate next,
        ILogger<BodyOnErrorLoggingMiddleware> logger,
        IOptionsMonitor<BodyLoggingOptions> options)
    {
        _next = next;
        _logger = logger;

        _snapshot = Snapshot.From(options.CurrentValue);

        options.OnChange((o, _) => _snapshot = Snapshot.From(o));
    }

    public async Task Invoke(HttpContext ctx)
    {
        var s = _snapshot;

        if (!s.Enabled)
        {
            await _next(ctx);
            return;
        }

        // MaxBytes=0 -> nie buforujemy payloadu (tylko metadane)
        var limitBytes = s.MaxBytes;
        string? requestBody = null;

        if (limitBytes > 0 && MayHaveJsonRequestBody(ctx.Request))
            requestBody = await ReadRequestBodySafe(ctx, limitBytes);

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

            var policy = BodyLogPolicyHttpContext.Get(ctx);

            var doLog =
                policy == BodyLogPolicy.Force ||
                (policy == BodyLogPolicy.Default && ShouldLogForMode(s.Mode, ctx.Response.StatusCode));

            if (policy == BodyLogPolicy.Suppress)
                doLog = false;

            // Przygotowanie wspólnych metadanych (tylko jeśli w ogóle logujemy)
            string? correlationId = null;
            string? traceId = null;

            if (doLog)
            {
                correlationId =
                    CorrelationIdMiddleware.TryGet(ctx)
                    ?? ctx.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();

                traceId = Activity.Current?.TraceId.ToString() ?? ctx.TraceIdentifier;
            }

            // flagi zamiast return w finally
            var shouldLogMetadataOnly = false;
            var shouldLogWithBodies = false;
            string? responseBody = null;

            if (doLog)
            {
                // jeśli limit=0, logujemy tylko metadane
                if (limitBytes <= 0)
                {
                    shouldLogMetadataOnly = true;
                }
                else
                {
                    var mayLogBody =
                        (tee is not null) &&
                        (MayHaveJsonResponseBody(ctx.Response) || tee.BufferedCount > 0);

                    if (mayLogBody)
                    {
                        try
                        {
                            // czytamy PRZED dispose
                            responseBody = tee!.ReadBufferedAsString();
                        }
                        catch
                        {
                            // nie blokuj odpowiedzi przez logowanie
                        }

                        shouldLogWithBodies = true;
                    }
                }
            }

            // dispose po odczycie bufora
            if (tee is not null)
                await tee.DisposeAsync();

            // logowanie (bez return)
            if (shouldLogMetadataOnly)
            {
                _logger.LogWarning(
                    "BodyOnErrorLogging. {Method} {Path} Status={Status} Policy={Policy} CorrelationId={CorrelationId} TraceId={TraceId} RemoteIp={RemoteIp}",
                    ctx.Request.Method,
                    ctx.Request.Path.Value,
                    ctx.Response.StatusCode,
                    policy,
                    correlationId,
                    traceId,
                    ctx.Connection.RemoteIpAddress?.ToString());
            }
            else if (shouldLogWithBodies)
            {
                _logger.LogWarning(
                    "BodyOnErrorLogging. {Method} {Path} Status={Status} Policy={Policy} CorrelationId={CorrelationId} TraceId={TraceId} RemoteIp={RemoteIp} RequestBody={RequestBody} ResponseBody={ResponseBody}",
                    ctx.Request.Method,
                    ctx.Request.Path.Value,
                    ctx.Response.StatusCode,
                    policy,
                    correlationId,
                    traceId,
                    ctx.Connection.RemoteIpAddress?.ToString(),
                    requestBody,
                    responseBody);
            }
        }
    }

    private static bool ShouldLogForMode(BodyLoggingMode mode, int statusCode)
        => mode == BodyLoggingMode.Always
           || (mode == BodyLoggingMode.OnError && statusCode >= 500);

    private static bool MayHaveJsonRequestBody(HttpRequest req)
    {
        var ct = req.ContentType ?? "";
        return ct.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || ct.Contains("application/problem+json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MayHaveJsonResponseBody(HttpResponse resp)
    {
        var ct = resp.ContentType ?? "";
        return ct.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || ct.Contains("application/problem+json", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> ReadRequestBodySafe(HttpContext ctx, int limitBytes)
    {
        try
        {
            ctx.Request.EnableBuffering();

            using var sr = new StreamReader(
                ctx.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);

            var text = await ReadTextLimited(sr, limitBytes);

            if (ctx.Request.Body.CanSeek)
                ctx.Request.Body.Position = 0;

            return text;
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
        var sb = new StringBuilder(capacity: Math.Min(limitBytes, 4096));
        var buf = new char[1024];
        var total = 0;

        while (true)
        {
            var read = await tr.ReadAsync(buf, 0, buf.Length);
            if (read <= 0) break;

            sb.Append(buf, 0, read);
            total += Encoding.UTF8.GetByteCount(buf, 0, read);

            if (total >= limitBytes)
            {
                sb.Append("…(truncated)");
                break;
            }
        }

        return sb.ToString();
    }

    private sealed record class Snapshot(bool Enabled, int MaxBytes, BodyLoggingMode Mode)
    {
        public static Snapshot From(BodyLoggingOptions o)
        {
            // hard-guard na “nieskończone” wartości z configu
            var max = o.MaxBytes;
            if (max < 0) max = 0;
            if (max > 1024 * 1024) max = 1024 * 1024;

            return new Snapshot(
                Enabled: o.Enabled,
                MaxBytes: max,
                Mode: o.Mode);
        }
    }

    private sealed class TeeLimitedBufferingStream : Stream
    {
        private readonly Stream _inner;
        private readonly int _limitBytes;
        private readonly MemoryStream _buffer = new();
        private bool _truncated;

        public TeeLimitedBufferingStream(Stream inner, int limitBytes)
        {
            _inner = inner;
            _limitBytes = Math.Max(0, limitBytes);
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

            _buffer.Write(data.Slice(0, toCopy));
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
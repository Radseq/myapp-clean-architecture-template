using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyApp.Domain.Common;
using System.Diagnostics;
using System.Text;

namespace MyApp.Presentation.Observability.Middleware;

public sealed class BodyOnErrorLoggingMiddleware(
    RequestDelegate next,
    ILogger<BodyOnErrorLoggingMiddleware> logger)
{
    private const int RequestLimitBytes = 16 * 1024;
    private const int ResponseLimitBytes = 16 * 1024;

    public async Task Invoke(HttpContext ctx)
    {
        string? requestBody = null;
        if (MayHaveJsonRequestBody(ctx.Request))
            requestBody = await ReadRequestBodySafe(ctx, RequestLimitBytes);

        var originalBody = ctx.Response.Body;

        await using var tee = new TeeLimitedBufferingStream(originalBody, ResponseLimitBytes);
        ctx.Response.Body = tee;

        try
        {
            await next(ctx);
        }
        finally
        {
            // zawsze przywróć body
            ctx.Response.Body = originalBody;

            var policy = BodyLogPolicyHttpContext.Get(ctx);

            var doLog =
                policy == BodyLogPolicy.Force ||
                (policy == BodyLogPolicy.Default && ctx.Response.StatusCode >= 500);

            if (policy == BodyLogPolicy.Suppress)
                doLog = false;

            // NIE returnujemy w finally. Zamiast tego warunkujemy logowanie.
            if (doLog)
            {
                // response body: loguj gdy content-type wygląda na JSON/ProblemDetails
                // albo gdy cokolwiek zostało zbuforowane (czasem ContentType jest puste).
                var mayLogBody = MayHaveJsonResponseBody(ctx.Response) || tee.BufferedCount > 0;

                if (mayLogBody)
                {
                    string? responseBody = null;
                    try
                    {
                        responseBody = tee.ReadBufferedAsString();
                    }
                    catch
                    {
                        // nie blokuj odpowiedzi przez logowanie
                    }

                    var correlationId =
                        CorrelationIdMiddleware.TryGet(ctx)
                        ?? ctx.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();

                    var traceId = Activity.Current?.TraceId.ToString() ?? ctx.TraceIdentifier;

                    logger.LogWarning(
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
    }

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

    private sealed class TeeLimitedBufferingStream(Stream inner, int limitBytes) : Stream
    {
        private readonly int _limitBytes = Math.Max(0, limitBytes);
        private readonly MemoryStream _buffer = new();

        public int BufferedCount => (int)_buffer.Length;

        public string ReadBufferedAsString()
        {
            if (_buffer.Length == 0)
                return string.Empty;

            return Encoding.UTF8.GetString(_buffer.ToArray());
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            CopyToLocalBuffer(buffer.AsSpan(offset, count));
            inner.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            CopyToLocalBuffer(buffer.AsSpan(offset, count));
            await inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

#if NET8_0_OR_GREATER
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            CopyToLocalBuffer(buffer);
            inner.Write(buffer);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            CopyToLocalBuffer(buffer.Span);
            return inner.WriteAsync(buffer, cancellationToken);
        }
#endif

        private void CopyToLocalBuffer(ReadOnlySpan<byte> data)
        {
            if (_limitBytes <= 0) return;

            var remaining = _limitBytes - (int)_buffer.Length;
            if (remaining <= 0) return;

            var toCopy = Math.Min(remaining, data.Length);
            if (toCopy <= 0) return;

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
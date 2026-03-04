using MyApp.BuildingBlocks.Domain.Common;
using MyApp.IntegrationContracts.Transport.Commands;
using MyApp.Modules.Transport.Application;
using MyApp.Modules.Transport.Application.Abstractions;
using System.Net.Http.Json;

namespace MyApp.Modules.Transport.Infrastructure.ExternalServices;

public sealed class TransportApiClient(HttpClient http) : ITransportApiClient
{
    public async Task<MessageResult> SendTransportOrderAsync(CreateTransportOrderV1 dto, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/Transport")
            {
                Content = JsonContent.Create(dto)
            };

            // Idempotency is mandatory for safe "no worker" synchronous integration
            req.Headers.TryAddWithoutValidation("Idempotency-Key", dto.ExternalCorrelationId);

            using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            if (resp.IsSuccessStatusCode)
                return MessageResult.Ok();

            // jeœli API zwraca np. 409 i to jest "ok" businessowo -> mo¿esz tu zrobiæ Partial/Warning
            // ale w tej wersji: traktujemy jako FAIL integracji.
            var body = await SafeReadBody(resp, ct);
            var status = (int)resp.StatusCode;

            // ErrorData: Code+Key+Args (Description fallback zostaje; localizer zrobi resztê)
            var err = Errors.Transport.ApiFailed
                .WithArgs(
                    dto.ExternalCorrelationId,
                    status,
                    resp.ReasonPhrase ?? string.Empty,
                    Trunc(body, 400));

            return MessageResult.Fail(err);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // anulowanie po stronie requestu (np. klient przerwa³, timeout, itp.)
            return MessageResult.Fail(Errors.Transport.ApiCanceled.WithArgs(dto.ExternalCorrelationId));
        }
        catch (HttpRequestException ex)
        {
            // problemy sieci/DNS/TLS itp.
            return MessageResult.Fail(Errors.Transport.ApiException.WithArgs(dto.ExternalCorrelationId, ex.Message));
        }
        catch (Exception ex)
        {
            // nieprzewidziane
            return MessageResult.Fail(Errors.Transport.ApiException.WithArgs(dto.ExternalCorrelationId, ex.GetType().Name));
        }
    }

    private static async Task<string> SafeReadBody(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max];
}

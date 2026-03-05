using MediatR;
using MyApp.BuildingBlocks.Application.Abstractions.Caching;
using MyApp.BuildingBlocks.Application.Abstractions.Security;
using MyApp.BuildingBlocks.Application.Common.Caching;
using MyApp.BuildingBlocks.Application.Common.Messaging;
using MyApp.BuildingBlocks.Domain.Common;
using System.Security.Cryptography;
using System.Text;

namespace MyApp.BuildingBlocks.Application.Common.Behaviors;

public sealed class QueryCachingBehavior<TRequest, TResponse>(
    IAppCache cache,
    ICurrentUserService currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IQueryMarker
    where TResponse : MessageResult
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheableQuery q)
            return await next(cancellationToken);

        var key = BuildKey(typeof(TRequest), q);

        // hit
        if (cache.TryGet(key, out TResponse? cached) && cached is not null)
            return cached;

        // single-flight
        await using var _ = await cache.AcquireAsync(key, cancellationToken);

        // re-check
        if (cache.TryGet(key, out cached) && cached is not null)
            return cached;

        var result = await next(cancellationToken);

        if (result.IsSuccess)
        {
            cache.Set(key, result, q.Ttl);
            return result;
        }

        if (q.CacheNotFound && IsNotFound(result.PrimaryError))
        {
            cache.Set(key, result, q.NotFoundTtl);
        }

        return result;
    }

    private string BuildKey(Type requestType, ICacheableQuery q)
    {
        var scopePart = q.Scope switch
        {
            CacheScope.Global => "g",
            CacheScope.User => currentUser.IsAuthenticated
                ? $"u:{currentUser.UserId ?? "missing"}"
                : "u:anon",
            CacheScope.Tenant => $"t:{currentUser.TenantId ?? "missing"}",
            _ => "x"
        };

        // Uwaga: to powoduje różne klucze dla Swagger vs curl (Accept-Language)
        var culture = currentUser.Culture ?? "none";

        var rolesPart = q.VaryByRoles
            ? $":r:{ComputeRolesFingerprint(currentUser.Roles)}"
            : ":r:none";

        return $"{requestType.FullName}:{q.CacheKey}:{scopePart}:c:{culture}{rolesPart}";
    }

    private static string ComputeRolesFingerprint(IReadOnlyCollection<string> roles)
    {
        if (roles.Count == 0)
            return "none";

        var joined = string.Join('|', roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase));

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(joined), hash);

        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }

    private static bool IsNotFound(ErrorData? e)
        => e is not null
           && (e.Kind == ErrorKind.NotFound ||
               !string.IsNullOrWhiteSpace(e.Key) &&
                e.Key.EndsWith(".not_found", StringComparison.OrdinalIgnoreCase));
}

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MyApp.BuildingBlocks.Application.Abstractions.Security;

namespace MyApp.BuildingBlocks.Infrastructure.Security;

public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public string? UserId =>
        GetClaimValue(ClaimTypes.NameIdentifier) ?? GetClaimValue("sub");

    public string? TenantId =>
        GetClaimValue("tenant_id") ?? GetClaimValue("tenant");

    public string Culture
    {
        get
        {
            var raw = accessor.HttpContext?.Request?.Headers["Accept-Language"].ToString();
            if (string.IsNullOrWhiteSpace(raw))
                return "pl-PL"; // albo "en-US" – Twoja domyślna kultura API

            // bierz tylko pierwszy token, bez q=...
            var first = raw.Split(',')[0].Trim();
            if (string.IsNullOrWhiteSpace(first))
                return "pl-PL";

            return first;
        }
    }

    public IReadOnlyCollection<string> Roles =>
        User is null
            ? Array.Empty<string>()
            : User.FindAll(ClaimTypes.Role)
                  .Concat(User.FindAll("role"))
                  .Concat(User.FindAll("roles"))
                  .Select(c => c.Value)
                  .Where(v => !string.IsNullOrWhiteSpace(v))
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                  .ToArray();

    private string? GetClaimValue(string type)
        => User?.FindFirst(type)?.Value;
}

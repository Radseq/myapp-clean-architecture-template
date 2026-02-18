namespace MyApp.Application.Abstractions.Security;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    string? UserId { get; }   // najlepiej Keycloak "sub"
    string? TenantId { get; } // jeśli masz multi-tenant
    string Culture { get; }  // np. "pl-PL" z Accept-Language (opcjonalnie)
    IReadOnlyCollection<string> Roles { get; }
}

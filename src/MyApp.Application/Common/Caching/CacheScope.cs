namespace MyApp.Application.Common.Caching;

/// <summary>
/// Zakres cache:
/// - Global: wspólne dla wszystkich (używaj tylko dla danych nie-per-user)
/// - User: per user (Keycloak sub) – bezpieczne dla endpointów autoryzowanych
/// - Tenant: per tenant (jeśli masz multi-tenant)
/// </summary>
public enum CacheScope
{
    Global,   // wspólne dla wszystkich (uwaga na dane użytkownika!)
    User,     // per user (Keycloak sub)
    Tenant    // per tenant (jeśli masz tenantId claim)
}

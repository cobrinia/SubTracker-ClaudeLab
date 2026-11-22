using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace SubTracker.Api.Security;

/// <summary>Налаштування постачальника OpenID Connect (секція «Oidc» конфігурації).</summary>
public sealed class OidcOptions
{
    public const string SectionName = "Oidc";

    /// <summary>Адреса realm Keycloak; звідси API читає /.well-known/openid-configuration і ключі підпису.</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>Очікуване значення claim «aud» — ідентифікатор цього API.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Публічний клієнт (PKCE), яким користуються Scalar і Postman.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Назва claim з ролями (у Keycloak налаштовано плаский claim «roles»).</summary>
    public string RoleClaimType { get; set; } = "roles";

    /// <summary>Вимагати HTTPS для метаданих постачальника. false лише для локального Keycloak.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}

/// <summary>Ролі користувачів (realm roles у Keycloak).</summary>
public static class Roles
{
    /// <summary>Звичайний користувач: керує лише власними підписками і способами оплати.</summary>
    public const string User = "user";

    /// <summary>Адміністратор: довідник категорій, перегляд усіх користувачів і підписок.</summary>
    public const string Admin = "admin";
}

/// <summary>Іменовані політики авторизації (Policy-based authorization).</summary>
public static class Policies
{
    /// <summary>Будь-який автентифікований користувач із роллю user або admin.</summary>
    public const string RegisteredUser = nameof(RegisteredUser);

    /// <summary>Лише адміністратор.</summary>
    public const string AdminOnly = nameof(AdminOnly);

    public static AuthorizationBuilder AddSubTrackerPolicies(this AuthorizationBuilder builder) => builder
        // Fallback: кожна кінцева точка без [AllowAnonymous] вимагає автентифікації — нічого не відкрито «випадково».
        .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
        .AddPolicy(RegisteredUser, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim("sub")
            .RequireRole(Roles.User, Roles.Admin))
        .AddPolicy(AdminOnly, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(Roles.Admin));
}

/// <summary>Операції над ресурсами для Resource-based authorization.</summary>
public static class ResourceOperations
{
    public static readonly OperationAuthorizationRequirement Read = new() { Name = nameof(Read) };
    public static readonly OperationAuthorizationRequirement Update = new() { Name = nameof(Update) };
    public static readonly OperationAuthorizationRequirement Delete = new() { Name = nameof(Delete) };
}

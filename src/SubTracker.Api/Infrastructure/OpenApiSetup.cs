using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using SubTracker.Api.Security;

namespace SubTracker.Api.Infrastructure;

/// <summary>
/// Налаштування документа OpenAPI: загальні відомості про API та схема безпеки OAuth 2.0
/// (Authorization Code + PKCE через Keycloak), щоб із Scalar можна було увійти й виконувати захищені запити.
/// </summary>
public static class OpenApiSetup
{
    public const string SchemeName = "OAuth2";

    public static IServiceCollection AddSubTrackerOpenApi(this IServiceCollection services, OidcOptions oidc)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, context, ct) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "SubTracker API",
                    Version = "v1",
                    Description = "REST API менеджера підписок і регулярних платежів (варіант 10). " +
                                  "Вхід — OpenID Connect (Keycloak), Authorization Code + PKCE. " +
                                  "Ролі: user — власні підписки; admin — довідник категорій і всі дані.",
                    Contact = new OpenApiContact { Name = "Кафедра ПЗ, НУ «Львівська політехніка»", Url = new Uri("https://lpnu.ua") },
                    License = new OpenApiLicense { Name = "MIT" }
                };

                var openIdBase = oidc.Authority.TrimEnd('/') + "/protocol/openid-connect";
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Description = "Keycloak realm «subtracker»: Authorization Code + PKCE (S256).",
                    Flows = new OpenApiOAuthFlows
                    {
                        AuthorizationCode = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri($"{openIdBase}/auth"),
                            TokenUrl = new Uri($"{openIdBase}/token"),
                            Scopes = new Dictionary<string, string>
                            {
                                ["openid"] = "Ідентифікація користувача (OIDC)",
                                ["profile"] = "Ім'я користувача",
                                ["email"] = "E-mail користувача"
                            }
                        }
                    }
                };
                return Task.CompletedTask;
            });

            // Захищеним операціям (усім, крім [AllowAnonymous]) додається вимога схеми OAuth2.
            options.AddOperationTransformer((operation, context, ct) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                if (metadata.OfType<IAllowAnonymous>().Any())
                    return Task.CompletedTask;

                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = ["openid", "profile", "email"]
                });
                return Task.CompletedTask;
            });
        });

        return services;
    }
}

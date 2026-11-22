using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace SubTracker.Api.Security;

/// <summary>
/// Відомості про підписку, потрібні для перевірки доступу (без завантаження всієї сутності).
/// Власник і учасники ідентифікуються значенням claim «sub», збереженим у Users.ExternalId.
/// </summary>
/// <param name="SubscriptionId">Ідентифікатор підписки.</param>
/// <param name="OwnerExternalId">sub власника.</param>
/// <param name="MemberExternalIds">sub учасників спільної підписки.</param>
public sealed record SubscriptionAccess(int SubscriptionId, string? OwnerExternalId, IReadOnlyCollection<string> MemberExternalIds);

/// <summary>Ресурс, що має одного власника (спосіб оплати).</summary>
/// <param name="OwnerExternalId">sub власника.</param>
public sealed record OwnedResource(string? OwnerExternalId);

/// <summary>
/// Resource-based авторизація підписок (власний IAuthorizationHandler через AuthorizationHandler&lt;,&gt;).
/// Read — власник, учасник спільної підписки або адміністратор.
/// Update/Delete — лише власник або адміністратор (учасник сімейної підписки не може її змінити).
/// Singleton: обробник не має стану й не залежить від scoped-сервісів — усе потрібне приходить у ресурсі.
/// </summary>
public sealed class SubscriptionAuthorizationHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, SubscriptionAccess>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        SubscriptionAccess resource)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        if (sub is null)
            return Task.CompletedTask;

        var isAdmin = context.User.IsInRole(Roles.Admin);
        var isOwner = resource.OwnerExternalId == sub;
        var isMember = resource.MemberExternalIds.Contains(sub);

        var allowed = requirement.Name switch
        {
            nameof(ResourceOperations.Read) => isAdmin || isOwner || isMember,
            nameof(ResourceOperations.Update) or nameof(ResourceOperations.Delete) => isAdmin || isOwner,
            _ => false
        };

        if (allowed)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Resource-based авторизація ресурсів з одним власником (способи оплати): доступ має власник або адміністратор.
/// </summary>
public sealed class OwnedResourceAuthorizationHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, OwnedResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        OwnedResource resource)
    {
        var sub = context.User.FindFirst("sub")?.Value;
        if (sub is not null && (context.User.IsInRole(Roles.Admin) || resource.OwnerExternalId == sub))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

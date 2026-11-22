using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace SubTracker.Api.Controllers;

/// <summary>
/// Спільна основа контролерів: версія API в URI (api/v1/…), формат JSON,
/// стандартні відповіді 401/403/500 у форматі ProblemDetails для кожної операції.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Resource-based авторизація: викликає IAuthorizationService з конкретним ресурсом і операцією,
    /// рішення ухвалює власний IAuthorizationHandler (див. Security/ResourceAuthorizationHandlers.cs).
    /// Повертає null, якщо доступ дозволено, або ForbidResult (403).
    /// </summary>
    protected async Task<ActionResult?> AuthorizeResourceAsync(
        IAuthorizationService authorization, object resource, OperationAuthorizationRequirement operation)
    {
        var result = await authorization.AuthorizeAsync(User, resource, operation);
        return result.Succeeded ? null : Forbid();
    }
}

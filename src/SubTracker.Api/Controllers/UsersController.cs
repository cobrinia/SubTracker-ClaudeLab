using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubTracker.Api.Contracts;
using SubTracker.Api.Security;
using SubTracker.Api.Services;

namespace SubTracker.Api.Controllers;

/// <summary>Адміністрування користувачів (policy AdminOnly).</summary>
[Route("api/v{version:apiVersion}/users")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class UsersController(IUserService users) : ApiControllerBase
{
    /// <summary>Сторінка користувачів застосунку.</summary>
    /// <param name="query">Номер і розмір сторінки.</param>
    /// <response code="200">Сторінка користувачів; IsLinked — чи пов'язаний обліковий запис з Keycloak.</response>
    /// <response code="400">Некоректні параметри сторінки.</response>
    /// <response code="403">Потрібна роль admin.</response>
    [HttpGet(Name = "GetUsers")]
    [ProducesResponseType<PagedResponse<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResponse<UserDto>>> GetPage([FromQuery] PageQuery query, CancellationToken ct)
        => await users.GetPageAsync(query, ct);
}

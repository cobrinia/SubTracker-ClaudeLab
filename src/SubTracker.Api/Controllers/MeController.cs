using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubTracker.Api.Contracts;
using SubTracker.Api.Security;
using SubTracker.Api.Services;

namespace SubTracker.Api.Controllers;

/// <summary>Профіль поточного користувача.</summary>
[Route("api/v{version:apiVersion}/me")]
[Authorize(Policy = Policies.RegisteredUser)]
public sealed class MeController(IUserService users) : ApiControllerBase
{
    /// <summary>Хто я: користувач застосунку, пов'язаний з токеном (claim «sub»), і його ролі.</summary>
    /// <remarks>Під час першого виклику користувача буде створено або прив'язано за e-mail (JIT-провізія).</remarks>
    /// <response code="200">Профіль користувача.</response>
    /// <response code="401">Немає або недійсний токен доступу.</response>
    /// <response code="403">У токені немає ролі user/admin.</response>
    [HttpGet(Name = "GetMe")]
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<MeDto>> Get(CancellationToken ct) => await users.GetMeAsync(ct);
}

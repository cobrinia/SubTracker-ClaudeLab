using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubTracker.Api.Security;

namespace SubTracker.Api.Controllers;

/// <summary>Службові кінцеві точки для демонстрації глобальної обробки помилок (лише admin).</summary>
[Route("api/v{version:apiVersion}/diagnostics")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class DiagnosticsController : ApiControllerBase
{
    /// <summary>Навмисно кидає необроблений виняток.</summary>
    /// <remarks>
    /// У Development відповідь 500 містить текст винятку; у Production — лише загальне повідомлення і traceId,
    /// без стеку викликів і внутрішніх деталей.
    /// </remarks>
    /// <response code="500">Внутрішня помилка сервера (ProblemDetails).</response>
    /// <response code="403">Потрібна роль admin.</response>
    [HttpGet("error", Name = "ThrowError")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public IActionResult Throw()
        => throw new InvalidOperationException("Демонстраційна помилка: рядок підключення Server=secret-host;Password=***");
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SubTracker.Api.Contracts;
using SubTracker.Api.Infrastructure;
using SubTracker.Api.Security;
using SubTracker.Api.Services;

namespace SubTracker.Api.Controllers;

/// <summary>Звіти: календар списань з нагадуваннями і підсумки витрат.</summary>
[Route("api/v{version:apiVersion}/reports")]
[Authorize(Policy = Policies.RegisteredUser)]
public sealed class ReportsController(IReportService reports) : ApiControllerBase
{
    /// <summary>Календар майбутніх списань за період (за замовчуванням — 30 днів від сьогодні).</summary>
    /// <param name="query">Початок і кінець періоду (yyyy-MM-dd).</param>
    /// <response code="200">Списання, упорядковані за датою; IsReminderDue = true, якщо час нагадати.</response>
    /// <response code="400">Кінець раніше за початок або період довший за 366 днів.</response>
    [HttpGet("calendar", Name = "GetCalendar")]
    [ProducesResponseType<IReadOnlyList<UpcomingChargeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<UpcomingChargeDto>>> GetCalendar(
        [FromQuery] CalendarQuery query, CancellationToken ct)
        => Ok(await reports.GetCalendarAsync(query, ct));

    /// <summary>Підсумки активних підписок на місяць і рік — окремо за кожною валютою та з розбивкою за категоріями.</summary>
    /// <response code="200">Підсумки за валютами.</response>
    [HttpGet("totals", Name = "GetTotals")]
    [OutputCache(PolicyName = CachePolicies.Subscriptions)]
    [ProducesResponseType<IReadOnlyList<CurrencyTotalsDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CurrencyTotalsDto>>> GetTotals(CancellationToken ct)
        => Ok(await reports.GetTotalsAsync(ct));
}

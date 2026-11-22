using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubTracker.Api.Contracts;
using SubTracker.Api.Security;
using SubTracker.DB;
using SubTracker.Domain.Billing;
using SubTracker.Domain.Enums;

namespace SubTracker.Api.Services;

/// <summary>Звіти варіанта 10: календар майбутніх списань і суми на місяць/рік. Scoped.</summary>
public sealed class ReportService(
    SubTrackerDbContext db,
    ICurrentUserService currentUser,
    TimeProvider time,
    IOptions<PerformanceOptions> performance) : IReportService
{
    public async Task<IReadOnlyList<UpcomingChargeDto>> GetCalendarAsync(CalendarQuery q, CancellationToken ct)
    {
        var user = await currentUser.GetUserAsync(ct);
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var from = q.From ?? today;
        var to = q.To ?? from.AddDays(30);

        var schedule = await db.Subscriptions
            .AsNoTracking()
            .Where(s => s.Status == SubscriptionStatus.Active && s.NextChargeDate <= to)
            .Where(s => s.OwnerId == user.Id || s.Members.Any(m => m.UserId == user.Id))
            .Select(s => new
            {
                s.Id, s.Name, s.Amount, s.Currency, s.Period, s.NextChargeDate, s.EndDate, s.RemindDaysBefore,
                PaymentMethodTitle = s.PaymentMethod.Title
            })
            .ToListAsync(ct);

        // Повторення (щотижня/щомісяця…) розгортаються доменною логікою з ЛР № 2.
        return schedule
            .SelectMany(s => BillingCalculator.Occurrences(s.NextChargeDate, s.Period, s.EndDate, from, to)
                .Select(date => new UpcomingChargeDto(
                    date, s.Id, s.Name, s.PaymentMethodTitle, s.Amount, s.Currency,
                    IsReminderDue: date.DayNumber - today.DayNumber is >= 0 and var left && left <= s.RemindDaysBefore)))
            .OrderBy(e => e.Date)
            .ThenBy(e => e.SubscriptionName)
            .ToList();
    }

    public async Task<IReadOnlyList<CurrencyTotalsDto>> GetTotalsAsync(CancellationToken ct)
    {
        var user = await currentUser.GetUserAsync(ct);
        var rows = performance.Value.Optimized
            ? await TotalsInSqlAsync(user.Id, ct)
            : await TotalsInMemoryAsync(user.Id, ct);

        return rows
            .GroupBy(r => r.Currency)
            .OrderBy(g => g.Key)
            .Select(g => new CurrencyTotalsDto(
                g.Key,
                g.Sum(r => r.Count),
                decimal.Round(g.Sum(r => r.Monthly), 2),
                decimal.Round(g.Sum(r => r.Monthly) * 12, 2),
                g.OrderByDescending(r => r.Monthly)
                    .Select(r => new CategoryTotalDto(r.Category, r.Count, decimal.Round(r.Monthly, 2), decimal.Round(r.Monthly * 12, 2)))
                    .ToList()))
            .ToList();
    }

    private sealed record TotalRow(string Category, string Currency, int Count, decimal Monthly);

    /// <summary>Після оптимізації: GROUP BY і SUM(CASE …) виконує SQL Server, повертається кілька рядків.</summary>
    private async Task<List<TotalRow>> TotalsInSqlAsync(int userId, CancellationToken ct) =>
        await db.Subscriptions
            .AsNoTracking()
            .Where(s => s.OwnerId == userId && s.Status == SubscriptionStatus.Active)
            .GroupBy(s => new { Category = s.Category.Name, s.Currency })
            .Select(g => new TotalRow(
                g.Key.Category,
                g.Key.Currency,
                g.Count(),
                g.Sum(s => s.Period == BillingPeriod.Weekly ? s.Amount * 52m / 12m
                         : s.Period == BillingPeriod.Quarterly ? s.Amount / 3m
                         : s.Period == BillingPeriod.Yearly ? s.Amount / 12m
                         : s.Amount)))
            .ToListAsync(ct);

    /// <summary>До оптимізації: усі підписки з категоріями завантажуються з відстеженням і групуються в пам'яті.</summary>
    private async Task<List<TotalRow>> TotalsInMemoryAsync(int userId, CancellationToken ct)
    {
        var all = await db.Subscriptions
            .Include(s => s.Category)
            .Include(s => s.PaymentMethod)
            .Where(s => s.OwnerId == userId)
            .ToListAsync(ct);

        return all
            .Where(s => s.Status == SubscriptionStatus.Active)
            .GroupBy(s => new { Category = s.Category.Name, s.Currency })
            .Select(g => new TotalRow(g.Key.Category, g.Key.Currency, g.Count(),
                g.Sum(s => BillingCalculator.ToMonthly(s.Amount, s.Period))))
            .ToList();
    }
}

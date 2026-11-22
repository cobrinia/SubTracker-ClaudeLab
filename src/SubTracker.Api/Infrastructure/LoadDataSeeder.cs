using Microsoft.EntityFrameworkCore;
using SubTracker.DB;
using SubTracker.Domain.Billing;
using SubTracker.Domain.Entities;
using SubTracker.Domain.Enums;

namespace SubTracker.Api.Infrastructure;

/// <summary>
/// Наповнює ОКРЕМУ базу для навантажувального тестування: N підписок користувача 1 (oleksandr)
/// з кількома платежами кожна. На малих даних різниця між «наївною» та оптимізованою реалізаціями
/// непомітна, тому потрібен обсяг у тисячі рядків.
/// Запуск: dotnet run --project src/SubTracker.Api -- --seed-load-data 5000
/// </summary>
public static class LoadDataSeeder
{
    private const string Prefix = "Load test #";

    public static async Task RunAsync(SubTrackerDbContext db, int count, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        var existing = await db.Subscriptions.CountAsync(s => s.Name.StartsWith(Prefix), ct);
        if (existing >= count)
        {
            Console.WriteLine($"Навантажувальні дані вже є: {existing} підписок.");
            return;
        }

        var categories = await db.Categories.Select(c => c.Id).ToArrayAsync(ct);
        var method = await db.PaymentMethods.FirstAsync(p => p.UserId == 1 && p.Type == PaymentMethodType.Card, ct);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var periods = Enum.GetValues<BillingPeriod>();
        var currencies = new[] { "UAH", "UAH", "UAH", "USD", "EUR" };
        var random = new Random(42); // фіксоване зерно — однакові дані для «до» і «після»

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        const int batchSize = 500;
        for (var i = existing + 1; i <= count; i++)
        {
            var period = periods[random.Next(periods.Length)];
            var next = today.AddDays(random.Next(0, 60));
            var amount = Math.Round((decimal)(random.NextDouble() * 900 + 20), 2);
            var subscription = new Subscription
            {
                OwnerId = 1,
                CategoryId = categories[random.Next(categories.Length)],
                PaymentMethodId = method.Id,
                Name = $"{Prefix}{i:D5}",
                Amount = amount,
                Currency = currencies[random.Next(currencies.Length)],
                Period = period,
                NextChargeDate = next,
                Status = random.Next(10) < 8 ? SubscriptionStatus.Active : SubscriptionStatus.Paused,
                RemindDaysBefore = random.Next(0, 8)
            };

            // Три попередні списання.
            var date = next;
            for (var k = 0; k < 3; k++)
            {
                date = BillingCalculator.PreviousDate(date, period);
                subscription.Payments.Add(new Payment
                {
                    PaymentMethodId = method.Id,
                    ChargedAt = date.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc),
                    Amount = amount,
                    Currency = subscription.Currency,
                    Status = PaymentStatus.Succeeded
                });
            }

            db.Subscriptions.Add(subscription);
            if (i % batchSize == 0 || i == count)
            {
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
                Console.WriteLine($"  додано {i} / {count}");
            }
        }

        Console.WriteLine("Готово.");
    }
}

using System.ComponentModel.DataAnnotations;
using SubTracker.Domain.Enums;

namespace SubTracker.Api.Contracts;

// ===================== Способи оплати =====================

/// <summary>Спосіб оплати для читання.</summary>
/// <param name="Id">Ідентифікатор.</param>
/// <param name="Type">Тип: Card, PayPal, BankTransfer, GooglePay, ApplePay.</param>
/// <param name="Title">Назва, зрозуміла користувачу.</param>
/// <param name="Last4">Останні 4 цифри картки.</param>
/// <param name="ExpiryDate">Термін дії.</param>
/// <param name="LastUsedAt">Коли використовувався востаннє (UTC).</param>
public sealed record PaymentMethodDto(
    int Id, PaymentMethodType Type, string Title, string? Last4, DateOnly? ExpiryDate, DateTime? LastUsedAt);

/// <summary>Дані для створення способу оплати.</summary>
/// <param name="Type">Тип способу оплати.</param>
/// <param name="Title">Назва, 2–100 символів.</param>
/// <param name="Last4">Останні 4 цифри (обов'язково для Card).</param>
/// <param name="ExpiryDate">Термін дії (обов'язковий для Card, не в минулому).</param>
public sealed record CreatePaymentMethodDto(
    PaymentMethodType Type,
    [Required(ErrorMessage = "Назва є обов'язковою.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Назва має містити від 2 до 100 символів.")]
    string Title,
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Last4 — рівно 4 цифри.")]
    string? Last4,
    DateOnly? ExpiryDate);

/// <summary>Дані для повної заміни способу оплати (перевипуск картки).</summary>
/// <param name="Title">Нова назва, 2–100 символів.</param>
/// <param name="ExpiryDate">Новий термін дії.</param>
public sealed record UpdatePaymentMethodDto(
    [Required(ErrorMessage = "Назва є обов'язковою.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Назва має містити від 2 до 100 символів.")]
    string Title,
    DateOnly? ExpiryDate);

// ===================== Платежі =====================

/// <summary>Платіж (факт списання).</summary>
/// <param name="Id">Ідентифікатор.</param>
/// <param name="SubscriptionId">Підписка.</param>
/// <param name="PaymentMethodId">Спосіб оплати.</param>
/// <param name="ChargedAt">Момент списання (UTC).</param>
/// <param name="Amount">Сума.</param>
/// <param name="Currency">Валюта.</param>
/// <param name="Status">Результат: Succeeded, Failed, Refunded.</param>
public sealed record PaymentDto(
    int Id, int SubscriptionId, int PaymentMethodId, DateTime ChargedAt, decimal Amount, string Currency, PaymentStatus Status);

// ===================== Звіти =====================

/// <summary>Період календаря списань (за замовчуванням — 30 днів від сьогодні).</summary>
public sealed class CalendarQuery
{
    /// <summary>Початок періоду (yyyy-MM-dd).</summary>
    public DateOnly? From { get; set; }

    /// <summary>Кінець періоду (yyyy-MM-dd), не більше 366 днів від початку.</summary>
    public DateOnly? To { get; set; }
}

/// <summary>Одна подія календаря: конкретне майбутнє списання.</summary>
/// <param name="Date">Дата списання.</param>
/// <param name="SubscriptionId">Підписка.</param>
/// <param name="SubscriptionName">Назва підписки.</param>
/// <param name="PaymentMethodTitle">З якого способу оплати.</param>
/// <param name="Amount">Сума.</param>
/// <param name="Currency">Валюта.</param>
/// <param name="IsReminderDue">Чи настав час нагадування (до списання ≤ RemindDaysBefore днів).</param>
public sealed record UpcomingChargeDto(
    DateOnly Date, int SubscriptionId, string SubscriptionName, string PaymentMethodTitle,
    decimal Amount, string Currency, bool IsReminderDue);

/// <summary>Витрати в одній категорії.</summary>
/// <param name="CategoryName">Категорія.</param>
/// <param name="ActiveCount">Активних підписок.</param>
/// <param name="MonthlyTotal">Сума на місяць.</param>
/// <param name="YearlyTotal">Сума на рік.</param>
public sealed record CategoryTotalDto(string CategoryName, int ActiveCount, decimal MonthlyTotal, decimal YearlyTotal);

/// <summary>Загальні витрати в одній валюті.</summary>
/// <param name="Currency">Валюта.</param>
/// <param name="ActiveCount">Активних підписок.</param>
/// <param name="MonthlyTotal">Сума на місяць.</param>
/// <param name="YearlyTotal">Сума на рік.</param>
/// <param name="ByCategory">Розбивка за категоріями.</param>
public sealed record CurrencyTotalsDto(
    string Currency, int ActiveCount, decimal MonthlyTotal, decimal YearlyTotal, IReadOnlyList<CategoryTotalDto> ByCategory);

// ===================== Користувачі =====================

/// <summary>Профіль поточного користувача.</summary>
/// <param name="Id">Ідентифікатор у БД.</param>
/// <param name="Name">Ім'я.</param>
/// <param name="Email">E-mail.</param>
/// <param name="ExternalId">Ідентифікатор у постачальника OIDC (claim sub).</param>
/// <param name="Roles">Ролі з маркера доступу.</param>
public sealed record MeDto(int Id, string Name, string Email, string ExternalId, IReadOnlyList<string> Roles);

/// <summary>Користувач у списку адміністратора.</summary>
/// <param name="Id">Ідентифікатор.</param>
/// <param name="Name">Ім'я.</param>
/// <param name="Email">E-mail.</param>
/// <param name="IsLinked">Чи входив користувач через OIDC.</param>
/// <param name="SubscriptionCount">Кількість власних підписок.</param>
/// <param name="CreatedAt">Дата реєстрації (UTC).</param>
public sealed record UserDto(int Id, string Name, string Email, bool IsLinked, int SubscriptionCount, DateTime CreatedAt);

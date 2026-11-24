using System.ComponentModel.DataAnnotations;
using SubTracker.Domain.Enums;

namespace SubTracker.Api.Contracts;

/// <summary>Підписка для читання (у списках і як результат створення).</summary>
/// <param name="Id">Ідентифікатор.</param>
/// <param name="Name">Назва сервісу або платежу.</param>
/// <param name="CategoryId">Ідентифікатор категорії.</param>
/// <param name="CategoryName">Назва категорії.</param>
/// <param name="PaymentMethodId">Ідентифікатор способу оплати.</param>
/// <param name="PaymentMethodTitle">Назва способу оплати.</param>
/// <param name="OwnerId">Ідентифікатор власника.</param>
/// <param name="OwnerName">Ім'я власника.</param>
/// <param name="Amount">Сума одного списання.</param>
/// <param name="Currency">Код валюти ISO 4217.</param>
/// <param name="Period">Періодичність списання.</param>
/// <param name="NextChargeDate">Дата наступного списання.</param>
/// <param name="EndDate">Дата завершення або null.</param>
/// <param name="TrialEndDate">Кінець пробного періоду або null.</param>
/// <param name="Status">Статус підписки.</param>
/// <param name="RemindDaysBefore">За скільки днів нагадувати.</param>
/// <param name="Notes">Нотатка до 500 символів або null.</param>
/// <param name="IsShared">Чи є підписка спільною.</param>
public sealed record SubscriptionDto(
    int Id,
    string Name,
    int CategoryId,
    string CategoryName,
    int PaymentMethodId,
    string PaymentMethodTitle,
    int OwnerId,
    string OwnerName,
    decimal Amount,
    string Currency,
    BillingPeriod Period,
    DateOnly NextChargeDate,
    DateOnly? EndDate,
    DateOnly? TrialEndDate,
    SubscriptionStatus Status,
    int RemindDaysBefore,
    string? Notes,
    bool IsShared);

/// <summary>Учасник спільної підписки.</summary>
/// <param name="UserId">Ідентифікатор користувача.</param>
/// <param name="UserName">Ім'я користувача.</param>
/// <param name="SharePercent">Частка оплати, %.</param>
public sealed record SubscriptionMemberDto(int UserId, string UserName, decimal SharePercent);

/// <summary>Повні відомості про підписку (GET за Id).</summary>
/// <param name="Subscription">Основні дані підписки.</param>
/// <param name="MonthlyAmount">Еквівалент суми за місяць.</param>
/// <param name="YearlyAmount">Еквівалент суми за рік.</param>
/// <param name="Members">Учасники спільної підписки (порожньо для індивідуальної).</param>
public sealed record SubscriptionDetailsDto(
    SubscriptionDto Subscription,
    decimal MonthlyAmount,
    decimal YearlyAmount,
    IReadOnlyList<SubscriptionMemberDto> Members);

/// <summary>Дані для створення підписки (POST).</summary>
/// <param name="Name">Назва сервісу, 2–100 символів; унікальна в межах власника.</param>
/// <param name="CategoryId">Ідентифікатор існуючої категорії.</param>
/// <param name="PaymentMethodId">Ідентифікатор власного дійсного способу оплати.</param>
/// <param name="Amount">Сума одного списання, 0.01–1 000 000.</param>
/// <param name="Currency">Код валюти ISO 4217 з трьох великих латинських літер.</param>
/// <param name="Period">Періодичність: Weekly, Monthly, Quarterly, Yearly.</param>
/// <param name="NextChargeDate">Дата наступного списання.</param>
/// <param name="EndDate">Дата завершення (не раніше за наступне списання) або null.</param>
/// <param name="TrialEndDate">Кінець пробного періоду або null.</param>
/// <param name="RemindDaysBefore">За скільки днів нагадувати, 0–30.</param>
/// <param name="Notes">Нотатка до 500 символів або null.</param>
/// <param name="IsShared">Чи є підписка спільною.</param>
public sealed record CreateSubscriptionDto(
    [Required(ErrorMessage = "Назва є обов'язковою.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Назва має містити від 2 до 100 символів.")]
    string Name,
    [Range(1, int.MaxValue, ErrorMessage = "Вкажіть категорію.")]
    int CategoryId,
    [Range(1, int.MaxValue, ErrorMessage = "Вкажіть спосіб оплати.")]
    int PaymentMethodId,
    [Range(typeof(decimal), "0.01", "1000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Сума має бути від 0.01 до 1 000 000.")]
    decimal Amount,
    [Required(ErrorMessage = "Валюта є обов'язковою.")]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Валюта — три великі латинські літери (UAH, USD, EUR).")]
    string Currency,
    BillingPeriod Period,
    DateOnly NextChargeDate,
    DateOnly? EndDate = null,
    DateOnly? TrialEndDate = null,
    [Range(0, 30, ErrorMessage = "Нагадування — від 0 до 30 днів.")]
    int RemindDaysBefore = 3,
    [StringLength(500, ErrorMessage = "Нотатка — не більше 500 символів.")]
    string? Notes = null,
    bool IsShared = false);

/// <summary>Дані для повної заміни підписки (PUT). Статус змінюється окремо (PATCH .../status).</summary>
/// <param name="Name">Назва сервісу, 2–100 символів.</param>
/// <param name="CategoryId">Ідентифікатор категорії.</param>
/// <param name="PaymentMethodId">Ідентифікатор способу оплати.</param>
/// <param name="Amount">Сума одного списання.</param>
/// <param name="Currency">Код валюти ISO 4217.</param>
/// <param name="Period">Періодичність списання.</param>
/// <param name="NextChargeDate">Дата наступного списання.</param>
/// <param name="EndDate">Дата завершення або null.</param>
/// <param name="TrialEndDate">Кінець пробного періоду або null.</param>
/// <param name="RemindDaysBefore">За скільки днів нагадувати, 0–30.</param>
/// <param name="Notes">Нотатка до 500 символів або null.</param>
/// <param name="IsShared">Чи є підписка спільною.</param>
public sealed record UpdateSubscriptionDto(
    [Required(ErrorMessage = "Назва є обов'язковою.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Назва має містити від 2 до 100 символів.")]
    string Name,
    [Range(1, int.MaxValue, ErrorMessage = "Вкажіть категорію.")]
    int CategoryId,
    [Range(1, int.MaxValue, ErrorMessage = "Вкажіть спосіб оплати.")]
    int PaymentMethodId,
    [Range(typeof(decimal), "0.01", "1000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Сума має бути від 0.01 до 1 000 000.")]
    decimal Amount,
    [Required(ErrorMessage = "Валюта є обов'язковою.")]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Валюта — три великі латинські літери (UAH, USD, EUR).")]
    string Currency,
    BillingPeriod Period,
    DateOnly NextChargeDate,
    DateOnly? EndDate,
    DateOnly? TrialEndDate,
    [Range(0, 30, ErrorMessage = "Нагадування — від 0 до 30 днів.")]
    int RemindDaysBefore,
    [StringLength(500, ErrorMessage = "Нотатка — не більше 500 символів.")]
    string? Notes = null,
    bool IsShared = false);

/// <summary>Зміна статусу підписки (PATCH).</summary>
/// <param name="Status">Новий статус: Active, Paused або Cancelled.</param>
public sealed record ChangeSubscriptionStatusDto(
    [Required(ErrorMessage = "Вкажіть новий статус.")]
    SubscriptionStatus? Status);

/// <summary>Поле сортування списку підписок.</summary>
public enum SubscriptionSortField { NextChargeDate, Amount, Name }

/// <summary>Фільтрація, сортування та пагінація списку підписок.</summary>
public sealed class SubscriptionQuery : PageQuery
{
    /// <summary>Фільтр за статусом.</summary>
    public SubscriptionStatus? Status { get; set; }

    /// <summary>Фільтр за категорією.</summary>
    public int? CategoryId { get; set; }

    /// <summary>Фільтр за валютою (UAH, USD, EUR).</summary>
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Валюта — три великі латинські літери.")]
    public string? Currency { get; set; }

    /// <summary>Мінімальна сума.</summary>
    [Range(0, 1_000_000)]
    public decimal? MinAmount { get; set; }

    /// <summary>Максимальна сума.</summary>
    [Range(0, 1_000_000)]
    public decimal? MaxAmount { get; set; }

    /// <summary>Пошук за частиною назви.</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    /// <summary>Поле сортування (за замовчуванням — дата наступного списання).</summary>
    public SubscriptionSortField SortBy { get; set; } = SubscriptionSortField.NextChargeDate;

    /// <summary>Напрям сортування.</summary>
    public SortDirection SortDir { get; set; } = SortDirection.Asc;

    /// <summary>Лише для адміністратора: підписки конкретного власника.</summary>
    public int? OwnerId { get; set; }
}

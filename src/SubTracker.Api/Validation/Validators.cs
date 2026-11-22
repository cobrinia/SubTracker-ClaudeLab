using FluentValidation;
using SubTracker.Api.Contracts;
using SubTracker.Domain.Enums;

namespace SubTracker.Api.Validation;

// Прості правила (обов'язковість, довжина, діапазон, формат) задано атрибутами DataAnnotations у DTO —
// їх перевіряє [ApiController] ще до виклику дії. Правила, що зв'язують кілька полів або залежать від
// поточної дати, винесено у FluentValidation: їх виконує FluentValidationFilter.

/// <summary>Перехресні правила для створення підписки.</summary>
public sealed class CreateSubscriptionDtoValidator : AbstractValidator<CreateSubscriptionDto>
{
    public CreateSubscriptionDtoValidator(TimeProvider time)
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);

        RuleFor(x => x.Period).IsInEnum().WithMessage("Періодичність: Weekly, Monthly, Quarterly або Yearly.");
        RuleFor(x => x.NextChargeDate)
            .GreaterThanOrEqualTo(today.AddYears(-1)).WithMessage("Дата наступного списання не може бути більш ніж рік тому.")
            .LessThanOrEqualTo(today.AddYears(5)).WithMessage("Дата наступного списання — не далі ніж через 5 років.");
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.NextChargeDate).When(x => x.EndDate is not null)
            .WithMessage("Дата завершення не може бути раніше за наступне списання.");
        RuleFor(x => x.TrialEndDate)
            .LessThanOrEqualTo(x => x.NextChargeDate).When(x => x.TrialEndDate is not null)
            .WithMessage("Пробний період має закінчитися не пізніше за перше платне списання.");
    }
}

/// <summary>Перехресні правила для повної заміни підписки.</summary>
public sealed class UpdateSubscriptionDtoValidator : AbstractValidator<UpdateSubscriptionDto>
{
    public UpdateSubscriptionDtoValidator()
    {
        RuleFor(x => x.Period).IsInEnum().WithMessage("Періодичність: Weekly, Monthly, Quarterly або Yearly.");
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.NextChargeDate).When(x => x.EndDate is not null)
            .WithMessage("Дата завершення не може бути раніше за наступне списання.");
        RuleFor(x => x.TrialEndDate)
            .LessThanOrEqualTo(x => x.NextChargeDate).When(x => x.TrialEndDate is not null)
            .WithMessage("Пробний період має закінчитися не пізніше за наступне списання.");
    }
}

/// <summary>Статус має бути одним із допустимих значень.</summary>
public sealed class ChangeSubscriptionStatusDtoValidator : AbstractValidator<ChangeSubscriptionStatusDto>
{
    public ChangeSubscriptionStatusDtoValidator()
    {
        RuleFor(x => x.Status).NotNull().IsInEnum().WithMessage("Статус: Active, Paused або Cancelled.");
    }
}

/// <summary>Для картки обов'язкові Last4 і дійсний термін; для інших типів Last4 не вказується.</summary>
public sealed class CreatePaymentMethodDtoValidator : AbstractValidator<CreatePaymentMethodDto>
{
    public CreatePaymentMethodDtoValidator(TimeProvider time)
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);

        RuleFor(x => x.Type).IsInEnum().WithMessage("Тип: Card, PayPal, BankTransfer, GooglePay або ApplePay.");
        When(x => x.Type == PaymentMethodType.Card, () =>
        {
            RuleFor(x => x.Last4).NotEmpty().WithMessage("Для картки вкажіть останні 4 цифри.");
            RuleFor(x => x.ExpiryDate).NotNull().WithMessage("Для картки вкажіть термін дії.")
                .GreaterThanOrEqualTo(today).WithMessage("Термін дії картки вже минув.");
        }).Otherwise(() =>
        {
            RuleFor(x => x.Last4).Empty().WithMessage("Last4 вказується лише для карток.");
        });
    }
}

/// <summary>Максимум не менший за мінімум.</summary>
public sealed class SubscriptionQueryValidator : AbstractValidator<SubscriptionQuery>
{
    public SubscriptionQueryValidator()
    {
        RuleFor(x => x.MaxAmount)
            .GreaterThanOrEqualTo(x => x.MinAmount).When(x => x.MinAmount is not null && x.MaxAmount is not null)
            .WithMessage("maxAmount не може бути меншим за minAmount.");
        RuleFor(x => x.SortBy).IsInEnum();
        RuleFor(x => x.SortDir).IsInEnum();
    }
}

/// <summary>Період календаря: кінець не раніше початку, не довше року.</summary>
public sealed class CalendarQueryValidator : AbstractValidator<CalendarQuery>
{
    public CalendarQueryValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("Кінець періоду не може бути раніше за початок.");
        RuleFor(x => x)
            .Must(x => x.From is null || x.To is null || x.To.Value.DayNumber - x.From.Value.DayNumber <= 366)
            .WithName("To")
            .WithMessage("Період календаря — не довше 366 днів.");
    }
}

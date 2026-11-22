using Riok.Mapperly.Abstractions;
using SubTracker.Api.Contracts;
using SubTracker.Domain.Entities;

namespace SubTracker.Api.Mapping;

/// <summary>
/// Мапінг «сутність ↔ DTO» на основі Mapperly: код мапінгу генерується під час компіляції
/// (source generator), без рефлексії. Методи ProjectToDto будують Expression для IQueryable,
/// тому EF Core перетворює їх на SELECT лише потрібних стовпців.
/// Singleton: згенерований клас не має стану і потокобезпечний.
/// </summary>
[Mapper]
public partial class ApiMapper
{
    // ---------- Підписки ----------

    /// <summary>Сутність → DTO. CategoryName/PaymentMethodTitle/OwnerName Mapperly бере
    /// з навігацій Category.Name, PaymentMethod.Title, Owner.Name (flattening).</summary>
    [MapperIgnoreSource(nameof(Subscription.RowVersion))]
    [MapperIgnoreSource(nameof(Subscription.Payments))]
    [MapperIgnoreSource(nameof(Subscription.Members))]
    public partial SubscriptionDto ToDto(Subscription entity);

    /// <summary>Проєкція в SQL: SELECT лише полів SubscriptionDto з JOIN-ами за навігаціями.</summary>
    public partial IQueryable<SubscriptionDto> ProjectToDto(IQueryable<Subscription> query);

    /// <summary>Нова сутність із DTO створення. Ключ, власник, статус і навігації заповнює сервіс.</summary>
    [MapperIgnoreTarget(nameof(Subscription.Id))]
    [MapperIgnoreTarget(nameof(Subscription.OwnerId))]
    [MapperIgnoreTarget(nameof(Subscription.Status))]
    [MapperIgnoreTarget(nameof(Subscription.RowVersion))]
    [MapperIgnoreTarget(nameof(Subscription.Owner))]
    [MapperIgnoreTarget(nameof(Subscription.Category))]
    [MapperIgnoreTarget(nameof(Subscription.PaymentMethod))]
    [MapperIgnoreTarget(nameof(Subscription.Payments))]
    [MapperIgnoreTarget(nameof(Subscription.Members))]
    public partial Subscription ToEntity(CreateSubscriptionDto dto);

    /// <summary>PUT: перезаписує всі редаговані поля наявної (відстежуваної) сутності.</summary>
    [MapperIgnoreTarget(nameof(Subscription.Id))]
    [MapperIgnoreTarget(nameof(Subscription.OwnerId))]
    [MapperIgnoreTarget(nameof(Subscription.Status))]
    [MapperIgnoreTarget(nameof(Subscription.RowVersion))]
    [MapperIgnoreTarget(nameof(Subscription.Owner))]
    [MapperIgnoreTarget(nameof(Subscription.Category))]
    [MapperIgnoreTarget(nameof(Subscription.PaymentMethod))]
    [MapperIgnoreTarget(nameof(Subscription.Payments))]
    [MapperIgnoreTarget(nameof(Subscription.Members))]
    public partial void ApplyUpdate(UpdateSubscriptionDto dto, Subscription target);

    // ---------- Способи оплати ----------

    [MapperIgnoreSource(nameof(PaymentMethod.UserId))]
    [MapperIgnoreSource(nameof(PaymentMethod.User))]
    [MapperIgnoreSource(nameof(PaymentMethod.Subscriptions))]
    [MapperIgnoreSource(nameof(PaymentMethod.Payments))]
    public partial PaymentMethodDto ToDto(PaymentMethod entity);

    public partial IQueryable<PaymentMethodDto> ProjectToDto(IQueryable<PaymentMethod> query);

    [MapperIgnoreTarget(nameof(PaymentMethod.Id))]
    [MapperIgnoreTarget(nameof(PaymentMethod.UserId))]
    [MapperIgnoreTarget(nameof(PaymentMethod.LastUsedAt))]
    [MapperIgnoreTarget(nameof(PaymentMethod.User))]
    [MapperIgnoreTarget(nameof(PaymentMethod.Subscriptions))]
    [MapperIgnoreTarget(nameof(PaymentMethod.Payments))]
    public partial PaymentMethod ToEntity(CreatePaymentMethodDto dto);

    // ---------- Платежі ----------

    [MapperIgnoreSource(nameof(Payment.Subscription))]
    [MapperIgnoreSource(nameof(Payment.PaymentMethod))]
    public partial PaymentDto ToDto(Payment entity);

    public partial IQueryable<PaymentDto> ProjectToDto(IQueryable<Payment> query);
}

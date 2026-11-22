using SubTracker.Api.Contracts;
using SubTracker.Api.Security;

namespace SubTracker.Api.Services;

/// <summary>Налаштування продуктивності для навантажувального тестування «до / після».</summary>
public sealed class PerformanceOptions
{
    public const string SectionName = "Performance";

    /// <summary>
    /// true — оптимізовані запити (AsNoTracking, проєкція в DTO, пагінація і агрегування в SQL, кешування);
    /// false — «наївна» реалізація: відстежувані сутності з Include, сортування й пагінація в пам'яті.
    /// </summary>
    public bool Optimized { get; set; } = true;
}

/// <summary>Довідник категорій.</summary>
public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct);
    Task<CategoryDto> GetByIdAsync(int id, CancellationToken ct);
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto, CancellationToken ct);
    Task UpdateAsync(int id, UpdateCategoryDto dto, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

/// <summary>Підписки, їхні статуси та платежі.</summary>
public interface ISubscriptionService
{
    Task<PagedResponse<SubscriptionDto>> GetPageAsync(SubscriptionQuery query, CancellationToken ct);
    Task<SubscriptionDetailsDto> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>Мінімальні відомості для перевірки доступу (власник і учасники). 404, якщо підписки немає.</summary>
    Task<SubscriptionAccess> GetAccessAsync(int id, CancellationToken ct);

    Task<SubscriptionDto> CreateAsync(CreateSubscriptionDto dto, CancellationToken ct);
    Task UpdateAsync(int id, UpdateSubscriptionDto dto, CancellationToken ct);
    Task ChangeStatusAsync(int id, ChangeSubscriptionStatusDto dto, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);

    /// <summary>Провести списання: новий платіж + зсув дати наступного списання.</summary>
    Task<PaymentDto> ChargeAsync(int id, CancellationToken ct);
    Task<PagedResponse<PaymentDto>> GetPaymentsAsync(int id, PageQuery query, CancellationToken ct);
    Task<PaymentDto> GetPaymentAsync(int subscriptionId, int paymentId, CancellationToken ct);
}

/// <summary>Способи оплати поточного користувача.</summary>
public interface IPaymentMethodService
{
    Task<IReadOnlyList<PaymentMethodDto>> GetMineAsync(CancellationToken ct);
    Task<PaymentMethodDto> GetByIdAsync(int id, CancellationToken ct);
    Task<OwnedResource> GetAccessAsync(int id, CancellationToken ct);
    Task<PaymentMethodDto> CreateAsync(CreatePaymentMethodDto dto, CancellationToken ct);
    Task UpdateAsync(int id, UpdatePaymentMethodDto dto, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

/// <summary>Звіти: календар майбутніх списань і суми на місяць/рік.</summary>
public interface IReportService
{
    Task<IReadOnlyList<UpcomingChargeDto>> GetCalendarAsync(CalendarQuery query, CancellationToken ct);
    Task<IReadOnlyList<CurrencyTotalsDto>> GetTotalsAsync(CancellationToken ct);
}

/// <summary>Користувачі (адміністрування).</summary>
public interface IUserService
{
    Task<MeDto> GetMeAsync(CancellationToken ct);
    Task<PagedResponse<UserDto>> GetPageAsync(PageQuery query, CancellationToken ct);
}

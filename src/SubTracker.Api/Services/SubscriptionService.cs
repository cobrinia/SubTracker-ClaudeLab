using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubTracker.Api.Contracts;
using SubTracker.Api.Errors;
using SubTracker.Api.Infrastructure;
using SubTracker.Api.Mapping;
using SubTracker.Api.Security;
using SubTracker.DB;
using SubTracker.Domain;
using SubTracker.Domain.Billing;
using SubTracker.Domain.Entities;
using SubTracker.Domain.Enums;

namespace SubTracker.Api.Services;

/// <summary>
/// Бізнес-логіка підписок. Контролер лише приймає HTTP-запит і повертає код стану,
/// а правила предметної області зосереджені тут.
/// Scoped: працює з DbContext і ICurrentUserService, що живуть один запит.
/// </summary>
public sealed class SubscriptionService(
    SubTrackerDbContext db,
    ICurrentUserService currentUser,
    ApiMapper mapper,
    TimeProvider time,
    IOutputCacheStore cache,
    IOptions<PerformanceOptions> performance) : ISubscriptionService
{
    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    // ===================== Читання =====================

    public async Task<PagedResponse<SubscriptionDto>> GetPageAsync(SubscriptionQuery q, CancellationToken ct)
    {
        var user = await currentUser.GetUserAsync(ct);
        IQueryable<Subscription> query = db.Subscriptions;

        // Видимість: користувач бачить власні підписки і ті, де він учасник; адміністратор — усі.
        if (currentUser.IsAdmin)
        {
            if (q.OwnerId is { } ownerId)
                query = query.Where(s => s.OwnerId == ownerId);
        }
        else
        {
            query = query.Where(s => s.OwnerId == user.Id || s.Members.Any(m => m.UserId == user.Id));
        }

        // Фільтрація — завжди в SQL (WHERE).
        if (q.Status is { } status) query = query.Where(s => s.Status == status);
        if (q.CategoryId is { } categoryId) query = query.Where(s => s.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(q.Currency)) query = query.Where(s => s.Currency == q.Currency);
        if (q.MinAmount is { } min) query = query.Where(s => s.Amount >= min);
        if (q.MaxAmount is { } max) query = query.Where(s => s.Amount <= max);
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(s => s.Name.Contains(q.Search));

        return performance.Value.Optimized
            ? await GetPageOptimizedAsync(query, q, ct)
            : await GetPageNaiveAsync(query, q, ct);
    }

    /// <summary>Після оптимізації: COUNT + сторінка з OFFSET/FETCH і проєкцією лише потрібних стовпців.</summary>
    private async Task<PagedResponse<SubscriptionDto>> GetPageOptimizedAsync(
        IQueryable<Subscription> query, SubscriptionQuery q, CancellationToken ct)
    {
        query = query.AsNoTracking();
        var total = await query.CountAsync(ct);

        var page = await mapper
            .ProjectToDto(Sort(query, q).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize))
            .ToListAsync(ct);

        return new PagedResponse<SubscriptionDto>(page, q.Page, q.PageSize, total);
    }

    /// <summary>
    /// До оптимізації («наївна» реалізація, лишена для навантажувального тесту):
    /// завантажує ВСІ відповідні сутності з Include і відстеженням змін, а сортує й пагінує в пам'яті.
    /// </summary>
    private async Task<PagedResponse<SubscriptionDto>> GetPageNaiveAsync(
        IQueryable<Subscription> query, SubscriptionQuery q, CancellationToken ct)
    {
        var all = await query
            .Include(s => s.Category)
            .Include(s => s.Owner)
            .Include(s => s.PaymentMethod)
            .Include(s => s.Members)
            .ToListAsync(ct);

        var sorted = Sort(all.AsQueryable(), q).ToList();
        var page = sorted.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(s => mapper.ToDto(s)).ToList();
        return new PagedResponse<SubscriptionDto>(page, q.Page, q.PageSize, sorted.Count);
    }

    /// <summary>Стабільне сортування: вибране поле + Id як другий ключ (інакше сторінки «перемішуються»).</summary>
    private static IQueryable<Subscription> Sort(IQueryable<Subscription> query, SubscriptionQuery q) =>
        (q.SortBy, q.SortDir) switch
        {
            (SubscriptionSortField.Amount, SortDirection.Asc) => query.OrderBy(s => s.Amount).ThenBy(s => s.Id),
            (SubscriptionSortField.Amount, SortDirection.Desc) => query.OrderByDescending(s => s.Amount).ThenBy(s => s.Id),
            (SubscriptionSortField.Name, SortDirection.Asc) => query.OrderBy(s => s.Name).ThenBy(s => s.Id),
            (SubscriptionSortField.Name, SortDirection.Desc) => query.OrderByDescending(s => s.Name).ThenBy(s => s.Id),
            (_, SortDirection.Desc) => query.OrderByDescending(s => s.NextChargeDate).ThenBy(s => s.Id),
            _ => query.OrderBy(s => s.NextChargeDate).ThenBy(s => s.Id)
        };

    public async Task<SubscriptionDetailsDto> GetByIdAsync(int id, CancellationToken ct)
    {
        var dto = await mapper.ProjectToDto(db.Subscriptions.AsNoTracking().Where(s => s.Id == id))
                      .FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Підписку", id);

        var members = await db.SubscriptionMembers
            .AsNoTracking()
            .Where(m => m.SubscriptionId == id)
            .OrderBy(m => m.UserId)
            .Select(m => new SubscriptionMemberDto(m.UserId, m.User.Name, m.SharePercent))
            .ToListAsync(ct);

        var monthly = decimal.Round(BillingCalculator.ToMonthly(dto.Amount, dto.Period), 2);
        return new SubscriptionDetailsDto(dto, monthly, monthly * 12, members);
    }

    public async Task<SubscriptionAccess> GetAccessAsync(int id, CancellationToken ct)
    {
        var access = await db.Subscriptions
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new
            {
                OwnerExternalId = s.Owner.ExternalId,
                Members = s.Members.Where(m => m.User.ExternalId != null).Select(m => m.User.ExternalId!).ToList()
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Підписку", id);

        return new SubscriptionAccess(id, access.OwnerExternalId, access.Members);
    }

    // ===================== Створення і зміна =====================

    public async Task<SubscriptionDto> CreateAsync(CreateSubscriptionDto dto, CancellationToken ct)
    {
        var owner = await currentUser.GetUserAsync(ct);
        await ValidateReferencesAsync(owner.Id, dto.CategoryId, dto.PaymentMethodId, ct);
        await EnsureUniqueNameAsync(owner.Id, dto.Name.Trim(), exceptId: null, ct);

        var subscription = mapper.ToEntity(dto);
        subscription.Name = subscription.Name.Trim();
        subscription.OwnerId = owner.Id;
        subscription.Status = SubscriptionStatus.Active;

        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Subscriptions, ct);

        return await mapper.ProjectToDto(db.Subscriptions.AsNoTracking().Where(s => s.Id == subscription.Id))
            .FirstAsync(ct);
    }

    public async Task UpdateAsync(int id, UpdateSubscriptionDto dto, CancellationToken ct)
    {
        var subscription = await db.Subscriptions.FindAsync([id], ct) ?? throw new NotFoundException("Підписку", id);
        await ValidateReferencesAsync(subscription.OwnerId, dto.CategoryId, dto.PaymentMethodId, ct);
        await EnsureUniqueNameAsync(subscription.OwnerId, dto.Name.Trim(), exceptId: id, ct);

        // PUT замінює всі редаговані поля: повтор того самого запиту дає той самий стан (ідемпотентність).
        mapper.ApplyUpdate(dto, subscription);
        subscription.Name = subscription.Name.Trim();

        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Subscriptions, ct);
    }

    public async Task ChangeStatusAsync(int id, ChangeSubscriptionStatusDto dto, CancellationToken ct)
    {
        var subscription = await db.Subscriptions.FindAsync([id], ct) ?? throw new NotFoundException("Підписку", id);
        var target = dto.Status!.Value;

        if (subscription.Status == target)
            return; // той самий статус — нічого не змінюємо

        // Скасована підписка є кінцевим станом: відновити її не можна, лише створити нову.
        if (subscription.Status == SubscriptionStatus.Cancelled)
            throw new ConflictException($"Підписку «{subscription.Name}» скасовано — змінити її статус неможливо.");

        subscription.Status = target;
        if (target == SubscriptionStatus.Cancelled)
            subscription.EndDate ??= Today;

        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Subscriptions, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var subscription = await db.Subscriptions.FindAsync([id], ct) ?? throw new NotFoundException("Підписку", id);
        db.Subscriptions.Remove(subscription); // платежі й учасники — каскадом (ЛР № 2)
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Subscriptions, ct);
    }

    // ===================== Платежі =====================

    public async Task<PaymentDto> ChargeAsync(int id, CancellationToken ct)
    {
        var subscription = await db.Subscriptions
            .Include(s => s.PaymentMethod)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Підписку", id);

        if (subscription.Status != SubscriptionStatus.Active)
            throw new ConflictException($"Підписка «{subscription.Name}» має статус {subscription.Status} — списання неможливе.");

        if (subscription.PaymentMethod.IsExpiredOn(Today))
            throw new BusinessRuleException(
                $"Картка «{subscription.PaymentMethod.Title}» прострочена ({subscription.PaymentMethod.ExpiryDate:yyyy-MM-dd}) — списання відхилено.");

        var now = time.GetUtcNow().UtcDateTime;
        var payment = new Payment
        {
            PaymentMethodId = subscription.PaymentMethodId,
            ChargedAt = now,
            Amount = subscription.Amount,
            Currency = subscription.Currency,
            Status = PaymentStatus.Succeeded
        };
        subscription.Payments.Add(payment);

        subscription.NextChargeDate = BillingCalculator.NextDate(subscription.NextChargeDate, subscription.Period);
        if (subscription.EndDate is { } end && subscription.NextChargeDate > end)
            subscription.Status = SubscriptionStatus.Cancelled;
        subscription.PaymentMethod.LastUsedAt = now;

        // Один SaveChanges = одна транзакція для трьох таблиць (Payments, Subscriptions, PaymentMethods).
        // Паралельне списання тієї самої підписки отримає DbUpdateConcurrencyException (rowversion) → 409.
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Subscriptions, ct);

        return mapper.ToDto(payment);
    }

    public async Task<PagedResponse<PaymentDto>> GetPaymentsAsync(int id, PageQuery q, CancellationToken ct)
    {
        var query = db.Payments.AsNoTracking().Where(p => p.SubscriptionId == id);
        var total = await query.CountAsync(ct);
        var items = await mapper
            .ProjectToDto(query.OrderByDescending(p => p.ChargedAt).ThenByDescending(p => p.Id)
                .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize))
            .ToListAsync(ct);
        return new PagedResponse<PaymentDto>(items, q.Page, q.PageSize, total);
    }

    public async Task<PaymentDto> GetPaymentAsync(int subscriptionId, int paymentId, CancellationToken ct) =>
        await mapper.ProjectToDto(db.Payments.AsNoTracking()
                .Where(p => p.Id == paymentId && p.SubscriptionId == subscriptionId))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Платіж", paymentId);

    // ===================== Бізнес-правила =====================

    /// <summary>Категорія існує; спосіб оплати належить власнику і не прострочений → інакше 422.</summary>
    private async Task ValidateReferencesAsync(int ownerId, int categoryId, int paymentMethodId, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, ct))
            throw new BusinessRuleException($"Категорії з Id={categoryId} не існує.");

        var method = await db.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == paymentMethodId, ct);
        if (method is null || method.UserId != ownerId)
            throw new BusinessRuleException($"Спосіб оплати Id={paymentMethodId} не належить власнику підписки.");
        if (method.IsExpiredOn(Today))
            throw new BusinessRuleException($"Спосіб оплати «{method.Title}» прострочений.");
    }

    /// <summary>У власника не може бути двох підписок з однаковою назвою → 409.</summary>
    private async Task EnsureUniqueNameAsync(int ownerId, string name, int? exceptId, CancellationToken ct)
    {
        if (await db.Subscriptions.AnyAsync(s => s.OwnerId == ownerId && s.Name == name && s.Id != exceptId, ct))
            throw new ConflictException($"Підписка «{name}» у цього власника вже існує.");
    }
}

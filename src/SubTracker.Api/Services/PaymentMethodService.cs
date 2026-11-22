using Microsoft.EntityFrameworkCore;
using SubTracker.Api.Contracts;
using SubTracker.Api.Errors;
using SubTracker.Api.Mapping;
using SubTracker.Api.Security;
using SubTracker.DB;

namespace SubTracker.Api.Services;

/// <summary>Способи оплати поточного користувача. Scoped (DbContext, ICurrentUserService).</summary>
public sealed class PaymentMethodService(
    SubTrackerDbContext db,
    ICurrentUserService currentUser,
    ApiMapper mapper) : IPaymentMethodService
{
    public async Task<IReadOnlyList<PaymentMethodDto>> GetMineAsync(CancellationToken ct)
    {
        var user = await currentUser.GetUserAsync(ct);
        return await mapper
            .ProjectToDto(db.PaymentMethods.AsNoTracking().Where(p => p.UserId == user.Id).OrderBy(p => p.Title))
            .ToListAsync(ct);
    }

    public async Task<PaymentMethodDto> GetByIdAsync(int id, CancellationToken ct) =>
        await mapper.ProjectToDto(db.PaymentMethods.AsNoTracking().Where(p => p.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Спосіб оплати", id);

    public async Task<OwnedResource> GetAccessAsync(int id, CancellationToken ct)
    {
        var owner = await db.PaymentMethods
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.User.ExternalId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Спосіб оплати", id);
        return new OwnedResource(owner.ExternalId);
    }

    public async Task<PaymentMethodDto> CreateAsync(CreatePaymentMethodDto dto, CancellationToken ct)
    {
        var user = await currentUser.GetUserAsync(ct);
        var method = mapper.ToEntity(dto);
        method.Title = method.Title.Trim();
        method.UserId = user.Id;

        db.PaymentMethods.Add(method);
        await db.SaveChangesAsync(ct);
        return mapper.ToDto(method);
    }

    public async Task UpdateAsync(int id, UpdatePaymentMethodDto dto, CancellationToken ct)
    {
        var method = await db.PaymentMethods.FindAsync([id], ct) ?? throw new NotFoundException("Спосіб оплати", id);
        method.Title = dto.Title.Trim();
        method.ExpiryDate = dto.ExpiryDate;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var method = await db.PaymentMethods.FindAsync([id], ct) ?? throw new NotFoundException("Спосіб оплати", id);

        // DeleteBehavior.Restrict (ЛР № 2): не можна видалити картку, з якої оплачуються підписки
        // або вже були платежі — це фінансова історія.
        var subscriptions = await db.Subscriptions.CountAsync(s => s.PaymentMethodId == id, ct);
        var payments = await db.Payments.CountAsync(p => p.PaymentMethodId == id, ct);
        if (subscriptions > 0 || payments > 0)
            throw new ConflictException(
                $"Спосіб оплати «{method.Title}» використовують {subscriptions} підписок і {payments} платежів — видалення заборонене.");

        db.PaymentMethods.Remove(method);
        await db.SaveChangesAsync(ct);
    }
}

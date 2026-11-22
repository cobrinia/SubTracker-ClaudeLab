using Microsoft.EntityFrameworkCore;
using SubTracker.Api.Contracts;
using SubTracker.Api.Security;
using SubTracker.DB;

namespace SubTracker.Api.Services;

/// <summary>Профіль поточного користувача і список користувачів для адміністратора. Scoped.</summary>
public sealed class UserService(SubTrackerDbContext db, ICurrentUserService currentUser) : IUserService
{
    public async Task<MeDto> GetMeAsync(CancellationToken ct)
    {
        var user = await currentUser.GetUserAsync(ct);
        return new MeDto(user.Id, user.Name, user.Email, currentUser.ExternalId, currentUser.Roles);
    }

    public async Task<PagedResponse<UserDto>> GetPageAsync(PageQuery q, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.Id)
            .Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(u => new UserDto(u.Id, u.Name, u.Email, u.ExternalId != null, u.OwnedSubscriptions.Count, u.CreatedAt))
            .ToListAsync(ct);
        return new PagedResponse<UserDto>(items, q.Page, q.PageSize, total);
    }
}

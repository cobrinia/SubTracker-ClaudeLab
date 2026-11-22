using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SubTracker.DB;
using SubTracker.Domain.Entities;

namespace SubTracker.Api.Security;

/// <summary>Поточний користувач запиту: claims з JWT + відповідний запис у таблиці Users.</summary>
public interface ICurrentUserService
{
    /// <summary>Значення claim «sub» — ідентифікатор у Keycloak.</summary>
    string ExternalId { get; }

    /// <summary>Чи має користувач роль admin.</summary>
    bool IsAdmin { get; }

    /// <summary>Ролі з маркера доступу.</summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Id користувача в БД. Під час першого входу запис створюється автоматично (just-in-time provisioning)
    /// або зв'язується з наявним користувачем за e-mail.
    /// </summary>
    Task<User> GetUserAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Scoped: один екземпляр на HTTP-запит. Запам'ятовує знайденого користувача, щоб кілька сервісів
/// у межах одного запиту не шукали його в БД повторно; залежить від scoped DbContext.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor, SubTrackerDbContext db) : ICurrentUserService
{
    private User? _cached;

    private System.Security.Claims.ClaimsPrincipal Principal =>
        httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("Поточний користувач доступний лише в межах HTTP-запиту.");

    public string ExternalId =>
        Principal.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Маркер доступу не містить claim «sub».");

    public bool IsAdmin => Principal.IsInRole(Security.Roles.Admin);

    public IReadOnlyList<string> Roles =>
        Principal.FindAll("roles").Select(c => c.Value).Distinct().OrderBy(r => r).ToList();

    public async Task<User> GetUserAsync(CancellationToken cancellationToken)
    {
        if (_cached is not null)
            return _cached;

        var sub = ExternalId;
        var user = await db.Users.FirstOrDefaultAsync(u => u.ExternalId == sub, cancellationToken);
        if (user is not null)
            return _cached = user;

        var email = (Principal.FindFirst("email")?.Value ?? $"{sub}@users.subtracker.local").Trim().ToLowerInvariant();
        var name = Principal.FindFirst("name")?.Value
                   ?? Principal.FindFirst("preferred_username")?.Value
                   ?? email;

        // Користувач з таким e-mail уже є (тестові дані ЛР № 2) — прив'язуємо його до облікового запису Keycloak.
        user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.ExternalId == null, cancellationToken);
        if (user is not null)
        {
            user.ExternalId = sub;
        }
        else
        {
            user = new User { Name = name.Length > 100 ? name[..100] : name, Email = email, ExternalId = sub };
            db.Users.Add(user);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Паралельний перший запит того самого користувача вже створив запис — беремо його.
            db.ChangeTracker.Clear();
            user = await db.Users.FirstAsync(u => u.ExternalId == sub, cancellationToken);
        }

        return _cached = user;
    }
}

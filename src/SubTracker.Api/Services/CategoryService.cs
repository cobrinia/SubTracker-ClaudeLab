using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using SubTracker.Api.Contracts;
using SubTracker.Api.Errors;
using SubTracker.Api.Infrastructure;
using SubTracker.DB;
using SubTracker.Domain.Entities;

namespace SubTracker.Api.Services;

/// <summary>
/// Довідник категорій. Scoped: залежить від SubTrackerDbContext, який живе один HTTP-запит.
/// </summary>
public sealed class CategoryService(SubTrackerDbContext db, IOutputCacheStore cache) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct) =>
        await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Subscriptions.Count))
            .ToListAsync(ct);

    public async Task<CategoryDto> GetByIdAsync(int id, CancellationToken ct) =>
        await db.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Subscriptions.Count))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Категорію", id);

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto, CancellationToken ct)
    {
        var name = dto.Name.Trim();
        await EnsureUniqueAsync(name, exceptId: null, ct);

        var category = new Category { Name = name };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Categories, ct);

        return new CategoryDto(category.Id, category.Name, 0);
    }

    public async Task UpdateAsync(int id, UpdateCategoryDto dto, CancellationToken ct)
    {
        var category = await db.Categories.FindAsync([id], ct) ?? throw new NotFoundException("Категорію", id);
        var name = dto.Name.Trim();
        await EnsureUniqueAsync(name, exceptId: id, ct);

        // PUT ідемпотентний: та сама назва вдруге — той самий стан, жодної помилки.
        category.Name = name;
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Categories, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var category = await db.Categories.FindAsync([id], ct) ?? throw new NotFoundException("Категорію", id);

        // Бізнес-правило (і FK RESTRICT у БД): категорію, що використовується, видалити не можна.
        var used = await db.Subscriptions.CountAsync(s => s.CategoryId == id, ct);
        if (used > 0)
            throw new ConflictException($"Категорію «{category.Name}» використовують {used} підписок — спершу змініть їм категорію.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        await cache.EvictByTagAsync(CacheTags.Categories, ct);
    }

    private async Task EnsureUniqueAsync(string name, int? exceptId, CancellationToken ct)
    {
        // SQL Server порівнює рядки без урахування регістру (collation за замовчуванням).
        if (await db.Categories.AnyAsync(c => c.Name == name && c.Id != exceptId, ct))
            throw new ConflictException($"Категорія «{name}» вже існує.");
    }
}

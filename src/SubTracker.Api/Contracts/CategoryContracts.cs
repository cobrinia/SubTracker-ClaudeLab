using System.ComponentModel.DataAnnotations;

namespace SubTracker.Api.Contracts;

/// <summary>Категорія для читання.</summary>
/// <param name="Id">Ідентифікатор.</param>
/// <param name="Name">Назва (унікальна).</param>
/// <param name="SubscriptionCount">Скільки підписок належить до категорії.</param>
public sealed record CategoryDto(int Id, string Name, int SubscriptionCount);

/// <summary>Дані для створення категорії.</summary>
/// <param name="Name">Назва категорії, 2–50 символів.</param>
public sealed record CreateCategoryDto(
    [Required(ErrorMessage = "Назва категорії є обов'язковою.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Назва має містити від 2 до 50 символів.")]
    string Name);

/// <summary>Дані для повної заміни категорії (PUT).</summary>
/// <param name="Name">Нова назва категорії, 2–50 символів.</param>
public sealed record UpdateCategoryDto(
    [Required(ErrorMessage = "Назва категорії є обов'язковою.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Назва має містити від 2 до 50 символів.")]
    string Name);

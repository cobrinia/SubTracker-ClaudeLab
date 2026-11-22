using System.ComponentModel.DataAnnotations;

namespace SubTracker.Api.Contracts;

/// <summary>Параметри пагінації (рядок запиту <c>?page=1&amp;pageSize=20</c>).</summary>
public class PageQuery
{
    /// <summary>Номер сторінки, починаючи з 1.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Номер сторінки має бути не меншим за 1.")]
    public int Page { get; set; } = 1;

    /// <summary>Кількість елементів на сторінці (1–100).</summary>
    [Range(1, 100, ErrorMessage = "Розмір сторінки має бути від 1 до 100.")]
    public int PageSize { get; set; } = 20;
}

/// <summary>Напрям сортування.</summary>
public enum SortDirection { Asc, Desc }

/// <summary>Сторінка колекції разом із метаданими пагінації.</summary>
/// <typeparam name="T">Тип елемента.</typeparam>
/// <param name="Items">Елементи поточної сторінки.</param>
/// <param name="Page">Номер поточної сторінки.</param>
/// <param name="PageSize">Розмір сторінки.</param>
/// <param name="TotalCount">Загальна кількість елементів, що відповідають фільтру.</param>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>Загальна кількість сторінок.</summary>
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Чи існує попередня сторінка.</summary>
    public bool HasPrevious => Page > 1;

    /// <summary>Чи існує наступна сторінка.</summary>
    public bool HasNext => Page < TotalPages;
}

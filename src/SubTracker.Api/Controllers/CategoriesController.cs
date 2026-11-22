using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SubTracker.Api.Contracts;
using SubTracker.Api.Infrastructure;
using SubTracker.Api.Security;
using SubTracker.Api.Services;

namespace SubTracker.Api.Controllers;

/// <summary>
/// Довідник категорій. Читати можуть усі зареєстровані користувачі,
/// змінювати — лише адміністратор (policy AdminOnly).
/// </summary>
[Route("api/v{version:apiVersion}/categories")]
[Authorize(Policy = Policies.RegisteredUser)]
public sealed class CategoriesController(ICategoryService categories) : ApiControllerBase
{
    /// <summary>Усі категорії з кількістю підписок у кожній.</summary>
    /// <response code="200">Список категорій, упорядкований за назвою.</response>
    /// <response code="401">Немає або недійсний токен доступу.</response>
    [HttpGet(Name = "GetCategories")]
    [OutputCache(PolicyName = CachePolicies.Categories)]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken ct)
        => Ok(await categories.GetAllAsync(ct));

    /// <summary>Категорія за ідентифікатором.</summary>
    /// <param name="id">Ідентифікатор категорії.</param>
    /// <response code="200">Категорія.</response>
    /// <response code="404">Категорію не знайдено.</response>
    [HttpGet("{id:int}", Name = "GetCategory")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CategoryDto>> GetById(int id, CancellationToken ct)
        => await categories.GetByIdAsync(id, ct);

    /// <summary>Створити категорію (лише admin).</summary>
    /// <param name="dto">Назва нової категорії.</param>
    /// <response code="201">Категорію створено; заголовок Location містить її адресу.</response>
    /// <response code="400">Некоректні дані.</response>
    /// <response code="403">Потрібна роль admin.</response>
    /// <response code="409">Категорія з такою назвою вже існує.</response>
    [HttpPost(Name = "CreateCategory")]
    [Authorize(Policy = Policies.AdminOnly)]
    [Consumes("application/json")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryDto dto, CancellationToken ct)
    {
        var created = await categories.CreateAsync(dto, ct);
        return CreatedAtRoute("GetCategory", new { id = created.Id, version = "1" }, created);
    }

    /// <summary>Перейменувати категорію (лише admin). Ідемпотентно: повтор із тими самими даними дає той самий результат.</summary>
    /// <param name="id">Ідентифікатор категорії.</param>
    /// <param name="dto">Нова назва.</param>
    /// <response code="204">Зміни збережено.</response>
    /// <response code="400">Некоректні дані.</response>
    /// <response code="403">Потрібна роль admin.</response>
    /// <response code="404">Категорію не знайдено.</response>
    /// <response code="409">Категорія з такою назвою вже існує.</response>
    [HttpPut("{id:int}", Name = "UpdateCategory")]
    [Authorize(Policy = Policies.AdminOnly)]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Update(int id, UpdateCategoryDto dto, CancellationToken ct)
    {
        await categories.UpdateAsync(id, dto, ct);
        return NoContent();
    }

    /// <summary>Видалити категорію (лише admin). Категорію з підписками видалити не можна.</summary>
    /// <param name="id">Ідентифікатор категорії.</param>
    /// <response code="204">Категорію видалено.</response>
    /// <response code="403">Потрібна роль admin.</response>
    /// <response code="404">Категорію не знайдено.</response>
    /// <response code="409">У категорії є підписки.</response>
    [HttpDelete("{id:int}", Name = "DeleteCategory")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await categories.DeleteAsync(id, ct);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SubTracker.Api.Contracts;
using SubTracker.Api.Infrastructure;
using SubTracker.Api.Security;
using SubTracker.Api.Services;

namespace SubTracker.Api.Controllers;

/// <summary>
/// Підписки поточного користувача, їхні статуси та платежі.
/// Доступ до конкретної підписки перевіряється resource-based авторизацією:
/// читати — власник, учасник спільної підписки або admin; змінювати й видаляти — власник або admin.
/// </summary>
[Route("api/v{version:apiVersion}/subscriptions")]
[Authorize(Policy = Policies.RegisteredUser)]
public sealed class SubscriptionsController(ISubscriptionService subscriptions, IAuthorizationService authorization)
    : ApiControllerBase
{
    private const string ProblemJson = "application/problem+json";

    /// <summary>Сторінка підписок з фільтрацією та сортуванням.</summary>
    /// <remarks>
    /// Звичайний користувач бачить власні підписки і ті, де він учасник; admin — усі (або конкретного власника через ownerId).
    /// Приклад: <c>GET /api/v1/subscriptions?status=Active&amp;currency=UAH&amp;sortBy=Amount&amp;sortDir=Desc&amp;page=1&amp;pageSize=10</c>
    /// </remarks>
    /// <param name="query">Фільтри, сортування та параметри сторінки.</param>
    /// <response code="200">Сторінка підписок з метаданими (page, pageSize, totalCount, totalPages, hasNext, hasPrevious).</response>
    /// <response code="400">Некоректні параметри запиту (наприклад, pageSize &gt; 100 або maxAmount &lt; minAmount).</response>
    [HttpGet(Name = "GetSubscriptions")]
    [OutputCache(PolicyName = CachePolicies.Subscriptions)]
    [ProducesResponseType<PagedResponse<SubscriptionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    public async Task<ActionResult<PagedResponse<SubscriptionDto>>> GetPage(
        [FromQuery] SubscriptionQuery query, CancellationToken ct)
        => Ok(await subscriptions.GetPageAsync(query, ct));

    /// <summary>Підписка з деталями: місячний і річний еквівалент, учасники спільної підписки.</summary>
    /// <param name="id">Ідентифікатор підписки.</param>
    /// <response code="200">Підписка.</response>
    /// <response code="403">Підписка належить іншому користувачу.</response>
    /// <response code="404">Підписку не знайдено.</response>
    [HttpGet("{id:int}", Name = "GetSubscription")]
    [ProducesResponseType<SubscriptionDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<SubscriptionDetailsDto>> GetById(int id, CancellationToken ct)
    {
        var access = await subscriptions.GetAccessAsync(id, ct);
        if (await AuthorizeResourceAsync(authorization, access, ResourceOperations.Read) is { } denied)
            return denied;
        return await subscriptions.GetByIdAsync(id, ct);
    }

    /// <summary>Створити підписку. Власником стає поточний користувач.</summary>
    /// <param name="dto">Дані нової підписки.</param>
    /// <response code="201">Підписку створено; Location — адреса нового ресурсу.</response>
    /// <response code="400">Некоректні дані (валідація DataAnnotations / FluentValidation).</response>
    /// <response code="409">У користувача вже є підписка з такою назвою.</response>
    /// <response code="422">Порушено бізнес-правило: категорії немає, спосіб оплати чужий або прострочений.</response>
    [HttpPost(Name = "CreateSubscription")]
    [Consumes("application/json")]
    [ProducesResponseType<SubscriptionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    public async Task<ActionResult<SubscriptionDto>> Create(CreateSubscriptionDto dto, CancellationToken ct)
    {
        var created = await subscriptions.CreateAsync(dto, ct);
        return CreatedAtRoute("GetSubscription", new { id = created.Id, version = "1" }, created);
    }

    /// <summary>Повністю замінити дані підписки (ідемпотентно).</summary>
    /// <param name="id">Ідентифікатор підписки.</param>
    /// <param name="dto">Нові значення всіх змінюваних полів.</param>
    /// <response code="204">Зміни збережено.</response>
    /// <response code="400">Некоректні дані.</response>
    /// <response code="403">Змінювати може лише власник або admin.</response>
    /// <response code="404">Підписку не знайдено.</response>
    /// <response code="409">Назва вже зайнята або дані змінив інший запит (RowVersion).</response>
    /// <response code="422">Порушено бізнес-правило.</response>
    [HttpPut("{id:int}", Name = "UpdateSubscription")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    public async Task<IActionResult> Update(int id, UpdateSubscriptionDto dto, CancellationToken ct)
    {
        var access = await subscriptions.GetAccessAsync(id, ct);
        if (await AuthorizeResourceAsync(authorization, access, ResourceOperations.Update) is { } denied)
            return denied;
        await subscriptions.UpdateAsync(id, dto, ct);
        return NoContent();
    }

    /// <summary>Змінити статус: призупинити, відновити або скасувати.</summary>
    /// <remarks>Повтор із тим самим статусом нічого не змінює (204). Скасовану підписку відновити не можна (409).</remarks>
    /// <param name="id">Ідентифікатор підписки.</param>
    /// <param name="dto">Новий статус.</param>
    /// <response code="204">Статус змінено (або вже був таким).</response>
    /// <response code="400">Невідомий статус.</response>
    /// <response code="403">Змінювати може лише власник або admin.</response>
    /// <response code="404">Підписку не знайдено.</response>
    /// <response code="409">Підписку вже скасовано.</response>
    [HttpPatch("{id:int}/status", Name = "ChangeSubscriptionStatus")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> ChangeStatus(int id, ChangeSubscriptionStatusDto dto, CancellationToken ct)
    {
        var access = await subscriptions.GetAccessAsync(id, ct);
        if (await AuthorizeResourceAsync(authorization, access, ResourceOperations.Update) is { } denied)
            return denied;
        await subscriptions.ChangeStatusAsync(id, dto, ct);
        return NoContent();
    }

    /// <summary>Видалити підписку разом з її платежами та учасниками.</summary>
    /// <remarks>Ідемпотентно за станом сервера: повторний DELETE нічого не змінює і повертає 404.</remarks>
    /// <param name="id">Ідентифікатор підписки.</param>
    /// <response code="204">Підписку видалено.</response>
    /// <response code="403">Видаляти може лише власник або admin.</response>
    /// <response code="404">Підписку не знайдено (зокрема вже видалено).</response>
    [HttpDelete("{id:int}", Name = "DeleteSubscription")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var access = await subscriptions.GetAccessAsync(id, ct);
        if (await AuthorizeResourceAsync(authorization, access, ResourceOperations.Delete) is { } denied)
            return denied;
        await subscriptions.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Провести списання: створює платіж і переносить дату наступного списання на один період.</summary>
    /// <param name="id">Ідентифікатор підписки.</param>
    /// <response code="201">Платіж створено; Location — адреса платежу.</response>
    /// <response code="403">Списання проводить лише власник або admin.</response>
    /// <response code="404">Підписку не знайдено.</response>
    /// <response code="409">Підписка не активна (призупинена або скасована).</response>
    /// <response code="422">Термін дії способу оплати минув.</response>
    [HttpPost("{id:int}/charges", Name = "ChargeSubscription")]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, ProblemJson)]
    public async Task<ActionResult<PaymentDto>> Charge(int id, CancellationToken ct)
    {
        var access = await subscriptions.GetAccessAsync(id, ct);
        if (await AuthorizeResourceAsync(authorization, access, ResourceOperations.Update) is { } denied)
            return denied;
        var payment = await subscriptions.ChargeAsync(id, ct);
        return CreatedAtRoute("GetPayment", new { id, paymentId = payment.Id, version = "1" }, payment);
    }

    /// <summary>Історія платежів підписки (від новіших до старіших).</summary>
    /// <param name="id">Ідентифікатор підписки.</param>
    /// <param name="query">Номер і розмір сторінки.</param>
    /// <response code="200">Сторінка платежів з метаданими.</response>
    /// <response code="400">Некоректні параметри сторінки.</response>
    /// <response code="403">Немає доступу до підписки.</response>
    /// <response code="404">Підписку не знайдено.</response>
    [HttpGet("{id:int}/payments", Name = "GetPayments")]
    [ProducesResponseType<PagedResponse<PaymentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<PagedResponse<PaymentDto>>> GetPayments(
        int id, [FromQuery] PageQuery query, CancellationToken ct)
    {
        var access = await subscriptions.GetAccessAsync(id, ct);
        if (await AuthorizeResourceAsync(authorization, access, ResourceOperations.Read) is { } denied)
            return denied;
        return await subscriptions.GetPaymentsAsync(id, query, ct);
    }

    /// <summary>Один платіж підписки.</summary>
    /// <param name="id">Ідентифікатор підписки.</param>
    /// <param name="paymentId">Ідентифікатор платежу.</param>
    /// <response code="200">Платіж.</response>
    /// <response code="403">Немає доступу до підписки.</response>
    /// <response code="404">Підписку або платіж не знайдено.</response>
    [HttpGet("{id:int}/payments/{paymentId:int}", Name = "GetPayment")]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<PaymentDto>> GetPayment(int id, int paymentId, CancellationToken ct)
    {
        var access = await subscriptions.GetAccessAsync(id, ct);
        if (await AuthorizeResourceAsync(authorization, access, ResourceOperations.Read) is { } denied)
            return denied;
        return await subscriptions.GetPaymentAsync(id, paymentId, ct);
    }
}

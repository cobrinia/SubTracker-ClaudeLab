using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubTracker.Api.Contracts;
using SubTracker.Api.Security;
using SubTracker.Api.Services;

namespace SubTracker.Api.Controllers;

/// <summary>Способи оплати поточного користувача (картки, PayPal тощо). Чужі — лише для admin.</summary>
[Route("api/v{version:apiVersion}/payment-methods")]
[Authorize(Policy = Policies.RegisteredUser)]
public sealed class PaymentMethodsController(IPaymentMethodService methods, IAuthorizationService authorization)
    : ApiControllerBase
{
    private const string ProblemJson = "application/problem+json";

    /// <summary>Мої способи оплати.</summary>
    /// <response code="200">Список способів оплати поточного користувача.</response>
    [HttpGet(Name = "GetPaymentMethods")]
    [ProducesResponseType<IReadOnlyList<PaymentMethodDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentMethodDto>>> GetMine(CancellationToken ct)
        => Ok(await methods.GetMineAsync(ct));

    /// <summary>Спосіб оплати за ідентифікатором.</summary>
    /// <param name="id">Ідентифікатор.</param>
    /// <response code="200">Спосіб оплати.</response>
    /// <response code="403">Належить іншому користувачу.</response>
    /// <response code="404">Не знайдено.</response>
    [HttpGet("{id:int}", Name = "GetPaymentMethod")]
    [ProducesResponseType<PaymentMethodDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<PaymentMethodDto>> GetById(int id, CancellationToken ct)
    {
        var access = await methods.GetAccessAsync(id, ct);
        if (await AuthorizeResourceAsync(authorization, access, ResourceOperations.Read) is { } denied)
            return denied;
        return await methods.GetByIdAsync(id, ct);
    }

    /// <summary>Додати спосіб оплати. Для картки обов'язкові last4 і expiryDate.</summary>
    /// <param name="dto">Дані способу оплати.</param>
    /// <response code="201">Створено; Location — адреса ресурсу.</response>
    /// <response code="400">Некоректні дані.</response>
    [HttpPost(Name = "CreatePaymentMethod")]
    [Consumes("application/json")]
    [ProducesResponseType<PaymentMethodDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    public async Task<ActionResult<PaymentMethodDto>> Create(CreatePaymentMethodDto dto, CancellationToken ct)
    {
        var created = await methods.CreateAsync(dto, ct);
        return CreatedAtRoute("GetPaymentMethod", new { id = created.Id, version = "1" }, created);
    }

    /// <summary>Оновити назву і термін дії (наприклад, після перевипуску картки). Ідемпотентно.</summary>
    /// <param name="id">Ідентифікатор.</param>
    /// <param name="dto">Нові значення.</param>
    /// <response code="204">Збережено.</response>
    /// <response code="400">Некоректні дані.</response>
    /// <response code="403">Належить іншому користувачу.</response>
    /// <response code="404">Не знайдено.</response>
    [HttpPut("{id:int}", Name = "UpdatePaymentMethod")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> Update(int id, UpdatePaymentMethodDto dto, CancellationToken ct)
    {
        var access = await methods.GetAccessAsync(id, ct);
        if (await AuthorizeResourceAsync(authorization, access, ResourceOperations.Update) is { } denied)
            return denied;
        await methods.UpdateAsync(id, dto, ct);
        return NoContent();
    }

    /// <summary>Видалити спосіб оплати, якщо з нього нічого не оплачується.</summary>
    /// <param name="id">Ідентифікатор.</param>
    /// <response code="204">Видалено.</response>
    /// <response code="403">Належить іншому користувачу.</response>
    /// <response code="404">Не знайдено.</response>
    /// <response code="409">Спосіб оплати використовується підписками або платежами.</response>
    [HttpDelete("{id:int}", Name = "DeletePaymentMethod")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var access = await methods.GetAccessAsync(id, ct);
        if (await AuthorizeResourceAsync(authorization, access, ResourceOperations.Delete) is { } denied)
            return denied;
        await methods.DeleteAsync(id, ct);
        return NoContent();
    }
}

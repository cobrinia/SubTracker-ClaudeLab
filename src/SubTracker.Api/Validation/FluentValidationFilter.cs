using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace SubTracker.Api.Validation;

/// <summary>
/// Глобальний фільтр дій: для кожного аргументу дії шукає IValidator&lt;T&gt; і виконує його.
/// Пакет FluentValidation.AspNetCore з автоматичною валідацією більше не підтримується, тому валідатор
/// викликається явно — тут, в одному місці, а не в кожній дії контролера.
/// Помилки повертаються як 400 Bad Request у форматі ValidationProblemDetails (разом з помилками DataAnnotations).
/// Екземпляр фільтра створюється на кожен запит (Filters.Add&lt;T&gt;), сервіси беруться з області запиту.
/// </summary>
public sealed class FluentValidationFilter(IServiceProvider services, ProblemDetailsFactory problemDetailsFactory)
    : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
                continue;

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);

            foreach (var error in result.Errors)
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        if (!context.ModelState.IsValid)
        {
            var problem = problemDetailsFactory.CreateValidationProblemDetails(
                context.HttpContext, context.ModelState, StatusCodes.Status400BadRequest,
                title: "Помилка валідації вхідних даних");
            context.Result = new BadRequestObjectResult(problem)
            {
                ContentTypes = { "application/problem+json" }
            };
            return;
        }

        await next();
    }
}

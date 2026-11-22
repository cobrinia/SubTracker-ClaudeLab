using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SubTracker.Domain;

namespace SubTracker.Api.Errors;

/// <summary>
/// Глобальна обробка винятків (IExceptionHandler, .NET 8+). Перетворює відомі винятки на відповіді
/// у форматі ProblemDetails (RFC 9457) з правильним кодом стану, а для невідомих повертає 500
/// без внутрішніх деталей. Текст необробленого винятку додається лише в середовищі Development.
/// Реєструється як singleton (AddExceptionHandler): не має стану і не залежить від scoped-сервісів.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // Клієнт сам розірвав з'єднання — відповідати нікому.
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }

        var (status, title, safeDetail) = Map(exception);

        if (status >= 500)
            logger.LogError(exception, "Необроблений виняток під час {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            logger.LogInformation("Запит {Method} {Path} відхилено: {Status} {Message}",
                context.Request.Method, context.Request.Path, status, exception.Message);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // 4xx: повідомлення предметної області безпечне і корисне клієнту.
                // 5xx: деталі (текст винятку) — тільки в Development; у Production — нічого.
                Detail = safeDetail ?? (environment.IsDevelopment() ? exception.Message : null)
            }
        });
    }

    private static (int Status, string Title, string? SafeDetail) Map(Exception exception) => exception switch
    {
        NotFoundException e => (StatusCodes.Status404NotFound, "Ресурс не знайдено", e.Message),
        ConflictException e => (StatusCodes.Status409Conflict, "Конфлікт із поточним станом ресурсу", e.Message),
        BusinessRuleException e => (StatusCodes.Status422UnprocessableEntity, "Порушено бізнес-правило", e.Message),
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Конфлікт паралельного редагування",
            "Запис змінив інший користувач. Отримайте актуальні дані й повторіть запит."),
        DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } =>
            (StatusCodes.Status409Conflict, "Порушено унікальність", "Запис із такими даними вже існує."),
        DbUpdateException { InnerException: SqlException { Number: 547 } } =>
            (StatusCodes.Status409Conflict, "Порушено обмеження бази даних", "Операція порушує зв'язки між записами."),
        BadHttpRequestException e => (e.StatusCode, "Некоректний запит", e.Message),
        _ => (StatusCodes.Status500InternalServerError, "Внутрішня помилка сервера", null)
    };
}

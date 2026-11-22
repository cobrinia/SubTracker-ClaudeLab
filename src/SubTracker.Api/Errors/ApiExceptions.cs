namespace SubTracker.Api.Errors;

/// <summary>Запитаний ресурс не існує → 404 Not Found.</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} з Id={key} не знайдено.");

/// <summary>Запит коректний, але суперечить поточному стану ресурсу (дубль, ресурс використовується,
/// недопустимий перехід статусу) → 409 Conflict.</summary>
public sealed class ConflictException(string message) : Exception(message);

// Порушення бізнес-правила предметної області (прострочена картка, чужий спосіб оплати,
// дата завершення раніше за списання) — SubTracker.Domain.BusinessRuleException → 422 Unprocessable Entity.

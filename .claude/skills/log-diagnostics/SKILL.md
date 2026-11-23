---
name: log-diagnostics
description: Пошук причини помилки SubTracker.Api за логами Serilog, ProblemDetails і історією git. Використовуй, коли API повертає 500, не стартує, тести падають або "у мене не працює, а в колеги працює".
---

# Діагностика за логами

1. Знайди найновіший файл: src/SubTracker.Api/logs/api-*.log. Візьми останні рядки [ERR] і [FTL] разом зі stack trace
   (наприклад: `grep -n -A 25 "\[ERR\]\|\[FTL\]" <файл> | tail -80`).
2. Якщо логів немає — попроси повторити запит; у Development текст 500 є і в полі detail ProblemDetails.
3. За stack trace відкрий файл і рядок коду (Controllers → Services → DbContext).
4. Що змінилося: `git log --oneline -15`, `git log -p -3 -- <файл>`, після pull — `git diff ORIG_HEAD..HEAD --stat`.
5. Типові причини в SubTracker:
   - Invalid column name / Invalid object name → незастосована міграція → скіл ef-migrations
   - 401 / 403 → скіл keycloak-auth
   - "Не задано ConnectionStrings:SubTrackerDb" → немає user secrets (LAB.md)
   - A network-related or instance-specific error → LocalDB не запущена (`sqllocaldb info`, `sqllocaldb start MSSQLLocalDB`)
   - "file is locked" при build → запущений SubTracker.Api
   - type or namespace not found після pull → новий NuGet-пакет → `dotnet restore`
   - 409 "Конфлікт паралельного редагування" → RowVersion, це очікувана поведінка
6. Відповідь у форматі: Причина (1 речення) → Доказ (рядок логу + коміт/автор) → Виправлення (команди).
   Нічого не змінюй, доки користувач не підтвердить.

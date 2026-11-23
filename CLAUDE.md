# SubTracker (пісочниця SubTracker-ClaudeLab)

Менеджер підписок і регулярних платежів — КПЗ, варіант 10 (ЛР № 2 — ADO.NET/EF Core, ЛР № 3 — Web API).
.NET 10, EF Core 10, SQL Server LocalDB, ASP.NET Core Web API, Keycloak 26 (JWT), Mapperly, FluentValidation, Scalar, Serilog.
Це навчальна копія: БД `SubTrackerLabDb*`, власні user secrets (див. LAB.md). Оригінал — ..\SubTracker, його не чіпати.

## Проєкти (SubTracker.slnx)
- src/SubTracker.Domain — User, Category, PaymentMethod, Subscription (RowVersion, TrialEndDate), Payment, SubscriptionMember; BillingCalculator
- src/SubTracker.DB — SubTrackerDbContext, Migrations/, сідинг
- src/SubTracker.AdoNet — ADO.NET-запити; src/SubTracker.Console — демо-меню ЛР № 2 + DesignTimeDbContextFactory
- src/SubTracker.Scaffolded — scaffold з БД (ЛР № 2)
- src/SubTracker.Api — 24 кінцеві точки /api/v1/… (Controllers → Services → DbContext)
- tests/SubTracker.Api.Tests — 12 інтеграційних тестів (WebApplicationFactory, середовище Testing, БД SubTrackerLabDb_Tests)

## Команди
- Збірка: `dotnet build SubTracker.slnx`
- Тести: `dotnet test tests/SubTracker.Api.Tests`
- Запуск API: `dotnet run --project src/SubTracker.Api --launch-profile http` → http://localhost:5080, Scalar: /scalar/v1
- Keycloak: `scripts\keycloak-start.cmd` (порт 8080; користувачі oleksandr / maria / andriy / admin)
- Нова міграція: `dotnet ef migrations add <Name> --project src/SubTracker.DB --startup-project src/SubTracker.Console`
- Оновити БД: `dotnet ef database update --project src/SubTracker.DB --startup-project src/SubTracker.Console`
- Стан міграцій: `dotnet ef migrations list --project src/SubTracker.DB --startup-project src/SubTracker.Console`

## Логи і помилки
- Логи API: src/SubTracker.Api/logs/api-<yyyyMMdd>.log (Serilog; рядок на кожен запит + [ERR] зі stack trace).
- Винятки → Errors/GlobalExceptionHandler.cs → ProblemDetails: 404/409/422/500; текст 500 лише в Development.
- У Development `Database:MigrateOnStartup = true` — API сам застосовує міграції при старті.

## Правила
- Не редагувати вручну файли в src/SubTracker.DB/Migrations/ (зокрема SubTrackerDbContextModelSnapshot.cs) — лише через `dotnet ef`.
- Перед збіркою зупинити запущений SubTracker.Api (інакше "file is locked").
- HTTPS-редирект у Development вимкнено навмисно: під час 307 клієнт губить Authorization → 401.
- Рядок підключення — у user secrets, не в appsettings і не в git.
- Перед змінами — `git status` і `git fetch`; перед push — build і test.
- Заборонено `git push --force` у main і `git reset --hard` без мого підтвердження. Теги можна перевішувати з --force.
- Спочатку діагноз і план, потім зміни. Коміти українською: `feat: ...`, `fix: ...`, `docs: ...`, `chore: ...`.

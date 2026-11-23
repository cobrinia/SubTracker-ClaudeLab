---
name: ef-migrations
description: Створення, застосування, відкат і виправлення міграцій EF Core у src/SubTracker.DB. Використовуй, коли змінюється модель у SubTracker.Domain, БД не відповідає коду (Invalid column name / Invalid object name), треба відкотити міграцію або є конфлікт у Migrations/ чи SubTrackerDbContextModelSnapshot.cs.
---

# Міграції EF Core (SQL Server LocalDB)

Усі команди `dotnet ef` — з параметрами:
`--project src/SubTracker.DB --startup-project src/SubTracker.Console`
(DesignTimeDbContextFactory у Console читає рядок підключення з user secrets — БД SubTrackerLabDb).

## Нова міграція
1. Зміни сутність у src/SubTracker.Domain і, якщо треба, конфігурацію в src/SubTracker.DB.
2. `dotnet ef migrations list` — переконайся, що немає Pending-міграцій від колег.
3. `dotnet ef migrations add <PascalCaseName>` — ім'я описує зміну (AddSubscriptionNotes).
4. Переглянь згенерований Up/Down. Для NOT NULL колонки — defaultValue для наявних рядків.
5. `dotnet ef database update`.
6. Онови DTO (src/SubTracker.Api/Contracts), маппер (Mapping/ApiMapper.cs) і валідатори, якщо поле видно в API.
7. `dotnet build SubTracker.slnx` і `dotnet test tests/SubTracker.Api.Tests`.

## "Invalid column name" / "Invalid object name"
Код новіший за БД. `dotnet ef migrations list` → застосувати Pending через `dotnet ef database update`.
API в Development робить це сам при старті (Database:MigrateOnStartup), Console і тести — ні.

## Конфлікт у Migrations/ (паралельні міграції двох людей)
*ModelSnapshot.cs НЕ мерджити вручну (редагування Migrations/ заборонене в settings.json):
1. Відкотити свою БД до останньої спільної міграції: `dotnet ef database update <ОстанняСпільна>`
   (зробити до merge або після `git merge --abort`).
2. Snapshot взяти з main: merge → `git checkout --theirs <snapshot>`; rebase → `git checkout --ours <snapshot>`.
3. Видалити СВОЇ файли міграції (`git rm .../*_<Name>.cs .../*_<Name>.Designer.cs`).
4. `dotnet ef migrations add <Name>` — міграція перестворюється поверх чужої.
5. `dotnet ef database update`, build, test.

## Заборонено
- Видаляти чи змінювати міграцію, яка вже є в origin/main.
- `dotnet ef migrations remove` для вже запушеної міграції.
- `dotnet ef database drop` без підтвердження користувача.

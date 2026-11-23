# SubTracker-ClaudeLab — пісочниця для навчання з Claude

Це копія `C:\POLITEH\KPZ\SubTracker` (`git clone`, уся історія до `d8151ea` / тег `lab3`).
Тут можна ламати, мерджити й тренуватися — оригінал і його БД не зачіпаються.

## Чим відрізняється від оригіналу
| Що | Оригінал | Пісочниця |
| --- | --- | --- |
| UserSecretsId (Api + Console) | 825e9d5f-… | c2cfcb5b-812c-4896-9d4c-92885baf4327 |
| Основна БД | SubTrackerDb | SubTrackerLabDb (задати в user secrets, див. нижче) |
| БД тестів | SubTrackerDb_Tests | SubTrackerLabDb_Tests |
| БД навантаження (k6) | SubTrackerDb_Load | SubTrackerLabDb_Load |
| Keycloak | tools\ у проєкті | використовує ..\SubTracker\tools (без повторного завантаження) |
| git remote | origin = GitHub SubTracker | поки немає — додати новий репозиторій |

## Перший запуск (Windows, з кореня цієї папки)
```cmd
dotnet user-secrets set "ConnectionStrings:SubTrackerDb" "Server=(localdb)\MSSQLLocalDB;Database=SubTrackerLabDb;Trusted_Connection=True;TrustServerCertificate=True" --project src\SubTracker.Api
dotnet tool restore
dotnet ef database update --project src\SubTracker.DB --startup-project src\SubTracker.Console
dotnet build SubTracker.slnx
dotnet test tests\SubTracker.Api.Tests
```
Console і Api мають однаковий UserSecretsId, тому секрет задається один раз.

## Новий репозиторій на GitHub (без gh CLI)
1. https://github.com/new → Repository name `SubTracker-ClaudeLab`, Private, **без** README/.gitignore/license → Create.
2. У PowerShell **з цієї папки** (не з ..\SubTracker!):
```powershell
cd C:\POLITEH\KPZ\SubTracker-ClaudeLab
git remote add origin https://github.com/cobrinia/SubTracker-ClaudeLab.git
git push -u origin main
git push origin --tags
```
3. Settings → Collaborators → Add people — другий учасник.

(Або встановити GitHub CLI: `winget install GitHub.cli`, перезапустити термінал, `gh auth login`, далі
`gh repo create cobrinia/SubTracker-ClaudeLab --private --source=. --remote=origin --push`.)

## Що вже налаштовано для Claude
- CLAUDE.md — інструкції проєкту; .claude/settings.json — дозволи (Migrations/ заборонено редагувати вручну).
- Скіли: ef-migrations, log-diagnostics, git-safe-sync, keycloak-auth.
- Агенти: log-investigator (лише читає), merge-resolver.
- Команди: /sync, /migration.
- Serilog: src\SubTracker.Api\logs\api-<дата>.log.

Keycloak одночасно з оригіналом не запускати — обидва на порту 8080.

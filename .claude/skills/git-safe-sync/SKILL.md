---
name: git-safe-sync
description: Безпечна синхронізація з origin (fetch, pull --rebase, merge, push) без втрати комітів і з розв'язанням конфліктів. Використовуй для "запуль", "запуш", "push rejected", "fetch first", "у нас два коміти", merge/rebase conflict.
---

# Безпечна синхронізація

## Порядок (завжди)
1. `git status` — незакомічене? Запропонуй закомітити або `git stash push -u -m "wip"`.
2. `git branch backup/<дата>-<коротко>` — страховка до будь-якого rebase/merge.
3. `git fetch origin --tags`.
4. Покажи користувачу:
   - мої коміти: `git log --oneline origin/main..HEAD`
   - нові з origin: `git log --oneline HEAD..origin/main`
   - файли, змінені обома: перетин `git diff --name-only $(git merge-base HEAD origin/main) HEAD` і `... origin/main`
5. Нових на origin немає → просто `git push origin main` (fast-forward).
6. Є нові → `git pull --rebase origin main`.
   - конфлікт → розв'язати, зберігаючи зміни ОБОХ сторін (агент merge-resolver), `git add`, `git rebase --continue`
   - щось пішло не так → `git rebase --abort` і назад до кроку 4
   - конфлікт у Migrations/ → скіл ef-migrations
7. Якщо у вхідних комітах є нові міграції → `dotnet ef database update`.
8. `dotnet build SubTracker.slnx` і `dotnet test tests/SubTracker.Api.Tests` — мають бути зеленими.
9. `git push origin main` — без --force.
10. Теги: після rebase тег вказує на старий хеш → `git tag -f <tag> <новий хеш>`; тег пушиться з --force лише з підтвердження.
11. `git stash pop`, якщо був stash. Резервну гілку видалити лише після перевірки (`git branch -d`).

## Відновлення
`git reflog` — усі попередні стани HEAD; `git switch backup/...` — повернутись до копії.

## Заборонено
`git push --force` у main, `git reset --hard` без підтвердження, видалення чужих змін під час розв'язання конфлікту.

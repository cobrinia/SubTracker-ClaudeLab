@echo off
chcp 65001 >nul
rem ===================================================================
rem  Локальний Keycloak (OIDC-провайдер) для SubTracker API, ЛР № 3.
rem  Перший запуск: завантажує Keycloak у tools\ (тека в .gitignore),
rem  імпортує realm «subtracker» з keycloak\realm-subtracker.json.
rem  Адмін-консоль: http://localhost:8080/admin  (admin / admin)
rem ===================================================================
setlocal
set KC_VERSION=26.8.2
set ROOT=%~dp0..
set TOOLS=%ROOT%\tools
set KC_HOME=%TOOLS%\keycloak-%KC_VERSION%

where java >nul 2>nul
if errorlevel 1 (
    if exist "%USERPROFILE%\.jdks\openjdk-24.0.2+12-54\bin\java.exe" (
        set "JAVA_HOME=%USERPROFILE%\.jdks\openjdk-24.0.2+12-54"
        set "PATH=%USERPROFILE%\.jdks\openjdk-24.0.2+12-54\bin;%PATH%"
    ) else (
        echo Не знайдено Java 21+. Встановіть JDK і повторіть.
        exit /b 1
    )
)

if not exist "%KC_HOME%\bin\kc.bat" (
    if not exist "%TOOLS%" mkdir "%TOOLS%"
    echo Завантаження Keycloak %KC_VERSION% ...
    curl -L -o "%TOOLS%\keycloak-%KC_VERSION%.zip" https://github.com/keycloak/keycloak/releases/download/%KC_VERSION%/keycloak-%KC_VERSION%.zip || exit /b 1
    tar -xf "%TOOLS%\keycloak-%KC_VERSION%.zip" -C "%TOOLS%" || exit /b 1
)

if not exist "%KC_HOME%\data\import" mkdir "%KC_HOME%\data\import"
copy /Y "%ROOT%\keycloak\realm-subtracker.json" "%KC_HOME%\data\import\" >nul

set KC_BOOTSTRAP_ADMIN_USERNAME=admin
set KC_BOOTSTRAP_ADMIN_PASSWORD=admin
call "%KC_HOME%\bin\kc.bat" start-dev --http-port 8080 --import-realm

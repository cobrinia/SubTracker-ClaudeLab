# Навантажувальне тестування «до / після» (ЛР № 3, п. 12).
# 1) окрема БД SubTrackerDb_Load + N тестових підписок;
# 2) API з Performance:Optimized=false → k6 → results/before.json;
# 3) API з Performance:Optimized=true  → k6 → results/after.json.
# Keycloak має бути запущений (scripts\keycloak-start.cmd).
param(
    [int]$Records = 5000,
    [int]$Vus = 50,
    [string]$Duration = '60s'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$api = Join-Path $root 'src\SubTracker.Api'
$results = Join-Path $root 'loadtests\results'
New-Item -ItemType Directory -Force $results | Out-Null

$env:ConnectionStrings__SubTrackerDb = 'Server=(localdb)\MSSQLLocalDB;Database=SubTrackerDb_Load;Trusted_Connection=True;TrustServerCertificate=True'
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:Oidc__RequireHttpsMetadata = 'false'
$env:ASPNETCORE_URLS = 'http://localhost:5080'
$env:Logging__LogLevel__Default = 'Warning'

Write-Host '== Збірка (Release)' -ForegroundColor Cyan
dotnet build $api -c Release --nologo -v q | Out-Host

Write-Host "== Наповнення БД: $Records підписок" -ForegroundColor Cyan
dotnet run --project $api -c Release --no-build -- --seed-load-data $Records | Out-Host

function Invoke-Run([string]$name, [string]$optimized) {
    $env:Performance__Optimized = $optimized
    Write-Host "== Запуск API (Optimized=$optimized)" -ForegroundColor Cyan
    $proc = Start-Process dotnet -ArgumentList @('run', '--project', $api, '-c', 'Release', '--no-build', '--no-launch-profile') -PassThru -WindowStyle Minimized
    try {
        for ($i = 0; $i -lt 60; $i++) {
            try { Invoke-WebRequest 'http://localhost:5080/api/v1/me' -UseBasicParsing -TimeoutSec 2 | Out-Null } catch {
                if ($_.Exception.Response) { break } # 401 — сервер уже відповідає
            }
            Start-Sleep -Seconds 1
        }
        # Прогрів (JIT, пул з'єднань), щоб не змішувати холодний старт з вимірюванням.
        k6 run --quiet --vus 5 --duration 10s (Join-Path $root 'loadtests\subtracker-load.js') -e VUS=5 -e DURATION=5s | Out-Null
        Write-Host "== k6: $name" -ForegroundColor Cyan
        k6 run (Join-Path $root 'loadtests\subtracker-load.js') -e VUS=$Vus -e DURATION=$Duration `
            --summary-export (Join-Path $results "$name.json") | Tee-Object (Join-Path $results "$name.txt")
        $p = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
        $children = Get-CimInstance Win32_Process -Filter "ParentProcessId=$($proc.Id)"
        foreach ($c in $children) {
            $cp = Get-Process -Id $c.ProcessId -ErrorAction SilentlyContinue
            if ($cp) { "{0}: CPU {1:N1} c, пам'ять {2:N0} МБ" -f $name, $cp.TotalProcessorTime.TotalSeconds, ($cp.PeakWorkingSet64 / 1MB) |
                Tee-Object -Append (Join-Path $results 'server-resources.txt') }
        }
    }
    finally {
        Get-CimInstance Win32_Process -Filter "ParentProcessId=$($proc.Id)" | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
}

Remove-Item (Join-Path $results 'server-resources.txt') -ErrorAction SilentlyContinue
Invoke-Run 'before' 'false'
Invoke-Run 'after' 'true'
Write-Host "Готово. Результати: $results" -ForegroundColor Green

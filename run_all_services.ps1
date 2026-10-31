# ==============================================================================
# ECOLINK PLATFORM - POWERSHELL RUNNER FOR ALL SERVICES
# ==============================================================================
$RootPath = $PSScriptRoot

Write-Host "==============================================================================" -ForegroundColor Green
Write-Host "       ECOLINK PLATFORM - KHOI DONG TOAN BO DICH VU DEV (POWERSHELL)" -ForegroundColor Green
Write-Host "==============================================================================" -ForegroundColor Green
Write-Host " 1. Python FastAPI AI Service  : http://localhost:8000" -ForegroundColor Cyan
Write-Host " 2. ASP.NET Core Web API       : http://localhost:5000" -ForegroundColor Cyan
Write-Host " 3. ngrok HTTP Tunneling       : http://localhost:5000 -> Public URL" -ForegroundColor Cyan
Write-Host " 4. ngrok Web Inspector        : http://localhost:4040" -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Green

# 0. Docker check
Write-Host "`n[0/3] Kiem tra ha tang CSDL Docker..." -ForegroundColor Yellow
try {
    $null = docker ps 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  [+] Docker dang chay. Khoi dong container ecolink-db..." -ForegroundColor Green
        docker start ecolink-db 2>$null | Out-Null
        Write-Host "  [+] Container CSDL PostgreSQL ecolink-db da san sang." -ForegroundColor Green
    } else {
        Write-Host "  [!] Docker Desktop chua bat. Bo qua container Docker." -ForegroundColor DarkYellow
    }
} catch {
    Write-Host "  [!] Bo qua kiem tra Docker." -ForegroundColor DarkGray
}

# 1. Python AI Service
Write-Host "`n[1/3] Khoi chay AI Vision Service (Port 8000)..." -ForegroundColor Yellow
$PythonExec = "python"
if (Test-Path "$RootPath\venv\Scripts\python.exe") {
    $PythonExec = "$RootPath\venv\Scripts\python.exe"
    Write-Host "  [+] Su dung venv: .\venv\Scripts\python.exe" -ForegroundColor Green
}

$aiCmd = "cd '$RootPath'; `$env:PYTHONPATH='$RootPath\src\Ecolink.AiService;' + `$env:PYTHONPATH; & '$PythonExec' -m uvicorn app:app --app-dir src/Ecolink.AiService --host 0.0.0.0 --port 8000 --reload"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$Host.UI.RawUI.WindowTitle='EcoLink AI Vision Service (8000)'; $aiCmd"

Start-Sleep -Seconds 3

# 2. ASP.NET Core Web API
Write-Host "`n[2/3] Khoi chay ASP.NET Core Web API (Port 5000)..." -ForegroundColor Yellow
$webCmd = "cd '$RootPath'; dotnet run --project src/Ecolink.Backend/Ecolink.WebApi --urls 'http://0.0.0.0:5000'"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$Host.UI.RawUI.WindowTitle='EcoLink Core Web API (5000)'; $webCmd"

Start-Sleep -Seconds 4

# 3. ngrok Tunneling
Write-Host "`n[3/3] Mo ngrok Tunneling cho cong 5000..." -ForegroundColor Yellow
if (Get-Command ngrok -ErrorAction SilentlyContinue) {
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$Host.UI.RawUI.WindowTitle='EcoLink ngrok Tunnel (5000)'; ngrok http 5000"
    Write-Host "  [+] Da mo tien trinh ngrok Tunneling!" -ForegroundColor Green
} else {
    Write-Host "  [X] Khong tim thay lenh 'ngrok' trong PATH." -ForegroundColor Red
}

Write-Host "`n==============================================================================" -ForegroundColor Green
Write-Host "  DA KHOI CHAY CAC DICH VU TRONG CAC CUA SO RIENG BIET!" -ForegroundColor Green
Write-Host "  - Swagger Backend API : http://localhost:5000/swagger" -ForegroundColor White
Write-Host "  - Swagger AI Vision   : http://localhost:8000/docs" -ForegroundColor White
Write-Host "  - ngrok Web Inspector : http://localhost:4040" -ForegroundColor White
Write-Host "==============================================================================" -ForegroundColor Green

# ==============================================================================
# ECOLINK PLATFORM - POWERSHELL RUNNER FOR ALL SERVICES
# ==============================================================================
$RootPath = $PSScriptRoot
$FrontendPath = Join-Path $RootPath "..\ecolink-web"

# Bo sung duong dan mac dinh cua cloudflared neu cua so PowerShell hien tai chua nap PATH moi
foreach ($cfDir in @("${env:ProgramFiles(x86)}\cloudflared", "$env:ProgramFiles\cloudflared")) {
    if ((Test-Path (Join-Path $cfDir "cloudflared.exe")) -and ($env:Path -notlike "*$cfDir*")) {
        $env:Path = "$env:Path;$cfDir"
    }
}

Write-Host "==============================================================================" -ForegroundColor Green
Write-Host "       ECOLINK PLATFORM - KHOI DONG TOAN BO DICH VU DEV (POWERSHELL)" -ForegroundColor Green
Write-Host "==============================================================================" -ForegroundColor Green
Write-Host " 1. Python FastAPI AI Service  : http://localhost:8000" -ForegroundColor Cyan
Write-Host " 2. ASP.NET Core Web API       : http://localhost:5000" -ForegroundColor Cyan
Write-Host " 3. EcoLink Frontend Web (Vite): http://localhost:5173" -ForegroundColor Cyan
Write-Host " 4. ngrok Backend Tunnel       : http://localhost:5000 -> *.ngrok-free.dev" -ForegroundColor Cyan
Write-Host " 5. Cloudflare Frontend Tunnel : http://localhost:5173 -> *.trycloudflare.com" -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Green

# 0. Docker check
Write-Host "`n[0/5] Kiem tra ha tang CSDL Docker..." -ForegroundColor Yellow
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
Write-Host "`n[1/5] Kiem tra / Khoi chay AI Vision Service (Port 8000)..." -ForegroundColor Yellow
$aiListening = Get-NetTCPConnection -LocalPort 8000 -State Listen -ErrorAction SilentlyContinue
if ($aiListening) {
    Write-Host "  [+] AI Vision Service dang chay san tai cong 8000." -ForegroundColor Green
} else {
    $PythonExec = "python"
    if (Test-Path "$RootPath\venv\Scripts\python.exe") {
        $PythonExec = "$RootPath\venv\Scripts\python.exe"
        Write-Host "  [+] Su dung venv: .\venv\Scripts\python.exe" -ForegroundColor Green
    }

    $aiCmd = "cd '$RootPath'; `$env:PYTHONPATH='$RootPath\src\Ecolink.AiService;' + `$env:PYTHONPATH; & '$PythonExec' -m uvicorn app:app --app-dir src/Ecolink.AiService --host 0.0.0.0 --port 8000 --reload"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$Host.UI.RawUI.WindowTitle='EcoLink AI Vision Service (8000)'; $aiCmd"
    Start-Sleep -Seconds 3
}

# 2. ASP.NET Core Web API
Write-Host "`n[2/5] Kiem tra / Khoi chay ASP.NET Core Web API (Port 5000)..." -ForegroundColor Yellow
$webListening = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue
if ($webListening) {
    Write-Host "  [+] ASP.NET Core Web API dang chay san tai cong 5000." -ForegroundColor Green
} else {
    $webCmd = "cd '$RootPath'; dotnet run --project src/Ecolink.Backend/Ecolink.WebApi --urls 'http://0.0.0.0:5000'"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$Host.UI.RawUI.WindowTitle='EcoLink Core Web API (5000)'; $webCmd"
    Start-Sleep -Seconds 4
}

# 3. EcoLink Frontend Web (Port 5173)
Write-Host "`n[3/5] Kiem tra / Khoi chay EcoLink Frontend Web (Port 5173)..." -ForegroundColor Yellow
$feListening = Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue
if ($feListening) {
    Write-Host "  [+] Frontend Vite dang chay san tai cong 5173." -ForegroundColor Green
} elseif (Test-Path (Join-Path $FrontendPath "package.json")) {
    $resolvedFePath = (Resolve-Path $FrontendPath).Path
    $feCmd = "cd '$resolvedFePath'; npm run dev"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$Host.UI.RawUI.WindowTitle='EcoLink Frontend Web (5173)'; $feCmd"
    Write-Host "  [+] Da khoi chay EcoLink Frontend Web (npm run dev)!" -ForegroundColor Green
    Start-Sleep -Seconds 3
} else {
    Write-Host "  [!] Khong tim thay thu muc '..\ecolink-web'. Vui long tu chay 'npm run dev' tai cong 5173." -ForegroundColor DarkYellow
}

# 4. ngrok Tunneling (Backend 5000)
Write-Host "`n[4/5] Kiem tra / Mo ngrok Tunneling cho Backend (Port 5000)..." -ForegroundColor Yellow
if (Get-Process -Name "ngrok" -ErrorAction SilentlyContinue) {
    Write-Host "  [+] ngrok Backend Tunnel dang chay san." -ForegroundColor Green
} elseif (Get-Command ngrok -ErrorAction SilentlyContinue) {
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$Host.UI.RawUI.WindowTitle='EcoLink ngrok Tunnel (5000)'; ngrok http 5000"
    Write-Host "  [+] Da mo tien trinh ngrok Backend Tunnel!" -ForegroundColor Green
} else {
    Write-Host "  [X] Khong tim thay lenh 'ngrok' trong PATH." -ForegroundColor Red
}

# 5. Cloudflare Quick Tunnel (Frontend 5173)
Write-Host "`n[5/5] Kiem tra / Mo Cloudflare Quick Tunnel cho Frontend (Port 5173)..." -ForegroundColor Yellow
if (Get-Process -Name "cloudflared" -ErrorAction SilentlyContinue) {
    Write-Host "  [+] Cloudflare Frontend Tunnel dang chay san." -ForegroundColor Green
} elseif (Get-Command cloudflared -ErrorAction SilentlyContinue) {
    $cfCmd = "`$env:Path = '$env:Path'; cloudflared tunnel --url http://localhost:5173"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$Host.UI.RawUI.WindowTitle='EcoLink Cloudflare Frontend Tunnel (5173)'; $cfCmd"
    Write-Host "  [+] Da mo tien trinh Cloudflare Frontend Tunnel (*.trycloudflare.com)!" -ForegroundColor Green
} else {
    Write-Host "  [X] Khong tim thay lenh 'cloudflared'. Cai dat bang: winget install Cloudflare.cloudflared" -ForegroundColor Red
}

Write-Host "`n==============================================================================" -ForegroundColor Green
Write-Host "  DA KHOI CHAY CAC DICH VU TRONG CAC CUA SO RIENG BIET!" -ForegroundColor Green
Write-Host "  - EcoLink Frontend Web : http://localhost:5173 (kem link *.trycloudflare.com)" -ForegroundColor White
Write-Host "  - Swagger Backend API  : http://localhost:5000/swagger (kem link *.ngrok-free.dev)" -ForegroundColor White
Write-Host "  - Swagger AI Vision    : http://localhost:8000/docs" -ForegroundColor White
Write-Host "  - ngrok Web Inspector  : http://localhost:4040" -ForegroundColor White
Write-Host "==============================================================================" -ForegroundColor Green

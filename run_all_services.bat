@echo off
title EcoLink - Start All Dev Services
cls

echo ==============================================================================
echo        ECOLINK PLATFORM - KHOI DONG TOAN BO DICH VU DEV
echo ==============================================================================
echo   1. Python FastAPI AI Service  : http://localhost:8000
echo   2. ASP.NET Core Web API       : http://localhost:5000
echo   3. EcoLink Frontend Web (Vite): http://localhost:5173
echo   4. ngrok Backend Tunnel       : http://localhost:5000 - Public API Tunnel
echo   5. Cloudflare Frontend Tunnel : http://localhost:5173 - Public Web Tunnel
echo ==============================================================================
echo.

cd /d %~dp0

:: Bo sung duong dan mac dinh cua cloudflared neu cua so hien tai chua nap PATH moi
if exist "%ProgramFiles(x86)%\cloudflared\cloudflared.exe" set "PATH=%PATH%;%ProgramFiles(x86)%\cloudflared"
if exist "%ProgramFiles%\cloudflared\cloudflared.exe" set "PATH=%PATH%;%ProgramFiles%\cloudflared"

:: ------------------------------------------------------------------------------
:: BUOC 0: KIEM TRA DOCKER DESKTOP (PostgreSQL ecolink-db)
:: ------------------------------------------------------------------------------
echo [0/5] Kiem tra Docker Desktop va khoi dong ecolink-db...
docker ps >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo   [+] Docker dang hoat dong. Khoi dong container ecolink-db...
    docker start ecolink-db >nul 2>&1
    echo   [+] Container CSDL PostgreSQL ecolink-db da san sang.
) else (
    echo   [!] Docker Desktop chua bat. Bo qua khoi dong container Docker.
)
echo.

:: ------------------------------------------------------------------------------
:: BUOC 1: KHOI CHAY AI VISION SERVICE (Port 8000)
:: ------------------------------------------------------------------------------
echo [1/5] Khoi chay AI Vision Service tai cong 8000...
start "EcoLink AI Vision Service" %~dp0run_ai_service.bat
echo   [+] AI Service dang khoi dong trong cua so rieng...
timeout /t 3 /nobreak >nul
echo.

:: ------------------------------------------------------------------------------
:: BUOC 2: KHOI CHAY CORE WEB API (Port 5000)
:: ------------------------------------------------------------------------------
echo [2/5] Khoi chay ASP.NET Core Web API tai cong 5000...
start "EcoLink Core Web API" cmd /k "title EcoLink Core Web API && cd /d %~dp0 && dotnet run --project src\Ecolink.Backend\Ecolink.WebApi --urls http://0.0.0.0:5000"
echo   [+] Web API dang khoi dong trong cua so rieng...
timeout /t 4 /nobreak >nul
echo.

:: ------------------------------------------------------------------------------
:: BUOC 3: KIEM TRA / KHOI CHAY FRONTEND VITE (Port 5173)
:: ------------------------------------------------------------------------------
echo [3/5] Kiem tra EcoLink Frontend Web tai cong 5173...
netstat -aon | findstr :5173 | findstr LISTENING >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo   [+] Frontend Vite dang chay san tai cong 5173.
) else if exist "%~dp0..\ecolink-web\package.json" (
    start "EcoLink Frontend Web" cmd /k "title EcoLink Frontend Web && cd /d ""%~dp0..\ecolink-web"" && npm run dev"
    echo   [+] Da khoi chay EcoLink Frontend Web (npm run dev) trong cua so rieng...
    timeout /t 3 /nobreak >nul
) else (
    echo   [!] Khong tim thay thu muc ..\ecolink-web. Hay tu chay 'npm run dev' truoc khi dung tunnel.
)
echo.

:: ------------------------------------------------------------------------------
:: BUOC 4: KHOI CHAY NGROK TUNNEL CHO BACKEND (Port 5000)
:: ------------------------------------------------------------------------------
echo [4/5] Khoi chay ngrok Tunnel cho Backend tai cong 5000...
where ngrok >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo   [X] Khong tim thay lenh ngrok trong PATH he thong!
) else (
    start "EcoLink ngrok Tunnel" cmd /k "title EcoLink ngrok Tunnel && ngrok http 5000"
    echo   [+] Da khoi chay ngrok Backend Tunnel trong cua so rieng!
)
echo.

:: ------------------------------------------------------------------------------
:: BUOC 5: KHOI CHAY CLOUDFLARE TUNNEL CHO FRONTEND (Port 5173)
:: ------------------------------------------------------------------------------
echo [5/5] Khoi chay Cloudflare Quick Tunnel cho Frontend tai cong 5173...
where cloudflared >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo   [X] Khong tim thay lenh cloudflared! Cai dat bang: winget install Cloudflare.cloudflared
) else (
    start "EcoLink Cloudflare Frontend Tunnel" cmd /k "title EcoLink Cloudflare Frontend Tunnel && cloudflared tunnel --url http://localhost:5173"
    echo   [+] Da khoi chay Cloudflare Frontend Tunnel (*.trycloudflare.com) trong cua so rieng!
)

echo.
echo ==============================================================================
echo   DA KHOI CHAY THANH CONG CAC DICH VU TRONG CAC CUA SO RIENG!
echo ==============================================================================
echo   - EcoLink Frontend Web : http://localhost:5173 (kem link *.trycloudflare.com)
echo   - Swagger Backend API  : http://localhost:5000/swagger (kem link *.ngrok-free.dev)
echo   - Swagger AI Vision    : http://localhost:8000/docs
echo   - ngrok Web Inspector  : http://localhost:4040
echo ==============================================================================
echo   De dung tat ca tien trinh tren, chay: stop_all_services.bat
echo ==============================================================================
echo.
pause

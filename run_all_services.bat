@echo off
title EcoLink - Start All Dev Services
cls

echo ==============================================================================
echo        ECOLINK PLATFORM - KHOI DONG TOAN BO DICH VU DEV
echo ==============================================================================
echo   1. Python FastAPI AI Service  : http://localhost:8000
echo   2. ASP.NET Core Web API       : http://localhost:5000
echo   3. ngrok HTTP Tunneling       : http://localhost:5000 - Public Tunnel
echo   4. ngrok Web Inspector        : http://localhost:4040
echo ==============================================================================
echo.

cd /d %~dp0

:: ------------------------------------------------------------------------------
:: BUOC 0: KIEM TRA DOCKER DESKTOP (PostgreSQL ecolink-db)
:: ------------------------------------------------------------------------------
echo [0/3] Kiem tra Docker Desktop va khoi dong ecolink-db...
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
echo [1/3] Khoi chay AI Vision Service tai cong 8000...
start "EcoLink AI Vision Service" %~dp0run_ai_service.bat
echo   [+] AI Service dang khoi dong trong cua so rieng...
timeout /t 3 /nobreak >nul
echo.

:: ------------------------------------------------------------------------------
:: BUOC 2: KHOI CHAY CORE WEB API (Port 5000)
:: ------------------------------------------------------------------------------
echo [2/3] Khoi chay ASP.NET Core Web API tai cong 5000...
start "EcoLink Core Web API" cmd /k "title EcoLink Core Web API && cd /d %~dp0 && dotnet run --project src\Ecolink.Backend\Ecolink.WebApi --urls http://0.0.0.0:5000"
echo   [+] Web API dang khoi dong trong cua so rieng...
timeout /t 4 /nobreak >nul
echo.

:: ------------------------------------------------------------------------------
:: BUOC 3: KHOI CHAY NGROK TUNNEL (Port 5000)
:: ------------------------------------------------------------------------------
echo [3/3] Khoi chay ngrok Tunnel tai cong 5000...
where ngrok >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo   [X] Khong tim thay lenh ngrok trong PATH he thong!
) else (
    start "EcoLink ngrok Tunnel" cmd /k "title EcoLink ngrok Tunnel && ngrok http 5000"
    echo   [+] Da khoi chay ngrok trong cua so rieng!
)

echo.
echo ==============================================================================
echo   DA KHOI CHAY THANH CONG CAC DICH VU TRONG CAC CUA SO RIENG!
echo ==============================================================================
echo   - Swagger Backend API  : http://localhost:5000/swagger
echo   - Swagger AI Vision    : http://localhost:8000/docs
echo   - ngrok Web Inspector  : http://localhost:4040
echo ==============================================================================
echo   De dung tat ca tien trinh tren, chay: stop_all_services.bat
echo ==============================================================================
echo.
pause

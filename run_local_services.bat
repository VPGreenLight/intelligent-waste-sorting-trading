@echo off
title EcoLink - Start All Local Services (No Tunnel)
cls

echo ==============================================================================
echo      ECOLINK PLATFORM - KHOI DONG TOAN BO DICH VU LOCAL (KHONG TUNNEL)
echo ==============================================================================
echo   0. PostgreSQL ^& Redis (Docker) : localhost:5432 ^& localhost:6379
echo   1. Python FastAPI AI Service   : http://localhost:8000
echo   2. ASP.NET Core Web API        : http://localhost:5000
echo   3. EcoLink Frontend Web [Vite] : http://localhost:5173
echo ==============================================================================
echo.

cd /d "%~dp0."

:: ------------------------------------------------------------------------------
:: BUOC 0: KIEM TRA DOCKER DESKTOP [PostgreSQL ecolink-db & Redis ecolink-redis]
:: ------------------------------------------------------------------------------
echo [0/3] Kiem tra Docker Desktop va khoi dong ecolink-db / ecolink-redis...
docker ps >nul 2>&1
if %ERRORLEVEL% equ 0 goto DOCKER_OK
echo   [!] Docker Desktop chua bat. Bo qua khoi dong container Docker.
goto DOCKER_AFTER

:DOCKER_OK
echo   [+] Docker dang hoat dong. Dang bat container ecolink-db ^& ecolink-redis...
docker start ecolink-db >nul 2>&1
if %ERRORLEVEL% neq 0 goto DOCKER_COMPOSE
docker start ecolink-redis >nul 2>&1
echo   [+] Container PostgreSQL [ecolink-db] ^& Redis [ecolink-redis] da san sang.
goto DOCKER_AFTER

:DOCKER_COMPOSE
echo   [*] Container chua ton tai, dang chay docker compose up -d ecolink-db ecolink-redis...
docker compose up -d ecolink-db ecolink-redis >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo   [+] Da khoi tao va bat container ecolink-db ^& ecolink-redis.
) else (
    echo   [!] Khong the khoi dong container Docker. Kiem tra lai Docker Compose.
)

:DOCKER_AFTER
echo.

:: ------------------------------------------------------------------------------
:: BUOC 1: KHOI CHAY AI VISION SERVICE [Port 8000]
:: ------------------------------------------------------------------------------
echo [1/3] Kiem tra / Khoi chay AI Vision Service tai cong 8000...
netstat -aon | findstr :8000 | findstr LISTENING >nul 2>&1
if %ERRORLEVEL% equ 0 goto AI_RUNNING

start "EcoLink AI Vision Service" "%~dp0run_ai_service.bat"
echo   [+] AI Service dang khoi dong trong cua so rieng...
timeout /t 3 /nobreak >nul
goto AI_AFTER

:AI_RUNNING
echo   [+] AI Vision Service dang chay san tai cong 8000.

:AI_AFTER
echo.

:: ------------------------------------------------------------------------------
:: BUOC 2: KHOI CHAY CORE WEB API [Port 5000]
:: ------------------------------------------------------------------------------
echo [2/3] Kiem tra / Khoi chay ASP.NET Core Web API tai cong 5000...
netstat -aon | findstr :5000 | findstr LISTENING >nul 2>&1
if %ERRORLEVEL% equ 0 goto API_RUNNING

start "EcoLink Core Web API" /D "%~dp0." cmd /k "title EcoLink Core Web API && dotnet run --project src\Ecolink.Backend\Ecolink.WebApi --urls http://localhost:5000"
echo   [+] Web API dang khoi dong trong cua so rieng...
timeout /t 4 /nobreak >nul
goto API_AFTER

:API_RUNNING
echo   [+] ASP.NET Core Web API dang chay san tai cong 5000.

:API_AFTER
echo.

:: ------------------------------------------------------------------------------
:: BUOC 3: KIEM TRA / KHOI CHAY FRONTEND VITE [Port 5173]
:: ------------------------------------------------------------------------------
echo [3/3] Kiem tra / Khoi chay EcoLink Frontend Web tai cong 5173...
netstat -aon | findstr :5173 | findstr LISTENING >nul 2>&1
if %ERRORLEVEL% equ 0 goto FE_DONE

if not exist "%~dp0..\ecolink-web\package.json" goto FE_MISSING

start "EcoLink Frontend Web" /D "%~dp0..\ecolink-web" cmd /k "title EcoLink Frontend Web && npm run dev"
echo   [+] Da khoi chay EcoLink Frontend Web [npm run dev] trong cua so rieng...
timeout /t 3 /nobreak >nul
goto FE_AFTER

:FE_DONE
echo   [+] Frontend Vite dang chay san tai cong 5173.
goto FE_AFTER

:FE_MISSING
echo   [!] Khong tim thay thu muc ..\ecolink-web. Hay tu chay npm run dev tai cong 5173.

:FE_AFTER
echo.
echo ==============================================================================
echo   DA KHOI CHAY TOAN BO DICH VU LOCAL [KHONG BAT TUNNEL]!
echo ==============================================================================
echo   - EcoLink Frontend Web : http://localhost:5173
echo   - Swagger Backend API  : http://localhost:5000/swagger
echo   - Swagger AI Vision    : http://localhost:8000/docs
echo   - PostgreSQL Database  : localhost:5432 [Container: ecolink-db]
echo   - Redis Cache          : localhost:6379 [Container: ecolink-redis]
echo ==============================================================================
echo   - Neu can mo link Public [ngrok + Cloudflare], dung: run_all_services.bat
echo   - De dung tat ca tien trinh tren, chay              : stop_all_services.bat
echo ==============================================================================
echo.
pause

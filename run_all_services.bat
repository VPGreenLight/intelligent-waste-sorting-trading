@echo off
title EcoLink - Start All Dev Services (Tunnel Mode)
cls

echo ==============================================================================
echo        ECOLINK PLATFORM - KHOI DONG TOAN BO DICH VU DEV + TUNNELS
echo ==============================================================================
echo   1. Python FastAPI AI Service   : http://localhost:8000
echo   2. ASP.NET Core Web API        : http://localhost:5000
echo   3. EcoLink Frontend Web [Vite] : http://localhost:5173
echo   4. ngrok Backend + DB Tunnels  : http 5000 + tcp 5432 [Public API ^& DB]
echo   5. Cloudflare Frontend Tunnel  : http://localhost:5173 - Public Web Tunnel
echo ==============================================================================
echo.

cd /d "%~dp0."

:: Bo sung duong dan mac dinh cua cloudflared neu cua so hien tai chua nap PATH moi
if exist "%ProgramFiles(x86)%\cloudflared\cloudflared.exe" set "PATH=%PATH%;%ProgramFiles(x86)%\cloudflared"
if exist "%ProgramFiles%\cloudflared\cloudflared.exe" set "PATH=%PATH%;%ProgramFiles%\cloudflared"

:: ------------------------------------------------------------------------------
:: BUOC 0: KIEM TRA DOCKER DESKTOP [PostgreSQL ecolink-db & Redis ecolink-redis]
:: ------------------------------------------------------------------------------
echo [0/5] Kiem tra Docker Desktop va khoi dong ecolink-db / ecolink-redis...
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
echo [1/5] Kiem tra / Khoi chay AI Vision Service tai cong 8000...
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
echo [2/5] Kiem tra / Khoi chay ASP.NET Core Web API tai cong 5000...
netstat -aon | findstr :5000 | findstr LISTENING >nul 2>&1
if %ERRORLEVEL% equ 0 goto API_RUNNING

start "EcoLink Core Web API" /D "%~dp0." cmd /k "title EcoLink Core Web API && dotnet run --project src\Ecolink.Backend\Ecolink.WebApi --urls http://0.0.0.0:5000"
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
echo [3/5] Kiem tra / Khoi chay EcoLink Frontend Web tai cong 5173...
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
echo   [!] Khong tim thay thu muc ..\ecolink-web. Hay tu chay npm run dev truoc khi dung tunnel.

:FE_AFTER
echo.

:: ------------------------------------------------------------------------------
:: BUOC 4: KHOI CHAY NGROK MULTI-TUNNELS [HTTP 5000 + PostgreSQL TCP 5432]
:: ------------------------------------------------------------------------------
echo [4/5] Kiem tra / Khoi chay ngrok Tunnels [Backend HTTP 5000 + DB TCP 5432]...
netstat -aon | findstr :4040 | findstr LISTENING >nul 2>&1
if %ERRORLEVEL% equ 0 goto NGROK_RUNNING

where ngrok >nul 2>&1
if %ERRORLEVEL% neq 0 goto NGROK_MISSING

set "NGROK_TUNNEL_CFG=%~dp0scripts\ngrok.tunnels.yml"
set "NGROK_USER_CFG="
if exist "%LOCALAPPDATA%\ngrok\ngrok.yml" set "NGROK_USER_CFG=%LOCALAPPDATA%\ngrok\ngrok.yml"
if not defined NGROK_USER_CFG if exist "%USERPROFILE%\.ngrok2\ngrok.yml" set "NGROK_USER_CFG=%USERPROFILE%\.ngrok2\ngrok.yml"

if not exist "%NGROK_TUNNEL_CFG%" goto NGROK_HTTP_ONLY
if defined NGROK_USER_CFG goto NGROK_WITH_USER_CFG

start "EcoLink ngrok Tunnels" cmd /k "title EcoLink ngrok Tunnels [HTTP 5000 + TCP 5432] && (ngrok start --all --config "%NGROK_TUNNEL_CFG%" || (echo [!] Khong mo duoc TCP 5432, fallback sang HTTP 5000... && ngrok http 5000))"
echo   [+] Da mo ngrok Tunnels [ecolink_api HTTP 5000 + ecolink_db TCP 5432]!
goto NGROK_AFTER

:NGROK_WITH_USER_CFG
start "EcoLink ngrok Tunnels" cmd /k "title EcoLink ngrok Tunnels [HTTP 5000 + TCP 5432] && (ngrok start --all --config "%NGROK_USER_CFG%" --config "%NGROK_TUNNEL_CFG%" || (echo [!] Khong mo duoc TCP 5432, fallback sang HTTP 5000... && ngrok http 5000))"
echo   [+] Da mo ngrok Tunnels [ecolink_api HTTP 5000 + ecolink_db TCP 5432] trong 1 phien!
goto NGROK_AFTER

:NGROK_HTTP_ONLY
start "EcoLink ngrok Tunnel" cmd /k "title EcoLink ngrok Tunnel && ngrok http 5000"
echo   [+] Da khoi chay ngrok Backend Tunnel [HTTP 5000] trong cua so rieng!
goto NGROK_AFTER

:NGROK_RUNNING
echo   [+] ngrok Tunnels dang chay san [Inspector cong 4040].
goto NGROK_AFTER

:NGROK_MISSING
echo   [X] Khong tim thay lenh ngrok trong PATH he thong!

:NGROK_AFTER
echo.

:: ------------------------------------------------------------------------------
:: BUOC 5: KHOI CHAY CLOUDFLARE TUNNEL CHO FRONTEND [Port 5173]
:: ------------------------------------------------------------------------------
echo [5/5] Kiem tra / Khoi chay Cloudflare Quick Tunnel cho Frontend tai cong 5173...
netstat -aon | findstr 127.0.0.1:2024 | findstr LISTENING >nul 2>&1
if %ERRORLEVEL% equ 0 goto CF_RUNNING

where cloudflared >nul 2>&1
if %ERRORLEVEL% neq 0 goto CF_MISSING

start "EcoLink Cloudflare Frontend Tunnel" cmd /k "title EcoLink Cloudflare Frontend Tunnel && cloudflared tunnel --url http://localhost:5173"
echo   [+] Da khoi chay Cloudflare Frontend Tunnel [*.trycloudflare.com] trong cua so rieng!
goto CF_AFTER

:CF_RUNNING
echo   [+] Cloudflare Frontend Tunnel dang chay san.
goto CF_AFTER

:CF_MISSING
echo   [X] Khong tim thay lenh cloudflared! Cai dat bang: winget install Cloudflare.cloudflared

:CF_AFTER
echo.
echo ==============================================================================
echo   DA KHOI CHAY THANH CONG CAC DICH VU TRONG CAC CUA SO RIENG!
echo ==============================================================================
echo   - EcoLink Frontend Web : http://localhost:5173 [kem link *.trycloudflare.com]
echo   - Swagger Backend API  : http://localhost:5000/swagger [kem link *.ngrok-free.dev]
echo   - PostgreSQL DB Tunnel : ngrok tcp 5432 [xem host:port tai http://localhost:4040]
echo   - Swagger AI Vision    : http://localhost:8000/docs
echo   - ngrok Web Inspector  : http://localhost:4040
echo ==============================================================================
echo   - Neu chi can chay Local [khong Tunnel], dung: run_local_services.bat
echo   - De dung tat ca tien trinh tren, chay       : stop_all_services.bat
echo ==============================================================================
echo.
pause

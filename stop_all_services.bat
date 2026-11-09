@echo off
title EcoLink - Stop All Dev Services
cls

echo ==============================================================================
echo        ECOLINK PLATFORM - DUNG TAT CA DICH VU DEV
echo ==============================================================================
echo.

echo [*] Dang dung tien trinh Cloudflare Frontend Tunnel [cloudflared]...
taskkill /F /IM cloudflared.exe >nul 2>&1
if %ERRORLEVEL% equ 0 goto CF_STOPPED
echo   [-] Cloudflare Tunnel khong hoat dong hoac da dong.
goto CF_DONE
:CF_STOPPED
echo   [+] Da dong Cloudflare Tunnel.
:CF_DONE

echo [*] Dang dung tien trinh ngrok Backend Tunnel...
taskkill /F /IM ngrok.exe >nul 2>&1
if %ERRORLEVEL% equ 0 goto NGROK_STOPPED
echo   [-] ngrok khong hoat dong hoac da dong.
goto NGROK_DONE
:NGROK_STOPPED
echo   [+] Da dong ngrok.
:NGROK_DONE

echo [*] Dang dung EcoLink Frontend Web [Port 5173]...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr :5173 ^| findstr LISTENING 2^>nul') do (
    taskkill /F /PID %%a >nul 2>&1
)
echo   [+] Da giai phong cong 5173.

echo [*] Dang dung ASP.NET Core Web API [Port 5000]...
taskkill /F /IM Ecolink.WebApi.exe >nul 2>&1
for /f "tokens=5" %%a in ('netstat -aon ^| findstr :5000 ^| findstr LISTENING 2^>nul') do (
    taskkill /F /PID %%a >nul 2>&1
)
echo   [+] Da giai phong cong 5000.

echo [*] Dang dung AI Vision Service [Port 8000]...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr :8000 ^| findstr LISTENING 2^>nul') do (
    taskkill /F /PID %%a >nul 2>&1
)
echo   [+] Da giai phong cong 8000.

echo.
echo ==============================================================================
echo   DA DUNG TOAN BO DICH VU VA GIAI PHONG CONG THANH CONG!
echo ==============================================================================
echo.
pause

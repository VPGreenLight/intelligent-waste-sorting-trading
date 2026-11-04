@echo off
title EcoLink - Stop All Dev Services
cls

echo ==============================================================================
echo        ECOLINK PLATFORM - DUNG TAT CA DICH VU DEV
echo ==============================================================================
echo.

echo [*] Dang dung tien trinh Cloudflare Frontend Tunnel (cloudflared)...
taskkill /F /IM cloudflared.exe 2>nul
taskkill /F /FI "WINDOWTITLE eq EcoLink Cloudflare Frontend Tunnel*" 2>nul
if %ERRORLEVEL% equ 0 (
    echo   [+] Da dong Cloudflare Tunnel.
) else (
    echo   [-] Cloudflare Tunnel khong hoat dong hoac da dong.
)

echo [*] Dang dung tien trinh ngrok Backend Tunnel...
taskkill /F /IM ngrok.exe 2>nul
taskkill /F /FI "WINDOWTITLE eq EcoLink ngrok Tunnel*" 2>nul
if %ERRORLEVEL% equ 0 (
    echo   [+] Da dong ngrok.
) else (
    echo   [-] ngrok khong hoat dong hoac da dong.
)

echo [*] Dang dung EcoLink Frontend Web (Port 5173)...
taskkill /F /FI "WINDOWTITLE eq EcoLink Frontend Web*" 2>nul
for /f "tokens=5" %%a in ('netstat -aon ^| findstr :5173 ^| findstr LISTENING 2^>nul') do (
    taskkill /F /PID %%a 2>nul
)
echo   [+] Da giai phong cong 5173.

echo [*] Dang dung ASP.NET Core Web API (Port 5000)...
taskkill /F /IM Ecolink.WebApi.exe 2>nul
taskkill /F /FI "WINDOWTITLE eq EcoLink Core Web API*" 2>nul

for /f "tokens=5" %%a in ('netstat -aon ^| findstr :5000 ^| findstr LISTENING 2^>nul') do (
    taskkill /F /PID %%a 2>nul
)
echo   [+] Da giai phong cong 5000.

echo [*] Dang dung AI Vision Service (Port 8000)...
taskkill /F /FI "WINDOWTITLE eq EcoLink AI Vision Service*" 2>nul

for /f "tokens=5" %%a in ('netstat -aon ^| findstr :8000 ^| findstr LISTENING 2^>nul') do (
    taskkill /F /PID %%a 2>nul
)
echo   [+] Da giai phong cong 8000.

echo.
echo ==============================================================================
echo   DA DUNG TOAN BO DICH VU VA GIAI PHONG CONG THANH CONG!
echo ==============================================================================
echo.
pause

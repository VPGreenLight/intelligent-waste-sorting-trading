@echo off
title EcoLink AI Vision Service (Port 8000)
echo ======================================================================
echo   Khoi dong EcoLink Dedicated AI Vision Service (FastAPI + YOLOv8)
echo   Swagger Docs: http://127.0.0.1:8000/docs
echo   Health Check: http://127.0.0.1:8000/health
echo ======================================================================
echo.

cd /d "%~dp0"
set PYTHONPATH=%~dp0src\Ecolink.AiService;%PYTHONPATH%

if exist ".\venv\Scripts\python.exe" (
    echo [+] Su dung Python tu Virtual Environment: .\venv\Scripts\python.exe
    .\venv\Scripts\python.exe -m uvicorn app:app --app-dir src/Ecolink.AiService --host 127.0.0.1 --port 8000 --reload
) else (
    echo [!] Khong tim thay venv, su dung Python he thong
    python -m uvicorn app:app --app-dir src/Ecolink.AiService --host 127.0.0.1 --port 8000 --reload
)

pause

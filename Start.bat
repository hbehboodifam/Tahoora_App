@echo off
chcp 65001 >nul
title Order Management System

echo.
echo  ============================================
echo   Starting Order Management System...
echo  ============================================
echo.

echo  [1/3] Starting API on port 5202...
start "OrderManagement API" cmd /k "cd /d %~dp0OrderManagementApi\OrderManagementApi && dotnet run"

timeout /t 8 /nobreak >nul

echo  [2/3] Starting UI on port 5255...
start "OrderManagement App" cmd /k "cd /d %~dp0OrderManagementApp\OrderManagementApp && dotnet run"

timeout /t 12 /nobreak >nul

echo  [3/3] Opening browser...
start http://localhost:5255

echo.
echo  ============================================
echo   System is ready!
echo  ============================================
echo.
echo   Local:  http://localhost:5255
echo.
echo   Mobile (same WiFi):
for /f "tokens=2 delims=:" %%a in ('ipconfig ^| findstr /c:"IPv4"') do (
    for /f "tokens=1" %%b in ("%%a") do (
        echo      http://%%b:5255
        goto :found
    )
)
:found
echo.
echo   To stop: close the two black windows.
echo.
pause
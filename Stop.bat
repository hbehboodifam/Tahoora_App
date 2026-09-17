@echo off
title Stopping Order Management System

echo.
echo  Stopping Order Management System...
echo.

taskkill /F /IM dotnet.exe >nul 2>&1

echo  ============================================
echo   System stopped.
echo  ============================================
echo.
timeout /t 3 >nul
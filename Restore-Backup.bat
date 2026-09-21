@echo off
setlocal EnableDelayedExpansion
chcp 65001 >nul
title Restore Backup - Order Management

set "PROJECT_DIR=%~dp0"
set "BACKUP_DIR=%PROJECT_DIR%Backups"
set "MYSQL_BIN=C:\Program Files\MySQL\MySQL Server 8.0\bin"
set "DB_NAME=OrderManagement"
set "DB_USER=root"
set "DB_PASS=13681370Hb@"

echo.
echo  ============================================
echo   RESTORE BACKUP - Order Management System
echo  ============================================
echo.

REM ===== Check if app is running =====
tasklist /FI "IMAGENAME eq dotnet.exe" 2>NUL | find /I "dotnet.exe" >NUL
if not errorlevel 1 (
    echo  [ERROR] The application is still running!
    echo.
    echo  Please close it first by running Stop.bat
    echo  Then try again.
    echo.
    pause
    exit /b 1
)

REM ===== Validate paths =====
if not exist "%MYSQL_BIN%\mysql.exe" (
    echo  [ERROR] mysql.exe not found at:
    echo         %MYSQL_BIN%
    echo.
    echo  Please edit this script and fix the MYSQL_BIN path.
    pause
    exit /b 1
)

if not exist "%BACKUP_DIR%" (
    echo  [ERROR] Backup folder not found:
    echo         %BACKUP_DIR%
    pause
    exit /b 1
)

REM ===== List backups =====
echo  Available backups:
echo  --------------------------------------------
set /a COUNT=0
for %%D in (manual monthly auto) do (
    if exist "%BACKUP_DIR%\%%D" (
        for /f "delims=" %%F in ('dir /b /o-d "%BACKUP_DIR%\%%D\*.sql" 2^>nul') do (
            set /a COUNT+=1
            echo  !COUNT!. [%%D] %%F
            set "BACKUP_!COUNT!=%BACKUP_DIR%\%%D\%%F"
        )
    )
)

if %COUNT%==0 (
    echo  No backups found.
    pause
    exit /b 1
)

echo  --------------------------------------------
echo.
set /p "CHOICE=Enter number to restore (or 0 to cancel): "

if "%CHOICE%"=="0" (
    echo  Cancelled.
    exit /b 0
)

if not defined BACKUP_%CHOICE% (
    echo  [ERROR] Invalid choice.
    pause
    exit /b 1
)

call set "RESTORE_FILE=%%BACKUP_%CHOICE%%%"

echo.
echo  Selected: %RESTORE_FILE%
echo.
echo  !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
echo  !!!  WARNING: ALL CURRENT DATA WILL BE  !!!
echo  !!!  DELETED and REPLACED with backup.  !!!
echo  !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
echo.
set /p "CONFIRM1=Type YES to continue: "
if /I not "%CONFIRM1%"=="YES" (
    echo  Cancelled.
    pause
    exit /b 0
)

echo.
echo  Are you REALLY sure? This cannot be undone.
set /p "CONFIRM2=Type CONFIRM to proceed: "
if /I not "%CONFIRM2%"=="CONFIRM" (
    echo  Cancelled.
    pause
    exit /b 0
)

REM ===== Safety backup before restore =====
echo.
echo  [1/3] Taking safety backup of current data...
set "SAFETY_DIR=%BACKUP_DIR%\manual"
if not exist "%SAFETY_DIR%" mkdir "%SAFETY_DIR%"

for /f "tokens=1-6 delims=/: " %%a in ("%date% %time%") do set "TS=%%c-%%b-%%a_%%d-%%e-%%f"
set "TS=%TS: =0%"
set "SAFETY_FILE=%SAFETY_DIR%\pre_restore_%TS%.sql"

"%MYSQL_BIN%\mysqldump.exe" --host=localhost --user=%DB_USER% --password=%DB_PASS% --default-character-set=utf8mb4 --routines --triggers --events --databases %DB_NAME% --result-file="%SAFETY_FILE%" 2>nul

if errorlevel 1 (
    echo  [WARNING] Safety backup failed, but continuing...
) else (
    echo  Safety backup saved: %SAFETY_FILE%
)

REM ===== Restore =====
echo.
echo  [2/3] Restoring backup...
"%MYSQL_BIN%\mysql.exe" --host=localhost --user=%DB_USER% --password=%DB_PASS% --default-character-set=utf8mb4 < "%RESTORE_FILE%"

if errorlevel 1 (
    echo.
    echo  [ERROR] Restore failed!
    echo.
    echo  Your safety backup is at:
    echo  %SAFETY_FILE%
    pause
    exit /b 1
)

echo  [3/3] Restore complete!
echo.
echo  ============================================
echo   SUCCESS
echo  ============================================
echo.
echo  You can now start the system by double-clicking Start.vbs
echo.
pause
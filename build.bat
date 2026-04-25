@echo off
chcp 65001 > nul
title Voice Typer — Сборка C#
echo.
echo ╔══════════════════════════════════════════════════╗
echo ║   Voice Typer — Сборка автономного EXE (C#)    ║
echo ╚══════════════════════════════════════════════════╝
echo.

:: ── Ищем dotnet (системный или локально установленный) ─────────────────────
set "DOTNET="
where dotnet > nul 2>&1 && set "DOTNET=dotnet"
if not defined DOTNET (
    if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
        set "DOTNET=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
    )
)
if not defined DOTNET (
    echo [ОШИБКА] .NET SDK не найден.
    echo          Запустите install_dotnet.bat или скачайте с https://dot.net
    pause
    exit /b 1
)
echo [OK] .NET SDK:
"%DOTNET%" --version

:: ── Восстановление пакетов (нужен интернет на машине разработчика) ──────────
echo.
echo [1/3] Загрузка NuGet-пакетов (NAudio, Vosk)...
"%DOTNET%" restore VoiceTyper.csproj --verbosity quiet
if errorlevel 1 (
    echo [ОШИБКА] Не удалось загрузить пакеты. Проверьте интернет-соединение.
    pause
    exit /b 1
)
echo [OK] Пакеты загружены.

:: ── Публикация — самодостаточный EXE ────────────────────────────────────────
echo.
echo [2/3] Компиляция и публикация (self-contained, win-x64)...
"%DOTNET%" publish VoiceTyper.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishReadyToRun=true ^
    -o .\dist\VoiceTyper ^
    --verbosity quiet
if errorlevel 1 (
    echo [ОШИБКА] Сборка не удалась.
    pause
    exit /b 1
)
echo [OK] EXE собран.

:: ── Копирование модели ───────────────────────────────────────────────────────
echo.
echo [3/3] Копирование модели VOSK...
if not exist ..\model\ (
    echo [ОШИБКА] Папка ..\model не найдена. Запустите install.bat из родительской папки.
    pause
    exit /b 1
)
xcopy /E /I /Y /Q ..\model .\dist\VoiceTyper\model
echo [OK] Модель скопирована.

echo.
echo ╔══════════════════════════════════════════════════════════════╗
echo ║  Готово!                                                    ║
echo ║                                                             ║
echo ║  Папка для копирования на другие ПК:                       ║
echo ║    VoiceTyperCS\dist\VoiceTyper\                           ║
echo ║                                                             ║
echo ║  Содержимое папки:                                         ║
echo ║    VoiceTyper.exe  ← запускать (без установки Python/.NET) ║
echo ║    model\          ← модель VOSK (~45 МБ)                  ║
echo ║    [прочие DLL]    ← нужны рядом с exe                     ║
echo ║                                                             ║
echo ║  На целевом ПК: просто скопировать папку и запустить exe   ║
echo ╚══════════════════════════════════════════════════════════════╝
echo.
echo Открыть папку? (нажмите любую клавишу)
pause > nul
explorer .\dist\VoiceTyper

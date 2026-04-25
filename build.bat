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
echo [1/3] Загрузка NuGet-пакетов (NAudio, Vosk, sherpa-onnx)...
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

:: ── Копирование моделей ──────────────────────────────────────────────────────
echo.
echo [3/3] Копирование моделей...

:: VOSK (опционально — нужна хотя бы одна модель)
if exist ..\model\ (
    xcopy /E /I /Y /Q ..\model .\dist\VoiceTyper\model
    echo [OK] Модель VOSK скопирована.
) else (
    echo [!] Папка ..\model не найдена — движок VOSK будет недоступен.
)

:: GigaAM v2 для sherpa-onnx (226 МБ + 196 байт)
:: Скачать: https://huggingface.co/k2-fsa/sherpa-onnx-nemo-ctc-giga-am-v2-russian-2025-04-19
if not exist .\dist\VoiceTyper\giga-am-v2.onnx (
    echo [!] giga-am-v2.onnx не найден — движок GigaAM v2 будет недоступен.
    echo     Файл должен лежать рядом с VoiceTyper.exe
)

:: Whisper (466 МБ), опционально
if not exist .\dist\VoiceTyper\ggml-small.bin (
    echo [!] ggml-small.bin не найден — движок Whisper будет недоступен.
)

echo.
echo ╔══════════════════════════════════════════════════════════════╗
echo ║  Готово!                                                    ║
echo ║                                                             ║
echo ║  Папка для копирования на другие ПК:                       ║
echo ║    VoiceTyperCS\dist\VoiceTyper\                           ║
echo ║                                                             ║
echo ║  Содержимое папки:                                         ║
echo ║    VoiceTyper.exe     ← запускать (без установки Python/.NET) ║
echo ║    model\             ← модель VOSK (~88 МБ, опционально)  ║
echo ║    giga-am-v2.onnx    ← модель GigaAM v2 (~226 МБ, опц.)  ║
echo ║    giga-am-tokens.txt ← токены GigaAM v2                  ║
echo ║    ggml-small.bin     ← модель Whisper (~466 МБ, опц.)    ║
echo ║    [прочие DLL]       ← нужны рядом с exe                  ║
echo ║                                                             ║
echo ║  На целевом ПК: просто скопировать папку и запустить exe   ║
echo ╚══════════════════════════════════════════════════════════════╝
echo.
echo Открыть папку? (нажмите любую клавишу)
pause > nul
explorer .\dist\VoiceTyper

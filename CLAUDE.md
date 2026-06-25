# Sonar — контекст проекта

## Что это

**Sonar** — офлайн-приложение голосового ввода текста для Windows, узкоспециализированная версия VoiceTyper.
Отличия от VoiceTyper:
- Только один движок: **GigaAM v3** (лучшее качество для русского, пунктуация встроена в модель)
- Три отраслевых словаря замен (нефтегаз, юридический, экономика/финансы) + пользовательский
- Режим голосовых команд Windows (100+ действий, без LLM, без сети)
- Имя, иконка, версионирование, копирайт Varzakov Stanislav

Исходник-донор: `c:\Users\Zver\Documents\Claude\VSCode Voice\VoiceTyperCS\` (VoiceTyper v4.5 Final).

---

## ОБЯЗАТЕЛЬНОЕ ПРАВИЛО: обновление документации

**При каждом изменении версии (любой — патч, минор, мажор) обязательно обновить:**

1. `AppConfig.cs` — поле `Version`
2. `Sonar.csproj` — тег `<Version>`
3. `CHANGELOG.md` — добавить новую секцию с датой и списком изменений
4. `README.md` — актуализировать список возможностей, ссылку на релиз, разделы функционала
5. `!readme.txt` — версия в заголовке, функциональный список, описание новых возможностей
6. `CLAUDE.md` (этот файл) — раздел «Текущее состояние», таблица файлов, архитектурные разделы

Документация обновляется **до** создания бэкапа и **до** финального коммита.

---

## Текущее состояние — v2.1

Директория `c:\Users\Zver\Documents\Claude\SonarCS\` — актуальный исходник.

| Версия | Дата | Описание |
|--------|------|----------|
| v1.0 | 2026-06-10 | Первый релиз: GigaAM v3, три словаря, оверлей, очередь фраз |
| v1.1 | 2026-06-15 | Словарь экономики, пользовательский словарь, выбор микрофона из трея |
| v1.2 | 2026-06-18 | Лицензия GigaAM уточнена (MIT), README для GitHub |
| v2.0 | 2026-06-22 | Режим голосовых команд: IntentMatcher, CommandExecutor, CommandsForm |
| v2.1 | 2026-06-25 | Нормализация в StartsWithTrigger, сортировка SimpleRulesN, удалён ShowBalloonTip |

Бэкапы: `Backup\V1.0\`, `Backup\V1.1\`, `Backup\V1.2\`, `Backup\V2.0\`, `Backup\V2.1\`

---

## Ключевые архитектурные решения

### KeyboardHook — ОБЯЗАТЕЛЬНО свой message loop
`KeyboardHook` создаёт **отдельный поток** с `GetMessage`/`DispatchMessage` loop.
Без этого хук `WH_KEYBOARD_LL` молча не работает — Windows доставляет коллбэк через `PostMessage`
в устанавливающий поток, которому нужен цикл сообщений.
`hModule = IntPtr.Zero` — для `WH_KEYBOARD_LL` это правильно (убирает риск NullReferenceException).

### TextTyper — вставка через буфер обмена
Отдельный STA-поток, `Clipboard.SetText` + `keybd_event(Ctrl+V)`. Не SendInput посимвольно —
это надёжнее для кириллицы. Сохраняет/восстанавливает предыдущее содержимое буфера.

### GigaAmEngine — прямой ONNX без промежуточных библиотек
- `Microsoft.ML.OnnxRuntime 1.20.1`
- Модель: `giga-am-v3.onnx` (215 МБ int8) + `giga-am-v3-vocab.txt` (257 токенов)
- Vocab: строчные + заглавные + цифры + пунктуация — пунктуация встроена в модель, постобработка не нужна
- Кастомный DFT (n_fft=320, не степень 2): преднасчитанные twiddle-таблицы [161, 320]
- Mel-спектрограмма: 64 фильтра HTK, hop=160, периодическое окно Ханна, `ln(max(e, 1e-9))`
- Per-feature нормализация (NeMo normalize=per_feature)
- CTC greedy decode
- ONNX вход: `"features"` float32 [1, 64, T], `"feature_lengths"` int64 [1]; выход: `"log_probs"` float32 [1, T', 257]

### AudioCapture
NAudio WaveInEvent. `DeviceNumber = -1` передаётся напрямую (WAVE_MAPPER).
`_active` должен быть `volatile` — используется из двух потоков.

### DictionaryEngine — постпроцессинг через словари замен
Применяется после `GigaAmEngine.Transcribe()` в `TrayApp.RecognizeAsync`.
Жадный поиск слева направо, длинные ключи в приоритете (список отсортирован по убыванию длины).
Совпадение засчитывается только на **границах слов** (пробел / начало/конец строки / пунктуация) —
это критически важно, иначе «а особенно» превращается в «АОсобенно».
Четыре словаря: `dictionary_oil_gas.txt`, `dictionary_legal.txt`, `dictionary_economy.txt`, `dictionary_user.txt`.
Отсутствие файла словаря не является ошибкой — движок просто не загружает его.

### IntentMatcher — голосовые команды (~0 мс, без LLM)
Статический класс, сопоставляет транскрипт команды с действием Windows.
- `Normalize(string t)` — заменяет визуально похожие латинские символы (a e o c x y p) на кириллические;
  решает проблему смешанного скрипта GigaAM. Метод `internal`, используется также в `TrayApp.StartsWithTrigger`.
- Предварительно нормализованные массивы `LaunchVerbsN`, `LaunchAppsN`, `FolderRulesN`, `SimpleRulesN` —
  создаются один раз при старте, нулевые накладные расходы в рантайме.
- `SimpleRulesN` и `LaunchAppsN`, `FolderRulesN` отсортированы по убыванию длины ключа — длинные
  правила проверяются первыми, короткие не перехватывают более специфичные совпадения.
- `AppNames` — канонический список приложений для `CommandsForm` (единственный источник истины).
  При добавлении приложения обновлять `AppNames`, `LaunchApps` и `CommandExecutor.AppAliases`.
- Порядок матчинга: `TryLaunch` → `TryOpenFolder` → `TrySetVolume` → `TryTypeText` → `TrySimple`.

### CommandExecutor — исполнение команд
Статический класс. Принимает `CommandResult`, исполняет через `SendInput`/`keybd_event`,
`Process.Start`, `ShellOpen`, NAudio (громкость), WinAPI (окна, монитор, блокировка).
Открытие сторонних UI-форм (CommandsForm) — через отдельный STA-поток.
`AppAliases` — словарь канонических имён → имён исполняемых файлов.

### CommandResult — структура результата матчинга
`Action` (string) + `Args` (Dictionary<string,string>). Метод `Arg(key)` — безопасное получение аргумента.

### CommandsForm — список голосовых команд
RichTextBox с категоризированным списком команд. Раздел «Приложения» генерируется из
`IntentMatcher.AppNames` — всегда актуален без ручной синхронизации.
Открывается через STA-поток из `CommandExecutor.OpenCommandsWindow()`.

### Оверлей у курсора
`OverlayForm` — полупрозрачная плашка, TopMost, следует за курсором (таймер 50мс).
`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` — не захватывает фокус, не видна в Alt+Tab.

Состояния отображения:
| Метод | Текст | Цвет | Размер |
|-------|-------|------|--------|
| ShowRecording() | ● Запись… | Красный | 168×28 |
| ShowRecognizing() | ◌ Распознавание… | Оранжевый | 168×28 |
| ShowCommandRecording() | ⚡ Команда… | Зелёный | 168×28 |
| ShowCommandExecuting() | ⚙ Выполнение… | Бирюзовый | 168×28 |
| ShowError(msg) | msg | Красный | 220×28 |
| ShowCommandError(text) | ✗ Команда не найдена / «text» | Красный | 260×44 |

`_errorTimer` (2 сек) — автоскрытие ошибки. `_isShowingError` — флаг, запрещающий `HideOverlay()`
прервать показ ошибки. `ClearError()` вызывается из всех ShowXxx для сброса ошибочного состояния.

**Важно:** все вызовы методов оверлея из фоновых потоков — только через `_overlay.BeginInvoke()`.

### Очередь фраз
Нажатие хоткея во время распознавания → запись следующей фразы в `_pendingPcm`.
После завершения текущего `RecognizeAsync` → автоматически обрабатывает следующую. Поле `_capturingForQueue`.

### Цвета иконки в трее
| Цвет | Состояние |
|------|-----------|
| Серый | Загрузка модели |
| Синий RGB(30,120,255) | Готов |
| Красный RGB(220,40,40) | Запись голосового ввода |
| Оранжевый RGB(220,120,0) | Распознавание |
| Зелёный RGB(40,180,60) | Запись голосовой команды |
| Бирюзовый RGB(0,190,180) | Выполнение команды |
| Фиолетовый | Ошибка |

### Logger
Потокобезопасный файловый лог, ротация при >1 МБ. Пишет в `sonar.log` рядом с exe.
`Logger.UnknownCommand(text)` — нераспознанные команды пишутся в отдельный `unknown_commands.log`.

---

## Файлы исходников

| Файл | Назначение |
|------|-----------|
| TrayApp.cs | Главный ApplicationContext, состояния, меню трея, очередь фраз, оверлей, триггер-детектор |
| KeyboardHook.cs | WH_KEYBOARD_LL с собственным message loop |
| AudioCapture.cs | NAudio WaveInEvent, захват PCM 16 кГц |
| GigaAmEngine.cs | OnnxRuntime, e2e CTC, кастомный DFT, 257-токенный vocab |
| DictionaryEngine.cs | Постпроцессинг — словари замен, проверка границ слов |
| IntentMatcher.cs | Мгновенный матчинг команд (~0 мс): Normalize, LaunchApps, FolderRules, SimpleRules |
| CommandResult.cs | Структура результата матчинга: Action + Args |
| CommandExecutor.cs | Исполнение 100+ команд Windows; открытие CommandsForm |
| CommandsForm.cs | Окно со списком голосовых команд; Приложения из IntentMatcher.AppNames |
| TextTyper.cs | Вставка текста через STA + Clipboard + Ctrl+V |
| FirstRunForm.cs | Диалог выбора микрофона при первом запуске |
| SettingsForm.cs | Настройки (микрофон, хоткей, автозапуск, словари, режим команд, триггер) |
| AppConfig.cs | sonar.json — все настройки приложения |
| AutoStartManager.cs | Автозапуск через реестр HKCU |
| OverlayForm.cs | Полупрозрачная плашка-индикатор у курсора мыши (7 состояний + ошибки) |
| Logger.cs | Потокобезопасный лог (sonar.log + unknown_commands.log, ротация 1 МБ) |
| AboutForm.cs | Окно «О программе» |
| Program.cs | Точка входа, [STAThread] |

---

## Сборка

SDK: `C:\Users\Zver\AppData\Local\Microsoft\dotnet\dotnet.exe` (v8.0.x)
Целевой каталог publish: `dist/Sonar2`

```
dotnet publish Sonar.csproj --configuration Release --runtime win-x64 --self-contained true -o dist/Sonar2
```

Папка dist очищается при каждом publish — все модели и словари должны быть зарегистрированы
в csproj через `<None CopyToPublishDirectory="Always">`.

---

## Известные грабли

- **DeviceNumber -1 → 0**: не конвертировать, передавать -1 напрямую в WaveInEvent.
- **Хук без message loop**: KeyboardHook должен создавать свой поток с GetMessage loop.
- **Бэкап раньше кода**: создавать бэкап только после завершения, тестирования и обновления документации.
- **dotnet publish очищает output**: все файлы (модели, dll) должны быть в csproj, иначе удалятся.
- **Множественные окна настроек**: нужен флаг `_settingsOpen` в TrayApp.
- **SolidBrush утечка**: оборачивать в `using` при создании иконок (MakeGearIcon и аналоги).
- **Границы слов в словарях**: DictionaryEngine без leftOk/rightOk превращает «а особенно» в «АОсобенно».
- **Тождественные записи в словарях**: строки вида «медиация = медиация» не делают замен — не добавлять.
- **BeginInvoke для оверлея**: все вызовы методов OverlayForm из фоновых потоков — только BeginInvoke, не Invoke и не прямой вызов.
- **SimpleRulesN порядок**: массив сортируется по убыванию длины — не убирать OrderByDescending, иначе короткие ключи начнут перехватывать длинные.
- **Normalize в StartsWithTrigger**: триггер и входной текст нормализуются через IntentMatcher.Normalize() — без этого триггер не срабатывает при смешанном скрипте GigaAM.
- **Три места при добавлении приложения**: IntentMatcher.AppNames + IntentMatcher.LaunchApps + CommandExecutor.AppAliases.

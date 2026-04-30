# Sonar — контекст проекта

## Что это

**Sonar** — офлайн-приложение голосового ввода текста для Windows, узкоспециализированная версия VoiceTyper.
Отличия от VoiceTyper:
- Только один движок: **GigaAM v3** (лучшее качество для русского, пунктуация встроена в модель)
- Три отраслевых словаря замен (нефтегаз, юридический, экономика/финансы) с возможностью вкл/выкл каждого
- Имя, иконка, версионирование, копирайт Varzakov Stanislav

Исходник-донор: `c:\Users\Zver\Documents\Claude\VSCode Voice\VoiceTyperCS\` (VoiceTyper v4.5 Final).

---

## Текущее состояние — v1.0 (адаптация завершена)

Директория `c:\Users\Zver\Documents\Claude\SonarCS\` — полностью адаптированный форк.

Выполнено:
- Переименование: namespace `Sonar`, AssemblyName/RootNamespace, лог `sonar.log`, конфиг `sonar.json`
- Удалены VoskEngine.cs, WhisperEngine.cs, все ветки vosk/whisper
- Убраны пакеты Vosk, Whisper.net×3 из csproj
- FirstRunForm упрощена — только выбор микрофона, без выбора движка
- Создан DictionaryEngine.cs — постпроцессинг через словари замен
- AppConfig: три флага dict_oil_gas, dict_legal, dict_economy
- SettingsForm: три чекбокса словарей (серые если файл не найден)
- Три словаря: dictionary_oil_gas.txt, dictionary_legal.txt, dictionary_economy.txt
- AboutForm, !readme.txt, NOTICES.txt обновлены под Sonar
- Копирайт: © 2026 Varzakov Stanislav (AboutForm, !readme.txt, csproj)
- Бэкап v1.0: `Backup\V1.0\`

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
Три словаря: `dictionary_oil_gas.txt`, `dictionary_legal.txt`, `dictionary_economy.txt`.
Отсутствие файла словаря не является ошибкой — движок просто не загружает его.

### Очередь фраз
Нажатие хоткея во время распознавания → запись следующей фразы в `_pendingPcm`.
После завершения текущего `RecognizeAsync` → автоматически обрабатывает следующую. Поле `_capturingForQueue`.

### Оверлей у курсора
`OverlayForm` — полупрозрачная плашка 168×28px, TopMost, следует за курсором (таймер 50мс).
`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` — не захватывает фокус, не видна в Alt+Tab.
Состояния: "● Запись…" (красный) / "◌ Распознавание…" (оранжевый).

### Цвета иконки в трее
| Цвет | Состояние |
|------|-----------|
| Серый | Загрузка модели |
| Синий RGB(30,120,255) | Готов |
| Красный RGB(220,40,40) | Запись |
| Оранжевый RGB(220,120,0) | Распознавание |
| Фиолетовый | Ошибка |

### Logger
Потокобезопасный файловый лог, ротация при >1 МБ. Пишет в `sonar.log` рядом с exe.

---

## Файлы исходников

| Файл | Назначение |
|------|-----------|
| TrayApp.cs | Главный ApplicationContext, состояния, меню трея, очередь фраз, оверлей |
| KeyboardHook.cs | WH_KEYBOARD_LL с собственным message loop |
| AudioCapture.cs | NAudio WaveInEvent, захват PCM 16 кГц |
| GigaAmEngine.cs | OnnxRuntime, e2e CTC, кастомный DFT, 257-токенный vocab |
| DictionaryEngine.cs | Постпроцессинг — словари замен, проверка границ слов |
| TextTyper.cs | Вставка текста через STA + Clipboard + Ctrl+V |
| FirstRunForm.cs | Диалог выбора микрофона при первом запуске |
| SettingsForm.cs | Настройки (микрофон, хоткей, автозапуск, три словаря) |
| AppConfig.cs | sonar.json (microphone_device, hotkey_vk, dict_oil_gas, dict_legal, dict_economy) |
| AutoStartManager.cs | Автозапуск через реестр HKCU |
| OverlayForm.cs | Полупрозрачная плашка-индикатор у курсора мыши |
| Logger.cs | Потокобезопасный файловый лог (sonar.log, ротация 1 МБ) |
| AboutForm.cs | Окно «О программе» |
| Program.cs | Точка входа, [STAThread] |

---

## Сборка

SDK: `C:\Users\Zver\AppData\Local\Microsoft\dotnet\dotnet.exe` (v8.0.x)

```
dotnet publish Sonar.csproj --configuration Release --runtime win-x64 --self-contained true -o dist/Sonar
```

Папка dist очищается при каждом publish — все модели должны быть зарегистрированы в csproj через `<None CopyToPublishDirectory="Always">`.

---

## Известные грабли

- **DeviceNumber -1 → 0**: не конвертировать, передавать -1 напрямую в WaveInEvent.
- **Хук без message loop**: KeyboardHook должен создавать свой поток с GetMessage loop.
- **Бэкап раньше кода**: создавать бэкап только после завершения и тестирования.
- **dotnet publish очищает output**: все файлы (модели, dll) должны быть в csproj, иначе удалятся.
- **Множественные окна настроек**: нужен флаг `_settingsOpen` в TrayApp.
- **SolidBrush утечка**: оборачивать в `using` при создании иконок (MakeGearIcon и аналоги).
- **Границы слов в словарях**: DictionaryEngine без leftOk/rightOk превращает «а особенно» в «АОсобенно».
- **Тождественные записи в словарях**: строки вида «медиация = медиация» не делают замен, но тратят время на проверку — не добавлять.

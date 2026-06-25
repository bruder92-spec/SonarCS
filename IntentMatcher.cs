using System.Text.RegularExpressions;

namespace Sonar;

/// <summary>
/// Мгновенное сопоставление голосовой команды с действием Windows.
/// Работает за ~0 мс — без LLM, без сети.
/// </summary>
public static class IntentMatcher
{
    // ── Нормализация: латинские визуальные аналоги → кириллица ──────────────────
    // Снижает число ad-hoc вариантов для слов со смешанным скриптом от GigaAM.
    internal static string Normalize(string t) =>
        t.Replace('a', 'а').Replace('e', 'е').Replace('o', 'о')
         .Replace('c', 'с').Replace('x', 'х').Replace('y', 'у')
         .Replace('p', 'р');

    public static CommandResult Match(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Unknown();

        string original = text.Trim();
        string t = Normalize(original.ToLowerInvariant());

        return TryLaunch(t)
            ?? TryOpenFolder(t)
            ?? TrySetVolume(t)
            ?? TryTypeText(t, original)
            ?? TrySimple(t)
            ?? Unknown();
    }

    // ── Запуск приложения ──────────────────────────────────────────────────────

    // Канонические названия приложений для отображения в CommandsForm.
    // При добавлении нового приложения в LaunchApps — добавить и сюда.
    internal static readonly string[] AppNames = [
        "браузер", "хром", "файерфокс", "хромиум",
        "ворд", "эксель", "аутлук",
        "телеграм", "ватсап", "влц",
        "блокнот", "пейнт", "калькулятор", "проводник",
        "vs code", "visual studio", "rider",
    ];

    private static readonly string[] LaunchVerbs =
        ["открой ", "запусти ", "включи ", "открыть ", "запустить ", "включить ",
         "запускай ", "покажи ", "стартуй ", "поставь ",
         "открыл ", "запустил ", "включил ", "запустить ", "открою ", "запущу ",
         "заперти "];

    private static readonly string[] LaunchVerbsN = LaunchVerbs.Select(Normalize).ToArray();

    // Только приложения без собственного open_X action
    private static readonly (string Trigger, string AppArg)[] LaunchApps =
    [
        ("visual studio", "студия"), ("vs code",    "code"),    ("vscode",      "code"),
        ("хромиум",  "хромиум"),     ("chrome",     "chrome"),  ("хром",        "хром"),
        ("хуm",      "хром"),                                   // вариант GigaAM
        ("файерфокс","firefox"),     ("firefox",    "firefox"), ("браузер",     "браузер"),
        ("fire fox", "firefox"),                                // GigaAM: пробел внутри
        ("ворд",     "ворд"),        ("word",       "ворд"),
        ("wort",     "ворд"),        ("vort",       "ворд"),   // варианты GigaAM
        ("woт",      "ворд"),        ("wor",        "ворд"),   // варианты GigaAM
        ("борт",     "ворд"),        ("морт",       "ворд"),   // варианты GigaAM
        ("молт",     "ворд"),        ("волт",       "ворд"),   // варианты GigaAM
        ("мот",      "ворд"),        ("в бок",      "ворд"),   // варианты GigaAM
        ("вот",      "ворд"),        ("вор",        "ворд"),
        ("эксель",   "эксель"),      ("excel",      "эксель"),
        ("exel",     "эксель"),                                // одна l вместо двух
        ("эксе",     "эксель"),      ("эксер",      "эксель"), // варианты GigaAM
        ("аксан",    "эксель"),      ("eксель",     "эксель"), // варианты GigaAM (лат. e)
        ("аутлук",   "аутлук"),      ("outlook",    "аутлук"),
        ("телеграм", "телеграм"),    ("telegram",   "телеграм"),
        ("ватсап",   "ватсап"),      ("whatsapp",   "ватсап"),
        ("влц",      "влц"),         ("vlc",        "влц"),
        ("студия",   "студия"),      ("rider",      "rider"),
        ("блокнот",  "блокнот"),     ("notepad",    "блокнот"),
        ("пант",     "пейнт"),      ("пайнц",      "пейнт"),   // варианты GigaAM
        ("pnt",      "пейнт"),      ("pнт",        "пейнт"),   // варианты GigaAM (лат.)
        ("паinт",    "пейнт"),                                  // вариант GigaAM (смешанный)
    ];

    // Триггеры LaunchApps, нормализованные одновременно с входным текстом
    private static readonly (string Trigger, string AppArg)[] LaunchAppsN =
        LaunchApps.Select(x => (Trigger: Normalize(x.Trigger), x.AppArg))
                  .OrderByDescending(x => x.Trigger.Length)
                  .ToArray();

    private static CommandResult? TryLaunch(string t)
    {
        if (!LaunchVerbsN.Any(v => t.Contains(v))) return null;

        foreach (var (trigger, app) in LaunchAppsN)
            if (t.Contains(trigger))
                return Cmd("launch", "app", app);

        return null;
    }

    // ── Открытие папки ─────────────────────────────────────────────────────────
    private static readonly (string Keyword, string FolderArg)[] FolderRules =
    [
        ("рабочий стол", "рабочий стол"),
        ("документы",    "документы"),
        ("загрузки",     "загрузки"),
        ("изображени",   "изображения"),  // ловит и "изображение", и "изображения"
        ("фотографии",   "фото"),
        ("фото",         "фото"),
        ("музыка",       "музыка"),
        ("видео",        "видео"),
    ];

    private static readonly (string Keyword, string FolderArg)[] FolderRulesN =
        FolderRules.Select(r => (Keyword: Normalize(r.Keyword), r.FolderArg))
                   .OrderByDescending(r => r.Keyword.Length)
                   .ToArray();

    private static CommandResult? TryOpenFolder(string t)
    {
        bool hasCtx = t.Contains("папк") || t.Contains("директори") || t.Contains("каталог")
                   || t.Contains("открой") || t.Contains("открыть") || t.Contains("покажи");
        if (!hasCtx) return null;
        if (t.Contains("браузер") || t.Contains("вкладк")) return null;

        foreach (var (kw, folder) in FolderRulesN)
            if (t.Contains(kw))
                return Cmd("open_folder", "folder", folder);

        return null;
    }

    // ── Установить громкость ───────────────────────────────────────────────────
    private static CommandResult? TrySetVolume(string t)
    {
        if (!t.Contains("громкост") && !t.Contains("volume")) return null;
        var m = Regex.Match(t, @"\b(\d{1,3})\b");
        return m.Success ? Cmd("set_volume", "value", m.Value) : null;
    }

    // ── Напечатать текст ───────────────────────────────────────────────────────
    private static readonly string[] TypeVerbs =
        ["вставь текст ", "напечатай ", "напиши ", "набери ", "введи "];

    private static CommandResult? TryTypeText(string t, string original)
    {
        foreach (var verb in TypeVerbs.OrderByDescending(v => v.Length))
        {
            int idx = t.IndexOf(verb, StringComparison.Ordinal);
            if (idx >= 0)
            {
                string rest = original[(idx + verb.Length)..].Trim().TrimStart(',', ':');
                if (!string.IsNullOrWhiteSpace(rest))
                    return Cmd("type_text", "text", rest);
            }
        }
        return null;
    }

    // ── Простые команды ────────────────────────────────────────────────────────
    private static readonly (string Keyword, string Action)[] SimpleRules =
    [
        // Управление окнами
        ("свернуть все окна",          "minimize_all"),
        ("свернуть все",               "minimize_all"),
        ("свернуть всё",               "minimize_all"),
        ("сверни все",                 "minimize_all"),
        ("сверни всё",                 "minimize_all"),
        ("восстановить все окна",      "restore_all"),
        ("восстановить все",           "restore_all"),
        ("восстанови все",             "restore_all"),
        ("прикрепить влево",           "snap_left"),
        ("прикрепи влево",             "snap_left"),
        ("прикрепить вправо",          "snap_right"),
        ("прикрепи вправо",            "snap_right"),
        ("развернуть на весь экран",   "snap_maximize"),
        ("на весь экран",              "snap_maximize"),
        ("свернуть окно",              "minimize_window"),
        ("сверни окно",                "minimize_window"),
        ("развернуть окно",            "maximize_window"),
        ("разверни окно",              "maximize_window"),
        ("закрыть текущее окно",       "close_window"),
        ("закрой текущее окно",        "close_window"),
        ("закрыть окно",               "close_window"),
        ("закрой окно",                "close_window"),
        ("следующий монитор",          "move_to_next_monitor"),
        ("предыдущий монитор",         "move_to_prev_monitor"),
        // Питание / система
        ("выключить компьютер",        "shutdown"),
        ("выключи компьютер",          "shutdown"),
        ("перезагрузить компьютер",    "restart"),
        ("перезагрузи компьютер",      "restart"),
        ("заблокировать экран",        "lock_screen"),
        ("заблокируй экран",           "lock_screen"),
        ("спящий режим",               "sleep"),
        ("режим сна",                  "sleep"),
        ("гибернация",                 "hibernate"),
        ("гибернировать",              "hibernate"),
        ("выключить монитор",          "monitor_off"),
        ("выключи монитор",            "monitor_off"),
        ("режим проекции",             "projection_mode"),
        ("подключить проектор",        "projection_mode"),
        ("проектор",                   "projection_mode"),
        // Звук
        ("сделай громче",              "volume_up"),
        ("сделать громче",             "volume_up"),
        ("сделай тише",                "volume_down"),
        ("сделать тише",               "volume_down"),
        ("выключить звук",             "mute"),
        ("выключи звук",               "mute"),
        ("включить звук",              "mute"),
        ("без звука",                  "mute"),
        ("тишина",                     "mute"),
        ("следующий трек",             "media_next"),
        ("следующая песня",            "media_next"),
        ("следующая композиция",       "media_next"),
        ("предыдущий трек",            "media_prev"),
        ("предыдущая песня",           "media_prev"),
        ("предыдущая композиция",      "media_prev"),
        ("поставить на паузу",         "media_play_pause"),
        ("поставь на паузу",           "media_play_pause"),
        ("снять с паузы",              "media_play_pause"),
        ("воспроизвести",              "media_play_pause"),
        ("воспроизведи",               "media_play_pause"),
        ("стоп музыка",                "media_play_pause"),
        ("пауза",                      "media_play_pause"),
        // Скриншоты
        ("скриншот текущего окна",     "screenshot_window"),
        ("скриншот окна",              "screenshot_window"),
        ("скриншот области",           "screenshot_region"),
        ("снимок области",             "screenshot_region"),
        ("выделить область",           "screenshot_region"),
        ("снимок экрана",              "screenshot"),
        ("скриншот",                   "screenshot"),
        // Запись экрана
        ("начать запись экрана",       "start_recording"),
        ("начать запись",              "start_recording"),
        ("запись экрана",              "start_recording"),
        ("остановить запись",          "stop_recording"),
        ("стоп запись",                "stop_recording"),
        ("игровая панель",             "game_bar"),
        // Виртуальные рабочие столы
        ("создать рабочий стол",       "desktop_new"),
        ("новый рабочий стол",         "desktop_new"),
        ("закрыть рабочий стол",       "desktop_close"),
        ("закрой рабочий стол",        "desktop_close"),
        ("следующий рабочий стол",     "desktop_next"),
        ("предыдущий рабочий стол",    "desktop_prev"),
        ("обзор задач",                "task_view"),
        ("переключить окно",           "alt_tab"),
        // Системные утилиты
        ("диспетчер задач",            "open_task_manager"),
        ("параметры windows",          "open_settings"),
        ("настройки windows",          "open_settings"),
        ("панель управления",          "open_control_panel"),
        ("диспетчер устройств",        "open_device_manager"),
        ("меню пуск",                  "open_start_menu"),
        ("центр уведомлений",          "notification_center"),
        ("быстрые настройки",          "quick_settings"),
        ("панель эмодзи",              "emoji_picker"),
        ("эмодзи",                     "emoji_picker"),
        ("история буфера обмена",      "clipboard_history"),
        ("экранная клавиатура",        "on_screen_keyboard"),
        ("таблица символов",           "char_map"),
        ("стикеры",                    "sticky_notes"),
        ("контекстное меню",           "context_menu"),
        ("инструмент ножницы",         "open_snipping"),
        ("ножницы",                    "open_snipping"),
        // Настройки Windows
        ("настройки дисплея",          "settings_display"),
        ("настройки экрана",           "settings_display"),
        ("настройки звука",            "settings_sound"),
        ("настройки интернета",        "settings_network"),
        ("настройки сети",             "settings_network"),
        ("настройки bluetooth",        "settings_bluetooth"),
        ("настройки блютуз",           "settings_bluetooth"),
        ("настройки приложений",       "settings_apps"),
        ("обновление windows",         "settings_updates"),
        ("центр обновлений",           "settings_updates"),
        ("настройки конфиденциальности","settings_privacy"),
        ("настройки питания",          "settings_power"),
        ("настройки аккаунтов",        "settings_accounts"),
        ("настройки аккаунта",         "settings_accounts"),
        // Специальные возможности
        ("включить диктор",            "narrator"),
        ("выключить диктор",           "narrator"),
        ("диктор",                     "narrator"),
        ("увеличить лупу",             "magnifier_in"),
        ("уменьшить лупу",             "magnifier_out"),
        ("закрыть лупу",               "magnifier_close"),
        ("caps lock",                  "toggle_caps"),
        ("капс лок",                   "toggle_caps"),
        ("капслок",                    "toggle_caps"),
        // Клавиатурные действия
        ("выделить всё",               "select_all"),
        ("выделить все",               "select_all"),
        ("скопировать",                "copy"),
        ("скопируй",                   "copy"),
        ("вырезать",                   "cut"),
        ("вырежи",                     "cut"),
        ("вставить",                   "paste"),
        ("вставь",                     "paste"),
        ("отменить действие",          "undo"),
        ("отмени действие",            "undo"),
        ("повторить действие",         "redo"),
        ("повтори действие",           "redo"),
        ("сохранить как",              "save_as"),
        ("сохрани как",                "save_as"),
        ("напечатать документ",        "print"),
        ("печать документа",           "print"),
        ("печать",                     "print"),
        ("поиск в файле",              "find"),
        ("найти в файле",              "find"),
        ("новый документ",             "new_document"),
        ("открыть закрытую вкладку",   "reopen_tab"),
        ("открыть новую вкладку",      "new_tab"),
        ("закрыть вкладку",            "close_tab"),
        ("закрой вкладку",             "close_tab"),
        ("новая вкладка",              "new_tab"),
        ("следующая вкладка",          "next_tab"),
        ("предыдущая вкладка",         "prev_tab"),
        ("инкогнито",                  "incognito_window"),
        ("адресная строка",            "focus_address_bar"),
        ("загрузки браузера",          "open_downloads"),
        ("история браузера",           "open_history"),
        ("открой историю",             "open_history"),   // когда "браузера" искажается
        ("инструменты разработчика",   "developer_tools"),
        ("увеличить масштаб",          "zoom_in"),
        ("уменьшить масштаб",          "zoom_out"),
        ("сбросить масштаб",           "zoom_reset"),
        ("полноэкранный режим",        "fullscreen"),
        ("полный экран",               "fullscreen"),
        ("обновить страницу",          "refresh"),
        ("перейти вперёд",             "go_forward"),
        ("перейти вперед",             "go_forward"),
        ("вернуться назад",            "go_back"),
        ("жирный шрифт",               "bold"),
        ("жирный",                     "bold"),
        ("курсив",                     "italic"),
        ("подчёркивание",              "underline"),
        ("подчеркивание",              "underline"),
        ("прокрутить вниз",            "scroll_down"),
        ("прокрутить вверх",           "scroll_up"),
        ("в начало страницы",          "scroll_top"),
        ("в конец страницы",           "scroll_bottom"),
        ("в начало строки",            "line_start"),
        ("в конец строки",             "line_end"),
        ("следующее слово",            "word_next"),
        ("предыдущее слово",           "word_prev"),
        ("удалить слово",              "delete_word"),
        ("переименовать",              "rename"),
        ("свойства файла",             "properties"),
        ("свойства",                   "properties"),
        ("очистить буфер обмена",      "clear_clipboard"),
        // Справка
        ("покажи команды",             "show_commands"),
        ("список команд",              "show_commands"),
        ("какие команды",              "show_commands"),
        ("что ты умеешь",              "show_commands"),
        ("открой команд",              "show_commands"),  // "команду" / "команды"
        // Короткие (последними — меньший приоритет)
        ("заблокируй",                 "lock_screen"),
        ("блокировка",                 "lock_screen"),
        ("перезагрузка",               "restart"),
        ("перезагрузи",                "restart"),
        ("выключение",                 "shutdown"),
        ("выключи",                    "shutdown"),
        ("громче",                     "volume_up"),
        ("тише",                       "volume_down"),
        ("мьют",                       "mute"),
        ("замьютить",                  "mute"),
        ("калькулятор",                "open_calculator"),
        ("пейнт",                      "open_paint"),
        ("paint",                      "open_paint"),
        ("корзин",                     "open_recycle_bin"),
        ("проводник",                  "open_explorer"),
        ("настройки",                  "open_settings"),
        ("обновить",                   "refresh"),
        ("вперёд",                     "go_forward"),
        ("вперед",                     "go_forward"),
        ("назад",                      "go_back"),
        ("удалить",                    "delete"),
        ("справка",                    "help"),
        ("поиск",                      "windows_search"),
        ("выполнить",                  "open_run"),
        ("свернуть",                   "minimize_window"),
        ("развернуть",                 "maximize_window"),
        ("закрыть",                    "close_window"),
        ("сохранить",                  "save"),
        ("отменить",                   "undo"),
        ("повторить",                  "redo"),
    ];

    private static readonly (string Keyword, string Action)[] SimpleRulesN =
        SimpleRules.Select(r => (Keyword: Normalize(r.Keyword), r.Action))
                   .OrderByDescending(r => r.Keyword.Length).ToArray();

    private static CommandResult? TrySimple(string t)
    {
        foreach (var (keyword, action) in SimpleRulesN)
            if (t.Contains(keyword))
                return new CommandResult { Action = action };
        return null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private static CommandResult Cmd(string action, string key, string val)
    {
        var r = new CommandResult { Action = action };
        r.Args[key] = val;
        return r;
    }

    private static CommandResult Unknown() => new() { Action = "unknown" };
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace Sonar;

/// <summary>
/// Исполняет команды, разобранные IntentMatcher.
/// Использует SendInput/keybd_event, Process.Start и Windows API.
/// </summary>
public static class CommandExecutor
{
    public static void Execute(CommandResult cmd)
    {
        Logger.Info($"CommandExecutor: {cmd.Action} args={string.Join(",", cmd.Args.Select(kv => $"{kv.Key}={kv.Value}"))}");
        try
        {
            switch (cmd.Action)
            {
                // ── приложения ─────────────────────────────────────────────
                case "launch":           Launch(cmd.Arg("app")); break;
                case "close_app":        CloseApp(cmd.Arg("app")); break;
                case "switch_to":        SwitchTo(cmd.Arg("app")); break;

                // ── управление окнами ──────────────────────────────────────
                case "minimize_window":  ShowForeground(SW_MINIMIZE); break;
                case "maximize_window":  ShowForeground(SW_MAXIMIZE); break;
                case "close_window":     CloseForeground(); break;
                case "minimize_all":     Keys(VK_LWIN, new Key(VK_D)); break;
                case "restore_all":      Keys(VK_LWIN, new Key(VK_D)); break;
                case "snap_left":        Keys(VK_LWIN, new Key(VK_LEFT)); break;
                case "snap_right":       Keys(VK_LWIN, new Key(VK_RIGHT)); break;
                case "snap_maximize":    Keys(VK_LWIN, new Key(VK_UP)); break;
                case "snap_restore":     Keys(VK_LWIN, new Key(VK_DOWN)); break;
                case "move_to_next_monitor": Keys(VK_LWIN, VK_SHIFT, new Key(VK_RIGHT)); break;
                case "move_to_prev_monitor": Keys(VK_LWIN, VK_SHIFT, new Key(VK_LEFT)); break;

                // ── файлы и папки ──────────────────────────────────────────
                case "open_folder":      OpenFolder(cmd.Arg("folder")); break;
                case "open_file":        OpenFile(cmd.Arg("file")); break;
                case "open_recycle_bin": ShellOpen("shell:RecycleBinFolder"); break;
                case "shell_open":       ShellOpen(cmd.Arg("target")); break;
                case "auto_launch":      ShellOpen(cmd.Arg("path")); break;

                // ── звук и медиа ───────────────────────────────────────────
                case "volume_up":        AdjustVolume(+0.1f); break;
                case "volume_down":      AdjustVolume(-0.1f); break;
                case "set_volume":       SetVolume(cmd.Arg("value")); break;
                case "mute":             ToggleMute(); break;
                case "media_play_pause": KeySingle(VK_MEDIA_PLAY_PAUSE); break;
                case "media_next":       KeySingle(VK_MEDIA_NEXT_TRACK); break;
                case "media_prev":       KeySingle(VK_MEDIA_PREV_TRACK); break;

                // ── скриншоты ──────────────────────────────────────────────
                case "screenshot":          KeySingle(VK_SNAPSHOT); break;
                case "screenshot_window":   Keys(VK_MENU, new Key(VK_SNAPSHOT)); break;
                case "screenshot_region":   Keys(VK_LWIN, VK_SHIFT, new Key(VK_S)); break;
                case "screenshot_clipboard": Keys(VK_CONTROL, new Key(VK_SNAPSHOT)); break;
                case "gaming_screenshot":   Keys(VK_LWIN, VK_MENU, new Key(VK_SNAPSHOT)); break;

                // ── запись экрана ──────────────────────────────────────────
                case "start_recording":
                case "stop_recording":   Keys(VK_LWIN, VK_MENU, new Key(VK_R)); break;
                case "game_bar":         Keys(VK_LWIN, new Key(VK_G)); break;

                // ── виртуальные рабочие столы ──────────────────────────────
                case "desktop_new":    Keys(VK_LWIN, VK_CONTROL, new Key(VK_D)); break;
                case "desktop_close":  Keys(VK_LWIN, VK_CONTROL, new Key(VK_F4)); break;
                case "desktop_next":   Keys(VK_LWIN, VK_CONTROL, new Key(VK_RIGHT)); break;
                case "desktop_prev":   Keys(VK_LWIN, VK_CONTROL, new Key(VK_LEFT)); break;
                case "task_view":      Keys(VK_LWIN, new Key(VK_TAB)); break;
                case "alt_tab":        Keys(VK_MENU, new Key(VK_TAB)); break;

                // ── системные действия ─────────────────────────────────────
                case "lock_screen":    LockWorkStation(); break;
                case "sleep":          Process.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0"); break;
                case "hibernate":      Process.Start("shutdown", "/h"); break;
                case "shutdown":       Process.Start("shutdown", "/s /t 5"); break;
                case "restart":        Process.Start("shutdown", "/r /t 5"); break;
                case "monitor_off":    MonitorOff(); break;
                case "projection_mode": Keys(VK_LWIN, new Key(VK_P)); break;

                // ── поиск и утилиты ────────────────────────────────────────
                case "windows_search":    Keys(VK_LWIN, new Key(VK_S)); break;
                case "open_run":          Keys(VK_LWIN, new Key(VK_R)); break;
                case "open_explorer":     Keys(VK_LWIN, new Key(VK_E)); break;
                case "open_calculator":   ShellOpen("calc"); break;
                case "open_snipping":     ShellOpen("SnippingTool"); break;
                case "open_paint":        ShellOpen("mspaint"); break;
                case "open_task_manager": ShellOpen("taskmgr"); break;
                case "open_settings":     ShellOpen("ms-settings:"); break;
                case "open_control_panel": ShellOpen("control"); break;
                case "open_device_manager": ShellOpen("devmgmt.msc"); break;
                case "open_start_menu":   KeySingle(VK_LWIN); break;
                case "notification_center": Keys(VK_LWIN, new Key(VK_N)); break;
                case "quick_settings":    Keys(VK_LWIN, new Key(VK_A)); break;
                case "emoji_picker":      Keys(VK_LWIN, new Key(VK_DOT)); break;
                case "clipboard_history": Keys(VK_LWIN, new Key(VK_V)); break;
                case "on_screen_keyboard": ShellOpen("osk"); break;
                case "char_map":          ShellOpen("charmap"); break;
                case "sticky_notes":      ShellOpen("ms-stickynotes:"); break;
                case "context_menu":      KeySingle(VK_APPS); break;
                case "help":              KeySingle(VK_F1); break;

                // ── настройки Windows ──────────────────────────────────────
                case "settings_display":  ShellOpen("ms-settings:display"); break;
                case "settings_sound":    ShellOpen("ms-settings:sound"); break;
                case "settings_network":  ShellOpen("ms-settings:network"); break;
                case "settings_bluetooth": ShellOpen("ms-settings:bluetooth"); break;
                case "settings_apps":     ShellOpen("ms-settings:appsfeatures"); break;
                case "settings_updates":  ShellOpen("ms-settings:windowsupdate"); break;
                case "settings_privacy":  ShellOpen("ms-settings:privacy"); break;
                case "settings_power":    ShellOpen("ms-settings:powersleep"); break;
                case "settings_accounts": ShellOpen("ms-settings:accounts"); break;

                // ── специальные возможности ────────────────────────────────
                case "narrator":        Keys(VK_LWIN, VK_CONTROL, new Key(VK_RETURN)); break;
                case "magnifier_in":    Keys(VK_LWIN, new Key(VK_OEM_PLUS)); break;
                case "magnifier_out":   Keys(VK_LWIN, new Key(VK_OEM_MINUS)); break;
                case "magnifier_close": Keys(VK_LWIN, new Key(VK_ESCAPE)); break;
                case "toggle_caps":     KeySingle(VK_CAPITAL); break;

                // ── универсальные действия клавиатуры ─────────────────────
                case "select_all":     Keys(VK_CONTROL, new Key(VK_A)); break;
                case "copy":           Keys(VK_CONTROL, new Key(VK_C)); break;
                case "cut":            Keys(VK_CONTROL, new Key(VK_X)); break;
                case "paste":          Keys(VK_CONTROL, new Key(VK_V)); break;
                case "undo":           Keys(VK_CONTROL, new Key(VK_Z)); break;
                case "redo":           Keys(VK_CONTROL, new Key(VK_Y)); break;
                case "save":           Keys(VK_CONTROL, new Key(VK_S)); break;
                case "save_as":        Keys(VK_CONTROL, VK_SHIFT, new Key(VK_S)); break;
                case "print":          Keys(VK_CONTROL, new Key(VK_P)); break;
                case "find":           Keys(VK_CONTROL, new Key(VK_F)); break;
                case "open_file_dialog": Keys(VK_CONTROL, new Key(VK_O)); break;
                case "new_document":   Keys(VK_CONTROL, new Key(VK_N)); break;
                case "close_tab":      Keys(VK_CONTROL, new Key(VK_W)); break;
                case "new_tab":        Keys(VK_CONTROL, new Key(VK_T)); break;
                case "reopen_tab":     Keys(VK_CONTROL, VK_SHIFT, new Key(VK_T)); break;
                case "next_tab":       Keys(VK_CONTROL, new Key(VK_TAB)); break;
                case "prev_tab":       Keys(VK_CONTROL, VK_SHIFT, new Key(VK_TAB)); break;
                case "incognito_window": Keys(VK_CONTROL, VK_SHIFT, new Key(VK_N)); break;
                case "focus_address_bar": Keys(VK_CONTROL, new Key(VK_L)); break;
                case "open_downloads": Keys(VK_CONTROL, new Key(VK_J)); break;
                case "open_history":   Keys(VK_CONTROL, new Key(VK_H)); break;
                case "developer_tools": KeySingle(VK_F12); break;
                case "zoom_in":        Keys(VK_CONTROL, new Key(VK_OEM_PLUS)); break;
                case "zoom_out":       Keys(VK_CONTROL, new Key(VK_OEM_MINUS)); break;
                case "zoom_reset":     Keys(VK_CONTROL, new Key(VK_0)); break;
                case "fullscreen":     KeySingle(VK_F11); break;
                case "refresh":        KeySingle(VK_F5); break;
                case "go_back":        Keys(VK_MENU, new Key(VK_LEFT)); break;
                case "go_forward":     Keys(VK_MENU, new Key(VK_RIGHT)); break;
                case "bold":           Keys(VK_CONTROL, new Key(VK_B)); break;
                case "italic":         Keys(VK_CONTROL, new Key(VK_I)); break;
                case "underline":      Keys(VK_CONTROL, new Key(VK_U)); break;
                case "scroll_down":    KeySingle(VK_NEXT); break;
                case "scroll_up":      KeySingle(VK_PRIOR); break;
                case "scroll_top":     Keys(VK_CONTROL, new Key(VK_HOME)); break;
                case "scroll_bottom":  Keys(VK_CONTROL, new Key(VK_END)); break;
                case "line_start":     KeySingle(VK_HOME); break;
                case "line_end":       KeySingle(VK_END); break;
                case "word_next":      Keys(VK_CONTROL, new Key(VK_RIGHT)); break;
                case "word_prev":      Keys(VK_CONTROL, new Key(VK_LEFT)); break;
                case "delete_word":    Keys(VK_CONTROL, new Key(VK_BACK)); break;
                case "rename":         KeySingle(VK_F2); break;
                case "properties":     Keys(VK_MENU, new Key(VK_RETURN)); break;
                case "delete":         KeySingle(VK_DELETE); break;

                // ── буфер и текст ──────────────────────────────────────────
                case "type_text":
                    var text = cmd.Arg("text");
                    if (!string.IsNullOrWhiteSpace(text))
                        TextTyper.Type(text);
                    break;
                case "clear_clipboard":
                    var sta = new Thread(() => System.Windows.Forms.Clipboard.Clear());
                    sta.SetApartmentState(ApartmentState.STA);
                    sta.Start(); sta.Join();
                    break;

                case "show_commands": OpenCommandsWindow(); break;

                case "unknown":
                    Logger.Info("CommandExecutor: команда не распознана");
                    break;

                default:
                    Logger.Info($"CommandExecutor: неизвестный action={cmd.Action}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"CommandExecutor.Execute({cmd.Action})", ex);
        }
    }

    // ── список команд ────────────────────────────────────────────────────────
    private static void OpenCommandsWindow()
    {
        var t = new Thread(() =>
        {
            Application.EnableVisualStyles();
            using var form = new CommandsForm();
            Application.Run(form);
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
    }

    // ── приложения ────────────────────────────────────────────────────────────
    private static readonly Dictionary<string, string> AppAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["хром"]        = "chrome",
        ["хромиум"]     = "chrome",
        ["firefox"]     = "firefox",
        ["браузер"]     = "chrome",
        ["проводник"]   = "explorer",
        ["блокнот"]     = "notepad",
        ["калькулятор"] = "calc",
        ["paint"]       = "mspaint",
        ["пейнт"]       = "mspaint",
        ["ворд"]        = "winword",
        ["эксель"]      = "excel",
        ["аутлук"]      = "outlook",
        ["телеграм"]    = "telegram",
        ["ватсап"]      = "whatsapp",
        ["влц"]         = "vlc",
        ["vs code"]     = "code",
        ["студия"]      = "devenv",
        ["rider"]       = "rider64",
    };

    private static void Launch(string app)
    {
        if (string.IsNullOrWhiteSpace(app)) return;
        var resolved = AppAliases.TryGetValue(app, out var alias) ? alias : app;
        try { Process.Start(new ProcessStartInfo(resolved) { UseShellExecute = true }); }
        catch { Logger.Info($"CommandExecutor: не удалось запустить '{resolved}'"); }
    }

    private static void CloseApp(string app)
    {
        if (string.IsNullOrWhiteSpace(app)) return;
        var resolved = AppAliases.TryGetValue(app, out var alias) ? alias : app;
        var proc = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(resolved)).FirstOrDefault();
        proc?.CloseMainWindow();
    }

    private static void SwitchTo(string app)
    {
        if (string.IsNullOrWhiteSpace(app)) return;
        var resolved = AppAliases.TryGetValue(app, out var alias) ? alias : app;
        var proc = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(resolved)).FirstOrDefault();
        if (proc?.MainWindowHandle is IntPtr h && h != IntPtr.Zero)
        {
            ShowWindow(h, SW_RESTORE);
            SetForegroundWindow(h);
        }
    }

    // ── папки ─────────────────────────────────────────────────────────────────
    private static readonly Dictionary<string, string> FolderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["документы"]   = "Documents",
        ["загрузки"]    = "Downloads",
        ["рабочий стол"] = "Desktop",
        ["музыка"]      = "Music",
        ["видео"]       = "Videos",
        ["изображения"] = "Pictures",
        ["фото"]        = "Pictures",
    };

    private static void OpenFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) { ShellOpen("explorer"); return; }
        if (FolderAliases.TryGetValue(folder, out var known))
        {
            var path = Environment.GetFolderPath(
                Enum.TryParse<Environment.SpecialFolder>(known, out var sf) ? sf : Environment.SpecialFolder.MyDocuments);
            ShellOpen(path);
        }
        else
        {
            ShellOpen(folder);
        }
    }

    private static void OpenFile(string file)
    {
        if (!string.IsNullOrWhiteSpace(file))
            ShellOpen(file);
    }

    private static void ShellOpen(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch { Logger.Info($"CommandExecutor: ShellOpen не удалось для '{target}'"); }
    }

    // ── громкость (NAudio) ────────────────────────────────────────────────────
    private static void AdjustVolume(float delta)
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device     = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var vol = device.AudioEndpointVolume;
        vol.MasterVolumeLevelScalar = Math.Clamp(vol.MasterVolumeLevelScalar + delta, 0f, 1f);
    }

    private static void SetVolume(string valueStr)
    {
        if (!int.TryParse(valueStr, out int pct)) return;
        using var enumerator = new MMDeviceEnumerator();
        using var device     = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(pct / 100f, 0f, 1f);
    }

    private static void ToggleMute()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device     = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var vol = device.AudioEndpointVolume;
        vol.Mute = !vol.Mute;
    }

    // ── управление окнами ─────────────────────────────────────────────────────
    private static void ShowForeground(int cmd)
    {
        var h = GetForegroundWindow();
        if (h != IntPtr.Zero) ShowWindow(h, cmd);
    }

    private static void CloseForeground()
    {
        var h = GetForegroundWindow();
        if (h != IntPtr.Zero) PostMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    private static void MonitorOff()
    {
        PostMessage(GetForegroundWindow(), WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)2);
    }

    // ── keybd_event helpers ───────────────────────────────────────────────────
    private const uint KEYEVENTF_KEYUP = 0x0002;

    // Маркер «это основная клавиша» (не модификатор)
    private record struct Key(byte Vk);

    private static void Keys(params object[] combo)
    {
        // Нажать модификаторы, нажать основную клавишу, отпустить в обратном порядке
        var mods  = combo.OfType<byte>().ToArray();
        var mainK = combo.OfType<Key>().FirstOrDefault();

        foreach (var m in mods)
            keybd_event(m, 0, 0, 0);

        if (mainK != default)
            keybd_event(mainK.Vk, 0, 0, 0);

        Thread.Sleep(30);

        if (mainK != default)
            keybd_event(mainK.Vk, 0, KEYEVENTF_KEYUP, 0);

        foreach (var m in mods.Reverse())
            keybd_event(m, 0, KEYEVENTF_KEYUP, 0);
    }

    private static void KeySingle(byte vk)
    {
        keybd_event(vk, 0, 0, 0);
        Thread.Sleep(30);
        keybd_event(vk, 0, KEYEVENTF_KEYUP, 0);
    }

    // ── виртуальные коды клавиш ───────────────────────────────────────────────
    private const byte VK_LWIN     = 0x5B;
    private const byte VK_CONTROL  = 0x11;
    private const byte VK_MENU     = 0x12;   // Alt
    private const byte VK_SHIFT    = 0x10;
    private const byte VK_RETURN   = 0x0D;
    private const byte VK_ESCAPE   = 0x1B;
    private const byte VK_TAB      = 0x09;
    private const byte VK_BACK     = 0x08;
    private const byte VK_DELETE   = 0x2E;
    private const byte VK_HOME     = 0x24;
    private const byte VK_END      = 0x23;
    private const byte VK_PRIOR    = 0x21;   // Page Up
    private const byte VK_NEXT     = 0x22;   // Page Down
    private const byte VK_LEFT     = 0x25;
    private const byte VK_RIGHT    = 0x27;
    private const byte VK_UP       = 0x26;
    private const byte VK_DOWN     = 0x28;
    private const byte VK_SNAPSHOT = 0x2C;   // Print Screen
    private const byte VK_F1       = 0x70;
    private const byte VK_F2       = 0x71;
    private const byte VK_F4       = 0x73;
    private const byte VK_F5       = 0x74;
    private const byte VK_F11      = 0x7A;
    private const byte VK_F12      = 0x7B;
    private const byte VK_A        = 0x41;
    private const byte VK_B        = 0x42;
    private const byte VK_C        = 0x43;
    private const byte VK_D        = 0x44;
    private const byte VK_E        = 0x45;
    private const byte VK_F        = 0x46;
    private const byte VK_G        = 0x47;
    private const byte VK_H        = 0x48;
    private const byte VK_I        = 0x49;
    private const byte VK_J        = 0x4A;
    private const byte VK_L        = 0x4C;
    private const byte VK_N        = 0x4E;
    private const byte VK_O        = 0x4F;
    private const byte VK_P        = 0x50;
    private const byte VK_R        = 0x52;
    private const byte VK_S        = 0x53;
    private const byte VK_T        = 0x54;
    private const byte VK_U        = 0x55;
    private const byte VK_V        = 0x56;
    private const byte VK_W        = 0x57;
    private const byte VK_X        = 0x58;
    private const byte VK_Y        = 0x59;
    private const byte VK_Z        = 0x5A;
    private const byte VK_0        = 0x30;
    private const byte VK_APPS     = 0x5D;   // контекстное меню
    private const byte VK_CAPITAL  = 0x14;   // Caps Lock
    private const byte VK_DOT      = 0xBE;   // '.'
    private const byte VK_OEM_PLUS = 0xBB;
    private const byte VK_OEM_MINUS = 0xBD;

    private const byte VK_MEDIA_NEXT_TRACK  = 0xB0;
    private const byte VK_MEDIA_PREV_TRACK  = 0xB1;
    private const byte VK_MEDIA_PLAY_PAUSE  = 0xB3;

    // ── WinAPI ────────────────────────────────────────────────────────────────
    private const int SW_MINIMIZE = 6;
    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE  = 9;
    private const uint WM_CLOSE      = 0x0010;
    private const uint WM_SYSCOMMAND = 0x0112;
    private const int SC_MONITORPOWER = 0xF170;

    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool LockWorkStation();
}

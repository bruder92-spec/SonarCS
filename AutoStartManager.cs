using Microsoft.Win32;

namespace VoiceTyper;

/// <summary>
/// Управляет записью автозапуска в реестре текущего пользователя.
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run → VoiceTyper = "путь к exe"
/// </summary>
public static class AutoStartManager
{
    private const string KeyName = "VoiceTyper";
    private const string RegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static string ExePath =>
        System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath);
            return key?.GetValue(KeyName) is string val &&
                   string.Equals(val, ExePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true);
        key?.SetValue(KeyName, ExePath);
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true);
        key?.DeleteValue(KeyName, throwOnMissingValue: false);
    }
}

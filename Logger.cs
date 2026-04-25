using System.Text;

namespace VoiceTyper;

/// <summary>
/// Ротирующий файловый лог. Максимум ~2 МБ на диске (основной + .bak).
/// Потокобезопасен.
/// </summary>
public static class Logger
{
    private static readonly string _path = Path.Combine(AppContext.BaseDirectory, "VoiceTyper.log");
    private static readonly string _bak  = Path.Combine(AppContext.BaseDirectory, "VoiceTyper.log.bak");
    private const long MaxBytes = 1 * 1024 * 1024;   // 1 МБ
    private static readonly object _lock = new();

    public static void Info (string msg)               => Write("INFO ", msg);
    public static void Warn (string msg)               => Write("WARN ", msg);
    public static void Error(string msg)               => Write("ERROR", msg);
    public static void Error(string msg, Exception ex) => Write("ERROR", $"{msg} — {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string msg)
    {
        lock (_lock)
        {
            try
            {
                Rotate();
                File.AppendAllText(_path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}{Environment.NewLine}",
                    Encoding.UTF8);
            }
            catch { /* лог не должен ронять приложение */ }
        }
    }

    private static void Rotate()
    {
        if (!File.Exists(_path)) return;
        if (new FileInfo(_path).Length < MaxBytes) return;
        if (File.Exists(_bak)) File.Delete(_bak);
        File.Move(_path, _bak);
    }
}

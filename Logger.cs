namespace Sonar;

/// <summary>
/// Потокобезопасный файловый лог.
/// Файл: sonar.log рядом с exe.
/// При превышении 1 МБ — ротация в sonar.log.bak.
/// </summary>
public static class Logger
{
    private static readonly string _path = Path.Combine(AppContext.BaseDirectory, "sonar.log");
    private static readonly object _lock = new();
    private const long MaxBytes = 1_048_576; // 1 МБ

    private static readonly string _unknownPath = Path.Combine(AppContext.BaseDirectory, "unknown_commands.log");

    public static void Info(string msg)                 => Write("INFO ", msg);
    public static void Warn(string msg)                 => Write("WARN ", msg);
    public static void Error(string msg)                => Write("ERROR", msg);
    public static void Error(string msg, Exception ex)  => Write("ERROR", $"{msg} | {ex.GetType().Name}: {ex.Message}");

    public static void UnknownCommand(string text)
    {
        try
        {
            lock (_lock)
                File.AppendAllText(_unknownPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {text}{Environment.NewLine}",
                    System.Text.Encoding.UTF8);
        }
        catch { }
    }

    private static void Write(string level, string msg)
    {
        try
        {
            lock (_lock)
            {
                if (File.Exists(_path) && new FileInfo(_path).Length > MaxBytes)
                {
                    string bak = _path + ".bak";
                    if (File.Exists(bak)) File.Delete(bak);
                    File.Move(_path, bak);
                }
                File.AppendAllText(_path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}{Environment.NewLine}",
                    System.Text.Encoding.UTF8);
            }
        }
        catch { }
    }
}

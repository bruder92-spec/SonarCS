using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiceTyper;

/// <summary>
/// Хранит настройки в config.json рядом с exe.
/// Десериализуется из файла при запуске; null означает «первый запуск».
/// </summary>
public sealed class AppConfig
{
    [JsonPropertyName("engine")]
    public string Engine { get; set; } = "vosk";          // "vosk" | "whisper"

    [JsonPropertyName("microphone_device")]
    public int MicrophoneDevice { get; set; } = -1;       // -1 = системный по умолчанию

    // ── пути к файлам моделей (относительно exe) ──────────────────────────────
    public static string VoskModelDir   => Path.Combine(AppContext.BaseDirectory, "model");
    public static string WhisperModel   => Path.Combine(AppContext.BaseDirectory, "ggml-small.bin");

    // ── загрузка / сохранение ─────────────────────────────────────────────────
    private static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "config.json");

    public static AppConfig? TryLoad()
    {
        if (!File.Exists(ConfigPath)) return null;
        try
        {
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath));
        }
        catch { return null; }
    }

    public void Save()
    {
        File.WriteAllText(ConfigPath,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

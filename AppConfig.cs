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
    public string Engine { get; set; } = "vosk";          // "vosk" | "whisper" | "sherpa"

    [JsonPropertyName("microphone_device")]
    public int MicrophoneDevice { get; set; } = -1;       // -1 = системный по умолчанию

    [JsonPropertyName("hotkey_vk")]
    public int HotkeyVk { get; set; } = 0xA4;             // VK_LMENU = левый Alt

    [JsonPropertyName("post_process")]
    public bool PostProcess { get; set; } = false;         // заглавная буква + точка

    [JsonPropertyName("use_punctuation")]
    public bool UsePunctuation { get; set; } = false;      // Silero TE пунктуация (CTC-движки)

    // ── пути к файлам моделей (относительно exe) ──────────────────────────────
    public static string VoskModelDir       => Path.Combine(AppContext.BaseDirectory, "model");
    public static string WhisperModel       => Path.Combine(AppContext.BaseDirectory, "ggml-small.bin");
    public static string SherpaModel        => Path.Combine(AppContext.BaseDirectory, "giga-am-v2.onnx");
    public static string SherpaTokens       => Path.Combine(AppContext.BaseDirectory, "giga-am-tokens.txt");
    public static string PunctuationModel   => Path.Combine(AppContext.BaseDirectory, "silero_te.onnx");

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
        try
        {
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* нет прав на запись — настройки не сохраняются, но работа продолжается */ }
    }
}

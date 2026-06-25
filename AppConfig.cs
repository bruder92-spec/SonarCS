using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sonar;

/// <summary>
/// Хранит настройки в sonar.json рядом с exe.
/// Десериализуется из файла при запуске; null означает «первый запуск».
/// </summary>
public sealed class AppConfig
{
    public const string Version = "2.3";  // отображается в меню трея и окне «О программе»

    [JsonPropertyName("microphone_device")]
    public int MicrophoneDevice { get; set; } = -1;        // -1 = системный по умолчанию

    [JsonPropertyName("hotkey_vk")]
    public int HotkeyVk { get; set; } = 0xA4;              // VK_LMENU = левый Alt

    [JsonPropertyName("dict_oil_gas")]
    public bool DictOilGas { get; set; } = false;

    [JsonPropertyName("dict_legal")]
    public bool DictLegal { get; set; } = false;

    [JsonPropertyName("dict_economy")]
    public bool DictEconomy { get; set; } = false;

    [JsonPropertyName("commands_enabled")]
    public bool CommandsEnabled { get; set; } = false;

    [JsonPropertyName("trigger_word")]
    public string TriggerWord { get; set; } = "компьютер";

    // ── пути к файлам модели (относительно exe) ───────────────────────────────
    public static string GigaAmV3Model => Path.Combine(AppContext.BaseDirectory, "giga-am-v3.onnx");
    public static string GigaAmV3Vocab => Path.Combine(AppContext.BaseDirectory, "giga-am-v3-vocab.txt");

    // ── папка пользовательских файлов ────────────────────────────────────────
    public static string UserDir          => Path.Combine(AppContext.BaseDirectory, "user");
    public static string CommandsUserFile => Path.Combine(UserDir, "commands_user.txt");
    public static string DictOilGasFile   => Path.Combine(UserDir, "dictionary_oil_gas.txt");
    public static string DictLegalFile    => Path.Combine(UserDir, "dictionary_legal.txt");
    public static string DictEconomyFile  => Path.Combine(UserDir, "dictionary_economy.txt");
    public static string DictUserFile     => Path.Combine(UserDir, "dictionary_user.txt");

    // ── загрузка / сохранение ─────────────────────────────────────────────────
    private static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "sonar.json");

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

using System.Runtime.InteropServices;
using Whisper.net;

namespace VoiceTyper;

/// <summary>
/// Движок распознавания на базе Whisper.net 1.9.0 (whisper.cpp под капотом).
/// При наличии Vulkan-совместимой видеокарты использует GPU автоматически,
/// иначе работает на CPU — переключение прозрачно для пользователя.
/// </summary>
public sealed class WhisperEngine : IDisposable
{
    private readonly string _modelPath;
    private WhisperFactory? _factory;

    /// <summary>true если при загрузке обнаружен Vulkan (GPU будет задействован).</summary>
    public bool IsGpu { get; private set; }

    public WhisperEngine(string modelPath) => _modelPath = modelPath;

    public void Load()
    {
        IsGpu = IsVulkanAvailable();
        Logger.Info($"WhisperEngine: загрузка модели, GPU(Vulkan)={IsGpu}, путь={_modelPath}");
        _factory = WhisperFactory.FromPath(_modelPath);
        Logger.Info("WhisperEngine: модель загружена");
    }

    /// <summary>Распознаёт PCM-аудио. Вызывается через await из RecognizeAsync.</summary>
    public async Task<string> TranscribeAsync(byte[] pcm16le)
    {
        if (_factory is null) return string.Empty;

        var sb = new System.Text.StringBuilder();

        using var proc = _factory.CreateBuilder()
            .WithLanguage("ru")
            .Build();

        using var wav = MakeWav(pcm16le, sampleRate: 16_000);

        await foreach (var seg in proc.ProcessAsync(wav))
            sb.Append(seg.Text);

        var result = sb.ToString().Trim();
        Logger.Info($"WhisperEngine: распознано «{result}»");
        return result;
    }

    /// <summary>
    /// Проверяет наличие Vulkan на машине (vulkan-1.dll входит в состав
    /// драйверов любой современной видеокарты: AMD, NVIDIA, Intel).
    /// </summary>
    public static bool IsVulkanAvailable()
    {
        try
        {
            if (NativeLibrary.TryLoad("vulkan-1.dll", out var h) && h != IntPtr.Zero)
            {
                NativeLibrary.Free(h);
                return true;
            }
            return false;
        }
        catch { return false; }
    }

    /// <summary>Оборачивает сырой PCM в валидный WAV-заголовок (RIFF/PCM).</summary>
    private static MemoryStream MakeWav(byte[] pcm, int sampleRate)
    {
        var ms = new MemoryStream(44 + pcm.Length);
        var bw = new BinaryWriter(ms);

        bw.Write("RIFF".ToCharArray());
        bw.Write(36 + pcm.Length);
        bw.Write("WAVE".ToCharArray());
        bw.Write("fmt ".ToCharArray());
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write("data".ToCharArray());
        bw.Write(pcm.Length);
        bw.Write(pcm);

        ms.Position = 0;
        return ms;
    }

    public void Dispose()
    {
        _factory?.Dispose();
        Logger.Info("WhisperEngine: выгружен");
    }
}

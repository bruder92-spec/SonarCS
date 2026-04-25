using Whisper.net;

namespace VoiceTyper;

/// <summary>
/// Движок распознавания на базе Whisper.net (whisper.cpp под капотом).
/// Использует GGML-модель ggml-medium.bin, работает полностью офлайн на CPU.
///
/// Правильный API в Whisper.net 1.5+: ProcessAsync → IAsyncEnumerable[SegmentData].
/// Синхронный Process() с WithSegmentEventHandler работает непредсказуемо.
/// </summary>
public sealed class WhisperEngine : IDisposable
{
    private readonly string  _modelPath;
    private WhisperFactory?  _factory;

    public WhisperEngine(string modelPath) => _modelPath = modelPath;

    public void Load()
    {
        _factory = WhisperFactory.FromPath(_modelPath);
    }

    /// <summary>Распознаёт PCM-аудио. Запускается через Task.Run из RecognizeAsync.</summary>
    /// <param name="pcm16le">PCM, 16 кГц, 16 бит, моно, little-endian</param>
    public async Task<string> TranscribeAsync(byte[] pcm16le)
    {
        if (_factory is null) return string.Empty;

        var sb = new System.Text.StringBuilder();

        using var proc = _factory.CreateBuilder()
            .WithLanguage("ru")
            .Build();

        using var wav = MakeWav(pcm16le, sampleRate: 16_000);

        // ProcessAsync → IAsyncEnumerable<SegmentData> — правильный API Whisper.net 1.5+
        await foreach (var seg in proc.ProcessAsync(wav))
            sb.Append(seg.Text);

        return sb.ToString().Trim();
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
        bw.Write(16);              // PCM chunk size
        bw.Write((short)1);       // PCM format
        bw.Write((short)1);       // моно
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2); // ByteRate
        bw.Write((short)2);       // BlockAlign
        bw.Write((short)16);      // BitsPerSample
        bw.Write("data".ToCharArray());
        bw.Write(pcm.Length);
        bw.Write(pcm);

        ms.Position = 0;
        return ms;
    }

    public void Dispose() => _factory?.Dispose();
}

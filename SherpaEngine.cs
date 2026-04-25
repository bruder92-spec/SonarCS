using SherpaOnnx;

namespace VoiceTyper;

/// <summary>
/// Движок распознавания на базе sherpa-onnx + GigaAM v2 (SberDevices, MIT).
/// Модель: sherpa-onnx-nemo-ctc-giga-am-v2-russian-2025-04-19 (int8, ~226 МБ).
/// WER ~4-6% на русском CPU — в 3-4 раза точнее Whisper Small.
/// RTF ~0.33 (5 сек речи обрабатывается за ~1.6 сек).
/// </summary>
public sealed class SherpaEngine : IDisposable
{
    private readonly string _modelPath;
    private readonly string _tokensPath;
    private OfflineRecognizer? _recognizer;

    public SherpaEngine(string modelPath, string tokensPath)
    {
        _modelPath  = modelPath;
        _tokensPath = tokensPath;
    }

    public void Load()
    {
        var cfg = new OfflineRecognizerConfig();
        cfg.FeatConfig.SampleRate        = 16_000;
        cfg.FeatConfig.FeatureDim        = 80;
        cfg.ModelConfig.Tokens           = _tokensPath;
        cfg.ModelConfig.NeMoCtc.Model    = _modelPath;
        cfg.ModelConfig.NumThreads       = 2;
        cfg.DecodingMethod               = "greedy_search";
        _recognizer = new OfflineRecognizer(cfg);
    }

    /// <param name="pcm16le">Сырые байты PCM, 16 кГц, 16 бит, моно, little-endian</param>
    public string Transcribe(byte[] pcm16le)
    {
        if (_recognizer is null) return string.Empty;

        var samples = new float[pcm16le.Length / 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = BitConverter.ToInt16(pcm16le, i * 2) / 32768f;

        using var stream = _recognizer.CreateStream();
        stream.AcceptWaveform(16_000, samples);
        _recognizer.Decode(stream);
        return stream.Result.Text.Trim();
    }

    public void Dispose() => _recognizer?.Dispose();
}

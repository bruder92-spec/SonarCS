using System.Text.Json;
using Vosk;

namespace VoiceTyper;

/// <summary>
/// Обёртка над Vosk: загружает модель из папки, распознаёт PCM-16 аудио.
/// Vosk.Vosk.SetLogLevel(-1) подавляет весь вывод библиотеки в консоль.
/// </summary>
public sealed class VoskEngine
{
    private readonly string _modelPath;
    private Model? _model;

    public VoskEngine(string modelPath)
    {
        _modelPath = modelPath;
        Vosk.Vosk.SetLogLevel(-1);
    }

    public void Load()
    {
        _model = new Model(_modelPath);
    }

    /// <param name="pcm16le">Сырые байты PCM, 16 кГц, 16 бит, моно, little-endian</param>
    public string Transcribe(byte[] pcm16le)
    {
        if (_model is null) return string.Empty;

        using var rec = new VoskRecognizer(_model, 16_000f);
        rec.AcceptWaveform(pcm16le, pcm16le.Length);

        using var doc = JsonDocument.Parse(rec.FinalResult());
        return doc.RootElement.GetProperty("text").GetString()?.Trim() ?? string.Empty;
    }
}

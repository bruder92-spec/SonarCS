namespace VoiceTyper;

/// <summary>
/// Пост-обработка текста CTC-движков (VOSK, GigaAM): восстановление пунктуации.
///
/// Текущий статус: инфраструктура готова, ONNX-инференс подключается после получения
/// конвертированной модели Silero TE (silero_te.onnx, ~65 МБ).
///
/// Конвертация в Python (разово):
///   import torch
///   model, *_, apply_te = torch.hub.load('snakers4/silero-models', 'silero_te')
///   dummy = torch.zeros(1, 10, dtype=torch.long)
///   torch.onnx.export(model, dummy, 'silero_te.onnx', opset_version=14,
///       input_names=['input'], output_names=['output'],
///       dynamic_axes={'input':{0:'batch',1:'seq'}, 'output':{0:'batch',1:'seq'}})
///
/// После конвертации положите silero_te.onnx рядом с VoiceTyper.exe и включите
/// галочку «Пунктуация» в Настройках.
/// </summary>
public sealed class PunctuationEngine : IDisposable
{
    public static bool IsAvailable => File.Exists(AppConfig.PunctuationModel);

    public PunctuationEngine() { }

    /// <summary>
    /// Применяет пунктуацию к тексту.
    /// Пока модель не загружена — возвращает текст без изменений.
    /// </summary>
    public string Apply(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        // TODO: загрузить silero_te.onnx через Microsoft.ML.OnnxRuntime и применить
        // Входной тензор  «input»  — int64 [1, seq], char-IDs из vocab модели
        // Выходной тензор «output» — float32 [1, seq, n_classes]
        // Классы (ru): 0=NONE, 1=PERIOD, 2=COMMA, 3=QUESTION, 4=DASH, 5=EXCLAMATION
        return text;
    }

    public void Dispose() { }
}

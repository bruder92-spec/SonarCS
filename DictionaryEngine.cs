using System.Text;

namespace Sonar;

/// <summary>
/// Постпроцессинг распознанного текста через отраслевые словари замен.
/// Применяется после GigaAmEngine.Transcribe() в TrayApp.RecognizeAsync.
///
/// Формат словаря (dictionary_*.txt):
///   # комментарий
///   исходный текст = замена
/// Регистр исходного текста не учитывается. Длинные совпадения имеют приоритет.
/// Совпадение срабатывает только на границах слов (пробел, начало/конец строки, пунктуация).
///
/// dictionary_user.txt загружается всегда (если файл существует) и имеет наивысший
/// приоритет за счёт более длинных ключей — пользователь может переопределить любую запись.
/// </summary>
public sealed class DictionaryEngine
{
    // Отсортировано по убыванию длины ключа: длинные совпадения имеют приоритет
    private readonly List<(string Key, string Value)> _entries = [];

    public bool IsEmpty => _entries.Count == 0;

    public DictionaryEngine(bool oilGas, bool legal, bool economy)
    {
        if (oilGas)   Load("dictionary_oil_gas.txt");
        if (legal)    Load("dictionary_legal.txt");
        if (economy)  Load("dictionary_economy.txt");
        Load("dictionary_user.txt");
        _entries.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
    }

    private void Load(string filename)
    {
        string path = Path.Combine(AppContext.BaseDirectory, filename);
        if (!File.Exists(path)) return;
        try
        {
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
                int eq = trimmed.IndexOf('=');
                if (eq < 1) continue;
                string key = trimmed[..eq].Trim().ToLowerInvariant();
                string val = trimmed[(eq + 1)..].Trim();
                if (key.Length > 0 && val.Length > 0)
                    _entries.Add((key, val));
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"DictionaryEngine: не удалось загрузить {filename}: {ex.Message}");
        }
    }

    /// <summary>
    /// Применяет словари к тексту. Жадный поиск слева направо, длинные ключи в приоритете.
    /// Совпадение засчитывается только на границах слов, чтобы избежать ложных срабатываний
    /// на коротких ключах внутри слов («а о» не должно срабатывать в «а особенно»).
    /// </summary>
    public string Apply(string text)
    {
        if (_entries.Count == 0 || string.IsNullOrEmpty(text)) return text;

        var lower = text.ToLowerInvariant();
        var sb    = new StringBuilder(text.Length);
        int i     = 0;

        while (i < text.Length)
        {
            bool matched  = false;
            bool leftOk   = i == 0 || lower[i - 1] == ' ' || char.IsPunctuation(lower[i - 1]);

            if (leftOk)
            {
                foreach (var (key, val) in _entries)
                {
                    int end = i + key.Length;
                    if (end > lower.Length) continue;
                    if (!lower.AsSpan(i, key.Length).SequenceEqual(key)) continue;

                    bool rightOk = end == lower.Length || lower[end] == ' ' || char.IsPunctuation(lower[end]);
                    if (!rightOk) continue;

                    sb.Append(val);
                    i       = end;
                    matched = true;
                    break;
                }
            }

            if (!matched)
                sb.Append(text[i++]);
        }
        return sb.ToString();
    }
}

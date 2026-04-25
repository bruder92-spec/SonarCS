namespace VoiceTyper;

/// <summary>
/// Постобработка распознанного текста (опциональная, управляется AppConfig.PostProcess).
/// Включает: заглавная буква в начале, точка в конце если нет знака препинания.
/// </summary>
public static class TextProcessor
{
    public static string Process(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        text = char.ToUpper(text[0]) + text[1..];

        char last = text[^1];
        if (last != '.' && last != '!' && last != '?' && last != '…' && last != ',')
            text += '.';

        return text;
    }
}

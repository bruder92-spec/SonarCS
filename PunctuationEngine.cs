using System.Text.RegularExpressions;

namespace VoiceTyper;

/// <summary>
/// Расстановка пунктуации для CTC-движков (VOSK, GigaAM).
/// Реализация: правилобазовая для русского языка, без внешних файлов.
///
/// Что расставляется:
///   • Запятая перед подчинительными и противительными союзами
///     (но, однако, зато, хотя, что, чтобы, который/ая/ое/ые и падежи,
///      потому что, так как, если, когда, пока, поскольку и др.)
///   • Запятая после вводных слов (конечно, впрочем, итак и др.)
///   • Знак вопроса, если фраза начинается с вопросительного слова
///   • Знак восклицания для коротких возгласов (ой, ах, нет и др.)
/// </summary>
public sealed class PunctuationEngine : IDisposable
{
    // Всегда доступен — работает без внешних файлов
    public static bool IsAvailable => true;

    // ── союзы / относительные местоимения — запятая ПЕРЕД ────────────────
    private static readonly Regex s_commaBefore = BuildCommaBefore();

    // ── вводные слова — запятая ПОСЛЕ ────────────────────────────────────
    private static readonly Regex s_commaAfter = BuildCommaAfter();

    // ── вопросительные слова — НАЧАЛО фразы ──────────────────────────────
    private static readonly Regex s_questionStart = new(
        @"^(где|куда|откуда|зачем|почему|отчего|кому|кого|сколько|"
        + @"какой|какая|какое|какие|каких|какому|каким|каком|"
        + @"чьё|чья|чьи|чей)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── возгласы — целиком одно-двусловная фраза ─────────────────────────
    private static readonly Regex s_exclamation = new(
        @"^(ой|ах|ох|эх|ну|ура|браво|нет|да|отлично|супер|класс|здорово)([\s\w]{0,8})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string Apply(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        text = text.Trim();

        // 1. Запятая перед союзами (не в начале предложения)
        text = s_commaBefore.Replace(text, m =>
        {
            string before = m.Groups[1].Value;
            string word   = m.Groups[2].Value;
            // не добавляем запятую если перед словом уже есть знак
            char lastChar = before.Length > 0 ? before[^1] : '\0';
            if (lastChar is ',' or '.' or '!' or '?' or '-' or ';' or ':')
                return m.Value;
            return before + ", " + word;
        });

        // 2. Запятая после вводных слов
        text = s_commaAfter.Replace(text, m =>
        {
            string word  = m.Groups[1].Value;
            string after = m.Groups[2].Value;
            if (after.Length > 0 && (after[0] is ',' or '.' or '!' or '?'))
                return m.Value;
            return word + ", " + after.TrimStart();
        });

        // 3. Знак вопроса
        if (s_questionStart.IsMatch(text))
            text = StripTrailingPunct(text) + "?";

        // 4. Знак восклицания
        else if (s_exclamation.IsMatch(text))
            text = StripTrailingPunct(text) + "!";

        return text;
    }

    private static string StripTrailingPunct(string s)
    {
        s = s.TrimEnd();
        if (s.Length > 0 && s[^1] is '.' or '?' or '!' or ',')
            s = s[..^1].TrimEnd();
        return s;
    }

    // ── построение регулярных выражений ───────────────────────────────────

    private static Regex BuildCommaBefore()
    {
        // Двухсловные союзы идут первыми, чтобы матчились раньше однословных
        string[] phrases =
        [
            "потому что", "так как", "оттого что", "ввиду того что",
            "несмотря на то что", "с тех пор как", "до тех пор пока",
            "после того как", "до того как", "как только", "при том что",
            "тогда как",
            // однословные
            "но", "однако", "зато", "хотя", "хоть",
            "что", "чтобы", "чтоб",
            "который", "которая", "которое", "которые",
            "которого", "которой", "которому", "которым", "которых",
            "если", "когда", "пока", "поскольку", "раз", "коли",
            "ибо", "ведь", "притом", "причём",
        ];

        // Группируем: (предшествующий_символ)\s+(союз)
        // Предшествующий символ — не начало строки и не пробел
        string alt = string.Join("|", phrases.Select(Regex.Escape));
        return new Regex(
            $@"(\S)\s+({alt})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private static Regex BuildCommaAfter()
    {
        string[] words =
        [
            "конечно", "безусловно", "бесспорно", "очевидно",
            "разумеется", "несомненно", "впрочем", "однако",
            "итак", "следовательно", "значит", "таким образом",
            "например", "в частности", "в общем", "в целом",
            "во-первых", "во-вторых", "в-третьих",
            "кстати", "кстати говоря", "между прочим",
            "наконец", "наоборот", "по-моему", "по-видимому",
        ];

        string alt = string.Join("|", words.Select(Regex.Escape));
        // Матчим в начале строки или после точки/восклицания/вопроса + пробел
        return new Regex(
            $@"(?:^|(?<=[.!?]\s))({alt})\s+(\w)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public void Dispose() { }
}

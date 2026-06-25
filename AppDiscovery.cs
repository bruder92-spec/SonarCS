using System.Text;

namespace Sonar;

/// <summary>
/// Сканирует ярлыки Start Menu и матчит голосовой запрос по транслитерации + Левенштейн.
/// Используется как fallback когда TryLaunch не нашёл встроенного приложения.
/// </summary>
internal static class AppDiscovery
{
    private static readonly string[] StartMenuPaths =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
    ];

    private static readonly string[] SkipKeywords =
    [
        "uninstall", "удалить", "help", "readme", "support",
        "website", "documentation", "manual", "release notes",
        "what's new", "getting started", "migrate",
    ];

    private static readonly string[] VersionTokens =
        ["cc", "pro", "lite", "free", "plus", "x86", "x64", "portable"];

    private static readonly Lazy<(string Name, string LnkPath)[]> _apps = new(Scan);

    internal static int AppCount => _apps.Value.Length;

    // ── Публичный матчер ───────────────────────────────────────────────────────

    /// <param name="cyrillicQuery">Текст после глагола запуска, нормализованный (Normalize уже применён)</param>
    internal static bool TryMatch(string cyrillicQuery, out string lnkPath)
    {
        lnkPath = string.Empty;
        if (string.IsNullOrWhiteSpace(cyrillicQuery)) return false;

        string latin = Transliterate(cyrillicQuery);
        string[] queryWords = latin
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length >= 2)
            .ToArray();
        if (queryWords.Length == 0) return false;

        (string Name, string LnkPath)? best = null;
        int bestScore = int.MaxValue;

        foreach (var app in _apps.Value)
        {
            int score = Score(queryWords, app.Name);
            if (score < bestScore)
            {
                bestScore = score;
                best = app;
            }
        }

        if (best.HasValue && bestScore < int.MaxValue)
        {
            Logger.Info($"AppDiscovery: '{cyrillicQuery}' → '{best.Value.Name}' (score={bestScore})");
            lnkPath = best.Value.LnkPath;
            return true;
        }
        return false;
    }

    // ── Скоринг ────────────────────────────────────────────────────────────────

    private static int Score(string[] queryWords, string appName)
    {
        string[] nameWords = appName.ToLowerInvariant()
            .Split([' ', '-', '_', '(', ')', '.', ',', '!'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !IsNoise(w))
            .ToArray();

        if (nameWords.Length == 0) return int.MaxValue;

        int total = 0;
        foreach (var qw in queryWords)
        {
            int minDist = nameWords.Min(nw => Levenshtein(qw, nw));
            int threshold = qw.Length switch { <= 3 => 0, <= 5 => 1, _ => 2 };
            if (minDist > threshold) return int.MaxValue;
            total += minDist;
        }
        return total;
    }

    private static bool IsNoise(string w) =>
        w.All(c => char.IsDigit(c) || c == '.' || c == '-') ||
        Array.IndexOf(VersionTokens, w) >= 0;

    // ── Сканирование Start Menu ────────────────────────────────────────────────

    private static (string Name, string LnkPath)[] Scan()
    {
        var entries = new List<(string, string)>();
        foreach (var dir in StartMenuPaths)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var lnk in Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories))
            {
                string name = Path.GetFileNameWithoutExtension(lnk);
                if (SkipKeywords.Any(kw => name.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                    continue;
                entries.Add((name, lnk));
            }
        }
        Logger.Info($"AppDiscovery: найдено {entries.Count} ярлыков в Start Menu");
        return [.. entries];
    }

    // ── Транслитерация рус → лат (фонетическая) ───────────────────────────────

    internal static string Transliterate(string text)
    {
        var sb = new StringBuilder(text.Length * 2);
        foreach (char c in text)
        {
            sb.Append(c switch
            {
                'а' => "a",   'б' => "b",   'в' => "v",   'г' => "g",   'д' => "d",
                'е' => "e",   'ё' => "yo",  'ж' => "zh",  'з' => "z",   'и' => "i",
                'й' => "y",   'к' => "k",   'л' => "l",   'м' => "m",   'н' => "n",
                'о' => "o",   'п' => "p",   'р' => "r",   'с' => "s",   'т' => "t",
                'у' => "u",   'ф' => "f",   'х' => "kh",  'ц' => "ts",  'ч' => "ch",
                'ш' => "sh",  'щ' => "sch", 'ъ' => "",    'ы' => "y",   'ь' => "",
                'э' => "e",   'ю' => "yu",  'я' => "ya",
                _ => c.ToString()
            });
        }
        return sb.ToString();
    }

    // ── Расстояние Левенштейна ─────────────────────────────────────────────────

    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = a[i - 1] == b[j - 1]
                    ? d[i - 1, j - 1]
                    : 1 + Math.Min(d[i - 1, j - 1], Math.Min(d[i - 1, j], d[i, j - 1]));

        return d[a.Length, b.Length];
    }
}

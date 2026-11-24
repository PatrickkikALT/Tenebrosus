using System;
using System.Globalization;
using System.Text;

public static class PhraseMatcher
{
    private const float TokenMatchFloor = 0.7f;

    public static string Normalize(string text)
    {
        string lower = (text ?? "").ToLowerInvariant().Replace("æ", "ae").Replace("œ", "oe");
        string decomposed = lower.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(decomposed.Length);
        bool pendingSpace = false;
        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (character == '\'' || character == '’') continue;
            if (!char.IsLetterOrDigit(character))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace && builder.Length > 0) builder.Append(' ');
            pendingSpace = false;
            builder.Append(character);
        }

        return builder.ToString();
    }

    public static string[] Tokens(string text)
    {
        string[] words = Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
            words[i] = Fold(words[i]);
        return words;
    }

    public static float Score(string heard, string target)
    {
        return Score(Tokens(heard), Tokens(target));
    }

    public static float Score(string[] heard, string[] target)
    {
        if (heard.Length == 0 || target.Length == 0) return 0f;
        string joinedTarget = string.Concat(target);
        float best = Similarity(string.Concat(heard), joinedTarget);
        int shortest = Math.Max(1, target.Length - 1);
        int longest = target.Length + 1;
        for (int start = 0; start < heard.Length; start++)
        for (int length = shortest; length <= longest && start + length <= heard.Length; length++)
            best = Math.Max(best, Similarity(Join(heard, start, length), joinedTarget));
        return Math.Max(best, TokenOverlap(heard, target));
    }

    public static float Similarity(string a, string b)
    {
        int longest = Math.Max(a.Length, b.Length);
        if (longest == 0) return 1f;
        return 1f - (float)Levenshtein(a, b) / longest;
    }

    private static string Fold(string word)
    {
        string merged = word.Replace("ae", "e").Replace("oe", "e").Replace("ph", "f");
        StringBuilder builder = new(merged.Length);
        foreach (char character in merged)
        {
            char folded = character switch
            {
                'j' or 'y' => 'i',
                'v' or 'w' => 'u',
                'k' or 'q' => 'c',
                'z' => 's',
                _ => character
            };
            if (folded == 'h') continue;
            if (builder.Length > 0 && builder[builder.Length - 1] == folded) continue;
            builder.Append(folded);
        }

        return builder.Length > 0 ? builder.ToString() : word;
    }

    private static string Join(string[] tokens, int start, int length)
    {
        StringBuilder builder = new();
        for (int i = start; i < start + length; i++)
            builder.Append(tokens[i]);
        return builder.ToString();
    }

    private static float TokenOverlap(string[] heard, string[] target)
    {
        float total = 0f;
        foreach (string word in target)
        {
            float best = 0f;
            foreach (string candidate in heard)
                best = Math.Max(best, Similarity(candidate, word));
            if (best >= TokenMatchFloor) total += best;
        }

        return total / target.Length;
    }

    private static int Levenshtein(string a, string b)
    {
        int[] previous = new int[b.Length + 1];
        int[] current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) previous[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}

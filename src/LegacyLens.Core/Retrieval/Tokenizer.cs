using System.Text.RegularExpressions;

namespace LegacyLens.Core.Retrieval;

/// <summary>
/// Splits text into lowercase search terms. Camel case and Pascal case words also give their parts,
/// so "CreateObject" yields "createobject", "create" and "object".
/// </summary>
public static class Tokenizer
{
    private static readonly Regex Words = new(@"[\p{L}\p{Nd}]+");

    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "the", "a", "an", "and", "or", "of", "to", "in", "is", "it", "for", "on", "with", "as", "by", "be",
        "this", "that", "from", "at", "are", "use", "not",
    };

    public static IReadOnlyList<string> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tokens = new List<string>();
        foreach (Match match in Words.Matches(text))
        {
            var piece = match.Value;
            AddIfUseful(tokens, piece.ToLowerInvariant());

            var parts = SplitCamelCase(piece);
            if (parts.Count > 1)
            {
                foreach (var part in parts)
                {
                    AddIfUseful(tokens, part.ToLowerInvariant());
                }
            }
        }

        return tokens;
    }

    private static void AddIfUseful(List<string> tokens, string token)
    {
        if (token.Length >= 2 && !Stopwords.Contains(token))
        {
            tokens.Add(token);
        }
    }

    /// <summary>Splits before an upper-case letter that follows a lower-case letter, or that ends an upper-case run ("HTMLEncode").</summary>
    private static List<string> SplitCamelCase(string piece)
    {
        var parts = new List<string>();
        var start = 0;
        for (var i = 1; i < piece.Length; i++)
        {
            var startsPart = char.IsUpper(piece[i]) &&
                (char.IsLower(piece[i - 1]) || (char.IsUpper(piece[i - 1]) && i + 1 < piece.Length && char.IsLower(piece[i + 1])));
            if (startsPart)
            {
                parts.Add(piece[start..i]);
                start = i;
            }
        }

        parts.Add(piece[start..]);
        return parts;
    }
}

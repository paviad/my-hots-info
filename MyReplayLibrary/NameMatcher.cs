using System.Globalization;
using System.Text;

namespace MyReplayLibrary;

/// <summary>
/// Maps a player name read by OCR to a known player name, tolerating dropped accents, i/l/1 and
/// 0/o confusion, and Cyrillic letters read as their Latin lookalikes.
/// </summary>
/// <remarks>
/// Deliberately no edit-distance matching: measured on 210 real screenshots, allowing even one
/// wrong letter matched new strangers to some other known player far more often than it fixed a
/// misread (+33 right, +196 wrong), because tens of thousands of known names leave few names
/// without a neighbour one letter away.
/// </remarks>
public class NameMatcher {
    private readonly Dictionary<string, string> _byLower = new();
    private readonly Dictionary<string, Dictionary<string, int>> _byFolded = new();

    /// <param name="known">Known player names with the number of games played with each.</param>
    public NameMatcher(IEnumerable<(string Name, int Games)> known) {
        foreach (var (name, games) in known) {
            _byLower.TryAdd(name.ToLowerInvariant(), name);
            var folded = Fold(name);
            if (!_byFolded.TryGetValue(folded, out var names)) {
                _byFolded[folded] = names = new Dictionary<string, int>();
            }

            names[name] = names.GetValueOrDefault(name) + games;
        }
    }

    /// <summary>
    /// The known name <paramref name="ocrName"/> most likely is, or null when there is none or
    /// two equally played names fit.
    /// </summary>
    public string? Resolve(string ocrName) {
        if (_byLower.TryGetValue(ocrName.ToLowerInvariant(), out var exact)) {
            return exact;
        }

        if (!_byFolded.TryGetValue(Fold(ocrName), out var names)) {
            return null;
        }

        var ranked = names.OrderByDescending(kv => kv.Value).ToList();
        return ranked.Count > 1 && ranked[0].Value == ranked[1].Value ? null : ranked[0].Key;
    }

    private static readonly Dictionary<char, string> Lookalikes = new() {
        ['а'] = "a", ['е'] = "e", ['ё'] = "e", ['о'] = "o", ['р'] = "p", ['с'] = "c", ['х'] = "x", ['у'] = "y",
        ['к'] = "k", ['м'] = "m", ['н'] = "h", ['т'] = "t", ['в'] = "b", ['з'] = "3", ['ł'] = "l", ['ø'] = "o",
        ['đ'] = "d", ['ß'] = "ss", ['i'] = "l", ['1'] = "l", ['|'] = "l", ['0'] = "o",
    };

    /// <summary>Lowercase, strip accents and collapse characters OCR confuses with each other.</summary>
    public static string Fold(string s) {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.ToLowerInvariant().Normalize(NormalizationForm.FormD)) {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) {
                continue;
            }

            if (Lookalikes.TryGetValue(c, out var r)) {
                sb.Append(r);
            }
            else {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}

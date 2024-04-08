using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace MyHotsInfo.Extensions;

public static partial class StringExtensions {
    private static readonly string BadChars = Regex.Escape("- '‘’:,!.\"”?()");

    [return: NotNullIfNotNull(nameof(s))]
    public static string? Strip(this string? s) {
        if (s is null) {
            return null;
        }

        var s1 = Regex.Replace(s, $"[{BadChars}]", "").ToLower();
        return s1.MyNormalize();
    }

    [return: NotNullIfNotNull(nameof(s))]
    public static string? MyNormalize(this string? s, bool lowerCase = false, bool noTrim = false) {
        if (s is null) {
            return null;
        }

        var rex1 = InvisibleCharactersRex();
        var rex2 = InvisibleCharactersRex2();
        var normalize = s.Normalize(NormalizationForm.FormD);
        var rc1 = rex1.Replace(normalize, "");
        var rc2 = rex2.Replace(rc1, "");
        var rc3 = noTrim ? rc2 : rc2.Trim();
        var rc = rc3.Replace(' ' /* ascii 160 */, ' ');
        return lowerCase ? rc.ToLower() : rc;
    }


    [GeneratedRegex("[^\\P{C}\\t\\r\\n]")]
    private static partial Regex InvisibleCharactersRex();

    [GeneratedRegex("[^\\P{M}\\t\\r\\n]")]
    private static partial Regex InvisibleCharactersRex2();
}

using System.Globalization;
using System.Text;

namespace UnityGitTool.Core
{
    /// <summary>
    /// Automatically fixes names that break a set of rules (branches, repositories...).
    /// Each kind of name has its own implementation (SRP) and the presenter only knows
    /// this interface (DIP), so new rules can be added without touching it (OCP).
    /// </summary>
    public interface INameSanitizer
    {
        /// <summary>
        /// "Live" fix while typing: replaces character by character without changing the
        /// text length, so the cursor does not jump (e.g. "my branch" → "my-branch").
        /// </summary>
        string SanitizeWhileTyping(string raw);

        /// <summary>Full, final fix, applied right before the name is used.</summary>
        string Sanitize(string raw);
    }

    public static class TextNormalizer
    {
        /// <summary>"Café Menú" → "Cafe Menu". Keeps the length of precomposed characters.</summary>
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var builder = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                var decomposed = c.ToString().Normalize(NormalizationForm.FormD);
                var baseChar = decomposed[0];
                builder.Append(CharUnicodeInfo.GetUnicodeCategory(baseChar) == UnicodeCategory.NonSpacingMark ? c : baseChar);
            }

            return builder.ToString();
        }
    }
}

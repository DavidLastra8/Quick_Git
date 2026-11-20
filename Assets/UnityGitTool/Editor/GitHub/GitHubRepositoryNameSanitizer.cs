using System.Text;
using System.Text.RegularExpressions;
using UnityGitTool.Core;

namespace UnityGitTool.GitHub
{
    /// <summary>GitHub repository name rules: only A-Z, a-z, 0-9, '.', '-' and '_' (max. 100).</summary>
    public sealed class GitHubRepositoryNameSanitizer : INameSanitizer
    {
        private const int MaxLength = 100;

        public string SanitizeWhileTyping(string raw)
        {
            var text = TextNormalizer.RemoveDiacritics(raw);
            var builder = new StringBuilder(text.Length);

            foreach (var c in text)
                builder.Append(IsAllowed(c) ? c : '-');

            return builder.ToString();
        }

        public string Sanitize(string raw)
        {
            var text = Regex.Replace(SanitizeWhileTyping(raw), "-{2,}", "-").Trim('-', '.');
            if (text.Length > MaxLength) text = text.Substring(0, MaxLength).TrimEnd('-', '.');
            return text;
        }

        private static bool IsAllowed(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_';
    }
}

using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityGitTool.Core;

namespace UnityGitTool.Git
{
    /// <summary>
    /// Git branch name rules (git check-ref-format):
    /// no spaces or ~ ^ : ? * [ \, no "..", "@{" or "//", must not start with "-" or ".",
    /// must not end with "/", "." or ".lock".
    /// </summary>
    public sealed class GitBranchNameSanitizer : INameSanitizer
    {
        private const string ForbiddenChars = "~^:?*[\\";

        public string SanitizeWhileTyping(string raw)
        {
            var text = TextNormalizer.RemoveDiacritics(raw);
            var builder = new StringBuilder(text.Length);

            foreach (var c in text)
            {
                var invalid = char.IsWhiteSpace(c) || char.IsControl(c) || c > 127 || ForbiddenChars.IndexOf(c) >= 0;
                builder.Append(invalid ? '-' : c);
            }

            return builder.ToString();
        }

        public string Sanitize(string raw)
        {
            var text = SanitizeWhileTyping(raw);

            text = Regex.Replace(text, "-{2,}", "-");
            text = Regex.Replace(text, @"\.{2,}", ".");
            text = Regex.Replace(text, "/{2,}", "/");
            text = text.Replace("@{", "@-");

            // Each segment between "/" cannot start with "." or end with "." or ".lock".
            var segments = text.Split('/')
                .Select(SanitizeSegment)
                .Where(s => s.Length > 0);

            text = string.Join("/", segments).Trim('-');
            return text == "@" ? string.Empty : text;
        }

        private static string SanitizeSegment(string segment)
        {
            segment = segment.Trim('-').TrimStart('.');
            while (segment.EndsWith(".lock")) segment = segment.Substring(0, segment.Length - 5);
            return segment.TrimEnd('.', '-');
        }
    }
}

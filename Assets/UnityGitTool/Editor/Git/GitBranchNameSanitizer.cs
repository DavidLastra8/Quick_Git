using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityGitTool.Core;

namespace UnityGitTool.Git
{
    /// <summary>
    /// Reglas de nombres de rama de git (git check-ref-format):
    /// sin espacios ni ~ ^ : ? * [ \, sin "..", "@{" ni "//", sin empezar por "-" o ".",
    /// sin terminar en "/", "." o ".lock".
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

            // Cada tramo entre "/" no puede empezar por "." ni terminar en "." o ".lock".
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

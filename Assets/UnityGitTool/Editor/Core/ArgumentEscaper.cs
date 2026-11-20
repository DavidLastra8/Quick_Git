using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityGitTool.Core
{
    /// <summary>
    /// Turns a list of arguments into a single escaped command line
    /// (CommandLineToArgvW rules, also compatible with Mono on macOS/Linux).
    /// This way commit messages with spaces or quotes reach git intact.
    /// </summary>
    public static class ArgumentEscaper
    {
        private static readonly char[] CharsRequiringQuotes = { ' ', '\t', '\n', '\v', '"' };

        public static string Join(IEnumerable<string> arguments) => string.Join(" ", arguments.Select(Quote));

        public static string Quote(string argument)
        {
            if (string.IsNullOrEmpty(argument)) return "\"\"";
            if (argument.IndexOfAny(CharsRequiringQuotes) < 0) return argument;

            var builder = new StringBuilder("\"");
            var backslashes = 0;

            foreach (var c in argument)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                {
                    builder.Append('\\', backslashes * 2 + 1);
                    builder.Append('"');
                }
                else
                {
                    builder.Append('\\', backslashes);
                    builder.Append(c);
                }

                backslashes = 0;
            }

            builder.Append('\\', backslashes * 2);
            builder.Append('"');
            return builder.ToString();
        }
    }
}

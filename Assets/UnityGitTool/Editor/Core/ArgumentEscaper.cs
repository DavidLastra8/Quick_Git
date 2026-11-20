using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityGitTool.Core
{
    /// <summary>
    /// Convierte una lista de argumentos en una única línea de comandos escapada
    /// (reglas de CommandLineToArgvW, compatibles también con Mono en macOS/Linux).
    /// Así los mensajes de commit con espacios o comillas llegan intactos a git.
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

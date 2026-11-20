using System.Collections.Generic;

namespace UnityGitTool.Git
{
    /// <summary>
    /// Interpreta la salida de "git status --porcelain=v1 -z".
    /// Cada entrada es "XY ruta" separada por NUL; en los renombrados le sigue la ruta original.
    /// X = estado en el stage, Y = estado en la carpeta de trabajo.
    /// </summary>
    public static class GitStatusParser
    {
        private static readonly HashSet<string> ConflictCodes = new HashSet<string> { "DD", "AU", "UD", "UA", "DU", "AA", "UU" };

        public static IReadOnlyList<GitFileChange> Parse(string output)
        {
            var changes = new List<GitFileChange>();
            var entries = (output ?? string.Empty).Split('\0');

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.Length < 4) continue;

                var code = entry.Substring(0, 2);
                var path = entry.Substring(3);
                var kind = Classify(code);

                string oldPath = null;
                if ((code[0] == 'R' || code[0] == 'C') && i + 1 < entries.Length)
                    oldPath = entries[++i];

                changes.Add(new GitFileChange(path, kind, oldPath));
            }

            return changes;
        }

        private static FileChangeKind Classify(string code)
        {
            if (code == "??") return FileChangeKind.Untracked;
            if (ConflictCodes.Contains(code)) return FileChangeKind.Conflicted;
            if (code.IndexOf('R') >= 0 || code.IndexOf('C') >= 0) return FileChangeKind.Renamed;
            if (code.IndexOf('D') >= 0) return FileChangeKind.Deleted;
            if (code.IndexOf('A') >= 0) return FileChangeKind.Added;
            return FileChangeKind.Modified;
        }
    }
}

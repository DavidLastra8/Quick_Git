using System;
using System.Linq;

namespace QuickGit.UseCases
{
    /// <summary>
    /// Marker added to the message of commits made with compile errors.
    /// It is visible on GitHub and tells us, when pushing, whether the last commit was broken.
    /// </summary>
    public static class CompileErrorMarker
    {
        public const string Key = "Unity-Compile-Errors";
        public const string Value = "true";

        public static bool IsPresentIn(string fullCommitMessage) =>
            (fullCommitMessage ?? string.Empty)
            .Split('\n')
            .Any(line => line.Trim().Equals($"{Key}: {Value}", StringComparison.OrdinalIgnoreCase));
    }
}

using System;
using System.Linq;

namespace UnityGitTool.UseCases
{
    /// <summary>
    /// Marca que se añade al mensaje de los commits hechos con errores de compilación.
    /// Queda visible en GitHub y permite saber, al hacer push, si el último commit estaba roto.
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

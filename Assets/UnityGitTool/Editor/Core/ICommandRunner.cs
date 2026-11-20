using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnityGitTool.Core
{
    /// <summary>
    /// Abstracción para ejecutar un programa externo. Permite sustituir el proceso real
    /// por un doble de prueba (DIP) sin tocar los servicios de Git.
    /// </summary>
    public interface ICommandRunner
    {
        Task<CommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
    }
}

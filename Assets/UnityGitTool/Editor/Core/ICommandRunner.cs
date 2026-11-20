using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnityGitTool.Core
{
    /// <summary>
    /// Abstraction to run an external program. Lets the real process be replaced
    /// by a test double (DIP) without touching the Git services.
    /// </summary>
    public interface ICommandRunner
    {
        Task<CommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
    }
}

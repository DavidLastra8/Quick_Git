using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QuickGit.Core
{
    /// <summary>
    /// Abstraction to run an external program. Lets the real process be replaced
    /// </summary>
    public interface ICommandRunner
    {
        Task<CommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
    }
}

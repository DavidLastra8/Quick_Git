using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;

namespace UnityGitTool.Git
{
    public sealed class GitCommandExecutor : IGitCommandExecutor
    {
        private readonly ICommandRunner _runner;
        private readonly IGitAuthArgumentsProvider _authArguments;

        public string WorkingDirectory { get; }

        public GitCommandExecutor(ICommandRunner runner, string workingDirectory, IGitAuthArgumentsProvider authArguments)
        {
            _runner = runner;
            WorkingDirectory = workingDirectory;
            _authArguments = authArguments;
        }

        public Task<CommandResult> RunAsync(CancellationToken cancellationToken, params string[] arguments) =>
            _runner.RunAsync(WorkingDirectory, arguments, cancellationToken);

        public Task<CommandResult> RunWithAuthAsync(CancellationToken cancellationToken, params string[] arguments)
        {
            // "-c key=value" options must come before the subcommand.
            var all = new List<string>(_authArguments.GetArguments());
            all.AddRange(arguments);
            return _runner.RunAsync(WorkingDirectory, all, cancellationToken);
        }
    }
}

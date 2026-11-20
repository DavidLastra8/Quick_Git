using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;

namespace UnityGitTool.Git
{
    /// <summary>Operaciones de red con el remoto: fetch, pull y push.</summary>
    public sealed class GitSyncService : IGitSyncService
    {
        private const string Remote = "origin";
        private readonly IGitCommandExecutor _git;

        public GitSyncService(IGitCommandExecutor git) => _git = git;

        public async Task<OperationResult> FetchAsync(CancellationToken ct)
        {
            var result = await _git.RunWithAuthAsync(ct, "fetch", "--prune", Remote);
            return GitOutput.ToResult(result, "Fetch completado.", "Error en fetch");
        }

        public async Task<OperationResult> PullAsync(PullStrategy strategy, CancellationToken ct)
        {
            var result = await _git.RunWithAuthAsync(ct, "pull", ToFlag(strategy));
            var message = string.IsNullOrWhiteSpace(result.Output) ? "Pull completado." : $"Pull completado:\n{result.Output}";
            return GitOutput.ToResult(result, message, "Error en pull");
        }

        public async Task<OperationResult> PushAsync(string branch, bool setUpstream, CancellationToken ct)
        {
            var result = setUpstream
                ? await _git.RunWithAuthAsync(ct, "push", "--set-upstream", Remote, branch)
                : await _git.RunWithAuthAsync(ct, "push");

            return GitOutput.ToResult(result, $"Push de '{branch}' completado.", "Error en push");
        }

        private static string ToFlag(PullStrategy strategy)
        {
            switch (strategy)
            {
                case PullStrategy.Rebase: return "--rebase";
                case PullStrategy.Merge: return "--no-rebase";
                default: return "--ff-only";
            }
        }
    }
}

using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;

namespace UnityGitTool.Git
{
    /// <summary>Network operations with the remote: fetch, pull and push.</summary>
    public sealed class GitSyncService : IGitSyncService
    {
        private const string Remote = "origin";
        private readonly IGitCommandExecutor _git;

        public GitSyncService(IGitCommandExecutor git) => _git = git;

        public async Task<OperationResult> FetchAsync(CancellationToken ct)
        {
            var result = await _git.RunWithAuthAsync(ct, "fetch", "--prune", Remote);
            return GitOutput.ToResult(result, "Fetch completed.", "Fetch failed");
        }

        public async Task<OperationResult> PullAsync(PullStrategy strategy, CancellationToken ct)
        {
            var result = await _git.RunWithAuthAsync(ct, "pull", ToFlag(strategy));
            var message = string.IsNullOrWhiteSpace(result.Output) ? "Pull completed." : $"Pull completed:\n{result.Output}";
            return GitOutput.ToResult(result, message, "Pull failed");
        }

        public async Task<OperationResult> PushAsync(string branch, bool setUpstream, CancellationToken ct)
        {
            var result = setUpstream
                ? await _git.RunWithAuthAsync(ct, "push", "--set-upstream", Remote, branch)
                : await _git.RunWithAuthAsync(ct, "push");

            return GitOutput.ToResult(result, $"Push of '{branch}' completed.", "Push failed");
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

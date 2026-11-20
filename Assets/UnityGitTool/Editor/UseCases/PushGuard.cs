using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;
using UnityGitTool.Git;
using UnityGitTool.Unity;

namespace UnityGitTool.UseCases
{
    public interface IPushGuard
    {
        /// <summary>Ok if the current branch can be pushed; Fail with the reason otherwise.</summary>
        Task<OperationResult> CheckAsync(RepositoryStatus status, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Rule: nothing with compile errors is pushed to a protected branch (main/master),
    /// neither if the project has them now nor if the last commit was made with errors.
    /// On every other branch, pushing is always allowed.
    /// </summary>
    public sealed class PushGuard : IPushGuard
    {
        private readonly IBranchProtectionPolicy _policy;
        private readonly IProjectHealthChecker _health;
        private readonly IGitCommitReader _commits;

        public PushGuard(IBranchProtectionPolicy policy, IProjectHealthChecker health, IGitCommitReader commits)
        {
            _policy = policy;
            _health = health;
            _commits = commits;
        }

        public async Task<OperationResult> CheckAsync(RepositoryStatus status, CancellationToken ct)
        {
            var branch = status.CurrentBranch;
            if (!_policy.IsProtected(branch)) return OperationResult.Ok();

            if (_health.HasCompileErrors)
                return OperationResult.Fail(
                    $"The project has compile errors: you cannot push to '{branch}'. " +
                    "Fix them, or create another branch and push there.");

            if (!status.HasCommits) return OperationResult.Ok();

            var head = await _commits.GetFullMessageAsync("HEAD", ct);
            if (head.Success && CompileErrorMarker.IsPresentIn(head.Value))
                return OperationResult.Fail(
                    $"The last commit was made with compile errors: you cannot push to '{branch}'. " +
                    "Make a new commit with the errors fixed, or push it to another branch.");

            return OperationResult.Ok();
        }
    }
}

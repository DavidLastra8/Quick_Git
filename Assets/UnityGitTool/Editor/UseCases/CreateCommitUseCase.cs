using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;
using UnityGitTool.Git;
using UnityGitTool.Unity;

namespace UnityGitTool.UseCases
{
    public interface ICreateCommitUseCase
    {
        Task<OperationResult> ExecuteAsync(CommitMessage message, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Creates the commit. If the project has compile errors it is still allowed (you may want to save
    /// work in progress on your branch), but it is marked so it cannot be pushed to a protected branch.
    /// </summary>
    public sealed class CreateCommitUseCase : ICreateCommitUseCase
    {
        private readonly IGitCommitService _commits;
        private readonly IProjectHealthChecker _health;

        public CreateCommitUseCase(IGitCommitService commits, IProjectHealthChecker health)
        {
            _commits = commits;
            _health = health;
        }

        public async Task<OperationResult> ExecuteAsync(CommitMessage message, CancellationToken ct)
        {
            // Read before any await: the Unity API can only be used on the main thread.
            var hasErrors = _health.HasCompileErrors;
            if (hasErrors) message = message.WithTrailer(CompileErrorMarker.Key, CompileErrorMarker.Value);

            var result = await _commits.CommitAllAsync(message, ct);
            if (!result.Success || !hasErrors) return result;

            return OperationResult.Ok(result.Message +
                "\nThe project had compile errors: this commit is marked and cannot be pushed to main/master.");
        }
    }
}

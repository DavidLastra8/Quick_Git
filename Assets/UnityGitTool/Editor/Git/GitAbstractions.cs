using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;

namespace UnityGitTool.Git
{
    
    /// <summary>Runs git commands in the project directory.</summary>
    public interface IGitCommandExecutor
    {
        string WorkingDirectory { get; }
        Task<CommandResult> RunAsync(CancellationToken cancellationToken, params string[] arguments);

        /// <summary>Same as RunAsync, but adds credentials for network operations (fetch/pull/push).</summary>
        Task<CommandResult> RunWithAuthAsync(CancellationToken cancellationToken, params string[] arguments);
    }

    /// <summary>Provides "-c ..." authentication arguments. </summary>
    public interface IGitAuthArgumentsProvider
    {
        IEnumerable<string> GetArguments();
    }

    public interface IGitRepositoryInspector
    {
        Task<RepositoryStatus> InspectAsync(CancellationToken cancellationToken);
    }

    public interface IGitRepositoryInitializer
    {
        Task<OperationResult> InitializeAsync(CancellationToken cancellationToken);
    }

    public interface IGitBranchService
    {
        Task<OperationResult<IReadOnlyList<GitBranch>>> GetBranchesAsync(CancellationToken cancellationToken);
        Task<OperationResult> CheckoutAsync(GitBranch branch, CancellationToken cancellationToken);
        Task<OperationResult> CreateBranchAsync(string name, CancellationToken cancellationToken);

        /// <summary>True if every commit of the branch is already in "target" (e.g. "HEAD" or "origin/x").</summary>
        Task<bool> IsMergedIntoAsync(GitBranch branch, string target, CancellationToken cancellationToken);

        Task<OperationResult> DeleteLocalBranchAsync(GitBranch branch, CancellationToken cancellationToken);
        Task<OperationResult> DeleteRemoteBranchAsync(GitBranch branch, CancellationToken cancellationToken);
    }

    /// <summary>History reader (read-only, separate from whoever writes commits).</summary>
    public interface IGitCommitReader
    {
        Task<OperationResult<string>> GetFullMessageAsync(string revision, CancellationToken cancellationToken);
    }

    /// <summary>Decides which branches are protected (cannot be deleted and reject pushes with errors).</summary>
    public interface IBranchProtectionPolicy
    {
        bool IsProtected(string branchName);
    }

    public interface IGitSyncService
    {
        Task<OperationResult> FetchAsync(CancellationToken cancellationToken);
        Task<OperationResult> PullAsync(PullStrategy strategy, CancellationToken cancellationToken);
        Task<OperationResult> PushAsync(string branch, bool setUpstream, CancellationToken cancellationToken);
    }

    public interface IGitCommitService
    {
        Task<OperationResult> CommitAllAsync(CommitMessage message, CancellationToken cancellationToken);
    }

    public interface IGitRemoteService
    {
        Task<OperationResult> SetRemoteAsync(string name, string url, CancellationToken cancellationToken);
    }

    public interface IGitIgnoreTemplate
    {
        string Content { get; }
    }
}

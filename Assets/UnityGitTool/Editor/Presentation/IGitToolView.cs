using System;
using UnityGitTool.Git;
using UnityGitTool.Hosting;

namespace UnityGitTool.Presentation
{
    public enum DialogChoice
    {
        Primary,
        Alternative,
        Cancel
    }

    public enum LogSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Passive View contract: it only raises user intentions (events) and draws what the
    /// presenter gives it. It holds no logic and knows nothing about git/GitHub.
    /// </summary>
    public interface IGitToolView
    {
        event Action RefreshRequested;

        /// <summary>Silent refresh (e.g. when the window gets focus). Must not block the UI.</summary>
        event Action BackgroundRefreshRequested;

        event Action CancelRequested;
        event Action FetchRequested;
        event Action<PullStrategy> PullRequested;
        event Action PushRequested;
        event Action<CommitMessage> CommitRequested;
        event Action<GitBranch> CheckoutRequested;
        event Action<string> CreateBranchRequested;
        event Action<GitBranch> DeleteBranchRequested;

        /// <summary>The user edited the new branch name (to fix it live).</summary>
        event Action<string> BranchNameEdited;

        /// <summary>The user edited the repository name (to fix it live).</summary>
        event Action<string> RepositoryNameEdited;
        event Action InitializeRepositoryRequested;
        event Action<CreateRepositoryRequest> PublishRepositoryRequested;
        event Action<string> SaveTokenRequested;
        event Action ClearTokenRequested;
        event Action OpenRemoteRequested;

        void Render(GitToolViewState state);
        void SetBusy(bool isBusy, string message);
        void AppendLog(string message, LogSeverity severity);
        bool Confirm(string title, string message);

        /// <summary>Dialog with two options besides Cancel.</summary>
        DialogChoice Choose(string title, string message, string primary, string alternative);
        void ClearCommitMessage();
        void ClearNewBranchName();
        void ShowBranchName(string name);
        void ShowRepositoryName(string name);
    }
}

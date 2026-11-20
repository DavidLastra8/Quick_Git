using System;
using UnityGitTool.Git;
using UnityGitTool.Hosting;

namespace UnityGitTool.Presentation
{
    public enum LogSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Contrato de la Vista pasiva: sólo emite intenciones del usuario (eventos) y pinta lo que
    /// el presentador le da. No contiene lógica ni conoce git/GitHub.
    /// </summary>
    public interface IGitToolView
    {
        event Action RefreshRequested;

        /// <summary>Refresco silencioso (p. ej. al enfocar la ventana). No debe bloquear la UI.</summary>
        event Action BackgroundRefreshRequested;

        event Action CancelRequested;
        event Action FetchRequested;
        event Action<PullStrategy> PullRequested;
        event Action PushRequested;
        event Action<string> CommitRequested;
        event Action<GitBranch> CheckoutRequested;
        event Action<string> CreateBranchRequested;
        event Action InitializeRepositoryRequested;
        event Action<CreateRepositoryRequest> PublishRepositoryRequested;
        event Action<string> SaveTokenRequested;
        event Action ClearTokenRequested;
        event Action OpenRemoteRequested;

        void Render(GitToolViewState state);
        void SetBusy(bool isBusy, string message);
        void AppendLog(string message, LogSeverity severity);
        bool Confirm(string title, string message);
        void ClearCommitMessage();
        void ClearNewBranchName();
    }
}

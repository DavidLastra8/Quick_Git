using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;

namespace UnityGitTool.Git
{
    // Interfaces pequeñas y específicas (ISP): cada cliente depende sólo de lo que usa.

    /// <summary>Ejecuta comandos git en el directorio del proyecto.</summary>
    public interface IGitCommandExecutor
    {
        string WorkingDirectory { get; }
        Task<CommandResult> RunAsync(CancellationToken cancellationToken, params string[] arguments);

        /// <summary>Igual que RunAsync, pero añade credenciales para operaciones de red (fetch/pull/push).</summary>
        Task<CommandResult> RunWithAuthAsync(CancellationToken cancellationToken, params string[] arguments);
    }

    /// <summary>Aporta argumentos "-c ..." de autenticación. Abierto a extensión (OCP): GitHub, GitLab, ninguno...</summary>
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

        /// <summary>True si todos los commits de la rama están ya en "target" (p. ej. "HEAD" u "origin/x").</summary>
        Task<bool> IsMergedIntoAsync(GitBranch branch, string target, CancellationToken cancellationToken);

        Task<OperationResult> DeleteLocalBranchAsync(GitBranch branch, CancellationToken cancellationToken);
        Task<OperationResult> DeleteRemoteBranchAsync(GitBranch branch, CancellationToken cancellationToken);
    }

    /// <summary>Lectura del historial (sólo lectura, separado de quien escribe commits).</summary>
    public interface IGitCommitReader
    {
        Task<OperationResult<string>> GetFullMessageAsync(string revision, CancellationToken cancellationToken);
    }

    /// <summary>Decide qué ramas están protegidas (no se borran y no aceptan push con errores).</summary>
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

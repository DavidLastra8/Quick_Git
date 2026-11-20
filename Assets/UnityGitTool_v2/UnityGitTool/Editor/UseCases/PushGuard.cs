using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;
using UnityGitTool.Git;
using UnityGitTool.Unity;

namespace UnityGitTool.UseCases
{
    public interface IPushGuard
    {
        /// <summary>Ok si se puede hacer push de la rama actual; Fail con el motivo si no.</summary>
        Task<OperationResult> CheckAsync(RepositoryStatus status, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Regla: a una rama protegida (main/master) no se sube nada con errores de compilación,
    /// ni si el proyecto los tiene ahora, ni si el último commit se hizo con errores.
    /// En el resto de ramas, el push siempre está permitido.
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
                    $"El proyecto tiene errores de compilación: no se puede hacer push a '{branch}'. " +
                    "Corrígelos, o crea otra rama y súbelos allí.");

            if (!status.HasCommits) return OperationResult.Ok();

            var head = await _commits.GetFullMessageAsync("HEAD", ct);
            if (head.Success && CompileErrorMarker.IsPresentIn(head.Value))
                return OperationResult.Fail(
                    $"El último commit se hizo con errores de compilación: no se puede hacer push a '{branch}'. " +
                    "Haz un commit nuevo con los errores corregidos, o súbelo a otra rama.");

            return OperationResult.Ok();
        }
    }
}

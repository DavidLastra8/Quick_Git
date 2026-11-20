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
    /// Crea el commit. Si el proyecto tiene errores de compilación lo permite (puedes querer guardar
    /// trabajo a medias en tu rama), pero lo marca para que no pueda subirse a una rama protegida.
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
            // Se lee antes de cualquier await: la API de Unity sólo puede usarse en el hilo principal.
            var hasErrors = _health.HasCompileErrors;
            if (hasErrors) message = message.WithTrailer(CompileErrorMarker.Key, CompileErrorMarker.Value);

            var result = await _commits.CommitAllAsync(message, ct);
            if (!result.Success || !hasErrors) return result;

            return OperationResult.Ok(result.Message +
                "\nEl proyecto tenía errores de compilación: este commit queda marcado y no podrá subirse a main/master.");
        }
    }
}

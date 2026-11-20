using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;

namespace UnityGitTool.Git
{
    public sealed class GitCommitService : IGitCommitService
    {
        private readonly IGitCommandExecutor _git;

        public GitCommitService(IGitCommandExecutor git) => _git = git;

        public async Task<OperationResult> CommitAllAsync(string message, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(message))
                return OperationResult.Fail("El mensaje de commit no puede estar vacío.");

            var add = await _git.RunAsync(ct, "add", "--all");
            if (!add.Success) return GitOutput.ToResult(add, null, "No se pudieron añadir los cambios");

            // Código de salida 0 = no hay nada en el stage.
            var staged = await _git.RunAsync(ct, "diff", "--cached", "--quiet");
            if (staged.Success) return OperationResult.Fail("No hay cambios para hacer commit.");

            var commit = await _git.RunAsync(ct, "commit", "-m", message.Trim());
            if (commit.Success) return OperationResult.Ok($"Commit creado: \"{message.Trim()}\"");

            if (commit.BestMessage.Contains("user.email") || commit.BestMessage.Contains("Please tell me who you are"))
                return OperationResult.Fail(
                    "Git no conoce tu identidad. Ejecuta una vez en una terminal:\n" +
                    "git config --global user.name \"Tu Nombre\"\n" +
                    "git config --global user.email \"tu@email.com\"");

            return GitOutput.ToResult(commit, null, "No se pudo crear el commit");
        }
    }
}

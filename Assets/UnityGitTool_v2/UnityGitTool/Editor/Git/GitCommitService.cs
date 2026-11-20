using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;

namespace UnityGitTool.Git
{
    public sealed class GitCommitService : IGitCommitService
    {
        private readonly IGitCommandExecutor _git;

        public GitCommitService(IGitCommandExecutor git) => _git = git;

        public async Task<OperationResult> CommitAllAsync(CommitMessage message, CancellationToken ct)
        {
            var validation = message.Validate();
            if (!validation.Success) return validation;

            var add = await _git.RunAsync(ct, "add", "--all");
            if (!add.Success) return GitOutput.ToResult(add, null, "No se pudieron añadir los cambios");

            // Código de salida 0 = no hay nada en el stage.
            var staged = await _git.RunAsync(ct, "diff", "--cached", "--quiet");
            if (staged.Success) return OperationResult.Fail("No hay cambios para hacer commit.");

            // Cada -m es un párrafo: el primero es el título y el segundo la descripción.
            var commit = message.HasDescription
                ? await _git.RunAsync(ct, "commit", "-m", message.Title, "-m", message.Description)
                : await _git.RunAsync(ct, "commit", "-m", message.Title);

            if (commit.Success) return OperationResult.Ok($"Commit creado: \"{message.Title}\"");

            if (commit.BestMessage.Contains("user.email") || commit.BestMessage.Contains("Please tell me who you are"))
                return OperationResult.Fail(
                    "Git no conoce tu identidad. Ejecuta una vez en una terminal:\n" +
                    "git config --global user.name \"Tu Nombre\"\n" +
                    "git config --global user.email \"tu@email.com\"");

            return GitOutput.ToResult(commit, null, "No se pudo crear el commit");
        }
    }
}

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;

namespace UnityGitTool.Git
{
    /// <summary>Crea un repositorio local con rama por defecto y .gitignore de Unity.</summary>
    public sealed class GitRepositoryInitializer : IGitRepositoryInitializer
    {
        private readonly IGitCommandExecutor _git;
        private readonly IGitIgnoreTemplate _gitIgnore;
        private readonly string _defaultBranch;

        public GitRepositoryInitializer(IGitCommandExecutor git, IGitIgnoreTemplate gitIgnore, string defaultBranch = "main")
        {
            _git = git;
            _gitIgnore = gitIgnore;
            _defaultBranch = defaultBranch;
        }

        public async Task<OperationResult> InitializeAsync(CancellationToken ct)
        {
            var init = await _git.RunAsync(ct, "init");
            if (!init.Success) return GitOutput.ToResult(init, null, "No se pudo inicializar el repositorio");

            // Equivale a "git init -b main" pero funciona también con versiones antiguas de git.
            await _git.RunAsync(ct, "symbolic-ref", "HEAD", "refs/heads/" + _defaultBranch);

            var gitIgnorePath = Path.Combine(_git.WorkingDirectory, ".gitignore");
            if (!File.Exists(gitIgnorePath))
                File.WriteAllText(gitIgnorePath, _gitIgnore.Content);

            return OperationResult.Ok($"Repositorio inicializado en '{_git.WorkingDirectory}' (rama '{_defaultBranch}').");
        }
    }
}

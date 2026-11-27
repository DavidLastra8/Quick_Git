using System.IO;
using System.Threading;
using System.Threading.Tasks;
using QuickGit.Core;

namespace QuickGit.Git
{
    /// <summary>Creates a local repository with a default branch and a Unity .gitignore.</summary>
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
            if (!init.Success) return GitOutput.ToResult(init, null, "Could not initialize the repository");

            // Same as "git init -b main", but also works with older git versions.
            await _git.RunAsync(ct, "symbolic-ref", "HEAD", "refs/heads/" + _defaultBranch);

            var gitIgnorePath = Path.Combine(_git.WorkingDirectory, ".gitignore");
            if (!File.Exists(gitIgnorePath))
                File.WriteAllText(gitIgnorePath, _gitIgnore.Content);

            return OperationResult.Ok($"Repository initialized in '{_git.WorkingDirectory}' (branch '{_defaultBranch}').");
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityGitTool.Core;
using UnityGitTool.Git;
using UnityGitTool.GitHub;
using UnityGitTool.Presentation;
using UnityGitTool.UseCases;
using UnityGitTool.Unity;

namespace UnityGitTool.Composition
{
    /// <summary>
    /// Composition root: el ÚNICO sitio que conoce las clases concretas. Para cambiar GitHub por
    /// GitLab, o EditorPrefs por un almacén cifrado, sólo se toca este archivo.
    /// </summary>
    public static class GitToolComposer
    {
        public const string GitExecutablePrefKey = "UnityGitTool.GitExecutable";
        private const string TokenPrefKey = "UnityGitTool.GitHub.Token";

        public static GitToolPresenter Compose(IGitToolView view)
        {
            var project = new UnityProjectInfo();
            var tokens = new EditorPrefsTokenStore(TokenPrefKey);

            // GIT_TERMINAL_PROMPT=0: si faltan credenciales git falla en lugar de quedarse
            // esperando una contraseña en una consola invisible.
            var runner = new ProcessCommandRunner(
                EditorPrefs.GetString(GitExecutablePrefKey, "git"),
                new Dictionary<string, string> { { "GIT_TERMINAL_PROMPT", "0" } });

            var git = new GitCommandExecutor(runner, project.RootPath, new GitHubTokenAuthArgumentsProvider(tokens));

            var inspector = new GitRepositoryInspector(git);
            var initializer = new GitRepositoryInitializer(git, new UnityGitIgnoreTemplate());
            var sync = new GitSyncService(git);
            var commits = new GitCommitService(git);
            var host = new GitHubRepositoryHost(tokens);
            var health = new UnityProjectHealthChecker();
            var protection = new ProtectedBranchPolicy("main", "master");

            return new GitToolPresenter(view, new GitToolDependencies
            {
                Inspector = inspector,
                Initializer = initializer,
                Branches = new GitBranchService(git),
                Sync = sync,
                CreateCommit = new CreateCommitUseCase(commits, health),
                Publish = new PublishRepositoryUseCase(inspector, initializer, commits, new GitRemoteService(git), sync, host),
                PushGuard = new PushGuard(protection, health, new GitCommitReader(git)),
                BranchProtection = protection,
                Host = host,
                Tokens = tokens,
                Assets = new UnityEditorAssetSynchronizer(),
                Health = health,
                UrlOpener = new UnityUrlOpener(),
                Project = project,
                BranchNames = new GitBranchNameSanitizer(),
                RepositoryNames = new GitHubRepositoryNameSanitizer()
            });
        }
    }
}

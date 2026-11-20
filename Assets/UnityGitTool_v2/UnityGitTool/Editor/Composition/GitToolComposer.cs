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
            var branches = new GitBranchService(git);
            var sync = new GitSyncService(git);
            var commits = new GitCommitService(git);
            var remotes = new GitRemoteService(git);
            var host = new GitHubRepositoryHost(tokens);
            var publish = new PublishRepositoryUseCase(inspector, initializer, commits, remotes, sync, host);

            return new GitToolPresenter(
                view, inspector, initializer, branches, sync, commits, publish, host, tokens,
                new UnityEditorAssetSynchronizer(), new UnityUrlOpener(), project,
                branchNames: new GitBranchNameSanitizer(),
                repositoryNames: new GitHubRepositoryNameSanitizer());
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace UnityGitTool.Git
{
    /// <summary>Detecta si el proyecto está en un repositorio y recoge su estado. Sólo lectura.</summary>
    public sealed class GitRepositoryInspector : IGitRepositoryInspector
    {
        private readonly IGitCommandExecutor _git;

        public GitRepositoryInspector(IGitCommandExecutor git) => _git = git;

        public async Task<RepositoryStatus> InspectAsync(CancellationToken ct)
        {
            var version = await _git.RunAsync(ct, "--version");
            if (!version.Success) return RepositoryStatus.GitNotInstalled(version.BestMessage);

            // Funciona aunque el proyecto de Unity esté en una subcarpeta del repositorio.
            var topLevel = await _git.RunAsync(ct, "rev-parse", "--show-toplevel");
            if (!topLevel.Success) return RepositoryStatus.NotARepository(version.Output);

            var status = new RepositoryStatus
            {
                State = RepositoryState.Ready,
                GitVersion = version.Output,
                RootPath = topLevel.Output
            };

            var head = await _git.RunAsync(ct, "rev-parse", "--verify", "--quiet", "HEAD");
            status.HasCommits = head.Success;

            var branch = await _git.RunAsync(ct, "branch", "--show-current");
            status.CurrentBranch = branch.Success ? branch.Output : string.Empty;

            if (string.IsNullOrEmpty(status.CurrentBranch) && status.HasCommits)
            {
                var sha = await _git.RunAsync(ct, "rev-parse", "--short", "HEAD");
                status.IsDetachedHead = true;
                status.CurrentBranch = sha.Output;
            }

            var remote = await _git.RunAsync(ct, "remote", "get-url", "origin");
            status.RemoteUrl = remote.Success ? remote.Output : null;

            // -z: rutas sin comillas ni escapes (soporta espacios y acentos).
            // --untracked-files=all: lista cada archivo nuevo, no sólo su carpeta.
            var changes = await _git.RunAsync(ct, "status", "--porcelain=v1", "-z", "--untracked-files=all");
            if (changes.Success) status.Changes = GitStatusParser.Parse(changes.Output);

            if (status.HasCommits && !status.IsDetachedHead)
                await ReadAheadBehindAsync(status, ct);

            return status;
        }

        private async Task ReadAheadBehindAsync(RepositoryStatus status, CancellationToken ct)
        {
            // Salida: "<ahead>\t<behind>". Falla si la rama no tiene upstream.
            var counts = await _git.RunAsync(ct, "rev-list", "--left-right", "--count", "HEAD...@{u}");
            if (!counts.Success) return;

            var parts = counts.Output.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return;

            status.HasUpstream = true;
            status.Ahead = int.TryParse(parts[0], out var ahead) ? ahead : 0;
            status.Behind = int.TryParse(parts[1], out var behind) ? behind : 0;
        }
    }
}

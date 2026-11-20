using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;

namespace UnityGitTool.Git
{
    public sealed class GitRemoteService : IGitRemoteService
    {
        private readonly IGitCommandExecutor _git;

        public GitRemoteService(IGitCommandExecutor git) => _git = git;

        public async Task<OperationResult> SetRemoteAsync(string name, string url, CancellationToken ct)
        {
            var exists = await _git.RunAsync(ct, "remote", "get-url", name);
            var result = exists.Success
                ? await _git.RunAsync(ct, "remote", "set-url", name, url)
                : await _git.RunAsync(ct, "remote", "add", name, url);

            return GitOutput.ToResult(result, $"Remoto '{name}' → {url}", $"No se pudo configurar el remoto '{name}'");
        }
    }
}

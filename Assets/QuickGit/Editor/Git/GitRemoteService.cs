using System.Threading;
using System.Threading.Tasks;
using QuickGit.Core;

namespace QuickGit.Git
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

            return GitOutput.ToResult(result, $"Remote '{name}' → {url}", $"Could not configure remote '{name}'");
        }
    }
}

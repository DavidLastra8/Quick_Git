using System.Threading;
using System.Threading.Tasks;
using QuickGit.Core;

namespace QuickGit.Git
{
    public sealed class GitCommitReader : IGitCommitReader
    {
        private readonly IGitCommandExecutor _git;

        public GitCommitReader(IGitCommandExecutor git) => _git = git;

        public async Task<OperationResult<string>> GetFullMessageAsync(string revision, CancellationToken ct)
        {
            var result = await _git.RunAsync(ct, "log", "-1", "--format=%B", revision);
            return result.Success
                ? OperationResult<string>.Ok(result.Output)
                : OperationResult<string>.Fail(result.BestMessage);
        }
    }
}

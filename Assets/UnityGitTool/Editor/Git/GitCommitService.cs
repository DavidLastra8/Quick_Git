using System.Collections.Generic;
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
            if (!add.Success) return GitOutput.ToResult(add, null, "Could not stage the changes");

            // Exit code 0 = nothing is staged.
            var staged = await _git.RunAsync(ct, "diff", "--cached", "--quiet");
            if (staged.Success) return OperationResult.Fail("There are no changes to commit.");

            // Each -m is a paragraph: title, description and, last, the trailers.
            var arguments = new List<string> { "commit", "-m", message.Title };
            if (message.HasDescription) arguments.AddRange(new[] { "-m", message.Description });
            if (message.Trailers.Count > 0) arguments.AddRange(new[] { "-m", string.Join("\n", message.Trailers) });

            var commit = await _git.RunAsync(ct, arguments.ToArray());

            if (commit.Success) return OperationResult.Ok($"Commit created: \"{message.Title}\"");

            if (commit.BestMessage.Contains("user.email") || commit.BestMessage.Contains("Please tell me who you are"))
                return OperationResult.Fail(
                    "Git does not know who you are. Run this once in a terminal:\n" +
                    "git config --global user.name \"Your Name\"\n" +
                    "git config --global user.email \"you@example.com\"");

            return GitOutput.ToResult(commit, null, "Could not create the commit");
        }
    }
}

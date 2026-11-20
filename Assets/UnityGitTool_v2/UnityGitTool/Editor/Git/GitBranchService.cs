using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;

namespace UnityGitTool.Git
{
    public sealed class GitBranchService : IGitBranchService
    {
        private const string BranchFormat = "--format=%(refname:short)";
        private readonly IGitCommandExecutor _git;

        public GitBranchService(IGitCommandExecutor git) => _git = git;

        public async Task<OperationResult<IReadOnlyList<GitBranch>>> GetBranchesAsync(CancellationToken ct)
        {
            var current = await _git.RunAsync(ct, "branch", "--show-current");
            var local = await _git.RunAsync(ct, "branch", BranchFormat);
            if (!local.Success)
                return OperationResult<IReadOnlyList<GitBranch>>.Fail(local.BestMessage);

            var branches = GitOutput.Lines(local.Output)
                .Where(name => !name.StartsWith("(")) // "(HEAD detached at ...)"
                .Select(name => new GitBranch(name, false, name == current.Output))
                .ToList();

            var localNames = new HashSet<string>(branches.Select(b => b.Name));

            var remote = await _git.RunAsync(ct, "branch", "-r", BranchFormat);
            if (remote.Success)
            {
                foreach (var name in GitOutput.Lines(remote.Output))
                {
                    var slash = name.IndexOf('/');
                    if (slash <= 0 || name.EndsWith("/HEAD")) continue;

                    var branch = new GitBranch(name, true, false);
                    if (!localNames.Contains(branch.LocalName)) branches.Add(branch);
                }
            }

            return OperationResult<IReadOnlyList<GitBranch>>.Ok(branches);
        }

        public async Task<OperationResult> CheckoutAsync(GitBranch branch, CancellationToken ct)
        {
            var result = branch.IsRemote
                ? await _git.RunAsync(ct, "checkout", "--track", branch.Name) // Crea la rama local que sigue a la remota.
                : await _git.RunAsync(ct, "checkout", branch.Name);

            return GitOutput.ToResult(result, $"Ahora estás en la rama '{branch.LocalName}'.", $"No se pudo cambiar a '{branch.LocalName}'");
        }

        public async Task<OperationResult> CreateBranchAsync(string name, CancellationToken ct)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || name.StartsWith("-"))
                return OperationResult.Fail("Introduce un nombre de rama válido.");

            var validation = await _git.RunAsync(ct, "check-ref-format", "--branch", name);
            if (!validation.Success)
                return OperationResult.Fail($"'{name}' no es un nombre de rama válido para git.");

            var result = await _git.RunAsync(ct, "checkout", "-b", name);
            return GitOutput.ToResult(result, $"Rama '{name}' creada y activa.", $"No se pudo crear la rama '{name}'");
        }
    }
}

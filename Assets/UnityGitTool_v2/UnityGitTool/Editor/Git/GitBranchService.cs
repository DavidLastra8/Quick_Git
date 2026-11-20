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

        // %09 = tabulador (git lo expande); así no hay caracteres especiales en el argumento.
        private const string LocalBranchFormat = "--format=%(refname:short)%09%(upstream:short)";
        private readonly IGitCommandExecutor _git;

        public GitBranchService(IGitCommandExecutor git) => _git = git;

        public async Task<OperationResult<IReadOnlyList<GitBranch>>> GetBranchesAsync(CancellationToken ct)
        {
            var current = await _git.RunAsync(ct, "branch", "--show-current");
            var local = await _git.RunAsync(ct, "branch", LocalBranchFormat);
            if (!local.Success)
                return OperationResult<IReadOnlyList<GitBranch>>.Fail(local.BestMessage);

            var remote = await _git.RunAsync(ct, "branch", "-r", BranchFormat);
            var remoteNames = remote.Success
                ? GitOutput.Lines(remote.Output).Where(n => n.IndexOf('/') > 0 && !n.EndsWith("/HEAD")).ToList()
                : new List<string>();
            var remoteSet = new HashSet<string>(remoteNames);

            var branches = new List<GitBranch>();
            foreach (var line in GitOutput.Lines(local.Output))
            {
                if (line.StartsWith("(")) continue; // "(HEAD detached at ...)"

                var parts = line.Split('\t');
                var name = parts[0];
                var upstream = parts.Length > 1 ? parts[1] : string.Empty;

                // La rama remota asociada es su upstream, o "origin/<nombre>" si existe aunque no se siga.
                string remoteBranch = null;
                if (remoteSet.Contains(upstream)) remoteBranch = upstream;
                else if (remoteSet.Contains("origin/" + name)) remoteBranch = "origin/" + name;

                branches.Add(new GitBranch(name, false, name == current.Output, remoteBranch));
            }

            var localNames = new HashSet<string>(branches.Select(b => b.Name));
            var trackedRemotes = new HashSet<string>(branches.Where(b => b.ExistsOnRemote).Select(b => b.RemoteBranchName));

            foreach (var name in remoteNames)
            {
                var branch = new GitBranch(name, true, false);
                if (!localNames.Contains(branch.LocalName) && !trackedRemotes.Contains(name)) branches.Add(branch);
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

        public async Task<bool> IsMergedIntoAsync(GitBranch branch, string target, CancellationToken ct)
        {
            // "merge-base --is-ancestor A B" devuelve 0 si todos los commits de A están en B.
            var result = await _git.RunAsync(ct, "merge-base", "--is-ancestor", branch.Name, target);
            return result.Success;
        }

        public async Task<OperationResult> DeleteLocalBranchAsync(GitBranch branch, CancellationToken ct)
        {
            if (branch.IsRemote || branch.IsCurrent)
                return OperationResult.Fail("Sólo se pueden borrar ramas locales que no sean la actual.");

            // -D: la comprobación de "sin fusionar" ya la hace IsMergedAsync y el usuario ha confirmado.
            var result = await _git.RunAsync(ct, "branch", "-D", branch.Name);
            return GitOutput.ToResult(result, $"Rama local '{branch.Name}' borrada.", $"No se pudo borrar la rama '{branch.Name}'");
        }

        public async Task<OperationResult> DeleteRemoteBranchAsync(GitBranch branch, CancellationToken ct)
        {
            if (!branch.ExistsOnRemote) return OperationResult.Fail($"'{branch.Name}' no existe en el remoto.");

            var slash = branch.RemoteBranchName.IndexOf('/');
            var remote = branch.RemoteBranchName.Substring(0, slash);
            var remoteBranch = branch.RemoteBranchName.Substring(slash + 1);

            var result = await _git.RunWithAuthAsync(ct, "push", remote, "--delete", remoteBranch);
            return GitOutput.ToResult(result, $"Rama '{remoteBranch}' borrada de {remote}.", $"No se pudo borrar '{remoteBranch}' del remoto");
        }
    }
}

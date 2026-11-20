using System;
using System.Collections.Generic;

namespace UnityGitTool.Git
{
    /// <summary>Ramas protegidas por nombre (por defecto main y master).</summary>
    public sealed class ProtectedBranchPolicy : IBranchProtectionPolicy
    {
        private readonly HashSet<string> _protected;

        public ProtectedBranchPolicy(params string[] protectedBranches)
        {
            _protected = new HashSet<string>(
                protectedBranches.Length > 0 ? protectedBranches : new[] { "main", "master" },
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Recibe el nombre local de la rama (para remotas, GitBranch.LocalName).</summary>
        public bool IsProtected(string branchName) =>
            !string.IsNullOrEmpty(branchName) && _protected.Contains(branchName);
    }
}

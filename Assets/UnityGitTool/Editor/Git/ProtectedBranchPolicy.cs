using System;
using System.Collections.Generic;

namespace UnityGitTool.Git
{
    /// <summary>Branches protected by name (main and master by default).</summary>
    public sealed class ProtectedBranchPolicy : IBranchProtectionPolicy
    {
        private readonly HashSet<string> _protected;

        public ProtectedBranchPolicy(params string[] protectedBranches)
        {
            _protected = new HashSet<string>(
                protectedBranches.Length > 0 ? protectedBranches : new[] { "main", "master" },
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Takes the local branch name (for remote branches, GitBranch.LocalName).</summary>
        public bool IsProtected(string branchName) =>
            !string.IsNullOrEmpty(branchName) && _protected.Contains(branchName);
    }
}

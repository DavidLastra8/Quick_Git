using System;
using System.Collections.Generic;
using UnityGitTool.Git;

namespace UnityGitTool.Presentation
{
    /// <summary>Everything the view needs to draw itself, prepared by the presenter.</summary>
    public sealed class GitToolViewState
    {
        public static readonly GitToolViewState Empty = new GitToolViewState();

        public RepositoryStatus Status { get; set; } = RepositoryStatus.Unknown;
        public IReadOnlyList<GitBranch> Branches { get; set; } = Array.Empty<GitBranch>();
        public IReadOnlyList<GitBranch> DeletableBranches { get; set; } = Array.Empty<GitBranch>();
        public bool IsCurrentBranchProtected { get; set; }

        /// <summary>Reason why pushing is not possible right now, or null if it is.</summary>
        public string PushBlockedReason { get; set; }
        public bool HasCompileErrors { get; set; }

        /// <summary>Preview of the repository and branch to create (null = show nothing).</summary>
        public NamePreview RepositoryPreview { get; set; }
        public NamePreview BranchPreview { get; set; }
        public bool HasToken { get; set; }
        public string AccountName { get; set; }
        public string HostName { get; set; } = "GitHub";
        public string TokenCreationUrl { get; set; }
        public string DefaultRepositoryName { get; set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccountName);
    }
}

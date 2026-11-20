using System;
using System.Collections.Generic;
using UnityGitTool.Git;

namespace UnityGitTool.Presentation
{
    /// <summary>Todo lo que la vista necesita para pintarse, preparado por el presentador.</summary>
    public sealed class GitToolViewState
    {
        public static readonly GitToolViewState Empty = new GitToolViewState();

        public RepositoryStatus Status { get; set; } = RepositoryStatus.Unknown;
        public IReadOnlyList<GitBranch> Branches { get; set; } = Array.Empty<GitBranch>();
        public bool HasToken { get; set; }
        public string AccountName { get; set; }
        public string HostName { get; set; } = "GitHub";
        public string TokenCreationUrl { get; set; }
        public string DefaultRepositoryName { get; set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccountName);
    }
}

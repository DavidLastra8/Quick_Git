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
        public IReadOnlyList<GitBranch> DeletableBranches { get; set; } = Array.Empty<GitBranch>();
        public bool IsCurrentBranchProtected { get; set; }

        /// <summary>Motivo por el que no se puede hacer push ahora mismo, o null si sí se puede.</summary>
        public string PushBlockedReason { get; set; }
        public bool HasCompileErrors { get; set; }

        /// <summary>Vista previa del repositorio a crear y de la rama a crear (null = no mostrar nada).</summary>
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

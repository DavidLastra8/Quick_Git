using System;
using System.Collections.Generic;

namespace UnityGitTool.Git
{
    public enum RepositoryState
    {
        Unknown,
        GitNotInstalled,
        NotARepository,
        Ready
    }

    public enum PullStrategy
    {
        FastForwardOnly,
        Merge,
        Rebase
    }

    public enum FileChangeKind
    {
        Modified,
        Added,
        Deleted,
        Renamed,
        Untracked,
        Conflicted
    }

    /// <summary>Un archivo con cambios respecto al último commit.</summary>
    public sealed class GitFileChange
    {
        public string Path { get; }
        public string OldPath { get; }
        public FileChangeKind Kind { get; }
        public bool IsMetaFile => Path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase);

        public GitFileChange(string path, FileChangeKind kind, string oldPath = null)
        {
            Path = path;
            Kind = kind;
            OldPath = oldPath;
        }
    }

    /// <summary>Instantánea del estado del repositorio (parte del Modelo en MVP).</summary>
    public sealed class RepositoryStatus
    {
        public static readonly RepositoryStatus Unknown = new RepositoryStatus { State = RepositoryState.Unknown };

        public RepositoryState State { get; set; }
        public string GitVersion { get; set; }
        public string RootPath { get; set; }
        public string CurrentBranch { get; set; }
        public bool IsDetachedHead { get; set; }
        public bool HasCommits { get; set; }
        public string RemoteUrl { get; set; }
        public IReadOnlyList<GitFileChange> Changes { get; set; } = Array.Empty<GitFileChange>();
        public bool HasUpstream { get; set; }
        public int Ahead { get; set; }
        public int Behind { get; set; }
        public string ErrorMessage { get; set; }

        public bool IsRepository => State == RepositoryState.Ready;
        public int ChangedFiles => Changes.Count;
        public bool HasRemote => !string.IsNullOrEmpty(RemoteUrl);
        public string CurrentBranchLabel => IsDetachedHead ? $"(HEAD separado en {CurrentBranch})" : CurrentBranch;

        public static RepositoryStatus GitNotInstalled(string error) =>
            new RepositoryStatus { State = RepositoryState.GitNotInstalled, ErrorMessage = error };

        public static RepositoryStatus NotARepository(string gitVersion) =>
            new RepositoryStatus { State = RepositoryState.NotARepository, GitVersion = gitVersion };
    }

    public sealed class GitBranch
    {
        public string Name { get; }
        public bool IsRemote { get; }
        public bool IsCurrent { get; }

        /// <summary>Rama equivalente en el remoto ("origin/feature"), o null si no existe allí.</summary>
        public string RemoteBranchName { get; }
        public bool ExistsOnRemote => RemoteBranchName != null;

        /// <summary>Nombre que tendrá la rama en local ("origin/feature/x" → "feature/x").</summary>
        public string LocalName => IsRemote ? Name.Substring(Name.IndexOf('/') + 1) : Name;

        public GitBranch(string name, bool isRemote, bool isCurrent, string remoteBranchName = null)
        {
            Name = name;
            IsRemote = isRemote;
            IsCurrent = isCurrent;
            RemoteBranchName = isRemote ? name : remoteBranchName;
        }
    }
}

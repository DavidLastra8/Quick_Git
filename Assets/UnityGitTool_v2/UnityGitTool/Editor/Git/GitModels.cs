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
        public int ChangedFiles { get; set; }
        public bool HasUpstream { get; set; }
        public int Ahead { get; set; }
        public int Behind { get; set; }
        public string ErrorMessage { get; set; }

        public bool IsRepository => State == RepositoryState.Ready;
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

        /// <summary>Nombre que tendrá la rama en local ("origin/feature/x" → "feature/x").</summary>
        public string LocalName => IsRemote ? Name.Substring(Name.IndexOf('/') + 1) : Name;

        public GitBranch(string name, bool isRemote, bool isCurrent)
        {
            Name = name;
            IsRemote = isRemote;
            IsCurrent = isCurrent;
        }
    }
}

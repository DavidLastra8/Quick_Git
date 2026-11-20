using System;

namespace UnityGitTool.GitHub
{
    // DTOs for JsonUtility: public fields named exactly like the GitHub API.
#pragma warning disable 0649, IDE1006
    [Serializable]
    internal sealed class GitHubCreateRepositoryBody
    {
        public string name;
        public string description;
        public bool @private;
        public bool auto_init;
    }

    [Serializable]
    internal sealed class GitHubRepositoryDto
    {
        public string full_name;
        public string clone_url;
        public string html_url;
    }

    [Serializable]
    internal sealed class GitHubUserDto
    {
        public string login;
    }

    [Serializable]
    internal sealed class GitHubErrorDto
    {
        public string message;
        public GitHubErrorItemDto[] errors;
    }

    [Serializable]
    internal sealed class GitHubErrorItemDto
    {
        public string message;
        public string field;
        public string code;
    }
#pragma warning restore 0649, IDE1006
}

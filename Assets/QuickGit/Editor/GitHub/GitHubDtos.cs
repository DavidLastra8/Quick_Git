using System;

namespace QuickGit.GitHub
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
    [Serializable]
    internal sealed class GitHubOwnerDto
    {
        public string login;
        public string type;
    }

    [Serializable]
    internal sealed class GitHubPermissionsDto
    {
        public bool admin;
        public bool push;
        public bool pull;
    }

    [Serializable]
    internal sealed class GitHubRepositoryDetailsDto
    {
        public string full_name;
        public string html_url;
        public bool @private;
        public GitHubOwnerDto owner;
        public GitHubPermissionsDto permissions;
    }

    [Serializable]
    internal sealed class GitHubVisibilityBody
    {
        public bool @private;
    }

    [Serializable]
    internal sealed class GitHubCollaboratorPermissionBody
    {
        public string permission;
    }

    [Serializable]
    internal sealed class GitHubCollaboratorDto
    {
        public string login;
        public string role_name;
    }

    [Serializable]
    internal sealed class GitHubCollaboratorListDto
    {
        public GitHubCollaboratorDto[] items;
    }

    [Serializable]
    internal sealed class GitHubInvitationDto
    {
        public long id;
        public GitHubUserDto invitee;
        public string permissions;
    }

    [Serializable]
    internal sealed class GitHubInvitationListDto
    {
        public GitHubInvitationDto[] items;
    }
#pragma warning restore 0649, IDE1006
}

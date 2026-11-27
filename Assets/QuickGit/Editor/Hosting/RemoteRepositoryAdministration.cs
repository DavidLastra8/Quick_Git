using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuickGit.Core;

namespace QuickGit.Hosting
{
    /// <summary>Identifies a repository on the remote service ("owner/name").</summary>
    public sealed class RemoteRepositoryRef
    {
        public string Owner { get; }
        public string Name { get; }
        public string FullName => $"{Owner}/{Name}";

        public RemoteRepositoryRef(string owner, string name)
        {
            Owner = owner;
            Name = name;
        }

        public bool SameAs(RemoteRepositoryRef other) =>
            other != null && string.Equals(FullName, other.FullName, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Access level for a collaborator. Only organization repositories honour it.</summary>
    public enum CollaboratorPermission
    {
        Read,
        Triage,
        Write,
        Maintain,
        Admin
    }

    public sealed class RemoteRepositoryInfo
    {
        public RemoteRepositoryRef Repository { get; }
        public string WebUrl { get; }
        public bool IsPrivate { get; }

        /// <summary>True if the authenticated user can change settings and manage collaborators.</summary>
        public bool CanAdminister { get; }

        /// <summary>Organization repositories support per-collaborator permission levels.</summary>
        public bool IsOrganization { get; }

        public RemoteRepositoryInfo(RemoteRepositoryRef repository, string webUrl, bool isPrivate, bool canAdminister, bool isOrganization)
        {
            Repository = repository;
            WebUrl = webUrl;
            IsPrivate = isPrivate;
            CanAdminister = canAdminister;
            IsOrganization = isOrganization;
        }
    }

    public sealed class RemoteCollaborator
    {
        public string Login { get; }
        public string Role { get; }
        public bool IsOwner { get; }

        public RemoteCollaborator(string login, string role, bool isOwner)
        {
            Login = login;
            Role = role;
            IsOwner = isOwner;
        }
    }

    public sealed class RemoteInvitation
    {
        public long Id { get; }
        public string Login { get; }
        public string Permission { get; }

        public RemoteInvitation(long id, string login, string permission)
        {
            Id = id;
            Login = login;
            Permission = permission;
        }
    }

    public enum InvitationOutcome
    {
        /// <summary>An invitation was created; the user must accept it.</summary>
        InvitationSent,

        /// <summary>The user already had access (their permission may have been updated).</summary>
        AlreadyCollaborator
    }

    // Small, focused interfaces (ISP): locating a repo, its settings and its collaborators
    // are separate concerns, each with its own implementation.

    /// <summary>Recognizes remote URLs that belong to this service.</summary>
    public interface IRemoteRepositoryLocator
    {
        bool TryParse(string remoteUrl, out RemoteRepositoryRef repository);
    }

    public interface IRemoteRepositorySettings
    {
        Task<OperationResult<RemoteRepositoryInfo>> GetInfoAsync(RemoteRepositoryRef repository, CancellationToken cancellationToken);
        Task<OperationResult> SetVisibilityAsync(RemoteRepositoryRef repository, bool makePrivate, CancellationToken cancellationToken);
    }

    public interface IRemoteCollaboratorService
    {
        Task<OperationResult<IReadOnlyList<RemoteCollaborator>>> GetCollaboratorsAsync(RemoteRepositoryInfo repository, CancellationToken cancellationToken);
        Task<OperationResult<IReadOnlyList<RemoteInvitation>>> GetInvitationsAsync(RemoteRepositoryRef repository, CancellationToken cancellationToken);
        Task<OperationResult<InvitationOutcome>> InviteAsync(RemoteRepositoryInfo repository, string login, CollaboratorPermission permission, CancellationToken cancellationToken);
        Task<OperationResult> RemoveCollaboratorAsync(RemoteRepositoryRef repository, string login, CancellationToken cancellationToken);
        Task<OperationResult> CancelInvitationAsync(RemoteRepositoryRef repository, RemoteInvitation invitation, CancellationToken cancellationToken);
    }
}

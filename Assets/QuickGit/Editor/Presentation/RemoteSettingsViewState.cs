using System;
using System.Collections.Generic;
using QuickGit.Hosting;

namespace QuickGit.Presentation
{
    public enum RemoteSettingsStatus
    {
        /// <summary>Not a repository: the section is not shown at all.</summary>
        Hidden,

        /// <summary>Cannot be used yet (no remote, not GitHub, no token). Message says why.</summary>
        Unavailable,

        /// <summary>Available but not loaded yet (Message may hold the last error).</summary>
        NotLoaded,

        Loaded
    }

    /// <summary>What the "GitHub repository" part of Settings shows.</summary>
    public sealed class RemoteSettingsViewState
    {
        public static readonly RemoteSettingsViewState Hidden = new RemoteSettingsViewState(RemoteSettingsStatus.Hidden, null);

        public RemoteSettingsStatus Status { get; }
        public string Message { get; }
        public RemoteRepositoryInfo Info { get; }
        public IReadOnlyList<RemoteCollaborator> Collaborators { get; }
        public IReadOnlyList<RemoteInvitation> Invitations { get; }

        public RemoteSettingsViewState(
            RemoteSettingsStatus status,
            string message,
            RemoteRepositoryInfo info = null,
            IReadOnlyList<RemoteCollaborator> collaborators = null,
            IReadOnlyList<RemoteInvitation> invitations = null)
        {
            Status = status;
            Message = message;
            Info = info;
            Collaborators = collaborators ?? Array.Empty<RemoteCollaborator>();
            Invitations = invitations ?? Array.Empty<RemoteInvitation>();
        }
    }
}

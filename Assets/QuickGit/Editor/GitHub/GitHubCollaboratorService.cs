using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using QuickGit.Core;
using QuickGit.Hosting;

namespace QuickGit.GitHub
{
    /// <summary>Lists, invites and removes collaborators of a GitHub repository.</summary>
    public sealed class GitHubCollaboratorService : IRemoteCollaboratorService
    {
        // GitHub usernames: letters, numbers and single hyphens, not at the start or end, max 39 chars.
        private static readonly Regex ValidLogin = new Regex(@"^[A-Za-z0-9](?:[A-Za-z0-9]|-(?=[A-Za-z0-9])){0,38}$");

        private readonly GitHubApiClient _api;

        public GitHubCollaboratorService(GitHubApiClient api) => _api = api;

        public static bool IsValidLogin(string login) => login != null && ValidLogin.IsMatch(login);

        public async Task<OperationResult<IReadOnlyList<RemoteCollaborator>>> GetCollaboratorsAsync(RemoteRepositoryInfo repository, CancellationToken ct)
        {
            var path = GitHubRepositorySettings.RepoPath(repository.Repository) + "/collaborators?affiliation=direct&per_page=100";
            var response = await _api.SendAsync(HttpMethod.Get, path, null, ct);
            if (!response.IsSuccess)
                return OperationResult<IReadOnlyList<RemoteCollaborator>>.Fail(GitHubApiClient.DescribeError(response));

            var dtos = JsonUtility.FromJson<GitHubCollaboratorListDto>(GitHubApiClient.WrapArray(response.Body)).items
                       ?? Array.Empty<GitHubCollaboratorDto>();

            var owner = repository.Repository.Owner;
            var collaborators = dtos
                .Select(c => new RemoteCollaborator(c.login, c.role_name, string.Equals(c.login, owner, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(c => c.IsOwner)
                .ThenBy(c => c.Login, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return OperationResult<IReadOnlyList<RemoteCollaborator>>.Ok(collaborators);
        }

        public async Task<OperationResult<IReadOnlyList<RemoteInvitation>>> GetInvitationsAsync(RemoteRepositoryRef repository, CancellationToken ct)
        {
            var path = GitHubRepositorySettings.RepoPath(repository) + "/invitations?per_page=100";
            var response = await _api.SendAsync(HttpMethod.Get, path, null, ct);
            if (!response.IsSuccess)
                return OperationResult<IReadOnlyList<RemoteInvitation>>.Fail(GitHubApiClient.DescribeError(response));

            var dtos = JsonUtility.FromJson<GitHubInvitationListDto>(GitHubApiClient.WrapArray(response.Body)).items
                       ?? Array.Empty<GitHubInvitationDto>();

            var invitations = dtos
                .Select(i => new RemoteInvitation(i.id, i.invitee?.login ?? "(unknown)", i.permissions))
                .ToList();

            return OperationResult<IReadOnlyList<RemoteInvitation>>.Ok(invitations);
        }

        public async Task<OperationResult<InvitationOutcome>> InviteAsync(RemoteRepositoryInfo repository, string login, CollaboratorPermission permission, CancellationToken ct)
        {
            login = NormalizeLogin(login);
            if (!IsValidLogin(login))
                return OperationResult<InvitationOutcome>.Fail($"'{login}' is not a valid GitHub username.");

            if (string.Equals(login, repository.Repository.Owner, StringComparison.OrdinalIgnoreCase))
                return OperationResult<InvitationOutcome>.Fail("The owner already has full access to the repository.");

            // The permission level is only honoured by organization repositories; personal
            // repositories always give collaborators write access, so an empty body is sent.
            var body = repository.IsOrganization
                ? JsonUtility.ToJson(new GitHubCollaboratorPermissionBody { permission = ToApiPermission(permission) })
                : "{}";

            var path = GitHubRepositorySettings.RepoPath(repository.Repository) + "/collaborators/" + Uri.EscapeDataString(login);
            var response = await _api.SendAsync(HttpMethod.Put, path, body, ct);

            switch (response.StatusCode)
            {
                case 201:
                    return OperationResult<InvitationOutcome>.Ok(InvitationOutcome.InvitationSent,
                        $"Invitation sent to @{login}. They must accept it (GitHub notifications or email) before they can push.");
                case 204:
                    return OperationResult<InvitationOutcome>.Ok(InvitationOutcome.AlreadyCollaborator,
                        $"@{login} already has access to '{repository.Repository.FullName}'.");
                default:
                    return OperationResult<InvitationOutcome>.Fail(GitHubApiClient.DescribeError(response, $"GitHub user '@{login}' does not exist."));
            }
        }

        public async Task<OperationResult> RemoveCollaboratorAsync(RemoteRepositoryRef repository, string login, CancellationToken ct)
        {
            var path = GitHubRepositorySettings.RepoPath(repository) + "/collaborators/" + Uri.EscapeDataString(login);
            var response = await _api.SendAsync(HttpMethod.Delete, path, null, ct);

            return response.IsSuccess
                ? OperationResult.Ok($"@{login} no longer has access to '{repository.FullName}'.")
                : OperationResult.Fail(GitHubApiClient.DescribeError(response));
        }

        public async Task<OperationResult> CancelInvitationAsync(RemoteRepositoryRef repository, RemoteInvitation invitation, CancellationToken ct)
        {
            var path = GitHubRepositorySettings.RepoPath(repository) + "/invitations/" + invitation.Id;
            var response = await _api.SendAsync(HttpMethod.Delete, path, null, ct);

            return response.IsSuccess
                ? OperationResult.Ok($"Invitation for @{invitation.Login} cancelled.")
                : OperationResult.Fail(GitHubApiClient.DescribeError(response, "That invitation no longer exists (it may have been accepted)."));
        }

        /// <summary>Accepts "@user" or " user " as well.</summary>
        public static string NormalizeLogin(string login) => (login ?? string.Empty).Trim().TrimStart('@');

        private static string ToApiPermission(CollaboratorPermission permission)
        {
            switch (permission)
            {
                case CollaboratorPermission.Read: return "pull";
                case CollaboratorPermission.Triage: return "triage";
                case CollaboratorPermission.Maintain: return "maintain";
                case CollaboratorPermission.Admin: return "admin";
                default: return "push";
            }
        }
    }
}

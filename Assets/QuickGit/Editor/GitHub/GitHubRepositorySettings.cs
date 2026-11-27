using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using QuickGit.Core;
using QuickGit.Hosting;

namespace QuickGit.GitHub
{
    /// <summary>Reads repository details and changes its visibility.</summary>
    public sealed class GitHubRepositorySettings : IRemoteRepositorySettings
    {
        private static readonly HttpMethod Patch = new HttpMethod("PATCH");
        private readonly GitHubApiClient _api;

        public GitHubRepositorySettings(GitHubApiClient api) => _api = api;

        public async Task<OperationResult<RemoteRepositoryInfo>> GetInfoAsync(RemoteRepositoryRef repository, CancellationToken ct)
        {
            if (!_api.HasToken) return OperationResult<RemoteRepositoryInfo>.Fail("No GitHub token is configured.");

            var response = await _api.SendAsync(HttpMethod.Get, RepoPath(repository), null, ct);
            if (!response.IsSuccess)
                return OperationResult<RemoteRepositoryInfo>.Fail(GitHubApiClient.DescribeError(response,
                    $"Repository '{repository.FullName}' was not found, or your token cannot access it."));

            var dto = JsonUtility.FromJson<GitHubRepositoryDetailsDto>(response.Body);
            var info = new RemoteRepositoryInfo(
                repository,
                dto.html_url,
                dto.@private,
                canAdminister: dto.permissions != null && dto.permissions.admin,
                isOrganization: dto.owner != null && dto.owner.type == "Organization");

            return OperationResult<RemoteRepositoryInfo>.Ok(info);
        }

        public async Task<OperationResult> SetVisibilityAsync(RemoteRepositoryRef repository, bool makePrivate, CancellationToken ct)
        {
            if (!_api.HasToken) return OperationResult.Fail("No GitHub token is configured.");

            var body = JsonUtility.ToJson(new GitHubVisibilityBody { @private = makePrivate });
            var response = await _api.SendAsync(Patch, RepoPath(repository), body, ct);
            if (!response.IsSuccess) return OperationResult.Fail(GitHubApiClient.DescribeError(response));

            return OperationResult.Ok($"'{repository.FullName}' is now {(makePrivate ? "private" : "public")}.");
        }

        internal static string RepoPath(RemoteRepositoryRef repository) =>
            $"/repos/{System.Uri.EscapeDataString(repository.Owner)}/{System.Uri.EscapeDataString(repository.Name)}";
    }
}

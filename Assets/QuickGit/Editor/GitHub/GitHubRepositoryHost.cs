using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using QuickGit.Core;
using QuickGit.Hosting;

namespace QuickGit.GitHub
{
    /// <summary>IRemoteRepositoryHost implementation on top of the GitHub REST API.</summary>
    public sealed class GitHubRepositoryHost : IRemoteRepositoryHost
    {
        private readonly GitHubApiClient _api;

        public string DisplayName => "GitHub";
        public string TokenCreationUrl => "https://github.com/settings/tokens/new?scopes=repo&description=Quick%20Git";

        public GitHubRepositoryHost(GitHubApiClient api) => _api = api;

        public string BuildRepositoryUrl(string owner, string repositoryName) =>
            $"https://github.com/{owner}/{repositoryName}";

        public string BuildBranchUrl(string repositoryWebUrl, string branchName) =>
            // Each branch segment is escaped, but "/" is kept (that is how GitHub shows it).
            $"{repositoryWebUrl?.TrimEnd('/')}/tree/{string.Join("/", branchName.Split('/').Select(Uri.EscapeDataString))}";

        public async Task<OperationResult<RemoteAccount>> AuthenticateAsync(CancellationToken ct)
        {
            if (!_api.HasToken) return OperationResult<RemoteAccount>.Fail("No GitHub token is configured.");

            var response = await _api.SendAsync(HttpMethod.Get, "/user", null, ct);
            if (!response.IsSuccess)
                return OperationResult<RemoteAccount>.Fail(GitHubApiClient.DescribeError(response));

            var user = JsonUtility.FromJson<GitHubUserDto>(response.Body);
            return OperationResult<RemoteAccount>.Ok(new RemoteAccount(user.login), $"Connected to GitHub as {user.login}.");
        }

        public async Task<OperationResult<RemoteRepository>> CreateRepositoryAsync(CreateRepositoryRequest repository, CancellationToken ct)
        {
            var validation = repository.Validate();
            if (!validation.Success) return OperationResult<RemoteRepository>.Fail(validation.Message);
            if (!_api.HasToken) return OperationResult<RemoteRepository>.Fail("No GitHub token is configured.");

            var payload = JsonUtility.ToJson(new GitHubCreateRepositoryBody
            {
                name = repository.Name,
                description = repository.Description,
                @private = repository.IsPrivate,
                auto_init = false // The content is uploaded by the initial push from the project.
            });

            var response = await _api.SendAsync(HttpMethod.Post, "/user/repos", payload, ct);
            if (!response.IsSuccess)
                return OperationResult<RemoteRepository>.Fail(GitHubApiClient.DescribeError(response));

            var dto = JsonUtility.FromJson<GitHubRepositoryDto>(response.Body);
            return OperationResult<RemoteRepository>.Ok(
                new RemoteRepository(dto.full_name, dto.clone_url, dto.html_url),
                $"Repository '{dto.full_name}' created on GitHub.");
        }
    }
}

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
        private const string ApiBase = "https://api.github.com";
        private static readonly HttpClient Http = CreateHttpClient();

        private readonly IAccessTokenStore _tokens;

        public string DisplayName => "GitHub";
        public string TokenCreationUrl => "https://github.com/settings/tokens/new?scopes=repo&description=Quick%20Git";

        public GitHubRepositoryHost(IAccessTokenStore tokens) => _tokens = tokens;

        public string BuildRepositoryUrl(string owner, string repositoryName) =>
            $"https://github.com/{owner}/{repositoryName}";

        public string BuildBranchUrl(string repositoryWebUrl, string branchName) =>
            // Each branch segment is escaped, but "/" is kept (that is how GitHub shows it).
            $"{repositoryWebUrl?.TrimEnd('/')}/tree/{string.Join("/", branchName.Split('/').Select(Uri.EscapeDataString))}";

        public async Task<OperationResult<RemoteAccount>> AuthenticateAsync(CancellationToken ct)
        {
            if (!_tokens.HasToken) return OperationResult<RemoteAccount>.Fail("No GitHub token is configured.");

            using (var request = CreateRequest(HttpMethod.Get, "/user"))
            using (var response = await Http.SendAsync(request, ct))
            {
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return OperationResult<RemoteAccount>.Fail(DescribeError(response.StatusCode, body));

                var user = JsonUtility.FromJson<GitHubUserDto>(body);
                return OperationResult<RemoteAccount>.Ok(new RemoteAccount(user.login), $"Connected to GitHub as {user.login}.");
            }
        }

        public async Task<OperationResult<RemoteRepository>> CreateRepositoryAsync(CreateRepositoryRequest repository, CancellationToken ct)
        {
            var validation = repository.Validate();
            if (!validation.Success) return OperationResult<RemoteRepository>.Fail(validation.Message);
            if (!_tokens.HasToken) return OperationResult<RemoteRepository>.Fail("No GitHub token is configured.");

            var payload = JsonUtility.ToJson(new GitHubCreateRepositoryBody
            {
                name = repository.Name,
                description = repository.Description,
                @private = repository.IsPrivate,
                auto_init = false // The content is uploaded by the initial push from the project.
            });

            using (var request = CreateRequest(HttpMethod.Post, "/user/repos"))
            {
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                using (var response = await Http.SendAsync(request, ct))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        return OperationResult<RemoteRepository>.Fail(DescribeError(response.StatusCode, body));

                    var dto = JsonUtility.FromJson<GitHubRepositoryDto>(body);
                    return OperationResult<RemoteRepository>.Ok(
                        new RemoteRepository(dto.full_name, dto.clone_url, dto.html_url),
                        $"Repository '{dto.full_name}' created on GitHub.");
                }
            }
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, ApiBase + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.Load());
            return request;
        }

        private static string DescribeError(HttpStatusCode status, string body)
        {
            switch ((int)status)
            {
                case 401: return "GitHub rejected the token (invalid or expired).";
                case 403: return "The token does not have enough permissions (it needs the 'repo' scope).";
            }

            try
            {
                var error = JsonUtility.FromJson<GitHubErrorDto>(body);
                var details = error.errors?.Select(e => e.message).Where(m => !string.IsNullOrEmpty(m)).ToArray();
                if (details != null && details.Length > 0) return $"GitHub: {string.Join("; ", details)}";
                if (!string.IsNullOrEmpty(error.message)) return $"GitHub: {error.message}";
            }
            catch (ArgumentException) { /* Body is not JSON */ }

            return $"GitHub responded with status code {(int)status}.";
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("QuickGit/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            return client;
        }
    }
}

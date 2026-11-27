using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using QuickGit.Hosting;

namespace QuickGit.GitHub
{
    public readonly struct GitHubResponse
    {
        public int StatusCode { get; }
        public string Body { get; }
        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;

        public GitHubResponse(int statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }
    }

    /// <summary>
    /// Single place that talks HTTP to the GitHub REST API: base URL, headers, token and error
    /// messages (SRP). The services built on top only deal with endpoints and DTOs.
    /// The HttpClient can be injected so the services can be tested without network.
    /// </summary>
    public sealed class GitHubApiClient
    {
        private const string ApiBase = "https://api.github.com";
        private static readonly HttpClient SharedHttp = CreateHttpClient(new HttpClientHandler());

        private readonly IAccessTokenStore _tokens;
        private readonly HttpClient _http;

        public GitHubApiClient(IAccessTokenStore tokens, HttpClient http = null)
        {
            _tokens = tokens;
            _http = http ?? SharedHttp;
        }

        public bool HasToken => _tokens.HasToken;

        public async Task<GitHubResponse> SendAsync(HttpMethod method, string path, string jsonBody, CancellationToken ct)
        {
            using (var request = new HttpRequestMessage(method, ApiBase + path))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.Load());
                if (jsonBody != null) request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                using (var response = await _http.SendAsync(request, ct))
                {
                    var body = response.Content != null ? await response.Content.ReadAsStringAsync() : string.Empty;
                    return new GitHubResponse((int)response.StatusCode, body);
                }
            }
        }

        /// <summary>Human readable error for a failed response.</summary>
        public static string DescribeError(GitHubResponse response, string notFoundMessage = null)
        {
            var apiMessage = ReadApiMessage(response.Body);

            switch (response.StatusCode)
            {
                case 401:
                    return "GitHub rejected the token (invalid or expired).";
                case 403:
                    return "GitHub denied the request. The token needs the 'repo' scope and you need admin rights on the repository." +
                           (apiMessage != null ? $"\n{apiMessage}" : string.Empty);
                case 404:
                    return notFoundMessage ?? "Not found on GitHub (or the token cannot see it).";
            }

            return apiMessage ?? $"GitHub responded with status code {response.StatusCode}.";
        }

        private static string ReadApiMessage(string body)
        {
            try
            {
                var error = JsonUtility.FromJson<GitHubErrorDto>(body);
                if (error == null) return null;

                var details = error.errors?.Select(e => e.message).Where(m => !string.IsNullOrEmpty(m)).ToArray();
                if (details != null && details.Length > 0) return $"GitHub: {string.Join("; ", details)}";
                if (!string.IsNullOrEmpty(error.message)) return $"GitHub: {error.message}";
            }
            catch (ArgumentException) { /* Body is not JSON */ }

            return null;
        }

        /// <summary>JsonUtility cannot read top-level arrays, so the array is wrapped in an object first.</summary>
        public static string WrapArray(string jsonArray) => "{\"items\":" + (string.IsNullOrWhiteSpace(jsonArray) ? "[]" : jsonArray) + "}";

        public static HttpClient CreateHttpClient(HttpMessageHandler handler)
        {
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("QuickGit/1.1");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            return client;
        }
    }
}

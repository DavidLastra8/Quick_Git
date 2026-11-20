using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityGitTool.Core;
using UnityGitTool.Hosting;

namespace UnityGitTool.GitHub
{
    /// <summary>Implementación de IRemoteRepositoryHost sobre la API REST de GitHub.</summary>
    public sealed class GitHubRepositoryHost : IRemoteRepositoryHost
    {
        private const string ApiBase = "https://api.github.com";
        private static readonly HttpClient Http = CreateHttpClient();

        private readonly IAccessTokenStore _tokens;

        public string DisplayName => "GitHub";
        public string TokenCreationUrl => "https://github.com/settings/tokens/new?scopes=repo&description=Unity%20Git%20Tool";

        public GitHubRepositoryHost(IAccessTokenStore tokens) => _tokens = tokens;

        public async Task<OperationResult<RemoteAccount>> AuthenticateAsync(CancellationToken ct)
        {
            if (!_tokens.HasToken) return OperationResult<RemoteAccount>.Fail("No hay ningún token de GitHub configurado.");

            using (var request = CreateRequest(HttpMethod.Get, "/user"))
            using (var response = await Http.SendAsync(request, ct))
            {
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return OperationResult<RemoteAccount>.Fail(DescribeError(response.StatusCode, body));

                var user = JsonUtility.FromJson<GitHubUserDto>(body);
                return OperationResult<RemoteAccount>.Ok(new RemoteAccount(user.login), $"Conectado a GitHub como {user.login}.");
            }
        }

        public async Task<OperationResult<RemoteRepository>> CreateRepositoryAsync(CreateRepositoryRequest repository, CancellationToken ct)
        {
            var validation = repository.Validate();
            if (!validation.Success) return OperationResult<RemoteRepository>.Fail(validation.Message);
            if (!_tokens.HasToken) return OperationResult<RemoteRepository>.Fail("No hay ningún token de GitHub configurado.");

            var payload = JsonUtility.ToJson(new GitHubCreateRepositoryBody
            {
                name = repository.Name,
                description = repository.Description,
                @private = repository.IsPrivate,
                auto_init = false // El contenido lo sube el push inicial desde el proyecto.
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
                        $"Repositorio '{dto.full_name}' creado en GitHub.");
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
                case 401: return "GitHub rechazó el token (inválido o caducado).";
                case 403: return "El token no tiene permisos suficientes (necesita el scope 'repo').";
            }

            try
            {
                var error = JsonUtility.FromJson<GitHubErrorDto>(body);
                var details = error.errors?.Select(e => e.message).Where(m => !string.IsNullOrEmpty(m)).ToArray();
                if (details != null && details.Length > 0) return $"GitHub: {string.Join("; ", details)}";
                if (!string.IsNullOrEmpty(error.message)) return $"GitHub: {error.message}";
            }
            catch (ArgumentException) { /* Cuerpo no JSON */ }

            return $"GitHub respondió con el código {(int)status}.";
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UnityGitTool/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            return client;
        }
    }
}

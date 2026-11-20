using System;
using System.Collections.Generic;
using System.Text;
using UnityGitTool.Git;
using UnityGitTool.Hosting;

namespace UnityGitTool.GitHub
{
    /// <summary>
    /// Inyecta el token como cabecera HTTP sólo para URLs de github.com, sin escribirlo
    /// nunca en .git/config ni en la URL del remoto. Si no hay token, git usará su
    /// gestor de credenciales habitual (Git Credential Manager).
    /// </summary>
    public sealed class GitHubTokenAuthArgumentsProvider : IGitAuthArgumentsProvider
    {
        private readonly IAccessTokenStore _tokens;

        public GitHubTokenAuthArgumentsProvider(IAccessTokenStore tokens) => _tokens = tokens;

        public IEnumerable<string> GetArguments()
        {
            var token = _tokens.Load();
            if (string.IsNullOrEmpty(token)) yield break;

            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + token));
            yield return "-c";
            yield return "http.https://github.com/.extraheader=AUTHORIZATION: basic " + basic;
        }
    }
}

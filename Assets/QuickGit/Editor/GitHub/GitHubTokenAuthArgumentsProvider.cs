using System;
using System.Collections.Generic;
using System.Text;
using QuickGit.Git;
using QuickGit.Hosting;

namespace QuickGit.GitHub
{
    /// <summary>
    /// Injects the token as an HTTP header only for github.com URLs, never writing it
    /// to .git/config or to the remote URL. Without a token, git falls back to its
    /// usual credential helper (Git Credential Manager).
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

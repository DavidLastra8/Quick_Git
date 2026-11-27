using QuickGit.Git;
using QuickGit.Hosting;

namespace QuickGit.GitHub
{
    /// <summary>Extracts "owner/repo" from GitHub remote URLs (https or ssh).</summary>
    public sealed class GitHubRemoteLocator : IRemoteRepositoryLocator
    {
        private const string Prefix = "https://github.com/";

        public bool TryParse(string remoteUrl, out RemoteRepositoryRef repository)
        {
            repository = null;

            // ToWebUrl normalizes ssh, credentials and the ".git" suffix: https://github.com/owner/repo
            var webUrl = RemoteUrlFormatter.ToWebUrl(remoteUrl);
            if (webUrl == null || !webUrl.StartsWith(Prefix, System.StringComparison.OrdinalIgnoreCase)) return false;

            var parts = webUrl.Substring(Prefix.Length).TrimEnd('/').Split('/');
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0) return false;

            repository = new RemoteRepositoryRef(parts[0], parts[1]);
            return true;
        }
    }
}

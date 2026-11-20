namespace UnityGitTool.Git
{
    /// <summary>Turns remote URLs (https or ssh) into the repository web URL.</summary>
    public static class RemoteUrlFormatter
    {
        public static string ToWebUrl(string remoteUrl)
        {
            if (string.IsNullOrWhiteSpace(remoteUrl)) return null;
            var url = remoteUrl.Trim();

            // git@github.com:user/repo.git → https://github.com/user/repo.git
            if (url.StartsWith("git@"))
            {
                var colon = url.IndexOf(':');
                if (colon > 4) url = "https://" + url.Substring(4, colon - 4) + "/" + url.Substring(colon + 1);
            }

            // https://user@github.com/... → https://github.com/...
            var schemeEnd = url.IndexOf("://");
            var at = url.IndexOf('@');
            if (schemeEnd > 0 && at > schemeEnd) url = url.Substring(0, schemeEnd + 3) + url.Substring(at + 1);

            return url.EndsWith(".git") ? url.Substring(0, url.Length - 4) : url;
        }
    }
}

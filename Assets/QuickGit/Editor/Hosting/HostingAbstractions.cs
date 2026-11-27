using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using QuickGit.Core;

namespace QuickGit.Hosting
{
    /// <summary>
    /// Remote hosting service (GitHub today; GitLab/Bitbucket tomorrow without touching the presenter: OCP + LSP).
    /// </summary>
    public interface IRemoteRepositoryHost
    {
        string DisplayName { get; }
        string TokenCreationUrl { get; }
        Task<OperationResult<RemoteAccount>> AuthenticateAsync(CancellationToken cancellationToken);
        Task<OperationResult<RemoteRepository>> CreateRepositoryAsync(CreateRepositoryRequest request, CancellationToken cancellationToken);

        /// <summary>Web URL a repository will have (e.g. https://github.com/user/repo).</summary>
        string BuildRepositoryUrl(string owner, string repositoryName);

        /// <summary>Web URL of a branch inside a repository.</summary>
        string BuildBranchUrl(string repositoryWebUrl, string branchName);
    }

    public interface IAccessTokenStore
    {
        bool HasToken { get; }
        string Load();
        void Save(string token);
        void Clear();
    }

    public sealed class RemoteAccount
    {
        public string Login { get; }
        public RemoteAccount(string login) => Login = login;
    }

    public sealed class RemoteRepository
    {
        public string FullName { get; }
        public string CloneUrl { get; }
        public string WebUrl { get; }

        public RemoteRepository(string fullName, string cloneUrl, string webUrl)
        {
            FullName = fullName;
            CloneUrl = cloneUrl;
            WebUrl = webUrl;
        }
    }

    public sealed class CreateRepositoryRequest
    {
        private static readonly Regex ValidName = new Regex(@"^[A-Za-z0-9._-]{1,100}$");

        public string Name { get; }
        public string Description { get; }
        public bool IsPrivate { get; }

        public CreateRepositoryRequest(string name, string description, bool isPrivate)
        {
            Name = name?.Trim() ?? string.Empty;
            Description = description?.Trim() ?? string.Empty;
            IsPrivate = isPrivate;
        }

        public OperationResult Validate()
        {
            if (string.IsNullOrEmpty(Name)) return OperationResult.Fail("The repository name is required.");
            if (!ValidName.IsMatch(Name) || Name == "." || Name == "..")
                return OperationResult.Fail("The name can only contain letters, numbers, '.', '-' and '_' (max. 100).");
            return OperationResult.Ok();
        }
    }
}

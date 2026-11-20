using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;

namespace UnityGitTool.Hosting
{
    /// <summary>
    /// Servicio de alojamiento remoto (GitHub hoy; GitLab/Bitbucket mañana sin tocar el presentador: OCP + LSP).
    /// </summary>
    public interface IRemoteRepositoryHost
    {
        string DisplayName { get; }
        string TokenCreationUrl { get; }
        Task<OperationResult<RemoteAccount>> AuthenticateAsync(CancellationToken cancellationToken);
        Task<OperationResult<RemoteRepository>> CreateRepositoryAsync(CreateRepositoryRequest request, CancellationToken cancellationToken);
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
            if (string.IsNullOrEmpty(Name)) return OperationResult.Fail("El nombre del repositorio es obligatorio.");
            if (!ValidName.IsMatch(Name) || Name == "." || Name == "..")
                return OperationResult.Fail("El nombre sólo puede contener letras, números, '.', '-' y '_' (máx. 100).");
            return OperationResult.Ok();
        }
    }
}

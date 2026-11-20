using System;
using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;
using UnityGitTool.Git;
using UnityGitTool.Hosting;

namespace UnityGitTool.UseCases
{
    public interface IPublishRepositoryUseCase
    {
        Task<OperationResult<RemoteRepository>> ExecuteAsync(CreateRepositoryRequest request, IProgress<string> progress, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Orquesta "publicar el proyecto": init (si hace falta) → commit inicial → crear repo remoto
    /// → configurar origin → push. Cada paso lo hace un servicio distinto (SRP); este caso de uso
    /// sólo coordina, y así el presentador no conoce el orden de los pasos.
    /// </summary>
    public sealed class PublishRepositoryUseCase : IPublishRepositoryUseCase
    {
        private const string InitialCommitMessage = "Initial commit";
        private const string RemoteName = "origin";

        private readonly IGitRepositoryInspector _inspector;
        private readonly IGitRepositoryInitializer _initializer;
        private readonly IGitCommitService _commits;
        private readonly IGitRemoteService _remotes;
        private readonly IGitSyncService _sync;
        private readonly IRemoteRepositoryHost _host;

        public PublishRepositoryUseCase(
            IGitRepositoryInspector inspector,
            IGitRepositoryInitializer initializer,
            IGitCommitService commits,
            IGitRemoteService remotes,
            IGitSyncService sync,
            IRemoteRepositoryHost host)
        {
            _inspector = inspector;
            _initializer = initializer;
            _commits = commits;
            _remotes = remotes;
            _sync = sync;
            _host = host;
        }

        public async Task<OperationResult<RemoteRepository>> ExecuteAsync(CreateRepositoryRequest request, IProgress<string> progress, CancellationToken ct)
        {
            var validation = request.Validate();
            if (!validation.Success) return Fail(validation.Message);

            var status = await _inspector.InspectAsync(ct);
            if (status.State == RepositoryState.GitNotInstalled) return Fail("Git no está instalado o no está en el PATH.");
            if (status.HasRemote) return Fail($"El repositorio ya tiene un remoto '{RemoteName}': {status.RemoteUrl}");

            if (!status.IsRepository)
            {
                progress?.Report("Inicializando repositorio local...");
                var init = await _initializer.InitializeAsync(ct);
                if (!init.Success) return Fail(init.Message);
                status = await _inspector.InspectAsync(ct);
            }

            if (!status.HasCommits)
            {
                progress?.Report("Creando commit inicial (puede tardar en proyectos grandes)...");
                var commit = await _commits.CommitAllAsync(InitialCommitMessage, ct);
                if (!commit.Success) return Fail(commit.Message);
            }

            progress?.Report($"Creando repositorio en {_host.DisplayName}...");
            var created = await _host.CreateRepositoryAsync(request, ct);
            if (!created.Success) return Fail(created.Message);

            progress?.Report("Configurando remoto...");
            var remote = await _remotes.SetRemoteAsync(RemoteName, created.Value.CloneUrl, ct);
            if (!remote.Success) return Fail(remote.Message);

            status = await _inspector.InspectAsync(ct);

            progress?.Report("Subiendo el proyecto (push inicial)...");
            var push = await _sync.PushAsync(status.CurrentBranch, setUpstream: true, ct);
            if (!push.Success)
                return Fail($"El repositorio se creó ({created.Value.WebUrl}) pero el push falló:\n{push.Message}");

            return OperationResult<RemoteRepository>.Ok(created.Value, $"Proyecto publicado en {created.Value.WebUrl}");
        }

        private static OperationResult<RemoteRepository> Fail(string message) => OperationResult<RemoteRepository>.Fail(message);
    }
}

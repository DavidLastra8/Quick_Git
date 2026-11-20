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
    /// Orchestrates "publish the project": init (if needed) → initial commit → create remote repo
    /// → configure origin → push. Each step is done by a different service (SRP); this use case
    /// only coordinates, so the presenter does not need to know the order of the steps.
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
            if (status.State == RepositoryState.GitNotInstalled) return Fail("Git is not installed or not in the PATH.");
            if (status.HasRemote) return Fail($"The repository already has a '{RemoteName}' remote: {status.RemoteUrl}");

            if (!status.IsRepository)
            {
                progress?.Report("Initializing local repository...");
                var init = await _initializer.InitializeAsync(ct);
                if (!init.Success) return Fail(init.Message);
                status = await _inspector.InspectAsync(ct);
            }

            if (!status.HasCommits)
            {
                progress?.Report("Creating initial commit (may take a while on big projects)...");
                var commit = await _commits.CommitAllAsync(new CommitMessage(InitialCommitMessage), ct);
                if (!commit.Success) return Fail(commit.Message);
            }

            progress?.Report($"Creating repository on {_host.DisplayName}...");
            var created = await _host.CreateRepositoryAsync(request, ct);
            if (!created.Success) return Fail(created.Message);

            progress?.Report("Configuring remote...");
            var remote = await _remotes.SetRemoteAsync(RemoteName, created.Value.CloneUrl, ct);
            if (!remote.Success) return Fail(remote.Message);

            status = await _inspector.InspectAsync(ct);

            progress?.Report("Uploading the project (initial push)...");
            var push = await _sync.PushAsync(status.CurrentBranch, setUpstream: true, ct);
            if (!push.Success)
                return Fail($"The repository was created ({created.Value.WebUrl}) but the push failed:\n{push.Message}");

            return OperationResult<RemoteRepository>.Ok(created.Value, $"Project published at {created.Value.WebUrl}");
        }

        private static OperationResult<RemoteRepository> Fail(string message) => OperationResult<RemoteRepository>.Fail(message);
    }
}

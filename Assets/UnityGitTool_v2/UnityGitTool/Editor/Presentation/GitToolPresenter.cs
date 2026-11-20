using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;
using UnityGitTool.Git;
using UnityGitTool.Hosting;
using UnityGitTool.UseCases;
using UnityGitTool.Unity;

namespace UnityGitTool.Presentation
{
    /// <summary>
    /// Presentador: traduce intenciones de la vista en llamadas a servicios y el resultado en
    /// estado para la vista. Sólo depende de abstracciones (DIP), así que es testeable sin Unity.
    /// </summary>
    public sealed class GitToolPresenter : IDisposable
    {
        private readonly IGitToolView _view;
        private readonly IGitRepositoryInspector _inspector;
        private readonly IGitRepositoryInitializer _initializer;
        private readonly IGitBranchService _branches;
        private readonly IGitSyncService _sync;
        private readonly IGitCommitService _commits;
        private readonly IPublishRepositoryUseCase _publish;
        private readonly IRemoteRepositoryHost _host;
        private readonly IAccessTokenStore _tokens;
        private readonly IEditorAssetSynchronizer _assets;
        private readonly IUrlOpener _urlOpener;
        private readonly IProjectInfo _project;

        private RepositoryStatus _status = RepositoryStatus.Unknown;
        private IReadOnlyList<GitBranch> _branchList = Array.Empty<GitBranch>();
        private string _accountName;
        private CancellationTokenSource _cancellation;
        private Task _backgroundRefresh = Task.CompletedTask;
        private bool _isBusy;
        private bool _disposed;

        public GitToolPresenter(
            IGitToolView view,
            IGitRepositoryInspector inspector,
            IGitRepositoryInitializer initializer,
            IGitBranchService branches,
            IGitSyncService sync,
            IGitCommitService commits,
            IPublishRepositoryUseCase publish,
            IRemoteRepositoryHost host,
            IAccessTokenStore tokens,
            IEditorAssetSynchronizer assets,
            IUrlOpener urlOpener,
            IProjectInfo project)
        {
            _view = view;
            _inspector = inspector;
            _initializer = initializer;
            _branches = branches;
            _sync = sync;
            _commits = commits;
            _publish = publish;
            _host = host;
            _tokens = tokens;
            _assets = assets;
            _urlOpener = urlOpener;
            _project = project;
        }

        public void Initialize()
        {
            _view.RefreshRequested += OnRefresh;
            _view.BackgroundRefreshRequested += OnBackgroundRefresh;
            _view.CancelRequested += OnCancel;
            _view.FetchRequested += OnFetch;
            _view.PullRequested += OnPull;
            _view.PushRequested += OnPush;
            _view.CommitRequested += OnCommit;
            _view.CheckoutRequested += OnCheckout;
            _view.CreateBranchRequested += OnCreateBranch;
            _view.InitializeRepositoryRequested += OnInitializeRepository;
            _view.PublishRepositoryRequested += OnPublishRepository;
            _view.SaveTokenRequested += OnSaveToken;
            _view.ClearTokenRequested += OnClearToken;
            _view.OpenRemoteRequested += OnOpenRemote;

            Render();
            Run(InitialLoadAsync);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cancellation?.Cancel();

            _view.RefreshRequested -= OnRefresh;
            _view.BackgroundRefreshRequested -= OnBackgroundRefresh;
            _view.CancelRequested -= OnCancel;
            _view.FetchRequested -= OnFetch;
            _view.PullRequested -= OnPull;
            _view.PushRequested -= OnPush;
            _view.CommitRequested -= OnCommit;
            _view.CheckoutRequested -= OnCheckout;
            _view.CreateBranchRequested -= OnCreateBranch;
            _view.InitializeRepositoryRequested -= OnInitializeRepository;
            _view.PublishRepositoryRequested -= OnPublishRepository;
            _view.SaveTokenRequested -= OnSaveToken;
            _view.ClearTokenRequested -= OnClearToken;
            _view.OpenRemoteRequested -= OnOpenRemote;
        }

        // ---------------------------------------------------------------- Eventos de la vista
        // Cada evento delega en un método async a través de Run(), que garantiza que ninguna
        // excepción se pierda en silencio.

        private void OnRefresh() => Run(() => ExecuteAsync("Actualizando estado...", null));
        private void OnBackgroundRefresh() => Run(BackgroundRefreshAsync);
        private void OnCancel() => _cancellation?.Cancel();
        private void OnFetch() => Run(FetchAsync);
        private void OnPull(PullStrategy strategy) => Run(() => PullAsync(strategy));
        private void OnPush() => Run(PushAsync);
        private void OnCommit(string message) => Run(() => CommitAsync(message));
        private void OnCheckout(GitBranch branch) => Run(() => CheckoutAsync(branch));
        private void OnCreateBranch(string name) => Run(() => CreateBranchAsync(name));
        private void OnInitializeRepository() => Run(InitializeRepositoryAsync);
        private void OnPublishRepository(CreateRepositoryRequest request) => Run(() => PublishRepositoryAsync(request));
        private void OnSaveToken(string token) => Run(() => SaveTokenAsync(token));
        private void OnClearToken() => Run(ClearTokenAsync);
        private void OnOpenRemote() => _urlOpener.Open(RemoteUrlFormatter.ToWebUrl(_status.RemoteUrl));

        // ---------------------------------------------------------------- Acciones

        private async Task InitialLoadAsync()
        {
            await ExecuteAsync("Analizando el proyecto...", null);
            if (_tokens.HasToken) await ExecuteAsync("Verificando token...", AuthenticateAsync);
        }

        /// <summary>Refresco silencioso (al enfocar la ventana): no bloquea la UI ni descarta clics.</summary>
        private async Task BackgroundRefreshAsync()
        {
            if (_isBusy || !_backgroundRefresh.IsCompleted) return;

            _backgroundRefresh = LoadStateAsync(CancellationToken.None);
            try { await _backgroundRefresh; }
            finally { Render(); }
        }

        private async Task FetchAsync()
        {
            if (!await CanActAsync() || !RequireRemote()) return;
            await ExecuteAsync("Haciendo fetch...", _sync.FetchAsync);
        }

        private async Task PullAsync(PullStrategy strategy)
        {
            if (!await CanActAsync() || !RequireRemote()) return;

            if (!_status.HasUpstream)
            {
                Log($"La rama '{_status.CurrentBranch}' no sigue ninguna rama remota. Haz push primero.", LogSeverity.Warning);
                return;
            }

            if (!SaveOpenWork()) return;
            await ExecuteAsync("Haciendo pull...", ct => _sync.PullAsync(strategy, ct), touchesWorkingTree: true);
        }

        private async Task PushAsync()
        {
            if (!await CanActAsync() || !RequireRemote()) return;

            if (!_status.HasCommits)
            {
                Log("No hay commits que subir todavía.", LogSeverity.Warning);
                return;
            }

            if (_status.IsDetachedHead)
            {
                Log("Estás en HEAD separado. Cambia a una rama antes de hacer push.", LogSeverity.Warning);
                return;
            }

            if (_status.ChangedFiles > 0)
                Log($"Hay {_status.ChangedFiles} archivo(s) sin commitear: no se incluirán en el push.", LogSeverity.Warning);

            var branch = _status.CurrentBranch;
            var setUpstream = !_status.HasUpstream;
            await ExecuteAsync("Haciendo push...", ct => _sync.PushAsync(branch, setUpstream, ct));
        }

        private async Task CommitAsync(string message)
        {
            if (!await CanActAsync()) return;
            if (!SaveOpenWork()) return; // Asegura que lo que hay en disco es lo último del editor.

            if (await ExecuteAsync("Creando commit...", ct => _commits.CommitAllAsync(message, ct)))
                _view.ClearCommitMessage();
        }

        private async Task CheckoutAsync(GitBranch branch)
        {
            if (branch == null || branch.IsCurrent) return;
            if (!await CanActAsync()) return;

            if (_status.ChangedFiles > 0 && !_view.Confirm(
                    "Cambios sin guardar en git",
                    $"Tienes {_status.ChangedFiles} archivo(s) modificados. Git puede impedir el cambio de rama " +
                    "o llevarse los cambios a la nueva rama.\n\n¿Continuar?"))
            {
                Log("Cambio de rama cancelado.", LogSeverity.Info);
                return;
            }

            if (!SaveOpenWork()) return;
            await ExecuteAsync($"Cambiando a '{branch.LocalName}'...", ct => _branches.CheckoutAsync(branch, ct), touchesWorkingTree: true);
        }

        private async Task CreateBranchAsync(string name)
        {
            if (!await CanActAsync()) return;
            if (await ExecuteAsync("Creando rama...", ct => _branches.CreateBranchAsync(name, ct)))
                _view.ClearNewBranchName();
        }

        private async Task InitializeRepositoryAsync()
        {
            if (!await CanActAsync()) return;
            await ExecuteAsync("Inicializando repositorio...", _initializer.InitializeAsync);
        }

        private async Task PublishRepositoryAsync(CreateRepositoryRequest request)
        {
            if (!await CanActAsync()) return;

            if (!_tokens.HasToken)
            {
                Log($"Configura un token de {_host.DisplayName} en la sección Cuenta.", LogSeverity.Error);
                return;
            }

            var validation = request.Validate();
            if (!validation.Success)
            {
                Log(validation.Message, LogSeverity.Error);
                return;
            }

            var visibility = request.IsPrivate ? "privado" : "PÚBLICO";
            if (!_view.Confirm("Publicar proyecto",
                    $"Se creará el repositorio {visibility} '{request.Name}' en {_host.DisplayName} y se subirá el proyecto.\n\n¿Continuar?"))
            {
                Log("Publicación cancelada.", LogSeverity.Info);
                return;
            }

            if (!SaveOpenWork()) return;

            Log($"Publicando '{request.Name}' en {_host.DisplayName}...", LogSeverity.Info);
            var progress = new Progress<string>(step =>
            {
                if (_disposed) return;
                _view.SetBusy(true, step);
                _view.AppendLog(step, LogSeverity.Info);
            });

            await ExecuteAsync("Publicando...", async ct => await _publish.ExecuteAsync(request, progress, ct));
        }

        private async Task SaveTokenAsync(string token)
        {
            if (!await CanActAsync()) return;

            if (string.IsNullOrWhiteSpace(token))
            {
                Log("El token está vacío.", LogSeverity.Warning);
                return;
            }

            _tokens.Save(token.Trim());
            await ExecuteAsync("Verificando token...", AuthenticateAsync);
        }

        private async Task ClearTokenAsync()
        {
            if (!await CanActAsync()) return;
            _tokens.Clear();
            _accountName = null;
            Log("Token eliminado.", LogSeverity.Info);
            Render();
        }

        // ---------------------------------------------------------------- Infraestructura

        private async Task<OperationResult> AuthenticateAsync(CancellationToken ct)
        {
            var result = await _host.AuthenticateAsync(ct);
            _accountName = result.Success ? result.Value.Login : null;
            return result;
        }

        /// <summary>
        /// Espera a que acabe un refresco en segundo plano (así el clic no se pierde) y comprueba
        /// que no haya otra operación del usuario en marcha. Nunca descarta una acción sin avisar.
        /// </summary>
        private async Task<bool> CanActAsync()
        {
            if (_disposed) return false;

            if (!_backgroundRefresh.IsCompleted)
            {
                try { await _backgroundRefresh; }
                catch (Exception) { /* El error ya se registra en Run(). */ }
            }

            if (_isBusy)
            {
                Log("Hay otra operación en curso. Espera a que termine o pulsa Cancelar.", LogSeverity.Warning);
                return false;
            }

            return !_disposed;
        }

        private bool SaveOpenWork()
        {
            if (_assets.SaveOpenWork()) return true;
            Log("Operación cancelada: no se guardaron las escenas abiertas.", LogSeverity.Warning);
            return false;
        }

        /// <summary>Punto de entrada de todos los eventos: cualquier excepción acaba en el registro.</summary>
        private async void Run(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
                Log("Operación cancelada.", LogSeverity.Warning);
            }
            catch (Exception exception)
            {
                Log($"Error inesperado: {exception.GetType().Name}: {exception.Message}", LogSeverity.Error);
            }
        }

        /// <summary>
        /// Plantilla común de toda operación: bloquea la UI, permite cancelar, registra el
        /// resultado y recarga el estado. Devuelve true si la operación tuvo éxito.
        /// </summary>
        private async Task<bool> ExecuteAsync(string busyMessage, Func<CancellationToken, Task<OperationResult>> operation, bool touchesWorkingTree = false)
        {
            if (_disposed) return false;

            if (_isBusy)
            {
                Log("Hay otra operación en curso. Espera a que termine o pulsa Cancelar.", LogSeverity.Warning);
                return false;
            }

            _isBusy = true;
            _cancellation = new CancellationTokenSource();
            _view.SetBusy(true, busyMessage);
            if (touchesWorkingTree) _assets.SuspendAutoRefresh();

            var succeeded = operation == null;
            try
            {
                if (operation != null)
                {
                    var result = await operation(_cancellation.Token);
                    succeeded = result.Success;
                    Log(result.Message, result.Success ? LogSeverity.Success : LogSeverity.Error);
                }

                await LoadStateAsync(_cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                Log("Operación cancelada.", LogSeverity.Warning);
            }
            catch (Exception exception)
            {
                succeeded = false;
                Log($"Error inesperado: {exception.GetType().Name}: {exception.Message}", LogSeverity.Error);
            }
            finally
            {
                // Siempre se refresca: un pull con conflictos también cambia archivos.
                if (touchesWorkingTree) _assets.ResumeAutoRefresh(refreshNow: true);

                _cancellation.Dispose();
                _cancellation = null;
                _isBusy = false;

                if (!_disposed)
                {
                    _view.SetBusy(false, null);
                    Render();
                }
            }

            return succeeded;
        }

        private async Task LoadStateAsync(CancellationToken ct)
        {
            var status = await _inspector.InspectAsync(ct);
            var branches = Array.Empty<GitBranch>() as IReadOnlyList<GitBranch>;

            if (status.IsRepository)
            {
                var result = await _branches.GetBranchesAsync(ct);
                if (result.Success) branches = result.Value;
            }

            _status = status;
            _branchList = branches;
        }

        private void Render()
        {
            if (_disposed) return;

            _view.Render(new GitToolViewState
            {
                Status = _status,
                Branches = _branchList,
                HasToken = _tokens.HasToken,
                AccountName = _accountName,
                HostName = _host.DisplayName,
                TokenCreationUrl = _host.TokenCreationUrl,
                DefaultRepositoryName = CreateRepositoryRequest.SanitizeName(_project.Name)
            });
        }

        private bool RequireRemote()
        {
            if (_status.HasRemote) return true;
            Log("El repositorio no tiene remoto 'origin'. Publícalo primero.", LogSeverity.Warning);
            return false;
        }

        private void Log(string message, LogSeverity severity)
        {
            if (_disposed || string.IsNullOrWhiteSpace(message)) return;
            _view.AppendLog(message, severity);
        }
    }
}

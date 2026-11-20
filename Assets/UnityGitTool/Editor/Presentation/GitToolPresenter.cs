using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityGitTool.Core;
using UnityGitTool.Git;
using UnityGitTool.Hosting;

namespace UnityGitTool.Presentation
{
    /// <summary>
    /// Presentador: traduce intenciones de la vista en llamadas a servicios y el resultado en
    /// estado para la vista. Sólo depende de abstracciones (DIP), así que es testeable sin Unity.
    /// </summary>
    public sealed class GitToolPresenter : IDisposable
    {
        private readonly IGitToolView _view;
        private readonly GitToolDependencies _deps;

        private RepositoryStatus _status = RepositoryStatus.Unknown;
        private IReadOnlyList<GitBranch> _branchList = Array.Empty<GitBranch>();
        private string _pushBlockedReason;
        private string _accountName;
        private string _repositoryNameInput; // null = el usuario aún no lo ha tocado (se usa el nombre por defecto)
        private string _branchNameInput = string.Empty;
        private CancellationTokenSource _cancellation;
        private Task _backgroundRefresh = Task.CompletedTask;
        private bool _isBusy;
        private bool _disposed;

        public GitToolPresenter(IGitToolView view, GitToolDependencies dependencies)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _deps = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
            _deps.Validate();
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
            _view.DeleteBranchRequested += OnDeleteBranch;
            _view.BranchNameEdited += OnBranchNameEdited;
            _view.RepositoryNameEdited += OnRepositoryNameEdited;
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
            _view.DeleteBranchRequested -= OnDeleteBranch;
            _view.BranchNameEdited -= OnBranchNameEdited;
            _view.RepositoryNameEdited -= OnRepositoryNameEdited;
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
        private void OnCommit(CommitMessage message) => Run(() => CommitAsync(message));
        private void OnCheckout(GitBranch branch) => Run(() => CheckoutAsync(branch));
        private void OnCreateBranch(string name) => Run(() => CreateBranchAsync(name));
        private void OnDeleteBranch(GitBranch branch) => Run(() => DeleteBranchAsync(branch));
        private void OnInitializeRepository() => Run(InitializeRepositoryAsync);
        private void OnPublishRepository(CreateRepositoryRequest request) => Run(() => PublishRepositoryAsync(request));
        private void OnSaveToken(string token) => Run(() => SaveTokenAsync(token));
        private void OnClearToken() => Run(ClearTokenAsync);
        private void OnOpenRemote() => _deps.UrlOpener.Open(RemoteUrlFormatter.ToWebUrl(_status.RemoteUrl));

        private void OnBranchNameEdited(string raw)
        {
            var fixedName = _deps.BranchNames.SanitizeWhileTyping(raw ?? string.Empty);
            if (fixedName != raw) _view.ShowBranchName(fixedName);

            _branchNameInput = fixedName;
            Render(); // Actualiza la vista previa en cada pulsación.
        }

        private void OnRepositoryNameEdited(string raw)
        {
            var fixedName = _deps.RepositoryNames.SanitizeWhileTyping(raw ?? string.Empty);
            if (fixedName != raw) _view.ShowRepositoryName(fixedName);

            _repositoryNameInput = fixedName;
            Render();
        }

        // ---------------------------------------------------------------- Acciones

        private async Task InitialLoadAsync()
        {
            await ExecuteAsync("Analizando el proyecto...", null);
            if (_deps.Tokens.HasToken) await ExecuteAsync("Verificando token...", AuthenticateAsync);
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
            await ExecuteAsync("Haciendo fetch...", _deps.Sync.FetchAsync);
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
            await ExecuteAsync("Haciendo pull...", ct => _deps.Sync.PullAsync(strategy, ct), touchesWorkingTree: true);
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

            var status = _status;
            await ExecuteAsync("Haciendo push...", async ct =>
            {
                // Se vuelve a comprobar justo antes de subir: el estado pudo cambiar tras el último refresco.
                var guard = await _deps.PushGuard.CheckAsync(status, ct);
                if (!guard.Success) return guard;

                return await _deps.Sync.PushAsync(status.CurrentBranch, !status.HasUpstream, ct);
            });
        }

        private async Task CommitAsync(CommitMessage message)
        {
            if (!await CanActAsync()) return;

            var validation = message.Validate();
            if (!validation.Success)
            {
                Log(validation.Message, LogSeverity.Warning);
                return;
            }

            if (!SaveOpenWork()) return; // Asegura que lo que hay en disco es lo último del editor.

            if (await ExecuteAsync("Creando commit...", ct => _deps.CreateCommit.ExecuteAsync(message, ct)))
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
                Render(); // Devuelve el desplegable a la rama actual.
                return;
            }

            if (!SaveOpenWork()) return;
            await ExecuteAsync($"Cambiando a '{branch.LocalName}'...", ct => _deps.Branches.CheckoutAsync(branch, ct), touchesWorkingTree: true);
        }

        private async Task CreateBranchAsync(string rawName)
        {
            if (!await CanActAsync()) return;

            var name = _deps.BranchNames.Sanitize(rawName);
            if (name.Length == 0)
            {
                Log("Introduce un nombre de rama válido.", LogSeverity.Warning);
                return;
            }

            if (name != rawName?.Trim())
            {
                Log($"Nombre de rama ajustado a '{name}'.", LogSeverity.Info);
                _view.ShowBranchName(name);
                _branchNameInput = name;
            }

            if (await ExecuteAsync("Creando rama...", ct => _deps.Branches.CreateBranchAsync(name, ct)))
            {
                _branchNameInput = string.Empty;
                _view.ClearNewBranchName();
                Render();
            }
        }

        private async Task DeleteBranchAsync(GitBranch branch)
        {
            if (branch == null || !await CanActAsync()) return;

            if (branch.IsCurrent)
            {
                Log("No puedes borrar la rama en la que estás. Cambia a otra primero.", LogSeverity.Warning);
                return;
            }

            if (_deps.BranchProtection.IsProtected(branch.LocalName))
            {
                Log($"'{branch.LocalName}' es una rama protegida y no se puede borrar desde aquí.", LogSeverity.Warning);
                return;
            }

            // Rama que sólo existe en GitHub.
            if (branch.IsRemote)
            {
                if (!_view.Confirm("Borrar rama remota",
                        $"Se borrará la rama '{branch.LocalName}' de {_deps.Host.DisplayName} para todo el equipo.\n\n¿Continuar?"))
                    return;

                await ExecuteAsync("Borrando rama remota...", ct => _deps.Branches.DeleteRemoteBranchAsync(branch, ct));
                return;
            }

            // Rama local: ¿también en GitHub?
            var alsoRemote = false;
            if (branch.ExistsOnRemote)
            {
                var choice = _view.Choose("Borrar rama",
                    $"La rama '{branch.Name}' también existe en {_deps.Host.DisplayName} ({branch.RemoteBranchName}).\n\n¿Qué quieres borrar?",
                    primary: $"Local y {_deps.Host.DisplayName}",
                    alternative: "Solo local");

                if (choice == DialogChoice.Cancel) return;
                alsoRemote = choice == DialogChoice.Primary;
            }
            else if (!_view.Confirm("Borrar rama", $"Se borrará la rama local '{branch.Name}'.\n\n¿Continuar?"))
            {
                return;
            }

            await ExecuteAsync($"Borrando '{branch.Name}'...", async ct =>
            {
                // ¿Se perdería algún commit? Está a salvo si ya está en la rama actual o, cuando sólo
                // se borra la copia local, si sigue existiendo en el remoto.
                var safe = await _deps.Branches.IsMergedIntoAsync(branch, "HEAD", ct)
                           || (!alsoRemote && branch.ExistsOnRemote &&
                               await _deps.Branches.IsMergedIntoAsync(branch, branch.RemoteBranchName, ct));

                if (!safe && !_view.Confirm("Rama con commits sin fusionar",
                        $"'{branch.Name}' tiene commits que no están en tu rama actual" +
                        (alsoRemote || !branch.ExistsOnRemote ? "" : $" ni en {branch.RemoteBranchName}") +
                        ". Si la borras, esos commits se perderán.\n\n¿Borrar igualmente?"))
                    return OperationResult.Ok("Borrado cancelado.");

                var local = await _deps.Branches.DeleteLocalBranchAsync(branch, ct);
                if (!local.Success || !alsoRemote) return local;

                var remote = await _deps.Branches.DeleteRemoteBranchAsync(branch, ct);
                return remote.Success
                    ? OperationResult.Ok($"{local.Message}\n{remote.Message}")
                    : OperationResult.Fail($"{local.Message}\nPero no se pudo borrar en el remoto:\n{remote.Message}");
            });
        }

        private async Task InitializeRepositoryAsync()
        {
            if (!await CanActAsync()) return;
            await ExecuteAsync("Inicializando repositorio...", _deps.Initializer.InitializeAsync);
        }

        private async Task PublishRepositoryAsync(CreateRepositoryRequest rawRequest)
        {
            if (!await CanActAsync()) return;

            var request = new CreateRepositoryRequest(_deps.RepositoryNames.Sanitize(rawRequest.Name), rawRequest.Description, rawRequest.IsPrivate);
            if (request.Name != rawRequest.Name)
            {
                if (request.Name.Length > 0) Log($"Nombre del repositorio ajustado a '{request.Name}'.", LogSeverity.Info);
                _view.ShowRepositoryName(request.Name);
                _repositoryNameInput = request.Name;
            }

            if (!_deps.Tokens.HasToken)
            {
                Log($"Configura un token de {_deps.Host.DisplayName} en la sección Cuenta.", LogSeverity.Error);
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
                    $"Se creará el repositorio {visibility} '{request.Name}' en {_deps.Host.DisplayName} y se subirá el proyecto.\n\n¿Continuar?"))
            {
                Log("Publicación cancelada.", LogSeverity.Info);
                return;
            }

            if (!SaveOpenWork()) return;

            Log($"Publicando '{request.Name}' en {_deps.Host.DisplayName}...", LogSeverity.Info);
            var progress = new Progress<string>(step =>
            {
                if (_disposed) return;
                _view.SetBusy(true, step);
                _view.AppendLog(step, LogSeverity.Info);
            });

            await ExecuteAsync("Publicando...", async ct => await _deps.Publish.ExecuteAsync(request, progress, ct));
        }

        private async Task SaveTokenAsync(string token)
        {
            if (!await CanActAsync()) return;

            if (string.IsNullOrWhiteSpace(token))
            {
                Log("El token está vacío.", LogSeverity.Warning);
                return;
            }

            _deps.Tokens.Save(token.Trim());
            await ExecuteAsync("Verificando token...", AuthenticateAsync);
        }

        private async Task ClearTokenAsync()
        {
            if (!await CanActAsync()) return;
            _deps.Tokens.Clear();
            _accountName = null;
            Log("Token eliminado.", LogSeverity.Info);
            Render();
        }

        // ---------------------------------------------------------------- Infraestructura

        private async Task<OperationResult> AuthenticateAsync(CancellationToken ct)
        {
            var result = await _deps.Host.AuthenticateAsync(ct);
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
            if (_deps.Assets.SaveOpenWork()) return true;
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
            if (touchesWorkingTree) _deps.Assets.SuspendAutoRefresh();

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
                if (touchesWorkingTree) _deps.Assets.ResumeAutoRefresh(refreshNow: true);

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
            var status = await _deps.Inspector.InspectAsync(ct);
            IReadOnlyList<GitBranch> branches = Array.Empty<GitBranch>();
            string pushBlockedReason = null;

            if (status.IsRepository)
            {
                var result = await _deps.Branches.GetBranchesAsync(ct);
                if (result.Success) branches = result.Value;

                var guard = await _deps.PushGuard.CheckAsync(status, ct);
                if (!guard.Success) pushBlockedReason = guard.Message;
            }

            _status = status;
            _branchList = branches;
            _pushBlockedReason = pushBlockedReason;
        }

        private void Render()
        {
            if (_disposed) return;

            _view.Render(new GitToolViewState
            {
                Status = _status,
                Branches = _branchList,
                DeletableBranches = _branchList
                    .Where(b => !b.IsCurrent && !_deps.BranchProtection.IsProtected(b.LocalName))
                    .ToList(),
                IsCurrentBranchProtected = _deps.BranchProtection.IsProtected(_status.CurrentBranch),
                PushBlockedReason = _pushBlockedReason,
                HasCompileErrors = _deps.Health.HasCompileErrors,
                RepositoryPreview = BuildRepositoryPreview(),
                BranchPreview = BuildBranchPreview(),
                HasToken = _deps.Tokens.HasToken,
                AccountName = _accountName,
                HostName = _deps.Host.DisplayName,
                TokenCreationUrl = _deps.Host.TokenCreationUrl,
                DefaultRepositoryName = _deps.RepositoryNames.Sanitize(_deps.Project.Name)
            });
        }

        // ---------------------------------------------------------------- Vistas previas de nombres

        private NamePreview BuildRepositoryPreview()
        {
            var raw = _repositoryNameInput ?? _deps.Project.Name;
            var name = _deps.RepositoryNames.Sanitize(raw);
            if (name.Length == 0)
                return NamePreview.Invalid("Escribe un nombre: sólo letras, números, '.', '-' y '_'.");

            var owner = _accountName ?? "tu-usuario";
            var note = name != raw.Trim() ? $"Se creará como '{name}' (se limpian guiones y puntos sobrantes)." : null;
            return NamePreview.Valid($"Se verá en {_deps.Host.DisplayName} como:", $"{owner} / {name}",
                _deps.Host.BuildRepositoryUrl(owner, name), note);
        }

        private NamePreview BuildBranchPreview()
        {
            if (string.IsNullOrWhiteSpace(_branchNameInput)) return null;

            var name = _deps.BranchNames.Sanitize(_branchNameInput);
            if (name.Length == 0)
                return NamePreview.Invalid("Ese nombre no es válido para una rama.");

            var notes = new List<string>();
            if (name != _branchNameInput.Trim()) notes.Add($"Se creará como '{name}'.");
            if (_branchList.Any(b => b.LocalName == name)) notes.Add("Ya existe una rama con este nombre.");

            if (!_status.HasRemote)
            {
                notes.Add($"Es una rama local: aparecerá en {_deps.Host.DisplayName} cuando publiques el repositorio y hagas push.");
                return NamePreview.Valid("Se creará la rama:", name, null, string.Join(" ", notes));
            }

            notes.Add($"Aparecerá en {_deps.Host.DisplayName} después de su primer push.");
            var repositoryUrl = RemoteUrlFormatter.ToWebUrl(_status.RemoteUrl);
            return NamePreview.Valid($"Se verá en {_deps.Host.DisplayName} como:", name,
                _deps.Host.BuildBranchUrl(repositoryUrl, name), string.Join(" ", notes));
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

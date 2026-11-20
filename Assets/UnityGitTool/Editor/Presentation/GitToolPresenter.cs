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
    /// Presenter: turns view intentions into service calls and results into view state.
    /// It only depends on abstractions (DIP), so it can be tested without Unity.
    /// </summary>
    public sealed class GitToolPresenter : IDisposable
    {
        private readonly IGitToolView _view;
        private readonly GitToolDependencies _deps;

        private RepositoryStatus _status = RepositoryStatus.Unknown;
        private IReadOnlyList<GitBranch> _branchList = Array.Empty<GitBranch>();
        private string _pushBlockedReason;
        private string _accountName;
        private string _repositoryNameInput; // null = the user has not touched it yet (the default name is used)
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

        // ---------------------------------------------------------------- View events
        // Each event delegates to an async method through Run(), which makes sure no
        // exception is silently lost.

        private void OnRefresh() => Run(() => ExecuteAsync("Refreshing status...", null));
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
            Render(); // Updates the preview on every keystroke.
        }

        private void OnRepositoryNameEdited(string raw)
        {
            var fixedName = _deps.RepositoryNames.SanitizeWhileTyping(raw ?? string.Empty);
            if (fixedName != raw) _view.ShowRepositoryName(fixedName);

            _repositoryNameInput = fixedName;
            Render();
        }

        // ---------------------------------------------------------------- Actions

        private async Task InitialLoadAsync()
        {
            await ExecuteAsync("Analyzing the project...", null);
            if (_deps.Tokens.HasToken) await ExecuteAsync("Verifying token...", AuthenticateAsync);
        }

        /// <summary>Silent refresh (when the window gets focus): does not block the UI or drop clicks.</summary>
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
            await ExecuteAsync("Fetching...", _deps.Sync.FetchAsync);
        }

        private async Task PullAsync(PullStrategy strategy)
        {
            if (!await CanActAsync() || !RequireRemote()) return;

            if (!_status.HasUpstream)
            {
                Log($"Branch '{_status.CurrentBranch}' does not track any remote branch. Push it first.", LogSeverity.Warning);
                return;
            }

            if (!SaveOpenWork()) return;
            await ExecuteAsync("Pulling...", ct => _deps.Sync.PullAsync(strategy, ct), touchesWorkingTree: true);
        }

        private async Task PushAsync()
        {
            if (!await CanActAsync() || !RequireRemote()) return;

            if (!_status.HasCommits)
            {
                Log("There are no commits to push yet.", LogSeverity.Warning);
                return;
            }

            if (_status.IsDetachedHead)
            {
                Log("You are in detached HEAD. Switch to a branch before pushing.", LogSeverity.Warning);
                return;
            }

            if (_status.ChangedFiles > 0)
                Log($"There are {_status.ChangedFiles} uncommitted file(s): they will not be included in the push.", LogSeverity.Warning);

            var status = _status;
            await ExecuteAsync("Pushing...", async ct =>
            {
                // Checked again right before pushing: the state may have changed since the last refresh.
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

            if (!SaveOpenWork()) return; // Makes sure what is on disk is the latest from the editor.

            if (await ExecuteAsync("Creating commit...", ct => _deps.CreateCommit.ExecuteAsync(message, ct)))
                _view.ClearCommitMessage();
        }

        private async Task CheckoutAsync(GitBranch branch)
        {
            if (branch == null || branch.IsCurrent) return;
            if (!await CanActAsync()) return;

            if (_status.ChangedFiles > 0 && !_view.Confirm(
                    "Uncommitted changes",
                    $"You have {_status.ChangedFiles} modified file(s). Git may refuse to switch branches " +
                    "or carry the changes over to the new branch.\n\nContinue?"))
            {
                Log("Branch switch cancelled.", LogSeverity.Info);
                Render(); // Puts the dropdown back on the current branch.
                return;
            }

            if (!SaveOpenWork()) return;
            await ExecuteAsync($"Switching to '{branch.LocalName}'...", ct => _deps.Branches.CheckoutAsync(branch, ct), touchesWorkingTree: true);
        }

        private async Task CreateBranchAsync(string rawName)
        {
            if (!await CanActAsync()) return;

            var name = _deps.BranchNames.Sanitize(rawName);
            if (name.Length == 0)
            {
                Log("Enter a valid branch name.", LogSeverity.Warning);
                return;
            }

            if (name != rawName?.Trim())
            {
                Log($"Branch name adjusted to '{name}'.", LogSeverity.Info);
                _view.ShowBranchName(name);
                _branchNameInput = name;
            }

            if (await ExecuteAsync("Creating branch...", ct => _deps.Branches.CreateBranchAsync(name, ct)))
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
                Log("You cannot delete the branch you are on. Switch to another one first.", LogSeverity.Warning);
                return;
            }

            if (_deps.BranchProtection.IsProtected(branch.LocalName))
            {
                Log($"'{branch.LocalName}' is a protected branch and cannot be deleted from here.", LogSeverity.Warning);
                return;
            }

            // Branch that only exists on the remote.
            if (branch.IsRemote)
            {
                if (!_view.Confirm("Delete remote branch",
                        $"Branch '{branch.LocalName}' will be deleted from {_deps.Host.DisplayName} for the whole team.\n\nContinue?"))
                    return;

                await ExecuteAsync("Deleting remote branch...", ct => _deps.Branches.DeleteRemoteBranchAsync(branch, ct));
                return;
            }

            // Local branch: also on the remote?
            var alsoRemote = false;
            if (branch.ExistsOnRemote)
            {
                var choice = _view.Choose("Delete branch",
                    $"Branch '{branch.Name}' also exists on {_deps.Host.DisplayName} ({branch.RemoteBranchName}).\n\nWhat do you want to delete?",
                    primary: $"Local and {_deps.Host.DisplayName}",
                    alternative: "Local only");

                if (choice == DialogChoice.Cancel) return;
                alsoRemote = choice == DialogChoice.Primary;
            }
            else if (!_view.Confirm("Delete branch", $"Local branch '{branch.Name}' will be deleted.\n\nContinue?"))
            {
                return;
            }

            await ExecuteAsync($"Deleting '{branch.Name}'...", async ct =>
            {
                // Would any commit be lost? It is safe if it is already in the current branch or, when only
                // the local copy is deleted, if it still exists on the remote.
                var safe = await _deps.Branches.IsMergedIntoAsync(branch, "HEAD", ct)
                           || (!alsoRemote && branch.ExistsOnRemote &&
                               await _deps.Branches.IsMergedIntoAsync(branch, branch.RemoteBranchName, ct));

                if (!safe && !_view.Confirm("Branch with unmerged commits",
                        $"'{branch.Name}' has commits that are not in your current branch" +
                        (alsoRemote || !branch.ExistsOnRemote ? "" : $" or in {branch.RemoteBranchName}") +
                        ". If you delete it, those commits will be lost.\n\nDelete anyway?"))
                    return OperationResult.Ok("Deletion cancelled.");

                var local = await _deps.Branches.DeleteLocalBranchAsync(branch, ct);
                if (!local.Success || !alsoRemote) return local;

                var remote = await _deps.Branches.DeleteRemoteBranchAsync(branch, ct);
                return remote.Success
                    ? OperationResult.Ok($"{local.Message}\n{remote.Message}")
                    : OperationResult.Fail($"{local.Message}\nBut it could not be deleted on the remote:\n{remote.Message}");
            });
        }

        private async Task InitializeRepositoryAsync()
        {
            if (!await CanActAsync()) return;
            await ExecuteAsync("Initializing repository...", _deps.Initializer.InitializeAsync);
        }

        private async Task PublishRepositoryAsync(CreateRepositoryRequest rawRequest)
        {
            if (!await CanActAsync()) return;

            var request = new CreateRepositoryRequest(_deps.RepositoryNames.Sanitize(rawRequest.Name), rawRequest.Description, rawRequest.IsPrivate);
            if (request.Name != rawRequest.Name)
            {
                if (request.Name.Length > 0) Log($"Repository name adjusted to '{request.Name}'.", LogSeverity.Info);
                _view.ShowRepositoryName(request.Name);
                _repositoryNameInput = request.Name;
            }

            if (!_deps.Tokens.HasToken)
            {
                Log($"Set up a {_deps.Host.DisplayName} token in the Account section.", LogSeverity.Error);
                return;
            }

            var validation = request.Validate();
            if (!validation.Success)
            {
                Log(validation.Message, LogSeverity.Error);
                return;
            }

            var visibility = request.IsPrivate ? "private" : "PUBLIC";
            if (!_view.Confirm("Publish project",
                    $"A {visibility} repository '{request.Name}' will be created on {_deps.Host.DisplayName} and the project will be uploaded.\n\nContinue?"))
            {
                Log("Publishing cancelled.", LogSeverity.Info);
                return;
            }

            if (!SaveOpenWork()) return;

            Log($"Publishing '{request.Name}' to {_deps.Host.DisplayName}...", LogSeverity.Info);
            var progress = new Progress<string>(step =>
            {
                if (_disposed) return;
                _view.SetBusy(true, step);
                _view.AppendLog(step, LogSeverity.Info);
            });

            await ExecuteAsync("Publishing...", async ct => await _deps.Publish.ExecuteAsync(request, progress, ct));
        }

        private async Task SaveTokenAsync(string token)
        {
            if (!await CanActAsync()) return;

            if (string.IsNullOrWhiteSpace(token))
            {
                Log("The token is empty.", LogSeverity.Warning);
                return;
            }

            _deps.Tokens.Save(token.Trim());
            await ExecuteAsync("Verifying token...", AuthenticateAsync);
        }

        private async Task ClearTokenAsync()
        {
            if (!await CanActAsync()) return;
            _deps.Tokens.Clear();
            _accountName = null;
            Log("Token removed.", LogSeverity.Info);
            Render();
        }

        // ---------------------------------------------------------------- Infrastructure

        private async Task<OperationResult> AuthenticateAsync(CancellationToken ct)
        {
            var result = await _deps.Host.AuthenticateAsync(ct);
            _accountName = result.Success ? result.Value.Login : null;
            return result;
        }

        /// <summary>
        /// Waits for a background refresh to finish (so the click is not lost) and checks that no
        /// other user operation is running. Never drops an action without telling the user.
        /// </summary>
        private async Task<bool> CanActAsync()
        {
            if (_disposed) return false;

            if (!_backgroundRefresh.IsCompleted)
            {
                try { await _backgroundRefresh; }
                catch (Exception) { /* The error is already logged by Run(). */ }
            }

            if (_isBusy)
            {
                Log("Another operation is in progress. Wait for it to finish or press Cancel.", LogSeverity.Warning);
                return false;
            }

            return !_disposed;
        }

        private bool SaveOpenWork()
        {
            if (_deps.Assets.SaveOpenWork()) return true;
            Log("Operation cancelled: the open scenes were not saved.", LogSeverity.Warning);
            return false;
        }

        /// <summary>Entry point for every event: any exception ends up in the log.</summary>
        private async void Run(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
                Log("Operation cancelled.", LogSeverity.Warning);
            }
            catch (Exception exception)
            {
                Log($"Unexpected error: {exception.GetType().Name}: {exception.Message}", LogSeverity.Error);
            }
        }

        /// <summary>
        /// Common template for every operation: blocks the UI, allows cancelling, logs the
        /// result and reloads the state. Returns true if the operation succeeded.
        /// </summary>
        private async Task<bool> ExecuteAsync(string busyMessage, Func<CancellationToken, Task<OperationResult>> operation, bool touchesWorkingTree = false)
        {
            if (_disposed) return false;

            if (_isBusy)
            {
                Log("Another operation is in progress. Wait for it to finish or press Cancel.", LogSeverity.Warning);
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
                Log("Operation cancelled.", LogSeverity.Warning);
            }
            catch (Exception exception)
            {
                succeeded = false;
                Log($"Unexpected error: {exception.GetType().Name}: {exception.Message}", LogSeverity.Error);
            }
            finally
            {
                // Always refresh: a pull with conflicts also changes files.
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

        // ---------------------------------------------------------------- Name previews

        private NamePreview BuildRepositoryPreview()
        {
            var raw = _repositoryNameInput ?? _deps.Project.Name;
            var name = _deps.RepositoryNames.Sanitize(raw);
            if (name.Length == 0)
                return NamePreview.Invalid("Type a name: only letters, numbers, '.', '-' and '_'.");

            var owner = _accountName ?? "your-username";
            var note = name != raw.Trim() ? $"It will be created as '{name}' (extra dashes and dots are removed)." : null;
            return NamePreview.Valid($"It will appear on {_deps.Host.DisplayName} as:", $"{owner} / {name}",
                _deps.Host.BuildRepositoryUrl(owner, name), note);
        }

        private NamePreview BuildBranchPreview()
        {
            if (string.IsNullOrWhiteSpace(_branchNameInput)) return null;

            var name = _deps.BranchNames.Sanitize(_branchNameInput);
            if (name.Length == 0)
                return NamePreview.Invalid("That is not a valid branch name.");

            var notes = new List<string>();
            if (name != _branchNameInput.Trim()) notes.Add($"It will be created as '{name}'.");
            if (_branchList.Any(b => b.LocalName == name)) notes.Add("A branch with this name already exists.");

            if (!_status.HasRemote)
            {
                notes.Add($"This is a local branch: it will appear on {_deps.Host.DisplayName} once you publish the repository and push.");
                return NamePreview.Valid("The branch will be created as:", name, null, string.Join(" ", notes));
            }

            notes.Add($"It will appear on {_deps.Host.DisplayName} after its first push.");
            var repositoryUrl = RemoteUrlFormatter.ToWebUrl(_status.RemoteUrl);
            return NamePreview.Valid($"It will appear on {_deps.Host.DisplayName} as:", name,
                _deps.Host.BuildBranchUrl(repositoryUrl, name), string.Join(" ", notes));
        }

        private bool RequireRemote()
        {
            if (_status.HasRemote) return true;
            Log("The repository has no 'origin' remote. Publish it first.", LogSeverity.Warning);
            return false;
        }

        private void Log(string message, LogSeverity severity)
        {
            if (_disposed || string.IsNullOrWhiteSpace(message)) return;
            _view.AppendLog(message, severity);
        }
    }
}

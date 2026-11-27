using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using QuickGit.Composition;
using QuickGit.Git;
using QuickGit.Hosting;
using QuickGit.Presentation;

namespace QuickGit.Views
{
    /// <summary>
    /// Passive view (MVP): draws the state it receives and turns clicks into events.
    /// It never calls git or GitHub and makes no decisions.
    /// </summary>
    public sealed class QuickGitWindow : EditorWindow, IQuickGitView
    {
        private const int MaxLogEntries = 200;
        private const int MaxVisibleChanges = 500;
        private const int MaxChangeRowsBeforeScroll = 12;
        private static readonly string[] PullStrategyLabels = { "Fast-forward only (safe)", "Merge", "Rebase" };

        public event Action RefreshRequested;
        public event Action BackgroundRefreshRequested;
        public event Action CancelRequested;
        public event Action FetchRequested;
        public event Action<PullStrategy> PullRequested;
        public event Action PushRequested;
        public event Action<CommitMessage> CommitRequested;
        public event Action<GitBranch> CheckoutRequested;
        public event Action<string> CreateBranchRequested;
        public event Action<GitBranch> DeleteBranchRequested;
        public event Action<string> BranchNameEdited;
        public event Action<string> RepositoryNameEdited;
        public event Action InitializeRepositoryRequested;
        public event Action<CreateRepositoryRequest> PublishRepositoryRequested;
        public event Action<string> SaveTokenRequested;
        public event Action ClearTokenRequested;
        public event Action OpenRemoteRequested;

        [Serializable]
        private struct LogEntry
        {
            public string Time;
            public string Message;
            public LogSeverity Severity;
        }

        private QuickGitPresenter _presenter;
        private QuickGitViewState _state = QuickGitViewState.Empty;
        private bool _isBusy;
        private string _busyMessage;
        private string[] _branchLabels = Array.Empty<string>();

        // User input: serialized so it survives recompiles.
        [SerializeField] private List<LogEntry> _log = new List<LogEntry>();
        [SerializeField] private string _commitTitle = string.Empty;
        [SerializeField] private string _commitDescription = string.Empty;
        [SerializeField] private bool _changesFoldout = true;
        [SerializeField] private bool _hideMetaFiles = true;
        [SerializeField] private string _newBranchName = string.Empty;
        [SerializeField] private string _repositoryName = string.Empty;
        [SerializeField] private bool _repositoryNameInitialized;
        [SerializeField] private string _repositoryDescription = string.Empty;
        [SerializeField] private bool _repositoryPrivate = true;
        [SerializeField] private PullStrategy _pullStrategy = PullStrategy.FastForwardOnly;
        [SerializeField] private bool _accountFoldout = true;
        [SerializeField] private bool _settingsFoldout;
        [SerializeField] private int _deleteBranchIndex;
        private string _tokenInput = string.Empty; // Never serialized.
        private string _gitExecutableInput;

        private Vector2 _scroll;
        private Vector2 _changesScroll;
        private Vector2 _logScroll;
        // [NonSerialized]: on recompile Unity keeps the window's private fields. Without this,
        // the styles from the previous version survive and the new ones stay null.
        [NonSerialized] private bool _stylesReady;
        [NonSerialized] private GUIStyle _logInfoStyle, _logSuccessStyle, _logWarningStyle, _logErrorStyle;
        [NonSerialized] private GUIStyle _placeholderStyle, _changePathStyle;
        [NonSerialized] private Dictionary<FileChangeKind, GUIStyle> _changeBadgeStyles;

        [MenuItem("Tools/Quick Git")]
        public static void Open()
        {
            var window = GetWindow<QuickGitWindow>();
            window.minSize = new Vector2(380, 460);
        }

        // ---------------------------------------------------------------- Lifecycle

        private void OnEnable()
        {
            // Set here (not only in Open) so windows already docked in a saved layout get the new name too.
            titleContent = new GUIContent("Quick Git");
            _gitExecutableInput = EditorPrefs.GetString(QuickGitComposer.GitExecutablePrefKey, "git");
            _presenter = QuickGitComposer.Compose(this);
            _presenter.Initialize();

            // After a recompile the typed text survives: hand it to the presenter
            // so the preview matches what is in the fields.
            if (_repositoryNameInitialized) RepositoryNameEdited?.Invoke(_repositoryName);
            if (!string.IsNullOrEmpty(_newBranchName)) BranchNameEdited?.Invoke(_newBranchName);
        }

        private void OnDisable()
        {
            _presenter?.Dispose();
            _presenter = null;
        }

        private void OnFocus() => BackgroundRefreshRequested?.Invoke();

        // ---------------------------------------------------------------- IQuickGitView

        public void Render(QuickGitViewState state)
        {
            _state = state ?? QuickGitViewState.Empty;

            // Only the first time: if the user clears the field, it is not refilled.
            if (!_repositoryNameInitialized && !string.IsNullOrEmpty(_state.DefaultRepositoryName))
            {
                _repositoryName = _state.DefaultRepositoryName;
                _repositoryNameInitialized = true;
            }

            _branchLabels = _state.Branches.Select(ToLabel).ToArray();

            Repaint();
        }

        public void SetBusy(bool isBusy, string message)
        {
            _isBusy = isBusy;
            _busyMessage = message;
            Repaint();
        }

        public void AppendLog(string message, LogSeverity severity)
        {
            _log.Add(new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Message = message, Severity = severity });
            if (_log.Count > MaxLogEntries) _log.RemoveAt(0);
            _logScroll.y = float.MaxValue;

            if (severity == LogSeverity.Error) Debug.LogWarning($"[Quick Git] {message}");
            Repaint();
        }

        public bool Confirm(string title, string message) =>
            EditorUtility.DisplayDialog(title, message, "Continue", "Cancel");

        public DialogChoice Choose(string title, string message, string primary, string alternative)
        {
            // DisplayDialogComplex returns 0 = ok, 1 = cancel, 2 = alt.
            switch (EditorUtility.DisplayDialogComplex(title, message, primary, "Cancel", alternative))
            {
                case 0: return DialogChoice.Primary;
                case 2: return DialogChoice.Alternative;
                default: return DialogChoice.Cancel;
            }
        }

        public void ClearCommitMessage()
        {
            _commitTitle = string.Empty;
            _commitDescription = string.Empty;
            GUIUtility.keyboardControl = 0; // Otherwise the focused TextArea keeps the old text.
            Repaint();
        }

        public void ClearNewBranchName()
        {
            _newBranchName = string.Empty;
            GUIUtility.keyboardControl = 0;
            Repaint();
        }

        public void ShowBranchName(string name)
        {
            _newBranchName = name ?? string.Empty;
            Repaint();
        }

        public void ShowRepositoryName(string name)
        {
            _repositoryName = name ?? string.Empty;
            Repaint();
        }

        // ---------------------------------------------------------------- Drawing

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUI.DisabledScope(_isBusy))
            {
                switch (_state.Status.State)
                {
                    case RepositoryState.Unknown:
                        EditorGUILayout.HelpBox("Analyzing the project...", MessageType.None);
                        break;
                    case RepositoryState.GitNotInstalled:
                        DrawGitNotInstalled();
                        break;
                    case RepositoryState.NotARepository:
                        DrawNoRepository();
                        break;
                    case RepositoryState.Ready:
                        DrawRepository();
                        break;
                }

                EditorGUILayout.Space(8);
                DrawAccount();
                DrawSettings();
            }
            EditorGUILayout.EndScrollView();

            DrawBusyBar();
            DrawLog();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var title = _state.Status.IsRepository ? $"⎇ {_state.Status.CurrentBranchLabel}" : "Quick Git";
                GUILayout.Label(title, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(_isBusy))
                {
                    if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                        RefreshRequested?.Invoke();
                }
            }
        }

        private void DrawGitNotInstalled()
        {
            EditorGUILayout.HelpBox(
                "Git was not found. Install it and restart Unity, or set the executable path in Settings.\n\n" +
                _state.Status.ErrorMessage, MessageType.Error);

            if (GUILayout.Button("Download Git"))
                Application.OpenURL("https://git-scm.com/downloads");

            _settingsFoldout = true;
        }

        private void DrawNoRepository()
        {
            EditorGUILayout.HelpBox("This project is not inside any Git repository.", MessageType.Warning);
            DrawPublishForm("Create repository and publish");

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Only initialize a local repository (no GitHub)"))
                InitializeRepositoryRequested?.Invoke();
        }

        private void DrawRepository()
        {
            var status = _state.Status;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Repository", status.RootPath);
                EditorGUILayout.LabelField("Current branch", status.CurrentBranchLabel);
                EditorGUILayout.LabelField("Remote", status.HasRemote ? status.RemoteUrl : "(no remote)");
                EditorGUILayout.LabelField("Local changes", status.ChangedFiles.ToString());
                EditorGUILayout.LabelField("Sync",
                    status.HasUpstream ? $"↑ {status.Ahead} to push   ↓ {status.Behind} to pull" : "Branch has no upstream");

                if (status.HasRemote && GUILayout.Button("Open in browser"))
                    OpenRemoteRequested?.Invoke();
            }

            if (!status.HasRemote)
            {
                EditorGUILayout.HelpBox("The local repository is not linked to any remote.", MessageType.Info);
                DrawPublishForm("Publish to " + _state.HostName);
            }

            DrawSync(status);
            DrawChanges(status);
            DrawCommit(status);
            DrawBranches();
        }

        private void DrawPublishForm(string buttonLabel)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"New repository on {_state.HostName}", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var editedName = LiveTextField("Name", _repositoryName);
                if (editedName != _repositoryName)
                {
                    _repositoryName = editedName;
                    _repositoryNameInitialized = true;
                    RepositoryNameEdited?.Invoke(editedName); // The presenter fixes it if needed.
                }

                DrawNamePreview(_state.RepositoryPreview);

                _repositoryDescription = EditorGUILayout.TextField("Description", _repositoryDescription);
                _repositoryPrivate = EditorGUILayout.Toggle("Private", _repositoryPrivate);

                if (!_state.HasToken)
                    EditorGUILayout.HelpBox("Add a token in the Account section first.", MessageType.Info);

                using (new EditorGUI.DisabledScope(!_state.HasToken))
                {
                    if (GUILayout.Button(buttonLabel, GUILayout.Height(26)))
                        PublishRepositoryRequested?.Invoke(
                            new CreateRepositoryRequest(_repositoryName, _repositoryDescription, _repositoryPrivate));
                }
            }
        }

        private void DrawSync(RepositoryStatus status)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Sync", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!status.HasRemote))
            {
                _pullStrategy = (PullStrategy)EditorGUILayout.Popup("Pull strategy", (int)_pullStrategy, PullStrategyLabels);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Fetch", GUILayout.Height(24)))
                        FetchRequested?.Invoke();

                    if (GUILayout.Button(status.Behind > 0 ? $"Pull ↓{status.Behind}" : "Pull", GUILayout.Height(24)))
                        PullRequested?.Invoke(_pullStrategy);

                    using (new EditorGUI.DisabledScope(_state.PushBlockedReason != null))
                    {
                        if (GUILayout.Button(status.Ahead > 0 ? $"Push ↑{status.Ahead}" : "Push", GUILayout.Height(24)))
                            PushRequested?.Invoke();
                    }
                }
            }

            if (_state.PushBlockedReason != null)
                EditorGUILayout.HelpBox("🔒 " + _state.PushBlockedReason, MessageType.Error);
            else if (_state.IsCurrentBranchProtected)
                EditorGUILayout.HelpBox($"'{status.CurrentBranch}' is a protected branch: pushes with compile errors are not allowed.", MessageType.None);
        }

        private void DrawChanges(RepositoryStatus status)
        {
            EditorGUILayout.Space(6);
            _changesFoldout = EditorGUILayout.Foldout(_changesFoldout, $"Changed files ({status.ChangedFiles})", true, EditorStyles.foldoutHeader);
            if (!_changesFoldout) return;

            if (status.ChangedFiles == 0)
            {
                EditorGUILayout.HelpBox("No changes since the last commit.", MessageType.None);
                return;
            }

            var metaCount = status.Changes.Count(c => c.IsMetaFile);
            _hideMetaFiles = EditorGUILayout.ToggleLeft($"Hide .meta files ({metaCount})", _hideMetaFiles);

            var visible = status.Changes.Where(c => !_hideMetaFiles || !c.IsMetaFile).ToList();

            // Height based on the real row height: up to 12 visible rows, the rest scrolls.
            var rowHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var rows = Mathf.Clamp(visible.Count, 1, MaxChangeRowsBeforeScroll);
            var height = rows * rowHeight + 10f;

            _changesScroll = EditorGUILayout.BeginScrollView(_changesScroll, false, visible.Count > MaxChangeRowsBeforeScroll,
                GUIStyle.none, GUI.skin.verticalScrollbar, EditorStyles.helpBox, GUILayout.Height(height));
            foreach (var change in visible.Take(MaxVisibleChanges))
            {
                using (new EditorGUILayout.HorizontalScope(GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                {
                    GUILayout.Label(new GUIContent(BadgeFor(change.Kind), KindName(change.Kind)), _changeBadgeStyles[change.Kind], GUILayout.Width(18));
                    var path = change.OldPath != null ? $"{change.OldPath} → {change.Path}" : change.Path;
                    GUILayout.Label(new GUIContent(path, path), _changePathStyle);
                }
            }

            if (visible.Count > MaxVisibleChanges)
                GUILayout.Label($"... and {visible.Count - MaxVisibleChanges} more", _placeholderStyle);
            EditorGUILayout.EndScrollView();

            var hiddenMeta = _hideMetaFiles ? metaCount : 0;
            var summary = $"Showing {Math.Min(visible.Count, MaxVisibleChanges)} of {status.ChangedFiles}";
            if (hiddenMeta > 0) summary += $" ({hiddenMeta} .meta hidden)";
            if (visible.Count > MaxChangeRowsBeforeScroll) summary += " · scroll to see the rest";
            GUILayout.Label(summary, _placeholderStyle);
        }

        private void DrawCommit(RepositoryStatus status)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Commit", EditorStyles.boldLabel);

            if (_state.HasCompileErrors)
                EditorGUILayout.HelpBox(
                    "The project has compile errors. You can commit, but the commit will be marked " +
                    "and cannot be pushed to main/master (other branches are fine).", MessageType.Warning);

            _commitTitle = PlaceholderField(EditorGUILayout.TextField(_commitTitle), "Title (short summary of the change)");

            _commitDescription = EditorGUILayout.TextArea(_commitDescription, GUILayout.MinHeight(54));
            DrawPlaceholder(_commitDescription, "Description (optional): what you changed and why");

            using (new EditorGUI.DisabledScope(status.ChangedFiles == 0 || string.IsNullOrWhiteSpace(_commitTitle)))
            {
                if (GUILayout.Button($"Commit {status.ChangedFiles} change(s)"))
                    CommitRequested?.Invoke(new CommitMessage(_commitTitle, _commitDescription));
            }
        }

        private void DrawBranches()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Branches", EditorStyles.boldLabel);

            if (_branchLabels.Length == 0)
            {
                EditorGUILayout.HelpBox("No branches yet (make the first commit).", MessageType.None);
            }
            else
            {
                // The dropdown always shows the current branch. Picking another one checks it out right away;
                // if that fails or is cancelled, the next Render shows the branch you are on again.
                var currentIndex = _state.Branches.ToList().FindIndex(b => b.IsCurrent);
                var selectedIndex = EditorGUILayout.Popup("Current branch", currentIndex, _branchLabels);

                if (selectedIndex != currentIndex && selectedIndex >= 0)
                    CheckoutRequested?.Invoke(_state.Branches[selectedIndex]);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var editedName = LiveTextField("New branch", _newBranchName);
                if (editedName != _newBranchName)
                {
                    _newBranchName = editedName;
                    BranchNameEdited?.Invoke(editedName); // The presenter replaces spaces, etc.
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newBranchName)))
                {
                    if (GUILayout.Button("Create", GUILayout.Width(80)))
                        CreateBranchRequested?.Invoke(_newBranchName);
                }
            }

            DrawNamePreview(_state.BranchPreview);

            DrawDeleteBranch();
        }

        private void DrawDeleteBranch()
        {
            var deletable = _state.DeletableBranches;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (deletable.Count == 0)
                {
                    EditorGUILayout.LabelField("Delete branch", "(no branches can be deleted)", _placeholderStyle);
                    return;
                }

                _deleteBranchIndex = Mathf.Clamp(_deleteBranchIndex, 0, deletable.Count - 1);
                _deleteBranchIndex = EditorGUILayout.Popup("Delete branch", _deleteBranchIndex, deletable.Select(ToLabel).ToArray());

                if (GUILayout.Button("Delete", GUILayout.Width(80)))
                    DeleteBranchRequested?.Invoke(deletable[_deleteBranchIndex]);
            }

            EditorGUILayout.LabelField("main and master are protected: they cannot be deleted from here.", _placeholderStyle);
        }

        private void DrawAccount()
        {
            _accountFoldout = EditorGUILayout.Foldout(_accountFoldout, $"{_state.HostName} account", true);
            if (!_accountFoldout) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_state.IsAuthenticated)
                    EditorGUILayout.LabelField("Connected as", _state.AccountName, EditorStyles.boldLabel);
                else if (_state.HasToken)
                    EditorGUILayout.HelpBox("A token is saved but it could not be verified.", MessageType.Warning);

                _tokenInput = EditorGUILayout.PasswordField("Personal token", _tokenInput);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_tokenInput)))
                    {
                        if (GUILayout.Button("Save token"))
                        {
                            SaveTokenRequested?.Invoke(_tokenInput);
                            _tokenInput = string.Empty;
                            GUIUtility.keyboardControl = 0;
                        }
                    }

                    using (new EditorGUI.DisabledScope(!_state.HasToken))
                    {
                        if (GUILayout.Button("Remove token"))
                            ClearTokenRequested?.Invoke();
                    }
                }

                if (!string.IsNullOrEmpty(_state.TokenCreationUrl) &&
                    GUILayout.Button("Create a token ('repo' scope) →", EditorStyles.linkLabel))
                    Application.OpenURL(_state.TokenCreationUrl);

                EditorGUILayout.HelpBox(
                    "The token is stored in this machine's EditorPrefs (unencrypted), never inside the project. " +
                    "It is used to create repositories and to authenticate pull/push over HTTPS.", MessageType.None);
            }
        }

        private void DrawSettings()
        {
            _settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "Settings", true);
            if (!_settingsFoldout) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _gitExecutableInput = EditorGUILayout.TextField("Git executable", _gitExecutableInput);
                EditorGUILayout.HelpBox(
                    "Usually 'git'. On macOS, if Unity does not inherit your PATH, use e.g. /opt/homebrew/bin/git.",
                    MessageType.None);

                if (GUILayout.Button("Apply"))
                {
                    EditorPrefs.SetString(QuickGitComposer.GitExecutablePrefKey,
                        string.IsNullOrWhiteSpace(_gitExecutableInput) ? "git" : _gitExecutableInput.Trim());

                    // Rebuilds the dependency graph with the new executable.
                    OnDisable();
                    OnEnable();
                }
            }
        }

        private void DrawBusyBar()
        {
            if (!_isBusy) return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("⏳ " + _busyMessage, EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                    CancelRequested?.Invoke();
            }
        }

        private void DrawLog()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(60)))
                    _log.Clear();
            }

            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, EditorStyles.helpBox, GUILayout.Height(130));
            foreach (var entry in _log)
                EditorGUILayout.LabelField($"[{entry.Time}] {entry.Message}", StyleFor(entry.Severity));
            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------- Helpers

        private static string ToLabel(GitBranch branch)
        {
            // "/" in a Popup creates submenus: Local/feature/x, Remote/origin/main...
            var prefix = branch.IsRemote ? "Remote/" : "Local/";
            var suffix = branch.IsCurrent ? "   ✓" : string.Empty;
            if (!branch.IsRemote && branch.ExistsOnRemote) suffix += "   (also on remote)";
            return prefix + branch.Name + suffix;
        }

        /// <summary>
        /// Text field that instantly reflects changes made from code (live fixing).
        /// GUILayout.TextField is used because EditorGUILayout.TextField keeps its own buffer
        /// while focused and would not show the fixed text until focus is lost.
        /// </summary>
        private static string LiveTextField(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                return GUILayout.TextField(value ?? string.Empty, EditorStyles.textField);
            }
        }

        private string PlaceholderField(string value, string placeholder)
        {
            DrawPlaceholder(value, placeholder);
            return value;
        }

        private void DrawPlaceholder(string value, string placeholder)
        {
            if (!string.IsNullOrEmpty(value)) return;
            var rect = GUILayoutUtility.GetLastRect();
            rect.x += 4;
            GUI.Label(rect, placeholder, _placeholderStyle);
        }

        /// <summary>Box under a name field showing how it will look on GitHub (computed by the presenter).</summary>
        private static void DrawNamePreview(NamePreview preview)
        {
            if (preview == null) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!preview.IsValid)
                {
                    EditorGUILayout.LabelField("⚠ " + preview.Note, EditorStyles.wordWrappedMiniLabel);
                    return;
                }

                EditorGUILayout.LabelField(preview.Heading, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(preview.DisplayName, EditorStyles.boldLabel);

                // Selectable so the URL can be copied.
                if (!string.IsNullOrEmpty(preview.Url))
                    EditorGUILayout.SelectableLabel(preview.Url, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));

                if (!string.IsNullOrEmpty(preview.Note))
                    EditorGUILayout.LabelField(preview.Note, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static string KindName(FileChangeKind kind)
        {
            switch (kind)
            {
                case FileChangeKind.Added: return "Added";
                case FileChangeKind.Deleted: return "Deleted";
                case FileChangeKind.Renamed: return "Renamed";
                case FileChangeKind.Untracked: return "Untracked (new)";
                case FileChangeKind.Conflicted: return "Conflicted";
                default: return "Modified";
            }
        }

        private static string BadgeFor(FileChangeKind kind)
        {
            switch (kind)
            {
                case FileChangeKind.Added: return "A";
                case FileChangeKind.Deleted: return "D";
                case FileChangeKind.Renamed: return "R";
                case FileChangeKind.Untracked: return "U";
                case FileChangeKind.Conflicted: return "!";
                default: return "M";
            }
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _changeBadgeStyles = new Dictionary<FileChangeKind, GUIStyle>();

            _placeholderStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } };
            _changePathStyle = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip };

            var badge = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            _changeBadgeStyles[FileChangeKind.Modified] = new GUIStyle(badge) { normal = { textColor = new Color(0.9f, 0.7f, 0.2f) } };
            _changeBadgeStyles[FileChangeKind.Added] = new GUIStyle(badge) { normal = { textColor = new Color(0.35f, 0.75f, 0.4f) } };
            _changeBadgeStyles[FileChangeKind.Untracked] = new GUIStyle(badge) { normal = { textColor = new Color(0.35f, 0.75f, 0.4f) } };
            _changeBadgeStyles[FileChangeKind.Deleted] = new GUIStyle(badge) { normal = { textColor = new Color(0.95f, 0.4f, 0.4f) } };
            _changeBadgeStyles[FileChangeKind.Renamed] = new GUIStyle(badge) { normal = { textColor = new Color(0.4f, 0.7f, 0.95f) } };
            _changeBadgeStyles[FileChangeKind.Conflicted] = new GUIStyle(badge) { normal = { textColor = new Color(1f, 0.3f, 0.9f) } };

            _stylesReady = true; // Last: if something fails halfway, it is retried on the next OnGUI.

            _logInfoStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = false };
            _logSuccessStyle = new GUIStyle(_logInfoStyle) { normal = { textColor = new Color(0.35f, 0.75f, 0.4f) } };
            _logWarningStyle = new GUIStyle(_logInfoStyle) { normal = { textColor = new Color(0.9f, 0.7f, 0.2f) } };
            _logErrorStyle = new GUIStyle(_logInfoStyle) { normal = { textColor = new Color(0.95f, 0.4f, 0.4f) } };
        }

        private GUIStyle StyleFor(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Success: return _logSuccessStyle;
                case LogSeverity.Warning: return _logWarningStyle;
                case LogSeverity.Error: return _logErrorStyle;
                default: return _logInfoStyle;
            }
        }
    }
}

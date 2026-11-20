using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityGitTool.Composition;
using UnityGitTool.Git;
using UnityGitTool.Hosting;
using UnityGitTool.Presentation;

namespace UnityGitTool.Views
{
    /// <summary>
    /// Vista pasiva (MVP): dibuja el estado recibido y convierte clics en eventos.
    /// No llama a git ni a GitHub, no toma decisiones.
    /// </summary>
    public sealed class GitToolWindow : EditorWindow, IGitToolView
    {
        private const int MaxLogEntries = 200;
        private const int MaxVisibleChanges = 300;
        private static readonly string[] PullStrategyLabels = { "Solo fast-forward (seguro)", "Merge", "Rebase" };

        public event Action RefreshRequested;
        public event Action BackgroundRefreshRequested;
        public event Action CancelRequested;
        public event Action FetchRequested;
        public event Action<PullStrategy> PullRequested;
        public event Action PushRequested;
        public event Action<CommitMessage> CommitRequested;
        public event Action<GitBranch> CheckoutRequested;
        public event Action<string> CreateBranchRequested;
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

        private GitToolPresenter _presenter;
        private GitToolViewState _state = GitToolViewState.Empty;
        private bool _isBusy;
        private string _busyMessage;
        private string[] _branchLabels = Array.Empty<string>();

        // Entradas del usuario: se serializan para sobrevivir a recompilaciones.
        [SerializeField] private List<LogEntry> _log = new List<LogEntry>();
        [SerializeField] private string _commitTitle = string.Empty;
        [SerializeField] private string _commitDescription = string.Empty;
        [SerializeField] private bool _changesFoldout = true;
        [SerializeField] private bool _hideMetaFiles = true;
        [SerializeField] private string _newBranchName = string.Empty;
        [SerializeField] private string _repositoryName = string.Empty;
        [SerializeField] private string _repositoryDescription = string.Empty;
        [SerializeField] private bool _repositoryPrivate = true;
        [SerializeField] private PullStrategy _pullStrategy = PullStrategy.FastForwardOnly;
        [SerializeField] private bool _accountFoldout = true;
        [SerializeField] private bool _settingsFoldout;
        private string _tokenInput = string.Empty; // Nunca se serializa.
        private string _gitExecutableInput;

        private Vector2 _scroll;
        private Vector2 _changesScroll;
        private Vector2 _logScroll;
        // [NonSerialized]: al recompilar, Unity conserva los campos privados de la ventana. Sin esto,
        // los estilos de la versión anterior sobreviven y los nuevos se quedan a null.
        [NonSerialized] private bool _stylesReady;
        [NonSerialized] private GUIStyle _logInfoStyle, _logSuccessStyle, _logWarningStyle, _logErrorStyle;
        [NonSerialized] private GUIStyle _placeholderStyle, _changePathStyle;
        [NonSerialized] private Dictionary<FileChangeKind, GUIStyle> _changeBadgeStyles;

        [MenuItem("Tools/Git Tool")]
        public static void Open()
        {
            var window = GetWindow<GitToolWindow>();
            window.titleContent = new GUIContent("Git Tool");
            window.minSize = new Vector2(380, 460);
        }

        // ---------------------------------------------------------------- Ciclo de vida

        private void OnEnable()
        {
            _gitExecutableInput = EditorPrefs.GetString(GitToolComposer.GitExecutablePrefKey, "git");
            _presenter = GitToolComposer.Compose(this);
            _presenter.Initialize();
        }

        private void OnDisable()
        {
            _presenter?.Dispose();
            _presenter = null;
        }

        private void OnFocus() => BackgroundRefreshRequested?.Invoke();

        // ---------------------------------------------------------------- IGitToolView

        public void Render(GitToolViewState state)
        {
            _state = state ?? GitToolViewState.Empty;

            if (string.IsNullOrEmpty(_repositoryName))
                _repositoryName = _state.DefaultRepositoryName;

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

            if (severity == LogSeverity.Error) Debug.LogWarning($"[Git Tool] {message}");
            Repaint();
        }

        public bool Confirm(string title, string message) =>
            EditorUtility.DisplayDialog(title, message, "Continuar", "Cancelar");

        public void ClearCommitMessage()
        {
            _commitTitle = string.Empty;
            _commitDescription = string.Empty;
            GUIUtility.keyboardControl = 0; // Si no, el TextArea con foco conserva el texto viejo.
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

        // ---------------------------------------------------------------- Dibujo

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
                        EditorGUILayout.HelpBox("Analizando el proyecto...", MessageType.None);
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
                var title = _state.Status.IsRepository ? $"⎇ {_state.Status.CurrentBranchLabel}" : "Git Tool";
                GUILayout.Label(title, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(_isBusy))
                {
                    if (GUILayout.Button("Refrescar", EditorStyles.toolbarButton))
                        RefreshRequested?.Invoke();
                }
            }
        }

        private void DrawGitNotInstalled()
        {
            EditorGUILayout.HelpBox(
                "No se encuentra git. Instálalo y reinicia Unity, o indica la ruta del ejecutable en Configuración.\n\n" +
                _state.Status.ErrorMessage, MessageType.Error);

            if (GUILayout.Button("Descargar Git"))
                Application.OpenURL("https://git-scm.com/downloads");

            _settingsFoldout = true;
        }

        private void DrawNoRepository()
        {
            EditorGUILayout.HelpBox("Este proyecto no está dentro de ningún repositorio Git.", MessageType.Warning);
            DrawPublishForm("Crear repositorio y publicar");

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Solo inicializar repositorio local (sin GitHub)"))
                InitializeRepositoryRequested?.Invoke();
        }

        private void DrawRepository()
        {
            var status = _state.Status;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Repositorio", status.RootPath);
                EditorGUILayout.LabelField("Rama actual", status.CurrentBranchLabel);
                EditorGUILayout.LabelField("Remoto", status.HasRemote ? status.RemoteUrl : "(sin remoto)");
                EditorGUILayout.LabelField("Cambios locales", status.ChangedFiles.ToString());
                EditorGUILayout.LabelField("Sincronización",
                    status.HasUpstream ? $"↑ {status.Ahead} por subir   ↓ {status.Behind} por bajar" : "Rama sin upstream");

                if (status.HasRemote && GUILayout.Button("Abrir en el navegador"))
                    OpenRemoteRequested?.Invoke();
            }

            if (!status.HasRemote)
            {
                EditorGUILayout.HelpBox("El repositorio local no está vinculado a ningún remoto.", MessageType.Info);
                DrawPublishForm("Publicar en " + _state.HostName);
            }

            DrawSync(status);
            DrawChanges(status);
            DrawCommit(status);
            DrawBranches();
        }

        private void DrawPublishForm(string buttonLabel)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Nuevo repositorio en {_state.HostName}", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var editedName = LiveTextField("Nombre", _repositoryName);
                if (editedName != _repositoryName)
                {
                    _repositoryName = editedName;
                    RepositoryNameEdited?.Invoke(editedName); // El presentador lo corrige si hace falta.
                }
                _repositoryDescription = EditorGUILayout.TextField("Descripción", _repositoryDescription);
                _repositoryPrivate = EditorGUILayout.Toggle("Privado", _repositoryPrivate);

                if (!_state.HasToken)
                    EditorGUILayout.HelpBox("Primero añade un token en la sección Cuenta.", MessageType.Info);

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
            EditorGUILayout.LabelField("Sincronizar", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!status.HasRemote))
            {
                _pullStrategy = (PullStrategy)EditorGUILayout.Popup("Estrategia de pull", (int)_pullStrategy, PullStrategyLabels);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Fetch", GUILayout.Height(24)))
                        FetchRequested?.Invoke();

                    if (GUILayout.Button(status.Behind > 0 ? $"Pull ↓{status.Behind}" : "Pull", GUILayout.Height(24)))
                        PullRequested?.Invoke(_pullStrategy);

                    if (GUILayout.Button(status.Ahead > 0 ? $"Push ↑{status.Ahead}" : "Push", GUILayout.Height(24)))
                        PushRequested?.Invoke();
                }
            }
        }

        private void DrawChanges(RepositoryStatus status)
        {
            EditorGUILayout.Space(6);
            _changesFoldout = EditorGUILayout.Foldout(_changesFoldout, $"Archivos cambiados ({status.ChangedFiles})", true, EditorStyles.foldoutHeader);
            if (!_changesFoldout) return;

            if (status.ChangedFiles == 0)
            {
                EditorGUILayout.HelpBox("No hay cambios desde el último commit.", MessageType.None);
                return;
            }

            var metaCount = status.Changes.Count(c => c.IsMetaFile);
            _hideMetaFiles = EditorGUILayout.ToggleLeft($"Ocultar archivos .meta ({metaCount})", _hideMetaFiles);

            var visible = status.Changes.Where(c => !_hideMetaFiles || !c.IsMetaFile).ToList();
            var height = Mathf.Clamp(visible.Count * 18f + 8f, 30f, 180f);

            _changesScroll = EditorGUILayout.BeginScrollView(_changesScroll, EditorStyles.helpBox, GUILayout.Height(height));
            foreach (var change in visible.Take(MaxVisibleChanges))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(BadgeFor(change.Kind), _changeBadgeStyles[change.Kind], GUILayout.Width(18));
                    var path = change.OldPath != null ? $"{change.OldPath} → {change.Path}" : change.Path;
                    GUILayout.Label(new GUIContent(path, path), _changePathStyle);
                }
            }

            if (visible.Count > MaxVisibleChanges)
                GUILayout.Label($"... y {visible.Count - MaxVisibleChanges} más", _placeholderStyle);
            EditorGUILayout.EndScrollView();
        }

        private void DrawCommit(RepositoryStatus status)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Commit", EditorStyles.boldLabel);

            _commitTitle = PlaceholderField(EditorGUILayout.TextField(_commitTitle), "Título (resumen corto del cambio)");

            _commitDescription = EditorGUILayout.TextArea(_commitDescription, GUILayout.MinHeight(54));
            DrawPlaceholder(_commitDescription, "Descripción (opcional): qué has cambiado y por qué");

            using (new EditorGUI.DisabledScope(status.ChangedFiles == 0 || string.IsNullOrWhiteSpace(_commitTitle)))
            {
                if (GUILayout.Button($"Commit de {status.ChangedFiles} cambio(s)"))
                    CommitRequested?.Invoke(new CommitMessage(_commitTitle, _commitDescription));
            }
        }

        private void DrawBranches()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Ramas", EditorStyles.boldLabel);

            if (_branchLabels.Length == 0)
            {
                EditorGUILayout.HelpBox("Aún no hay ramas (haz el primer commit).", MessageType.None);
            }
            else
            {
                // El desplegable siempre muestra la rama actual. Elegir otra hace el checkout al momento;
                // si falla o se cancela, el siguiente Render vuelve a mostrar la rama en la que estás.
                var currentIndex = _state.Branches.ToList().FindIndex(b => b.IsCurrent);
                var selectedIndex = EditorGUILayout.Popup("Rama actual", currentIndex, _branchLabels);

                if (selectedIndex != currentIndex && selectedIndex >= 0)
                    CheckoutRequested?.Invoke(_state.Branches[selectedIndex]);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var editedName = LiveTextField("Nueva rama", _newBranchName);
                if (editedName != _newBranchName)
                {
                    _newBranchName = editedName;
                    BranchNameEdited?.Invoke(editedName); // El presentador cambia espacios, etc.
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newBranchName)))
                {
                    if (GUILayout.Button("Crear", GUILayout.Width(80)))
                        CreateBranchRequested?.Invoke(_newBranchName);
                }
            }
        }

        private void DrawAccount()
        {
            _accountFoldout = EditorGUILayout.Foldout(_accountFoldout, $"Cuenta de {_state.HostName}", true);
            if (!_accountFoldout) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_state.IsAuthenticated)
                    EditorGUILayout.LabelField("Conectado como", _state.AccountName, EditorStyles.boldLabel);
                else if (_state.HasToken)
                    EditorGUILayout.HelpBox("Hay un token guardado pero no se ha podido verificar.", MessageType.Warning);

                _tokenInput = EditorGUILayout.PasswordField("Token personal", _tokenInput);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_tokenInput)))
                    {
                        if (GUILayout.Button("Guardar token"))
                        {
                            SaveTokenRequested?.Invoke(_tokenInput);
                            _tokenInput = string.Empty;
                            GUIUtility.keyboardControl = 0;
                        }
                    }

                    using (new EditorGUI.DisabledScope(!_state.HasToken))
                    {
                        if (GUILayout.Button("Eliminar token"))
                            ClearTokenRequested?.Invoke();
                    }
                }

                if (!string.IsNullOrEmpty(_state.TokenCreationUrl) &&
                    GUILayout.Button("Crear un token (scope 'repo') →", EditorStyles.linkLabel))
                    Application.OpenURL(_state.TokenCreationUrl);

                EditorGUILayout.HelpBox(
                    "El token se guarda en EditorPrefs de este equipo (sin cifrar), nunca dentro del proyecto. " +
                    "Se usa para crear repositorios y para autenticar pull/push por HTTPS.", MessageType.None);
            }
        }

        private void DrawSettings()
        {
            _settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "Configuración", true);
            if (!_settingsFoldout) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _gitExecutableInput = EditorGUILayout.TextField("Ejecutable de git", _gitExecutableInput);
                EditorGUILayout.HelpBox(
                    "Normalmente 'git'. En macOS, si Unity no hereda el PATH, usa p. ej. /opt/homebrew/bin/git.",
                    MessageType.None);

                if (GUILayout.Button("Aplicar"))
                {
                    EditorPrefs.SetString(GitToolComposer.GitExecutablePrefKey,
                        string.IsNullOrWhiteSpace(_gitExecutableInput) ? "git" : _gitExecutableInput.Trim());

                    // Recompone el grafo de dependencias con el nuevo ejecutable.
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
                if (GUILayout.Button("Cancelar", GUILayout.Width(80)))
                    CancelRequested?.Invoke();
            }
        }

        private void DrawLog()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Registro", EditorStyles.boldLabel);
                if (GUILayout.Button("Limpiar", EditorStyles.miniButton, GUILayout.Width(60)))
                    _log.Clear();
            }

            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, EditorStyles.helpBox, GUILayout.Height(130));
            foreach (var entry in _log)
                EditorGUILayout.LabelField($"[{entry.Time}] {entry.Message}", StyleFor(entry.Severity));
            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------- Utilidades

        private static string ToLabel(GitBranch branch)
        {
            // "/" en un Popup crea submenús: Local/feature/x, Remoto/origin/main...
            var prefix = branch.IsRemote ? "Remoto/" : "Local/";
            return prefix + branch.Name + (branch.IsCurrent ? "   ✓" : string.Empty);
        }

        /// <summary>
        /// Campo de texto que refleja al instante los cambios hechos desde código (corrección en vivo).
        /// Se usa GUILayout.TextField porque EditorGUILayout.TextField conserva su propio buffer
        /// mientras tiene el foco y no mostraría el texto corregido hasta perderlo.
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

        private static string BadgeFor(FileChangeKind kind)
        {
            switch (kind)
            {
                case FileChangeKind.Added: return "A";
                case FileChangeKind.Deleted: return "D";
                case FileChangeKind.Renamed: return "R";
                case FileChangeKind.Untracked: return "N";
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

            _stylesReady = true; // Al final: si algo falla a medias, se reintenta en el siguiente OnGUI.

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

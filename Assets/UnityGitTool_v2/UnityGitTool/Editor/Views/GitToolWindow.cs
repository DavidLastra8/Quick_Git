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
        private static readonly string[] PullStrategyLabels = { "Solo fast-forward (seguro)", "Merge", "Rebase" };

        public event Action RefreshRequested;
        public event Action BackgroundRefreshRequested;
        public event Action CancelRequested;
        public event Action FetchRequested;
        public event Action<PullStrategy> PullRequested;
        public event Action PushRequested;
        public event Action<string> CommitRequested;
        public event Action<GitBranch> CheckoutRequested;
        public event Action<string> CreateBranchRequested;
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
        [SerializeField] private string _commitMessage = string.Empty;
        [SerializeField] private string _newBranchName = string.Empty;
        [SerializeField] private string _repositoryName = string.Empty;
        [SerializeField] private string _repositoryDescription = string.Empty;
        [SerializeField] private bool _repositoryPrivate = true;
        [SerializeField] private PullStrategy _pullStrategy = PullStrategy.FastForwardOnly;
        [SerializeField] private int _selectedBranchIndex;
        [SerializeField] private bool _accountFoldout = true;
        [SerializeField] private bool _settingsFoldout;
        private string _tokenInput = string.Empty; // Nunca se serializa.
        private string _gitExecutableInput;

        private Vector2 _scroll;
        private Vector2 _logScroll;
        private GUIStyle _logInfoStyle, _logSuccessStyle, _logWarningStyle, _logErrorStyle;

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

            var current = _state.Branches.ToList().FindIndex(b => b.IsCurrent);
            if (_selectedBranchIndex < 0 || _selectedBranchIndex >= _branchLabels.Length)
                _selectedBranchIndex = Math.Max(0, current);

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
            _commitMessage = string.Empty;
            GUIUtility.keyboardControl = 0; // Si no, el TextArea con foco conserva el texto viejo.
            Repaint();
        }

        public void ClearNewBranchName()
        {
            _newBranchName = string.Empty;
            GUIUtility.keyboardControl = 0;
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
            DrawCommit(status);
            DrawBranches();
        }

        private void DrawPublishForm(string buttonLabel)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Nuevo repositorio en {_state.HostName}", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _repositoryName = EditorGUILayout.TextField("Nombre", _repositoryName);
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

        private void DrawCommit(RepositoryStatus status)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Commit ({status.ChangedFiles} cambios)", EditorStyles.boldLabel);

            _commitMessage = EditorGUILayout.TextArea(_commitMessage, GUILayout.MinHeight(38));

            using (new EditorGUI.DisabledScope(status.ChangedFiles == 0 || string.IsNullOrWhiteSpace(_commitMessage)))
            {
                if (GUILayout.Button("Commit de todos los cambios"))
                    CommitRequested?.Invoke(_commitMessage);
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
                using (new EditorGUILayout.HorizontalScope())
                {
                    _selectedBranchIndex = EditorGUILayout.Popup(_selectedBranchIndex, _branchLabels);
                    var selected = _state.Branches[_selectedBranchIndex];

                    using (new EditorGUI.DisabledScope(selected.IsCurrent))
                    {
                        if (GUILayout.Button("Checkout", GUILayout.Width(80)))
                            CheckoutRequested?.Invoke(selected);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _newBranchName = EditorGUILayout.TextField("Nueva rama", _newBranchName);

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

        private void EnsureStyles()
        {
            if (_logInfoStyle != null) return;

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

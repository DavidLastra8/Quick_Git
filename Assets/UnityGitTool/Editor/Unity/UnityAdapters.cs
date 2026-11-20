using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityGitTool.Hosting;

namespace UnityGitTool.Unity
{
    public sealed class UnityProjectInfo : IProjectInfo
    {
        public string RootPath { get; }
        public string Name { get; }

        public UnityProjectInfo()
        {
            // Application.dataPath = <project>/Assets → the root is its parent folder.
            RootPath = Directory.GetParent(Application.dataPath).FullName;
            Name = new DirectoryInfo(RootPath).Name;
        }
    }

    public sealed class UnityEditorAssetSynchronizer : IEditorAssetSynchronizer
    {
        private bool _suspended;

        public bool SaveOpenWork()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            AssetDatabase.SaveAssets();
            return true;
        }

        public void SuspendAutoRefresh()
        {
            if (_suspended) return;
            AssetDatabase.DisallowAutoRefresh();
            _suspended = true;
        }

        public void ResumeAutoRefresh(bool refreshNow)
        {
            if (_suspended)
            {
                AssetDatabase.AllowAutoRefresh();
                _suspended = false;
            }

            if (refreshNow) AssetDatabase.Refresh();
        }
    }

    public sealed class UnityProjectHealthChecker : IProjectHealthChecker
    {
        // True if the last script compilation failed (the red errors in the Console).
        public bool HasCompileErrors => EditorUtility.scriptCompilationFailed;
    }

    public sealed class UnityUrlOpener : IUrlOpener
    {
        public void Open(string url)
        {
            if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
        }
    }

    /// <summary>
    /// Stores the token in EditorPrefs (per machine, shared across projects, outside the repo).
    /// Note: EditorPrefs is not encrypted. For more security, write another IAccessTokenStore
    /// (e.g. Keychain / Credential Manager) and swap it in GitToolComposer.
    /// </summary>
    public sealed class EditorPrefsTokenStore : IAccessTokenStore
    {
        private readonly string _key;

        public EditorPrefsTokenStore(string key) => _key = key;

        public bool HasToken => !string.IsNullOrEmpty(Load());
        public string Load() => EditorPrefs.GetString(_key, string.Empty);
        public void Save(string token) => EditorPrefs.SetString(_key, token ?? string.Empty);
        public void Clear() => EditorPrefs.DeleteKey(_key);
    }
}

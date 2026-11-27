using UnityEditor;

namespace QuickGit.Unity
{
    /// <summary>
    /// The tool's settings used to be saved with "UnityGitTool." keys. Moves them to the
    /// "QuickGit." keys once, so nobody has to paste their token again after the rename.
    /// </summary>
    public static class EditorPrefsMigration
    {
        private static readonly (string OldKey, string NewKey)[] Keys =
        {
            ("UnityGitTool.GitHub.Token", "QuickGit.GitHub.Token"),
            ("UnityGitTool.GitExecutable", "QuickGit.GitExecutable")
        };

        public static void MigrateLegacyKeys()
        {
            foreach (var (oldKey, newKey) in Keys)
            {
                if (!EditorPrefs.HasKey(oldKey)) continue;
                if (!EditorPrefs.HasKey(newKey)) EditorPrefs.SetString(newKey, EditorPrefs.GetString(oldKey));
                EditorPrefs.DeleteKey(oldKey);
            }
        }
    }
}

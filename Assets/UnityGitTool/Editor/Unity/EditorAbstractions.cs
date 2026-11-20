namespace UnityGitTool.Unity
{
    // Abstractions over the Unity API: the presenter does not depend on UnityEditor (DIP),
    // so it can be tested with doubles in an EditMode test or outside Unity.

    public interface IProjectInfo
    {
        string RootPath { get; }
        string Name { get; }
    }

    public interface IEditorAssetSynchronizer
    {
        /// <summary>Saves scenes/assets. Returns false if the user cancels.</summary>
        bool SaveOpenWork();

        /// <summary>Stops Unity from reimporting while git is changing files.</summary>
        void SuspendAutoRefresh();

        void ResumeAutoRefresh(bool refreshNow);
    }

    /// <summary>Project "health" state. Today: compile errors; can be extended (tests, etc.).</summary>
    public interface IProjectHealthChecker
    {
        bool HasCompileErrors { get; }
    }

    public interface IUrlOpener
    {
        void Open(string url);
    }
}

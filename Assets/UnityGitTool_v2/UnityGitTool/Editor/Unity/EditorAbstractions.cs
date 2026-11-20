namespace UnityGitTool.Unity
{
    // Abstracciones de la API de Unity: el presentador no depende de UnityEditor (DIP),
    // lo que permite testearlo con dobles en un EditMode test o fuera de Unity.

    public interface IProjectInfo
    {
        string RootPath { get; }
        string Name { get; }
    }

    public interface IEditorAssetSynchronizer
    {
        /// <summary>Guarda escenas/assets. Devuelve false si el usuario cancela.</summary>
        bool SaveOpenWork();

        /// <summary>Evita que Unity reimporte mientras git está modificando archivos.</summary>
        void SuspendAutoRefresh();

        void ResumeAutoRefresh(bool refreshNow);
    }

    /// <summary>Estado de "salud" del proyecto. Hoy: errores de compilación; ampliable (tests, etc.).</summary>
    public interface IProjectHealthChecker
    {
        bool HasCompileErrors { get; }
    }

    public interface IUrlOpener
    {
        void Open(string url);
    }
}

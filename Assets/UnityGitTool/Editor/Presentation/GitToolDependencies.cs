using System;
using UnityGitTool.Core;
using UnityGitTool.Git;
using UnityGitTool.Hosting;
using UnityGitTool.UseCases;
using UnityGitTool.Unity;

namespace UnityGitTool.Presentation
{
    /// <summary>
    /// Agrupa las dependencias del presentador (parameter object). Evita un constructor con
    /// 15 parámetros y deja claro, en un solo sitio, todo lo que el presentador necesita.
    /// </summary>
    public sealed class GitToolDependencies
    {
        public IGitRepositoryInspector Inspector { get; set; }
        public IGitRepositoryInitializer Initializer { get; set; }
        public IGitBranchService Branches { get; set; }
        public IGitSyncService Sync { get; set; }
        public ICreateCommitUseCase CreateCommit { get; set; }
        public IPublishRepositoryUseCase Publish { get; set; }
        public IPushGuard PushGuard { get; set; }
        public IBranchProtectionPolicy BranchProtection { get; set; }
        public IRemoteRepositoryHost Host { get; set; }
        public IAccessTokenStore Tokens { get; set; }
        public IEditorAssetSynchronizer Assets { get; set; }
        public IProjectHealthChecker Health { get; set; }
        public IUrlOpener UrlOpener { get; set; }
        public IProjectInfo Project { get; set; }
        public INameSanitizer BranchNames { get; set; }
        public INameSanitizer RepositoryNames { get; set; }

        /// <summary>Falla pronto y con un mensaje claro si el composition root olvida algo.</summary>
        public void Validate()
        {
            foreach (var property in GetType().GetProperties())
                if (property.GetValue(this) == null)
                    throw new InvalidOperationException($"GitToolDependencies.{property.Name} no está asignado.");
        }
    }
}

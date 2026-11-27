using System;
using QuickGit.Core;
using QuickGit.Git;
using QuickGit.Hosting;
using QuickGit.UseCases;
using QuickGit.Unity;

namespace QuickGit.Presentation
{
    /// <summary>
    /// Groups the presenter's dependencies (parameter object). Avoids a constructor with
    /// 15 parameters and states, in one place, everything the presenter needs.
    /// </summary>
    public sealed class QuickGitDependencies
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

        /// <summary>Fails early, with a clear message, if the composition root forgets something.</summary>
        public void Validate()
        {
            foreach (var property in GetType().GetProperties())
                if (property.GetValue(this) == null)
                    throw new InvalidOperationException($"QuickGitDependencies.{property.Name} is not assigned.");
        }
    }
}

# Quick Git

An editor window to work with Git and GitHub without leaving Unity.
Menu: **Tools → Quick Git**.

## Features
- Detects whether the project is inside a git repository (also from a subfolder) and whether git is installed.
- **Publish to GitHub** in one click: init + Unity `.gitignore` + initial commit + create the repository + push.
- **Fetch / Pull / Push**, with fast-forward, merge or rebase pull strategies.
- **Commits** with a title and an optional description.
- **Changed files** list showing the change type (M/U/A/D/R/!), with an option to hide `.meta` files.
- **Branches**: switch instantly from the dropdown, create and delete them (locally and on GitHub).
- **Names fixed while typing** (spaces, accents, forbidden characters), with a preview of how the repository or branch will look on GitHub.
- **Protected branches** (`main`/`master`): pushing is blocked if the project has compile errors or the last commit was made with errors. Other branches are not affected.

## Installation

### Option A: Package Manager (recommended)
1. In Unity: **Window → Package Manager**.
2. **+** button → **Add package from git URL...**
3. Paste:
   ```
   https://github.com/DavidLastra8/Quick_Git.git?path=/Assets/QuickGit#v1.0.0
   ```
   - `?path=` tells Unity which folder of the repository contains the package.
   - `#v1.0.0` pins the version. Without it you get the latest commit on `main`.

Or add it by hand to `Packages/manifest.json`:
```json
"com.davidlastra8.quick-git": "https://github.com/DavidLastra8/Quick_Git.git?path=/Assets/QuickGit#v1.0.0"
```

> If the repository is **private**, Unity downloads it with your own git, so git needs GitHub
> credentials. With Git for Windows, Git Credential Manager opens the browser to sign in the
> first time. If that fails, make the repository public or clone it once from a terminal so the
> credentials are stored.

### Option B: .unitypackage
1. Download `QuickGit-vX.Y.Z.unitypackage` from the [releases page](https://github.com/DavidLastra8/Quick_Git/releases).
2. Double-click it, or use **Assets → Import Package → Custom Package...**, and import everything.

The simplest option (no git needed to install), but each update means importing the new `.unitypackage`.

> Use only one option per project. Having the package installed **and** a copy in `Assets`
> causes a duplicate assembly error.

## Requirements
- Unity 2021.3 or newer.
- Git installed (https://git-scm.com). If Unity cannot find it, set the path under **Settings** in the window.
- Your git identity configured:
  `git config --global user.name "..."` and `git config --global user.email "..."`.
- A GitHub personal access token (**Account** section of the window). A classic token with the `repo` scope works.
  It is stored in this machine's EditorPrefs, so it is shared by all your projects and never saved inside a project.

## Releasing a new version
1. Make and test your changes in the source project.
2. Pick the new version number ([Semantic Versioning](https://semver.org/)):
   - `1.0.0 → 1.0.1` bug fixes only,
   - `1.0.1 → 1.1.0` new features that break nothing,
   - `1.4.2 → 2.0.0` changes that require users to adapt.
3. Update `"version"` in `package.json` and add an entry at the top of `CHANGELOG.md`.
4. Commit and push.
5. On GitHub: **Releases → Draft a new release**, create the tag `vX.Y.Z` on `main` and publish it.
   A GitHub Action (`.github/workflows/release-unitypackage.yml`) then builds `QuickGit-vX.Y.Z.unitypackage`
   and attaches it to the release automatically. It fails if `package.json` does not have version `X.Y.Z`,
   so a mismatched release is caught immediately (check the **Actions** tab).
6. In each project using the package, change `#v1.0.0` to `#vX.Y.Z` in `Packages/manifest.json`.
   To roll back, put the old tag back.

## Architecture (MVP + SOLID)
```
Views/        QuickGitWindow                  Passive view (EditorWindow)
Presentation/ QuickGitPresenter, IQuickGitView, QuickGitViewState, QuickGitDependencies, NamePreview
UseCases/     PublishRepository, CreateCommit, PushGuard
Git/          Services over the git CLI, branch name sanitizer, protected branch policy
Hosting/      Remote hosting abstractions (IRemoteRepositoryHost...)
GitHub/       GitHub implementation (REST API, authentication, repository names)
Unity/        Adapters for AssetDatabase, EditorPrefs and compilation state
Core/         Process execution, OperationResult, INameSanitizer
Composition/  QuickGitComposer: the only place that creates concrete classes
```
- **S**: every service has a single job (detect, branches, sync, commit...).
- **O**: supporting GitLab means implementing `IRemoteRepositoryHost` and `IGitAuthArgumentsProvider`, without touching the presenter.
- **L**: any implementation of an interface can replace another one (test doubles included).
- **I**: small interfaces (`IGitSyncService`, `IGitBranchService`...) instead of one huge `IGitService`.
- **D**: the presenter only depends on interfaces, never on `UnityEditor` or `Process`, so it can be unit tested.

## Notes
- The token is never written to `.git/config` or to the remote URL. It is sent as an HTTP header to github.com only.
  Without a token, git uses your Git Credential Manager. SSH remotes use your SSH keys.
- Merge conflicts after a pull must be resolved with your usual tool. The window shows the conflicted files (`!`).
- GitHub rejects files larger than 100 MB. Use Git LFS for large assets (not managed by this tool).
- The protected branch check only applies to pushes made from this window. For real enforcement, enable
  GitHub branch protection together with a CI build.

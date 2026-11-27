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

### Option A: .unitypackage
1. Download `QuickGit-vX.Y.Z.unitypackage` from the [releases page](https://github.com/DavidLastra8/Quick_Git/releases).
2. Double-click it, or use **Assets → Import Package → Custom Package...**, and import everything.

The simplest option (no git needed to install), but each update means importing the new `.unitypackage`.

### Option B: Package Manager
1. In Unity: **Window → Package Manager**.
2. **+** button → **Add package from git URL...**
3. Paste:
   ```
   https://github.com/DavidLastra8/Quick_Git.git?path=/Assets/QuickGit#v1.0.0
   ```
   - To update later, replace `#v1.0.0` with the tag of the new version. 

Or add it by hand to `Packages/manifest.json`:
```json
"com.davidlastra8.quick-git": "https://github.com/DavidLastra8/Quick_Git.git?path=/Assets/QuickGit#v1.0.0"
```

> Use only one option per project. Having the package installed **and** a copy in `Assets`
> causes a duplicate assembly error.

## Requirements
- Unity 2021.3 or newer.
- Git installed (https://git-scm.com). If Unity cannot find it, set the path under **Settings** in the window.
- Your git identity configured:
  `git config --global user.name "..."` and `git config --global user.email "..."`.
- A GitHub personal access token (**Account** section of the window). A classic token with the `repo` scope works.
  It is stored in this machine's EditorPrefs, so it is shared by all your projects and never saved inside a project.

## Architecture
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

## Notes
- The token is never written to `.git/config` or to the remote URL. It is sent as an HTTP header to github.com only.
  Without a token, git uses your Git Credential Manager. SSH remotes use your SSH keys.
- Merge conflicts after a pull must be resolved with your usual tool. The window shows the conflicted files (`!`).
- GitHub rejects files larger than 100 MB. Use Git LFS for large assets (not managed by this tool).
- The protected branch check only applies to pushes made from this window. For real enforcement, enable
  GitHub branch protection together with a CI build.

# Quick Git: Git and GitHub inside Unity

**Version 1.0.0**

A Unity editor window to work with Git and GitHub without leaving the editor: publish your project
to GitHub in one click, commit, pull, push and manage branches from **Tools → Git Tool**.

Built with an **MVP architecture and SOLID principles**, so every layer can be tested and extended on its own.

## Features
- **Repository detection**: knows whether the project is inside a git repository (also from a subfolder) and whether git is installed.
- **Publish to GitHub in one click**: initializes the repository, adds a Unity `.gitignore`, makes the first commit, creates the GitHub repository and pushes.
- **Fetch, pull and push**, with fast-forward, merge or rebase pull strategies, and counters for commits to push (↑) and to pull (↓).
- **Commits with title and description**.
- **Changed files list** showing the change type (M/U/A/D/R/!), with an option to hide `.meta` files.
- **Branches**: switch instantly from a dropdown, create new ones, and delete them locally and on GitHub (with a warning if commits would be lost).
- **Names fixed while you type**: spaces, accents and forbidden characters in branch and repository names are corrected, with a preview of how they will look on GitHub.
- **Protected branches**: `main` and `master` reject pushes while the project has compile errors or the last commit was made with errors. Other branches are not affected.
- **Never blocks the editor**: every git operation runs in the background and can be cancelled.

## Installation

### Package Manager (recommended)
1. In Unity: **Window → Package Manager**.
2. **+** button → **Add package from git URL...**
3. Paste:
   ```
   https://github.com/DavidLastra8/Quick_Git.git?path=/Assets/UnityGitTool#v1.0.0
   ```

Or add this line to the `dependencies` of `Packages/manifest.json`:
```json
"com.davidlastra8.unity-git-tool": "https://github.com/DavidLastra8/Quick_Git.git?path=/Assets/UnityGitTool#v1.0.0"
```

`#v1.0.0` pins the version, so future releases never change your project unless you update the tag.

### .unitypackage
Copy the `Assets/UnityGitTool` folder into your project, or export it as a `.unitypackage` from this project
(right-click the folder → **Export Package...**). Updates then have to be copied by hand.

## Requirements
- Unity 2021.3 or newer.
- [Git](https://git-scm.com) installed. If Unity cannot find it, set the executable path under **Settings** in the window.
- Your git identity, configured once:
  ```
  git config --global user.name "Your Name"
  git config --global user.email "you@example.com"
  ```
- A GitHub personal access token with the `repo` scope, pasted in the **Account** section of the window.
  It is stored in your machine's EditorPrefs, so it works in all your projects and is never saved inside a project.

## Quick start
1. Open **Tools → Git Tool**.
2. If the project is not on GitHub yet, type a name and press **Create repository and publish**.
3. Daily loop:
   - **Fetch** to see if there is anything new (↓),
   - **Pull** before you start working,
   - work in Unity and check **Changed files**,
   - write a title (and optionally a description) and **Commit**,
   - **Push** to upload your commits (↑).
4. For a new feature, create a branch under **Branches → New branch**, work there, and merge it into `main` with a Pull Request on GitHub.

## Documentation
- [Package README](Assets/UnityGitTool/README.md): full feature list, releasing new versions and architecture.
- [Changelog](Assets/UnityGitTool/CHANGELOG.md): what changed in each version.

## Architecture
```
Views/        Passive view (EditorWindow): draws state, raises events
Presentation/ Presenter: validates, confirms and runs every action the same way
UseCases/     Publish repository, create commit, push guard
Git/          Services over the git CLI
GitHub/       GitHub REST API and authentication
Unity/        Adapters for AssetDatabase, EditorPrefs and compilation state
Core/         Process execution and operation results
Composition/  The only place that creates concrete classes
```

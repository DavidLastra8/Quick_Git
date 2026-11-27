# Quick Git: Git and GitHub inside Unity

**Version 1.0.0**

A Unity editor window to work with Git and GitHub without leaving the editor: publish your project
to GitHub in one click, commit, pull, push and manage branches from **Tools → Quick Git**.

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

### Option A: .unitypackage (easiest)
1. Download **`QuickGit-v1.0.0.unitypackage`** from the [latest release](https://github.com/DavidLastra8/Quick_Git/releases/latest).
2. Double-click it, or in Unity use **Assets → Import Package → Custom Package...**, and import everything.

The tool is copied into `Assets/QuickGit`. To update, import the `.unitypackage` of the new version over it.

### Option B: Package Manager (easy updates)
The tool stays out of your `Assets` folder and you update it by changing one version number.
1. In Unity: **Window → Package Manager**.
2. **+** button → **Add package from git URL...**
3. Paste:
   ```
   https://github.com/DavidLastra8/Quick_Git.git?path=/Assets/QuickGit#v1.0.0
   ```

Or add this line to the `dependencies` of `Packages/manifest.json`:
```json
"com.davidlastra8.quick-git": "https://github.com/DavidLastra8/Quick_Git.git?path=/Assets/QuickGit#v1.0.0"
```

`#v1.0.0` pins the version, so future releases never change your project unless you update the tag.

> Use only one option per project: having the package installed **and** a copy in `Assets` causes a duplicate assembly error.

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
1. Open **Tools → Quick Git**.
2. If the project is not on GitHub yet, type a name and press **Create repository and publish**.
3. Daily loop:
   - **Fetch** to see if there is anything new (↓),
   - **Pull** before you start working,
   - work in Unity and check **Changed files**,
   - write a title (and optionally a description) and **Commit**,
   - **Push** to upload your commits (↑).
4. For a new feature, create a branch under **Branches → New branch**, work there, and merge it into `main` with a Pull Request on GitHub.

## Documentation
- [Package README](Assets/QuickGit/README.md): full feature list, releasing new versions and architecture.
- [Changelog](Assets/QuickGit/CHANGELOG.md): what changed in each version.


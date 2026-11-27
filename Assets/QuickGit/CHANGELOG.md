# Changelog

## [1.1.0] - Unreleased
### Added
- **Settings → GitHub repository** section, available once the project is on GitHub:
  - Switch the repository between **private and public**, with a warning before each change.
  - **Collaborators**: list them, invite by GitHub username, remove them, and see or cancel
    pending invitations. Organization repositories also let you pick the permission level.
  - Read-only view when you are not an admin of the repository.

## [1.0.0] - 2026-11-26
### Added
- Repository and git installation detection.
- Publish to GitHub (init + Unity .gitignore + initial commit + repository + push).
- Fetch, pull (fast-forward / merge / rebase) and push.
- Commits with title and description.
- Changed files list with change type and a `.meta` filter.
- Create, switch (instantly) and delete branches, locally and on GitHub.
- Branch and repository names fixed while typing, with a preview of how they will look on GitHub.
- Protected branches (main/master): no pushes while there are compile errors.

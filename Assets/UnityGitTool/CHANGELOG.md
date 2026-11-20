# Changelog

All notable changes to this package are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this package follows [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-11-20
### Added
- Repository and git installation detection.
- Publish to GitHub (init + Unity .gitignore + initial commit + repository + push).
- Fetch, pull (fast-forward / merge / rebase) and push.
- Commits with title and description.
- Changed files list with change type and a `.meta` filter.
- Create, switch (instantly) and delete branches, locally and on GitHub.
- Branch and repository names fixed while typing, with a preview of how they will look on GitHub.
- Protected branches (main/master): no pushes while there are compile errors.

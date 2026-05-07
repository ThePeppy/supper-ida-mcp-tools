# Release Workflow

This repository uses three workflow layers so normal development, preview builds,
and public releases stay separate.

## 1. Pull Request / Main CI

Workflow: `.github/workflows/ci.yml`

Runs on pull requests and pushes to `main`.

It verifies:

- .NET solution restore and release build on Ubuntu, macOS, and Windows.
- Python syntax compilation for `ida-plugin/src`.
- The Python installer dry run.
- The desktop build contains bundled plugin resources under
  `PluginBundle/ida-plugin/src`.

This workflow does not publish release artifacts.

## 2. Manual Preview Build

Workflow: `.github/workflows/preview-build.yml`

Run it from GitHub Actions when you need installable artifacts for internal
testing without creating a GitHub Release.

Outputs:

- macOS: `SupperIdaMcpCenter-<version>-osx-arm64.dmg`
- Windows: `SupperIdaMcpCenter-<version>-win-x64.zip`

These artifacts are attached to the workflow run only.

## 3. Controlled Release

Workflow: `.github/workflows/release.yml`

Create a public release only when the repository is ready for a versioned cut:

```bash
git tag v0.1.0
git push origin v0.1.0
```

The release workflow builds macOS and Windows artifacts, then creates or updates
a draft GitHub Release for that tag. Draft releases let maintainers inspect
artifacts and release notes before publishing.

The same workflow can also be run manually with an explicit existing tag. Use
that path when a release draft needs to be rebuilt without pushing a new commit.

## Version Rules

- Use normal commits for development and bug fixes.
- Use pull requests or direct `main` pushes to trigger CI only.
- Use Preview Build for QA artifacts and screenshots.
- Use `vMAJOR.MINOR.PATCH` tags for release candidates that should become a
  GitHub Release.
- Publish the draft release only after manual verification on macOS and Windows.

## Packaged Resources

Desktop builds include:

- `PluginBundle/ida-plugin/src`: the IDA plugin package used by Settings >
  IDA Plugin > Install / Reinstall.
- `Bridge/`: the stdio bridge executable used for clients that do not support
  Streamable HTTP.

Users should not need to clone this repository to install the IDA plugin from a
compiled desktop build.

## Signing Notes

macOS CI builds are ad-hoc signed unless Apple signing credentials are imported
into the runner. Local developer builds use the first available signing identity.
For public distribution outside GitHub Releases, use a Developer ID Application
certificate and notarization.

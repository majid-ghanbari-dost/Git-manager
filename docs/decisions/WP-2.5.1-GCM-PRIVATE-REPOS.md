# WP-2.5.1: Git Credential Manager review for private repositories

Status: Code and unit-test review on `work/wp-2-5-1-journal-clone-bootstrap`.
This is **not** an interactive private-repository clone and **not** a Product Owner clean-machine test.

A successful public clone (`octocat/Hello-World`) does **not** substitute for this review.

## What the product does

- Clone invokes `git clone -- <canonical-https-url> <destination>` with `ArgumentList` (no shell interpolation).
- `GitProcess` does not set `GITHUB_TOKEN`, `GH_TOKEN`, `GIT_ASKPASS`, or any password/token environment variable.
- `GitHubRemote.Parse` rejects URLs with userinfo (`https://user:token@github.com/...`). Credentials cannot be embedded in the Clone URL box.
- Settings (`settings.json`) persist only `WorkspacePath`. The catalog and operation journal have no credential columns.
- There is no in-app token or password field.

Private HTTPS GitHub access therefore depends on Git for Windows and the user’s configured credential helper (Git Credential Manager when installed). Git Manager does not bundle GCM and does not prompt for a PAT.

## What was verified in WP-2.5.1

- Unit tests reject credential-bearing URLs.
- Unit tests assert Clone git arguments are only `clone -- https://github.com/<owner>/<repo>.git <path>`.
- Unit tests assert settings JSON has no token/password fields.
- Source contract test: `GitProcess.cs` does not mention credential environment variables.

## What was not verified

- Interactive GCM prompt / browser sign-in for a private repository.
- Non-interactive CI clone of a private repository (no secrets are stored in this repo or in CI).
- Azure/GitHub Actions `git credential` integration.

Those remain Product Owner / developer-machine checks. They are out of scope for WP-2.5.1 CI.

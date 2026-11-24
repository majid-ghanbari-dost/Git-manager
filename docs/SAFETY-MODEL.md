# Safety Model

## Credential boundary

Git Manager must not ask users to paste GitHub passwords or personal access tokens into ordinary text fields. Prefer Git Credential Manager or the configured Git credential helper. Never write credentials to logs, SQLite, project metadata, crash reports, or exported manifests.

## Importing untrusted repositories

A clone is data acquisition, not permission to execute code. Do not automatically:
- run setup scripts, package managers, build scripts, or tests;
- initialize and execute submodule content without explicit user choice;
- run repository hooks or custom tools;
- open downloaded executables.

Show a warning before opening a newly imported repository in an external IDE or terminal.

## Publish guard

Before publishing:
1. Confirm the selected project and canonical remote.
2. Show branch and ahead/behind status.
3. Show added, modified, deleted, renamed, and ignored files.
4. Show a diff preview where available.
5. Scan staged paths and content for likely credentials and private keys.
6. Stop if the remote has diverged; offer fetch/compare/reconcile, never force-push automatically.
7. Require explicit final confirmation.

## Destructive operations

Moving a project between lifecycle folders must not delete its backups. Delete project, delete backup, overwrite restore, and force operations must be separate, explicit actions with clear target paths and consequences.

## Backup integrity

Every completed backup must include a manifest and SHA-256 checksums. Verify checksums after writing and before restoring. Do not report success when any required payload is missing.

## Logging

Logs should include operation IDs, timestamps, project IDs, outcomes, and sanitized error messages. They must not include access tokens, passwords, private key contents, or full environment variable values.

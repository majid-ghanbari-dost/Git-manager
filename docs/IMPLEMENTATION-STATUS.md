# Implementation status

**Current baseline: `main`, Git Manager 2.5.6.** Work packages WP-2.1 through WP-2.5.6 have been integrated into the main branch.

## Delivered capabilities

- Persian RTL WPF desktop shell with Light / Dark / System theme.
- First-run workspace selection and persisted local settings.
- Workspace folder initialization and SQLite project catalog.
- GitHub HTTPS repository validation, canonical identity, and isolated project folders.
- Safe Git process invocation; full-history clone with staging, remote verification, and no-overwrite handling.
- Durable SQLite operation journal, crash recovery, and cross-process operation locks.
- Category management and physical Active / On-Hold / Archived lifecycle moves.
- Per-project backup creation (Git bundle + working-tree snapshot + SHA-256) and verified restore into a new destination.
- Clone workflow bootstrap and recovery handling.
- Safe Push using the configured Git Credential Manager / credential helper; no force-push.
- Fetch, fast-forward-only Pull, and local Commit with confirmation.
- Startup logging and frozen WPF brush replacement to address the reported theme crash.
- Self-contained Windows x64 publish, separate Inno Setup installer and portable ZIP, SHA-256 sidecars.
- Windows CI install / launch / repair / upgrade / uninstall smoke coverage.

## Latest validated delivery

- Product version: **2.5.6**
- PR: [#12](https://github.com/majid-ghanbari-dost/Git-manager/pull/12) — merged to `main`
- Merge commit: `6232d24878296c2e0f22800f1e5205f0281cb8de`
- Successful Windows CI: [run 45945175034](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45945175034)
- Restore, build, unit tests, self-contained publish, installer packaging, and install / launch / repair / upgrade / uninstall smoke all passed.
- Separate installer, portable, source-handoff, test-results, and install-evidence artifacts were uploaded. See [WP-2.5.6 delivery report](WP-2.5.6-DELIVERY-REPORT.md).

## Known limitations

- The hosted-runner smoke does not replace Product Owner installation and interactive visual validation on the target PC.
- Installer is not code-signed.
- Backup fidelity for submodule contents, Git LFS payloads, symlinks, and complete filesystem metadata is incomplete.
- Restore writes an unregistered folder and is not yet a lifecycle un-archive.
- Branch management and creation of new GitHub repositories are not implemented.
- Git for Windows is not bundled in the installer; it must be available on PATH.

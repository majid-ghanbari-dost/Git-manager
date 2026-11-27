# WP-2.5.6 Delivery Report — Git Manager 2.5.6

**Status: merged to `main` and CI delivery smoke passed.**

- Pull request: [#12](https://github.com/majid-ghanbari-dost/Git-manager/pull/12) — merged
- Merge commit: `6232d24878296c2e0f22800f1e5205f0281cb8de`
- Validated source commit: `f40978f2b38727d1e04246b7b67b69940a8b743f`
- Windows CI run: [45945175034](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45945175034)

## Validation

All steps completed successfully on GitHub Actions Windows runner:

- Restore and Release build
- Unit tests
- Self-contained win-x64 publish
- Inno Setup installer compilation
- Installer install, application launch, repair, upgrade, uninstall smoke
- Separate upload of installer, portable package, source handoff, test results, and install evidence

The installed-app smoke is a Windows hosted-runner test. It is not a Product Owner clean-machine installation or interactive visual inspection. The installer is not code-signed.

## Download deliverables

Open the [successful Actions run](https://github.com/majid-ghanbari-dost/Git-manager/actions/runs/45945175034) and download these separate artifacts:

| Artifact | GitHub artifact ID | Size |
|---|---:|---:|
| Installer x64 | `15175045072` | 45.1 MB |
| Portable x64 ZIP | `15174388172` | 61.9 MB |
| Install / uninstall evidence | `15174970243` | 14 KB |
| Source handoff ZIP + manifest | `15174596899` | 138 KB |
| Unit test results | `15174019395` | 28 KB |

Artifacts are retained by GitHub until **2027-02-25**. The installer, portable, and source-handoff artifacts each include their generated SHA-256 sidecar file. The artifact archive digests are recorded by GitHub on the Actions run.

## Remaining release limitations

- Product Owner must still perform the first installation and interactive UI check on the target PC.
- Installer is unsigned.
- Backup support for submodule contents and Git LFS payloads remains incomplete.
- This report does not claim those limitations are resolved.

# File Compass

A Windows file manager by Rathousky.pro, currently in development. Two-panel file management, local/network/cloud transfers, archives, document previews, a disk analyzer and SMART diagnostics.

## Run

Start **bin/File Compass.exe** and keep all supporting directories beside it (see the layout below). Read the [user guide](docs/README.md); packaged help is in `help`.

Settings offer Czech/English, light/dark/system themes, Solid/Mica/Mica Alt backgrounds, fonts, icon sets and operation defaults. Appearance changes are previewed immediately; Cancel restores saved settings. Language changes apply after saving and restarting.

## Build

On Windows with .NET Framework 4.8, run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File source/build.ps1
```

Use `-OutputDirectory ai-docs/artifacts/build` to build separately from an installed executable. Sources and bundled dependencies are in `source/`, supporting tools in `tools/`, maintained documentation in `docs/`. See [third-party notices](docs/THIRD-PARTY-NOTICES.md); original upstream licenses are retained unchanged.

## Features

- Side-by-side, stacked and single-panel layouts; tabs, metadata columns, saved folder views, workspaces and selection basket.
- Copy/move/delete with conflict policies, optional SHA-256 verification, background queues and recoverable network batches.
- SMB, FTP/FTPS, SFTP/SCP, NFS, SSH/SSHFS; optional Google Drive, OneDrive and Dropbox.
- Archives and split volumes, batch rename, metadata search, duplicates and file/folder comparison.
- Image/document/HTML/Markdown previews and highlighted source code in Auto and Text modes.
- Recursive analysis of the current folder with live interactive charts, filters, exports and saved-analysis comparisons; physical-disk SMART diagnostics.

Same-folder refresh updates changed rows without disabling or rebuilding the entire panel. Compact network queues show measured batch ETA, including between-file delays and excluding pauses. Transfer windows can run in the background without minimizing the main application.

## Google Drive

Every user signs in with their own Google account; developer IDs are supplied by the publisher. As of **23 October 2026**, the OAuth registration is **in production but not verified**. Branding verification awaits propagation of verified website ownership; restricted Drive-scope review remains pending. The unverified-app warning and project user cap still apply.

[Public website](https://rathousky-pro.github.io/file-compass-web/) · [Privacy Policy](https://rathousky-pro.github.io/file-compass-web/privacy.html). Website sources are published separately; this application repository remains private.

## Local data and development records

WebView2 profiles/cache use `%TEMP%/FileCompass/WebView2`, separated by installation location. User settings, credentials, cloud tokens and transfer recovery state must not be committed. Publisher OAuth configuration is supplied separately and ignored by Git.

AI/development notes, reports, screenshots and working builds belong under Git-ignored `ai-docs/`; put new outputs in `ai-docs/artifacts/`, not `source/` or `bin/`. Historical reports describe the tested version, not necessarily the current application.

## Installed directory layout

Copy the complete installation, not only the executable:

| Directory | Purpose |
| --- | --- |
| `config/` | Preferences, connection profiles, folder views, tags, workspaces and publisher OAuth configuration |
| `data/` | Encrypted cloud accounts, integrity key, transfer checkpoints/queue/history, SSHFS state and local working data |
| `languages/` | Translation catalogs; the selected language is stored in `config/language.txt` |
| `libraries/` | Managed .NET/UI libraries, templates and dependency licenses |
| `plugins/` | Native tools, protocol backends, codecs, optional archivers and their licenses |
| `icons/` | Application/action icon libraries |
| `preview/` | Local document-rendering assets and licenses |
| `help/` | Packaged user guides |

Older resource paths remain readable as a fallback. Runtime state is migrated once without overwriting existing destination files; original data remains recoverable. Configured managed-archiver/viewer paths are resolved to the new plugin location when available. WebView2 caches remain in system TEMP. Both config and data can contain sensitive local information and must be excluded when preparing a clean public distribution.

English is the default on a fresh installation. Czech remains selectable in Settings; a saved language preference takes priority over the default.

# File Compass

A Windows file manager by Rathousky.pro, currently in development. Two-panel file management, local/network/cloud transfers, archives, document previews, a disk analyzer and SMART diagnostics.

## Run

Start **bin/File Compass.exe** and keep its `config` directory beside it. Read the [user guide](docs/README.md); packaged help is in `config/help`.

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

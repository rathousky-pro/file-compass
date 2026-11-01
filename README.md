# File Compass

A Windows file manager by Rathousky.pro, currently in development. Two-panel file management, local/network/cloud transfers, archives, document previews, a disk analyzer and SMART diagnostics.

## Run

Start **bin/File Compass.exe** and keep all supporting directories beside it (see the layout below). Press **F1** or choose **Help → Program help** for searchable offline help inside the application. Read the [user guide](docs/README.md); packaged reference guides are in `help`. **Help → About File Compass** shows the application logo, developer logo, version, build ID and build date; Copy information prepares diagnostic version information.

Help is an external HTML package, not part of the EXE or interface language packs. Open `help/index.html` directly in any modern browser, or use **Open in browser** in the help window to open the current topic. Czech and English pages and their screenshots work offline; the application reads the same HTML files using its native help viewer. Replace the complete `help` folder to update help independently of the EXE. When editing source catalogs, run `python tools/build-help.py` and `python tools/check-release-content.py`; the normal application build copies the committed pages and images without requiring Python.

Settings offer English, Czech, German, Spanish, Dutch, Polish, French and Portuguese, fonts, icon sets and operation defaults. The application has one permanent appearance: a blue-grey window base, rounded white cards for logical areas, pale blue-grey option groups and white fields and controls. Colored Fluent toolbar icons are the default; Flat and Outline remain available. Existing disk and file/folder icons are retained. The outer blue-grey shell uses native Windows Desktop Acrylic with a light translucent tint; white cards and controls remain fully opaque. Windows transparency/high-contrast preferences are respected, with a solid fallback on unsupported systems. Copy and archive forms have symmetric inset content cards and measure their page height so ordinary desktop layouts show all basic options without scrolling. **‹‹ More / Více** at the left of operation and tool footers reveals compact blue-grey navigation; selecting a category replaces the settings in the main area while retaining entered values. Configuration keeps its navigation visible. Analyzer and converter sidebars use the same simple icon-and-label navigation as archive dialogs; converter navigation spans the settings and file queue. Clean file-list headers have subtle bottom and column separators. Archive summary chips open split/password/SFX settings directly. Self-extracting Windows EXE creation supports 7z (bundled module), ZIP/RAR (WinRAR modules) and ARJ (ARJ32), including split 7z launchers. See [archive requirements](docs/ARCHIVES.md). Compact background tiles place details, pause/resume and cancel controls at the right edge; active copy, archive, media, search and disk-tool operations expose their own controls. Clean panels use a combined blue-grey tab/navigation row, white lists with full filenames, readable sizes and dates, and a compact selection summary. Right-click a column header to choose visible base/metadata columns or move a column left/right; headers can also be dragged. Visibility, order and widths persist per panel and tab. The sort arrow follows the active sort column. Below drives, a single blue-grey row contains location tabs with close buttons and an adjacent + button; Root and Parent actions are white rounded buttons at the right. Ctrl+L edits the path; Ctrl+W, middle-click or a tab menu also closes a tab. The active Clean panel has a stronger three-pixel blue outline, detail rows use standard compact spacing, and the compact drive selector scales its frame and dropdown icons with the drive-icon size. All top-toolbar actions follow the selected Fluent, Flat or Outline icon set. Fluent symbols use light fills; Outline symbols consistently use the same monochrome ink. Hidden/system-file visibility has separate shown, hidden and partial-state icons and explicit tooltips. Panels can remain empty after closing their last tab, including after restart. Closing a connected remote tab offers disconnect, close-tab-only or cancel. Settings → Panels groups SI (MB/GB), IEC (MiB/GiB) or exact-byte sizes with hidden/system-file visibility and folder refresh. File columns refit to the viewport; unavailable extra columns cannot be enabled and no horizontal file-list scrollbar is shown. Owned tool windows block input to their owner while visible and restore it when closed or sent to the background; no blur effect is applied. Typing in a file list filters files and folders; Escape clears the filter. Code previews use the editor typography. All previous motif, dark/system and Mica preferences migrate to the permanent appearance. Icon/font changes remain configurable; Apply saves without closing settings. Language changes apply immediately after saving, without restarting.

## Build

On Windows with .NET Framework 4.8, run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File source/build.ps1
```

Restore the pinned Roslyn compiler first with `powershell -NoProfile -ExecutionPolicy Bypass -File source/prepare-media.ps1 -CompilerOnly`. Runtime dependencies are versioned in this repository; a clean checkout does not need to download a rotating FFmpeg archive merely to compile. For a deliberate full dependency restore, use `source/prepare-media.ps1`; it rejects changed hashes.

If the YAML editor dependency is absent, run `powershell -NoProfile -ExecutionPolicy Bypass -File source/prepare-editor.ps1`; the restore verifies pinned package, assembly and MIT license hashes.

Use `-OutputDirectory ai-docs/artifacts/build` to build separately from an installed executable. Sources and bundled dependencies are in `source/`, supporting tools in `tools/`, maintained documentation in `docs/`. See [third-party notices](docs/THIRD-PARTY-NOTICES.md) and the [distribution audit](docs/DISTRIBUTION-AUDIT.md); original upstream licenses are retained unchanged.

Version and channel are maintained in `source/version.json`. The current release is pinned to `0.9.5 beta`; subsequent builds increment the build ID without automatically increasing the minor version. Both build entry points generate embedded assembly metadata, a UTC date/sequence build ID (for example `20261024.01`) and timestamp automatically; the daily sequence restarts when the application version changes. The local counter and generated files live under ignored `source/obj`; build dates are not inferred from file modification times.

The SDK project is `source/FileCompass.csproj`; application code uses the `FileCompass` namespace and the native network adapter is `FileCompassNetwork.dll`. Historical cryptographic identifiers remain unchanged so existing profiles and recovery state stay readable.

## Features

- Side-by-side, stacked and single-panel layouts; tabs, metadata columns, saved folder views, workspaces and selection basket.
- Copy/move/delete with conflict policies, optional SHA-256 verification, background queues and recoverable network batches.
- SMB, FTP/FTPS, SFTP/SCP, NFS, SSH/SSHFS; optional Google Drive, OneDrive and Dropbox.
- Archives and split volumes, batch rename, metadata search, duplicates and file/folder comparison.
- Mixed-selection image/document previews, local video frame strips and highlighted source code.
- Internal tabbed text editor: F2/Ctrl+N for an unnamed file, filename-only first save to the captured folder, selectable encoding and LF/CRLF.
- Image and audio conversion, whole-file concurrency, OpenMP image processing, explicit percentages and weighted remaining-time estimates.
- Vendor component updates with verified downloads, staged activation and rollback; application update URL remains unconfigured.
- Disk analysis with explicit Start after scope/filter setup, live charts, exports and saved-analysis comparisons; HDD/SSD/NVMe SMART cards.

Same-folder refresh updates changed rows without disabling or rebuilding the entire panel. Compact network queues show measured batch ETA, including between-file delays and excluding pauses. Transfers and supported tools dock in the bottom activity area and can be restored through Detail.

## Google Drive

Every user signs in with their own Google account; developer IDs are supplied by the publisher. As of **23 October 2026**, the OAuth registration is **in production but not verified**. Branding verification awaits propagation of verified website ownership; restricted Drive-scope review remains pending. The unverified-app warning and project user cap still apply.

[Public website](https://rathousky-pro.github.io/file-compass-web/) · [Privacy Policy](https://rathousky-pro.github.io/file-compass-web/privacy.html). Website sources are published separately; this application repository remains private.

## Local data and development records

WebView2 profiles/cache use `%TEMP%/FileCompass/WebView2`, separated by installation location. User settings, credentials, cloud tokens and transfer recovery state must not be committed. Publisher OAuth configuration is supplied separately and ignored by Git.

AI/development notes, reports, screenshots and working builds belong under Git-ignored `ai-docs/`; put new outputs in `ai-docs/artifacts/`, not `source/` or `bin/`. Historical reports describe the tested version, not necessarily the current application.

`tools/clean-old-artifacts.ps1` previews removal of historical artifacts, staging builds and verification outputs. Run it with `-Apply` to remove those directories. It preserves current sources, test suites, build tools, `bin`, `local-profile` and development notes.

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

English is the default on a fresh installation. Czech, German, Spanish, Dutch, Polish, French and Portuguese remain selectable in Settings; a saved language preference takes priority over the default.

Image editing is integrated into the media converter; there is no separate quick-image command or window. Use the Size and rotation page for all six resize modes and rotation on one row. All landscape / All portrait rotates only images with the opposite orientation, after applying EXIF. The longest-edge limit follows the rotated image (1920 px becomes the height in portrait); exact width and height swap to match the requested orientation. Both image and audio format selectors offer Keep original format, resolved independently for each input in a mixed batch. Original files remain untouched; unchanged audio is stream-copied, while supported audio edits preserve the original codec/container. Unsupported original codecs fail explicitly instead of silently changing format.


File previews use a blue-grey card with basic location, size, date and image dimensions visible immediately. Details opens a separate detail window; Escape closes it or the preview. The preview starts with its carousel hidden. More/Less with vertical arrows reveals or hides the horizontal file carousel. The mouse wheel scrolls it without a scrollbar overlay; document/archive icons stay small. Drive buttons and the common-folders dropdown share one row, with the dropdown at the right. Drive label, free space and total capacity appear in the panel footer. All background jobs share one fixed-height horizontal activity strip. Tool tiles keep a compact width; copy/move, archive creation/extraction, deletion and media conversion share the remaining width, with horizontal scrolling only when needed. Progress updates retain tile instances and never toggle the strip visibility. Locked-file recycling uses the application error dialog with Skip all for the remainder of the task.


Fluent toolbar symbols use official Microsoft Fluent UI System Icons from the pinned commit recorded in `source/ui/icons/TOOLBAR-SOURCE.json`; the importer is `tools/fetch-toolbar-fluent.cjs`. Focused buttons retain a single rounded border. Drive buttons use tighter spacing, and the panel header includes a root-folder shortcut (`\`).


The internal editor places the active filename on the right of its title row and hides the redundant tab header for a single document. New text defaults to UTF-8 without BOM and CRLF; existing files retain their original line endings. Clean windows use a blue-grey shell with separate rounded white header, content and action cards. Explanatory dialogs render readable text instead of an input box. Unsaved editor changes offer Save, Discard changes and Keep editing, with cancellation preserved as the safe default.

The preview carousel follows the source-panel cursor in both opposite-panel and bottom previews. Choosing a carousel file selects that file in its source list while preserving marked files, and folder refreshes retain the choice. More/Less starts the file-information row, with a down chevron to expand and an up chevron to collapse. Successful document previews omit the redundant ready message.

Panel navigation uses one blue-grey row containing location tabs with individual close buttons and an adjacent new-tab action. Root and parent-folder actions sit at the right in white rounded buttons. The active panel uses a darker row. Disconnected FTP/SFTP tabs can also be closed, including the last tab. Opening a folder while the preview carousel is expanded refreshes its files without moving the source cursor back to a file.

Synchronization displays left and right folder cards with one large arrow control between them, cycling left, right and bidirectional using the configured toolbar icon set. Changing direction invalidates the previous plan; bidirectional mode still disables mirror deletion. Only permanent file deletion uses a red action; recycle-bin actions remain blue, and the bottom Delete key and connection/profile removal use normal button colors. Numeric up/down controls have an enclosing border and a separator between arrows.

Network-profile editors use More/Less at the left of the common footer to expand or collapse their sidebar; the redundant Advanced button in the general form has been removed.

The deletion confirmation follows the configured Recycle Bin preference: the preferred action sits at the right beside Cancel, while the alternative sits alone at the left. Permanent deletion is red with white text; moving to the Recycle Bin is blue. Enter accepts the preferred action, including permanent deletion when recycling is unavailable. Both transaction history and operation history provide Clear history with a confirmation. Clearing removes completed records and their undo receipts, preserves active/queued transactions, persists the empty history and leaves files unchanged.

The current help contains 44 topics in both Czech and English and includes 28 privacy-reviewed screenshot scenes per language as external PNG files. The screenshots match the public gallery and contain only generated demo files, fictional connections and synthetic SMART/progress data. Licensing notices retain their original English text. All eight interface catalogs cover 2,859 semantic keys; the latest media, remote-tab, size-unit and history options are localized. The offline content checker verifies help XML, topic/figure parity, local links and SHA-256 hashes of all 56 current illustrations.

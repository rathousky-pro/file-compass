# File Compass user guide

**F1** or **Help → Program help** opens native offline help with categories, accent-insensitive search, related-topic links and Back/Forward history. F1 opens the matching topic in supported dialogs. Ctrl+F focuses help search; Alt+Left/Right navigates history and Escape closes help. Core content is available in Czech and English; other interface languages use English help. Detailed reference guides are currently in English and labeled accordingly.

**Help → About File Compass** shows the application logo, the Rathousky.pro developer logo, version, build ID, actual build date and a clickable GitHub link. Copy information copies version/build/UTC timestamp and Windows version without profile paths.

File Compass is a Windows file manager by Rathousky.pro. Start **File Compass.exe** with all supporting installation directories. Settings are in `config`, working/account data in `data`, translations in `languages`, .NET libraries in `libraries`, native tools in `plugins`, artwork in `icons`, renderer assets in `preview`, and this guide in `help`. WebView2 working data is stored in system TEMP.

## Getting started

- Choose side-by-side, stacked or single-panel layout from the toolbar or View menu.
- F3 opens the viewer, F4 the editor, F5 Copy, F6 Move, F7 New folder and F8 Delete. Enter runs the default action in copy/move/delete confirmation dialogs; Escape cancels.
- Preview, Ctrl+Q and Ctrl+Shift+Q control the same preview: in the opposite panel, or below the list in single-panel mode. Collapse the lower preview using its header.
- Hidden files, Preview and Thumbnails show their active state. Invert selection and panel-direction arrows follow Hidden files; preview/edit/thumbnail actions form a separate group. Direction arrows open one panel's folder in the other panel when space permits.
- Returning to a parent selects the folder just left. Automatic refresh keeps unchanged rows, selection and scroll position.

## Appearance and language

Settings → Appearance offers light/dark/system themes, Solid/Mica/Mica Alt backgrounds and font-size arrows. Appearance and icon choices are previewed live; Cancel restores saved settings and Save persists them. English is the default for a fresh installation; Czech, German, Spanish, Dutch, Polish, French and Portuguese are also available. Language changes apply immediately after saving, without restarting. Open panels, selections and transfers stay in place; Cancel keeps the current language. Mica requires supported Windows 11; unavailable effects fall back to a solid background.

## Detailed guides

- [Panels, columns, icons and previews](PANELS.md)
- [Copying, recovery, synchronization and operations](OPERATIONS.md)
- [Search, metadata, duplicates and workspaces](TOOLS.md)
- [Archives and optional archivers](ARCHIVES.md)
- [Cloud storage and authorization](CLOUD.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)

Connections has Network connections and Cloud storage tabs. Settings reuse the same management tools; connecting is a separate action.

## Disk analyzer

Open Commands → Disk tools → Disk analyzer, or the panel context menu. It scans the active panel's **current folder and all subfolders**, not automatically the drive root.

The tree shows folder byte totals and file counts. Selecting a folder changes its folder chart. Donut/pie and bar charts group by folders, extensions, categories, modification age or size ranges, using bytes or file counts. Category/extension/age/size statistics apply to the original analysis root displayed above the chart.

Click a segment to list its files; Other includes every grouped category. The full file list uses a temporary disk index with 1,000-row pages, separate from the top 100 largest files. Closing the window or starting a new scan removes the index.

Combine category, minimum MiB, days since modification, name masks and excluded folders. Separate masks/exclusions with semicolons; exclusions accept names or relative/absolute paths. Apply and analyze starts a fresh scan. Categories use file extensions, not content inspection: images, video, audio, documents, archives, source code, applications and other.

Totals, charts, first-level folders, largest files and skipped items update about every half second while scanning. Detailed trees appear at completion; an open chart file list refreshes at most every two seconds. Stop retains a marked incomplete result.

Completed results export to CSV (all filtered files), standalone HTML with charts/tables, or PNG of the current chart at double resolution. PDF export is not implemented. Save analysis writes an FCA file with folder aggregates. Compare with saved analysis shows growth/shrinkage and added/removed folders; both scans must be complete with the same root and filters. Do not sum comparison rows: folder totals include descendants. Find duplicates opens the existing tool for the selected folder or analysis root.

Sizes are logical lengths, not allocated space. Hard links can count more than once. Links and reparse/mount points are not followed. Inaccessible items are skipped; at most 500 detailed skip records are kept, with a complete total count. Local folders and available mounted/network paths are supported; archive interiors and direct remote protocols require a local/mounted folder.

## Disk health / SMART

Commands → Disk tools → Disk health / SMART lists physical disks. Available data includes identification, volumes, temperature, SMART status, power-on hours/cycles, NVMe wear and read/write totals. SATA provides normalized/raw attributes; NVMe provides its health log. Unknown vendor attributes keep their original names. Missing values are identified, not estimated.

Select a disk from the top tile row: each tile shows health, temperature, drive letters and model. Many disks scroll horizontally; arrow keys navigate the tiles. Refresh preserves the selected device. Clipboard copying briefly retries if another application holds the clipboard, then offers a clear retry/TXT fallback message. Refresh manually or every one/five minutes, copy details, or use Save report to export PNG, PDF, TXT or JSON. The adaptive window places drive information beside the SMART table and splits attributes into two tables when needed. Health and temperature use prominent, contrasting tiles. PNG captures the complete result rather than the visible viewport; long PDF reports use multiple landscape pages. NVMe remaining percentage is 100 minus reported percentage used, not a failure prediction. Some SATA disks do not provide reliable lifetime or byte-count summaries.

smartmontools 7.5 runs as a separate `smartctl.exe`; GPLv2 license and corresponding source archive are in `plugins/smart`. Only diagnostics are read: no drive configuration changes or surface tests. If needed, elevation opens a separate SMART-only process; the main manager keeps its original privileges. Support depends on drive/controller/USB bridge. SMART does not replace backups.

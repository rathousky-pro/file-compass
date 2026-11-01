# File Compass

A Windows x64 file manager by Rathousky.pro, in development as **0.9.5 beta**. Two-panel local/network/cloud file operations, archives, document previews, media conversion, search, synchronization, space analysis and SMART diagnostics.

## Run and help

Start **bin/File Compass.exe** with all supporting directories beside it. Requires Windows and .NET Framework 4.8; document previews also use the system WebView2 runtime.

**F1** opens context-sensitive offline help. Help is external HTML, independent of the EXE and interface translations: open `help/index.html` in a browser, or choose **Open in browser** in the help window. Czech and English versions each contain 44 topics and 28 synthetic-demo screenshot scenes. Replace the complete `help` folder to update it independently. See the [user guide](docs/README.md).

There is one appearance: a blue-grey shell, rounded white cards and pale option groups. Windows Acrylic has a solid fallback. Colored Fluent toolbar icons are the default; Flat and monochrome Outline sets, button/drive sizes and fonts remain configurable. Folders use yellow Windows icons. English is the default; Czech, German, Spanish, Dutch, Polish, French and Portuguese remain selectable. **Apply** saves settings without closing; **Save** also closes.

Czech uses `languages/cs.lng` and `help/cs/index.html`; earlier saved Czech language identifiers are normalized automatically.

## Functions

- [Panels and previews](docs/PANELS.md): side-by-side, stacked or single panel, closable tabs including empty panels, adjustable columns and filtering. Document/code/image previews have a collapsible file carousel synchronized with the source-panel cursor. Video previews show six timestamped frames.
- [Operations](docs/OPERATIONS.md): guarded copy/move, conflicts, recycling/permanent deletion, resumable transfers, background controls and history. Permanent deletion is red; recycling is blue and follows the configured preference.
- [Archives](docs/ARCHIVES.md): browsing, compression/extraction, split archives, passwords and supported SFX creation. Bundled 7-Zip is 26.04; optional vendor updates can differ. External RAR/ARJ/LHA tools retain their own license requirements.
- [Tools](docs/TOOLS.md): image/audio conversion, metadata, batch rename, search, duplicates and comparison. Image editing is integrated into the converter; images/audio offer Keep original format. Originals are preserved and collisions receive new output names.
- [Cloud](docs/CLOUD.md) and networking: saved accounts, native FTP/FTPS/SFTP/SCP/SSH and optional SSHFS. Credentials and provider authorization stay local.
- [Updates](docs/UPDATES.md): official-source version checks, verified downloads, preparation and rollback. Application-update URLs remain unconfigured.

Background tools and transfers share one fixed-height activity strip. Tool windows block input to their owner until closed or sent to the background, without blurring panels. **Help → About File Compass** shows version, build ID, build date and developer information.

## Build and verify

On Windows, restore the pinned compiler if needed, then build:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File source/prepare-media.ps1 -CompilerOnly
powershell -NoProfile -ExecutionPolicy Bypass -File source/build.ps1
```

Runtime dependencies are versioned. A normal build does not download rotating vendor packages. YAML/Linux-filesystem restores use `source/prepare-editor.ps1` and `source/prepare-ext.ps1`. Full media restores verify pinned hashes; retain the verified FFmpeg archive.

Use `-OutputDirectory ai-docs/artifacts/build` for an isolated build. `-Release` requires an empty output directory and excludes test commands. `source/version.json` sets version/channel; generated build IDs use the UTC date and a per-version daily sequence in ignored `source/obj`.

After editing help catalogs, run `python tools/build-help.py`. `python tools/check-release-content.py` verifies eight translation catalogs, generated HTML, links, screenshot hashes and dependency notices. See [validation](docs/VALIDATION.md) for tested scope and [distribution review](docs/DISTRIBUTION-AUDIT.md) for release requirements.

## Installation and local data

| Directory | Contents |
|---|---|
| `config/` | Preferences, connections, views, tags, workspaces and local OAuth configuration |
| `data/` | Encrypted accounts, integrity keys, recovery/queue/history and working files |
| `languages/` | Interface translation catalogs |
| `libraries/` | Managed dependencies, templates and original licenses |
| `plugins/` | Native tools/codecs, protocol adapter, notices and SMART sources |
| `icons/` | Action/application icon libraries |
| `preview/` | Document-rendering library, loader and notices |
| `help/` | Offline HTML, images and maintained reference guides |

Development settings/data live separately in ignored `local-profile`. WebView2 caches use system TEMP. Preserve profiles, credentials, recovery records and user files during maintenance; exclude them from Git and release packages. Legacy resource paths remain readable for migration.

## Licenses and maintenance

[Third-party notices](docs/THIRD-PARTY-NOTICES.md) identify the components; `source/dependencies.json` records exact runtime and original-legal-file hashes. No open-source license has been selected for File Compass itself. Component licenses do not license application code. Public binary distribution still requires the FFmpeg corresponding-source bundle and applicable Microsoft runtime terms described in the distribution review.

[Public website](https://rathousky-pro.github.io/file-compass-web/) · [Privacy policy](https://rathousky-pro.github.io/file-compass-web/privacy.html). Website sources are in a separate repository; this application repository remains private.

Generated builds/reports belong in ignored `ai-docs`. `tools/clean-development-builds.ps1` previews removal of obsolete builds, test outputs, build metadata and `File Compass.before-*.exe` backups; `-Apply` removes checked untracked targets and retains compact test results. Sources, profiles, dependency archives, nested repositories and explicitly retained artifacts are protected. Historical reports describe their tested revision.

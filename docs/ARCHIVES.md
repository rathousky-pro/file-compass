# Archives and optional archivers

File Compass browses archives as folders and uses the bundled 7-Zip engine for supported formats. ZIP, 7z, TAR/GZ and supported external RAR/ARJ/LHA creation are available according to the selected format/tool. Existing ARJ/LHA/TAR archives have read-only limitations. Archive operations enforce path, entry-count, memory, elapsed-time, expanded-size, free-space and nesting limits configurable in Settings → Archives. Passwords remain outside worker command-line arguments.

Compression progress shows the current file, operation completion and compression-ratio history. The chart's horizontal axis is operation completion (0–100%); the vertical axis is output/input percentage and can exceed 100%. The connected samples show how the ratio changes during the operation. See [operations](OPERATIONS.md) for verified move-to-archive and split ZIP/7z volumes.

## Obtain optional tools

Settings → Archives can find an existing installation, select an EXE, or open Get add-on / Get WinRAR/RAR. Download begins only in the separate download window. Existing configured/common/PATH installations are used without downloading.

ARJ32 and LHa are prepared under `plugins/archivers` beside the program, without modifying Windows or Program Files. The window is modeless and supports cancellation. Completion saves the path; manually configured paths remain supported.

WinRAR uses RARLAB's original installer. After downloading, choose Run installer and complete its normal installation steps. File Compass does not run it automatically, install silently or redistribute a standalone Rar.exe. It subsequently searches for the installation; use Find WinRAR if installation is still running.

## Sources and licenses

- WinRAR/RAR 7.23 x64: https://www.rarlab.com/download.htm ; EULA https://www.rarlab.com/license.htm . Trial software, up to 40 days without buying a license. Downloading does not include a license.
- ARJ32 3.31: https://arj32.com/ . Shareware under ARJ Software's supplied terms. The original ZIP/nested SFX are extracted only; SFX installation/test commands are not executed. Original license/documentation files are retained.
- LHa 1.14i, GnuWin32: https://gnuwin32.sourceforge.net/packages/lha.htm . The original distribution and documentation are retained. This is the LHa compressor, not the Lhasa extractor.

RAR/ARJ/LHA creation runs external processes without passwords in arguments. Legacy ARJ/LHA filename-encoding limitations still apply.

## Integrity and maintenance

Downloads use certificate-validated HTTPS, a 128 MiB maximum and time limits. Pinned versions, URLs and SHA-256 package/EXE hashes are in `ArchivePluginInstaller.cs`. ARJ is verified before its nested SFX is opened; WinRAR installers also require Authenticode verification before publication and execution. Changed upstream content is rejected.

ARJ/LHA extraction uses the bundled verified codec in an isolated worker with normal archive/path limits. Prepared directories are published atomically; cancelled/failed downloads do not activate a tool. Managed archivers are verified and held open before execution. User-managed installations outside the add-on store use the chosen path.

There is no automatic search for the latest EXE or automatic update. A new managed release requires updated pinned metadata, license/package review and integration tests. A newer personal installation can be selected manually.

Compression quality is passed to the native codec for both ordinary and encrypted ZIP. ZIP no longer collapses all higher settings into the same .NET Optimal setting. The 0–9 choice maps to each codec's supported quality steps: native 7-Zip stages for ZIP/7z/GZip/BZip2/XZ, RAR methods 0–5, and ARJ methods 0–4 (where method 1 is strongest). TAR has no compression; the supported LHA tool exposes a fixed method, so their quality control is disabled and explained. Changing a level does not guarantee a different size for every payload, especially data already compressed.

When a source cannot be opened or read during folder preparation, a clean prompt offers Retry, Skip or Stop. Skip excludes that file, removes any partial staging copy and reports omitted paths after the operation. Stop preserves an existing destination archive. Move-to-archive keeps skipped source files and their containing directories; only included files that passed verification are deleted. If every directly selected file is skipped, no empty replacement is published.


## Compression levels and unreadable inputs

The selected level is mapped to each supported archiver rather than ignored by ZIP. Higher levels may produce identical sizes for small, incompressible or already compressed files; TAR stores data without compression. File preparation shows counts and names without percentage bars. Compression then shows blue total/current-file progress. Locked/unreadable inputs offer retry, skip, skip-all and cancellation where supported; skipped inputs remain at their original locations during move-to-archive.

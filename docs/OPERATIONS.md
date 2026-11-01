# File operations and synchronization

## Copy, move and delete

Use F5 Copy, F6 Move, F7 New folder and F8 Delete. Copy setup separates destination from file masks/options. Enter runs the default operation, Escape cancels. Choose an explicit collision policy rather than silently overwriting files. Optional controls include SHA-256 verification, timestamps, attributes, NTFS permissions, empty directories and hidden/system exclusions.

Recoverable copy/move errors offer **Try again**, **Skip**, **Skip all** and **Cancel** while preparing the file list as well as while processing files or folders. Skip all suppresses further recoverable-error prompts for this task; collision policies remain separate. Unsupported junctions/links are identified with their actual path and are never followed. Skipping an item during a move keeps it in the source and prevents whole-folder renaming from moving it anyway. Healthy siblings continue. Skipped failures remain recorded in operation history. If a busy folder cannot be renamed as a whole, its files are processed individually. Cancelling stops remaining work; already completed moves are retained.

Local and network work can run in background queues. The active-job and waiting-job panels show actual work. Network details open when transfer starts. **Background** hides only the transfer window into the main activity area; click that area to reopen it. It does not minimize the entire application. Speed charts retain the measured history against operation completion. Network ETA uses real transferred bytes over active batch time, includes between-file delays, excludes pauses and waits for a measurement warmup. It remains an estimate.

Network queue lists are compact and scroll for additional items. Speed limits offer presets and a manually editable KiB/s value; 0 is unlimited. Local delete settings may default to the Recycle Bin; deletion still requires confirmation. Shift chooses permanent deletion. The Recycle Bin is unavailable on remote storage and inside archives.

## Resume an interrupted network batch

Select the original source files/folders and the same destination, then choose Resume in copy setup. Verified owned partial files resume, missing targets transfer, and complete same-size targets are skipped. Different-size targets transfer again through staging. Already complete files are not fully hash-compared in this mode.

Ownership/checkpoints bind the server, account, source/destination paths, source size/time and sampled block hashes. Changed sources or invalid partial data are rejected. New uploads use `.FileCompass-upload-…part`; supported checkpoints from older versions remain readable.

A `.part.fcinfo.xml` sidecar describes original name, destination, length, modification time and transfer identifier. It contains no password or full local source path. This is descriptive only: safe resume also requires File Compass's verified local record in `data/transfers`. A filename or edited sidecar never authorizes adoption of another file. Successful completion removes the sidecar.

The network batch plan and completed-item receipts are saved during work. Interrupted normal network batches remain recoverable after restarting File Compass or Windows. Startup offers recovery; Commands → Operations → Restore saved tasks opens it manually. Sources/connections are revalidated, and only unfinished items continue. Successful plans are removed. Provider/protocol limits still apply: cloud uploads and non-seekable SCP work may restart the current file.

## Folder synchronization

Open two folders, then Commands → Synchronize folders. One-way synchronization and explicit mirroring remain available. For bidirectional work select Bidirectional ↔ and Compare; content comparison is enabled by default but can be disabled for size/time comparison.

Review each row: →, ← or Skip. Bidirectional arrows correspond to the left/right panels. An unresolved ≠ conflict blocks execution; file/folder collisions can be skipped. One-sided items are proposed for copying; differing files propose the newer side, while equal timestamps require a decision.

Bidirectional mode does not propagate deletion or maintain a previous-run database. If both copies changed, proposals use current timestamps and remain editable. Both plan endpoints are validated before the first write; stale plans are rejected. FTP/SFTP use the shared transport layer.

## Folder size overview

Alt+Shift+Enter or Commands → Analyze subfolder sizes calculates sizes in the background, with size/share bars. Press again to cancel/hide bars; leaving the folder cancels calculation. Local and mounted filesystems are supported. Logical lengths are measured, not allocated clusters or MFT records. Links are not followed. Skipped locations are counted in the status/tooltip and make totals partial.

## Image editing in the converter

Open Media → Convert images. Size and rotation has its own sidebar page for resizing, EXIF-aware rotation, All landscape and All portrait. Choose Keep original format to edit geometry without deliberately changing formats. Output names use _converted with collision numbers; originals and existing outputs remain untouched. Frame/metadata support depends on the selected format and options. See [media tools](TOOLS.md).

## Move into an archive and split volumes

Alt+F5 supports Move into archive and split ZIP/7z volumes, including passwords. Move requires subfolders and a new output name. The archive is tested, extracted content is SHA-256 compared, empty folders are checked where paths are retained, and sources are revalidated before deletion. Files are locked, verified and deleted through the same handle; only empty directories are removed. A source change stops further deletion. Cancellation can leave some already verified sources removed, with their content retained in the archive.

Volume presets are 100 MB / 700 MB / 4 GB or custom sizes, using multiples of 1024. Outputs include `data.7z.001`/`.002` and `.parts.json`; ZIP uses `.zip.001`, not `.z01`. Open the first volume with all parts together to browse, test or extract. In-place updates are not supported. Creation requires temporary space for a complete archive and never overwrites existing outputs.

## Split and combine ordinary files

File → Split file creates `.001`, `.002`, … and a `.parts.json` manifest, retaining the source. File → Combine files finds the series from `.001`; with a manifest it verifies part counts, lengths and SHA-256 hashes, including the final result. Missing/damaged parts prevent publication.

Other programs' parts can be combined without a manifest, but original hashes and a missing final part cannot be verified. Retain the manifest for your own transfers. Input parts are never deleted; the result requires a new name.

## Safe Undo and locking applications

Edit / Undo (Ctrl+Z) previews this session's new copies, created directories, renames and local/mounted-network moves. The session retains up to 20 groups and 5,000 receipts per group. New copies are checked by volume/file identity and SHA-256 before removal. Namespace moves are restored through guarded handles without replacing the original name. Newly created directories must be empty before removal. Reparse paths are rejected and parent directories are held during changes.

Overwrites, deletions, archives and remote/cloud operations are excluded. Undo may complete only part of a batch before a conflict; remaining receipts stay reviewable. The history is deliberately session-only. Cross-volume recovery can leave both copies if restoring/deleting fails; existing user files are never overwritten.

Sharing errors expose Windows Restart Manager's list of locking applications. The user can activate an available application window, save/close the file there, and choose Retry or Skip. The feature does not forcibly terminate processes. Network locks and background services may not have a useful local process/window.

## Transaction history from the toolbar

The curved Undo arrow immediately after the left/right transfer arrows opens the transaction list; Ctrl+Z opens the same window. Green rows have available Undo steps, amber rows are partially reversible, and grey rows show the reason Undo is unavailable. Select an operation to review paths and undo it; this is not restricted to the latest transaction. Availability refreshes while the window is open and is revalidated before any changes.

A later rename/move can disable an earlier one until the dependent action is undone. Deleting a renamed file makes that rename unavailable; a new file with the same name cannot inherit the original identity. Mixed new-copy/overwrite batches explicitly exclude overwritten files from Undo. Partial Undo preserves unavailable steps and user-added content. Completed transactions remain visible as Undone. The persistent journal also includes irreversible operations and previous-session records; safe Undo receipts remain session-only (20 retained groups / 5,000 steps per group).

## Deletion and clearing history

The confirmation follows the configured Recycle Bin preference. The preferred action sits at the right beside Cancel; the alternative is at the far left. Permanent deletion is red with white text, while recycling is blue. The bottom Delete command and removal of connection profiles keep their normal button colors. Locked-file prompts offer Skip all for the rest of the task.

Both operation and transaction history offer Clear history with confirmation. Completed records and their undo receipts are removed; active/queued work and the files themselves are preserved. Vendor update rollback packages are separate and are not removed by clearing history.

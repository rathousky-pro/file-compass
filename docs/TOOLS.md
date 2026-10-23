# Search, metadata and workflow tools

## Search and batch rename

Commands → Find files, the magnifier or Alt+F7 supports semicolon-separated masks, regular expressions, selected items, recursion depth and size ranges. Double-click a result to open its location. Name/size search supports local/SMB/NFS, open archives and FTP/SFTP/SCP through an independent connection. Content search supports local/SMB/NFS files up to 64 MB, using BOM or UTF-8. Results are capped at 20,000.

File/Commands → Batch rename, the A/B icon or Ctrl+M supports local/SMB/NFS files/folders. Archives and direct remote panels support individual rename instead. Masks include `[N]` name, `[E]` extension, `[C]` counter, `[N1-3]` name substring, `[E1]` extension character, `[RMD]` modification date and `[hms]` time. Find/replace supports regex and case sensitivity. Numeric fields include increment/decrement arrows.

Live previews identify invalid names/collisions and block execution. Temporary names permit swaps. Errors/cancellation roll back in-progress changes; the latest successful batch can be undone.

EXIF/music masks can be combined with counters:

| Mask | Value |
| --- | --- |
| `[EXIF.Date]` | Capture date, `yyyy-MM-dd` |
| `[EXIF.Time]` | Capture time, `HH-mm-ss` |
| `[EXIF.Camera]` | Camera make/model |
| `[MP3.Artist]` | Artist |
| `[MP3.Album]` | Album |
| `[MP3.Title]` | Track title |
| `[MP3.Track]` | Track number, at least two digits |
| `[MP3.Year]` | Year |

Presets prepare photo/music naming schemes. Missing/unreadable metadata retains the original name and explains why; invalid Windows filename characters become underscores. Reading never changes file contents/tags.

Space marks a folder and calculates recursive size in the background, including archives and supported remote panels. Links are not followed. An error leaves `<DIR>` and reports the problem rather than presenting an incomplete total.

## Metadata search

Alt+F7 → Metadata combines conditions with the General tab's location, masks and depth. Fields include width/height/megapixels, document pages, spreadsheet sheets, presentation slides, author/title/camera, artist/album/media duration/audio bitrate and capture date.

Numeric comparisons support greater/at least/equal/less/at most; text supports contains/equal/starts with. Case sensitivity is shared with normal search. Durations accept `hh:mm:ss` or `d.hh:mm:ss`; capture dates compare calendar dates. All conditions must match; missing values do not match.

Examples cover wide photos, PDFs over 20 pages, author names and videos over one hour. Presets change relevant masks/conditions while retaining other restrictions. Conditions persist in search profiles. Results show matched values and can be sent to a panel; the status shows the file being examined and Cancel stops work.

Metadata search supports local folders and ordinary network shares, not archive/direct FTP/SFTP search. Unsupported conditions are not silently ignored. Isolated workers impose time limits and skip unsupported extensions. Availability depends on formats/codecs and file protection/damage. PDF page counts are read directly; Office/ODF may report cached counts.

## Duplicates and comparison

Commands → Find duplicates accepts semicolon-separated folders and scans subfolders. Files are grouped by size then compared using full SHA-256. Aliases/hard links to the same physical file count once; symbolic/reparse links are skipped and inaccessible items reported.

Results show groups, names, paths, sizes and a shared preview. Long path prefixes are shortened with full-path tooltips. Mark copies retains the first path in each group and marks the others; adjust manually. Recycle requires confirmation and rechecks content, size, timestamps and physical identity. Each group must retain an unmarked verified representative; changes invalidate the plan. Partial completion remains visible. Download/extract direct remote/archive content first; mounted folders are supported.

Commands → Compare by content / Ctrl+Shift+D accepts two selected files, one in each panel, or manually chosen paths. Text uses colored side-by-side line comparison with difference navigation and case/trailing-space options; binary mode compares bytes. Images use side-by-side or a slider overlay with shared scale/origin. Images decode to at most 4096 px on the long side; limits are 128 MB and 100 million source pixels. Sources remain unchanged; visual comparison does not replace byte comparison. Two duplicate results can use the same dialog through Ctrl-selection and Compare.

## Code, Markdown and tags

Preview/Ctrl+Q/Ctrl+Shift+Q show source highlighting or rendered Markdown; the formatting checkbox switches to plain text. Highlighting works in Auto and explicit Text mode. Viewer settings can enable this internal preview for F3; F4 keeps the configured editor.

Markdown supports headings, lists, tables and code blocks. Embedded HTML is not executed, links are inactive and remote images are not downloaded. Text preview reads a bounded initial 128 KiB page.

File → Edit tags (also in the context menu) adds/replaces/removes tags. A quick filter such as `#Important` selects matching items; Commands → Files by tags also finds tagged files elsewhere. Tags live in the local profile and do not modify file content. File Compass moves/renames preserve tags; external moves are not automatically tracked. Where physical identity is available, a new file at the old path does not inherit old tags.

## Workspaces, basket and recursive listings

Workspaces save named sets of tabs, sorting, views, split ratios and filters. They contain no passwords and do not reopen network connections without a user action.

The selection basket collects references from different folders. Delete/F8 removes basket references only; copy/rename/archive act on the referenced files. Recursive contents displays subfolder files with their locations; return using the parent item or Back to normal folder contents. Directory links are skipped; the limit is 100,000 items and inaccessible folders can stop loading with an error. Cancel via Escape/View. Basket/aggregate listings cannot be destination folders: open an ordinary directory first.

## Everything and performance

Commands → Fast search with Everything uses a separately installed/running Everything index; only its SDK is bundled. Queries accept Everything syntax and return at most 20,000 results. The helper has a 15-second timeout. Ordinary Alt+F7 search remains available when Everything is unavailable.

Startup can use a JIT profile on later runs. Thumbnails load for realized rows and release images outside the visible area. Same-folder refresh applies incremental row changes and preserves selection/scroll. Performance depends on storage, antivirus and available caches.

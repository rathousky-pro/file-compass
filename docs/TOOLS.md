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

## Command search, linked browsing and operation presets

Ctrl+Shift+P searches registered menu commands. Settings / Custom keyboard shortcuts persists overrides by language-independent command IDs, checks collisions, and supports clearing or restoring bindings. Text editors keep normal typing.

Commands / Linked panel browsing captures the two current local or mounted-network folder roots. Relative navigation is mirrored only when the counterpart exists. Leaving a root disables linking. Missing counterparts do not change the other panel and do not create directories.

F5/F6 includes named operation presets for destination, include/exclude masks, verification, conflict policy and metadata flags. Loading fills the dialog only; the current Copy/Move action remains authoritative. At most 100 presets are stored.

Text search defaults to a zero (unlimited) source size limit and streams ordinary text. Results include line/location and an excerpt. Optional PDF/DOCX text search runs in a 512 MB worker with a 15-second deadline and 64 MB source limit. DOCX XML is bounded to 16 MB per part / 64 MB total. This does not perform OCR. Unsupported, protected and over-limit documents produce warnings.

## File names and embedded media metadata

Media / Metadata from file names previews a pattern against file names without extensions. For audio use `[MP3.Artist] - [MP3.Track] - [MP3.Title]`; MP3 tokens also represent supported other audio containers. For pictures use `[EXIF.Date]_[EXIF.Time] - [Image.Title]`. Dates accept `yyyy-MM-dd` or `yyyyMMdd`, times `HH-mm-ss` or `HHmmss`. `[Ignore]` skips a segment; variable fields require literal separators.

Existing values are retained unless Replace existing metadata is enabled. Only previewed changes are written, using a temporary copy and post-save field verification. An exact original `.filecompass-metadata-<id>.bak` is retained beside every successfully replaced file. Preserve those backups until satisfied with the result. Unsupported writes fail without replacing the original. Files changed after preview, read-only files and linked paths are rejected. Limits: 500 files per batch and 1 GB per source. The feature works with local and mounted network paths.

Media / Rename using metadata opens the existing collision-checked batch rename preview with a media preset. Additional tokens include image title/author/comment/copyright, separate EXIF make/model, and audio genre/disc/composer. Missing values produce per-file warnings and preserve the original name. Extensions are retained by default.

## File system fields in rename templates

The main Media menu contains batch rename, metadata-to-name rename, name-to-metadata writing and simple image editing. Existing keyboard shortcuts remain available.

Rename templates now accept `[File.Created]`, `[File.Modified]` and `[File.Accessed]` for local file-system dates, plus `[File.CreatedTime]`, `[File.ModifiedTime]` and `[File.AccessedTime]`. Defaults are `yyyy-MM-dd` and `HH-mm-ss`; a custom date/time pattern can be supplied as `[File.Created:yyyyMMdd_HHmmss]`. `[File.Size]` is the file size in bytes and `[File.Folder]` is the parent folder name. These fields work without embedded EXIF or audio tags and do not write file timestamps. Missing values preserve the old name and show a warning; file size is unavailable for directories. Filename-unsafe output characters are sanitized. Existing `[RMD]` and `[hms]` templates retain their behavior.

## Batch image dimensions

Select all images to process and open Simple image editing from the shared submenu. The same rotation, size and format settings apply to every selected image, producing new `_upraveno` files. Originals and existing output files are never overwritten.

In addition to longest edge and percentage, choose exact width × height, width only, or height only. Exact dimensions can stretch the image if the aspect ratio differs. A single specified side calculates the other side independently for each image after EXIF orientation and rotation, rounding to whole pixels. These explicit dimensions can enlarge smaller images; longest-edge mode still only shrinks. Output dimensions and aggregate frame pixel budgets are bounded before allocating resized images. Metadata is not carried into edited image copies, as described in the dialog.

## Média – převody obrázků a zvuku

Hlavní nabídka **Média** sdružuje převod obrázků a zvuku, jednoduché hromadné úpravy obrázků, přejmenování podle metadat a zápis metadat z názvu. Nástroje pracují s lokálními soubory. Pro cloud nebo archiv je nejdříve stáhněte či rozbalte.

Převodník má záložky **Obrázky** a **Převést zvuk**. Přidejte soubory, vyberte zaškrtnuté položky a nastavte výstupní složku. Parametry zdroje a stav převodu jsou viditelné v seznamu; po výběru řádku se zobrazí celá zdrojová a cílová cesta i podrobnosti chyby. Výstupy mají `_converted`, při kolizi další číslo. Originály se nepřepisují. Úspěšné položky se odškrtnou, aby se omylem nepřeváděly znovu. Zrušení ponechá již dokončené soubory. Výstupy jsou zapojené do historie a bezpečného vrácení operací.

- Obrázky: PNG, JPEG, WebP (také bezeztrátový), AVIF, TIFF, BMP, GIF; kvalita, otáčení, původní rozměry, delší strana, procenta, pevné rozměry nebo jedna strana se zachováním poměru. JPEG/BMP vyplní průhlednost bílou. GIF má omezenou paletu. Vícesnímkové soubory zachovávají snímky jen v GIF/WebP/TIFF, jinak je nutné výslovně zvolit první snímek. Zachování metadat závisí na cílovém formátu.
- Zvuk: MP3, AAC v M4A nebo ADTS, AC-3, E-AC-3, FLAC, WAV, Opus, Ogg Vorbis, ALAC v M4A. Bitrate a VBR podle kodeku, mono/stereo/5.1/7.1 podle podpory, vzorkovací frekvence, 16/24 bitů u bezeztrátových kodeků a také 32bit float u WAV. Pořadí zvukové stopy se počítá od 1. Dostupná je normalizace, výslovný stereo upmix a kopírování kompatibilní stopy bez překódování.
- Video: převádí se zvuková stopa nebo se extrahuje. U AAC/ALAC lze zachovat původní video v MP4 bez překódování; nekompatibilní video končí chybou. Převod obrazové části videa není součástí nástroje.

AC-3 a E-AC-3 umí nejvýše 5.1. Upmix ze sterea nevytváří původní prostorový záznam ani Dolby Atmos. Zvýšení bitrate nebo bitové hloubky neobnoví ztracené informace. Výsledek se před uložením znovu kontroluje; zvuk se také celý dekóduje pro ověření čitelnosti.

Převodník umožňuje zvolit 1, 2, 3, 4, 6 nebo 8 souběžně zpracovávaných souborů. Volba se pamatuje pro další otevření okna; výchozí jsou 2. Platí pro obrázky i zvuk. Zrušení zastaví všechny aktivní převody a ponechá již dokončené výstupy. Vyšší souběžnost zvyšuje nároky na paměť a CPU a nemusí vždy zkrátit čas. VBR je dostupné u MP3, AAC, Vorbis a Opus; AC-3/E-AC-3 tuto volbu nenabízejí.

Náhledy dokumentů: HTML a JavaScript pro DOCX a tabulky jsou přibalené jako prostředky v `preview/preview.dll`; na disk se při prohlížení nerozbalují. Samostatně zůstává nativní `WebView2Loader.dll` a licenční texty. PDF a HTML nadále vykresluje WebView2. Obrazové náhledy WPF se tímto balením nemění.

Nastavení převodníku včetně cílové složky a souběžnosti je nahoře. Pod ním je seznam souborů přes celou šířku okna; průběh a tlačítka zůstávají dole. Pole se vzorem názvu v obou metadata nástrojích nabízí po kliknutí nebo Alt+šipka dolů seznam platných příkazů. Výběr nahradí označený text nebo se vloží na pozici kurzoru.

Režim datového toku se vybírá samostatně: MP3 nabízí CBR, ABR s cílovým průměrem a VBR podle kvality (0 nejlepší až 9 nejnižší). AAC a Vorbis nabízejí cílový průměr ABR nebo kvalitu VBR; u Vorbis je vyšší číslo kvalitnější (−1 až 10), u AAC také vyšší. Opus podporuje CBR, VBR a omezené CVBR s cílovým bitrate. Skutečný průměr závisí na obsahu a délce nahrávky. Neaktivní pole se nezohledňují při kódování. Pokročilé volby pro obrázky i zvuk jsou ve společné sekci pod zarovnanými základními poli. Detailní okénko cílového názvu pod seznamem bylo odstraněno; dlouhé stavy jsou dostupné v nápovědě buněk.

### CSV table preview

F3 and Ctrl+Q render CSV/TSV/TAB/PSV in a read-only native table. The shared bounded parser detects comma, semicolon, tab and pipe outside quoted fields, honors `sep=`, and preserves escaped quotes, multiline values, trailing empty cells and leading zeros. Manual separator selection also supports colon; headers can be disabled. Source text remains available through the formatted-preview toggle. Local and remote text previews use the same parser; encoding follows the preview selector/BOM. Limits: 128 KiB source, 2,000 records, 128 columns, 100,000 cells, 16,384 characters per cell. Truncation is reported; unfinished final records are omitted.

Media converter layout: Conversion settings and Advanced are separate tabs without a nested settings ScrollViewer. Destination and parallelism remain below both tabs. Queue column widths are recomputed from the grid width, reserving vertical scrollbar space; long text uses ellipsis and tooltips. Column dragging is disabled to keep the last column visible.

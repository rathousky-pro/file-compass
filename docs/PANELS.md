# Panels, icons and previews

The Start menu contains Open in Explorer, Run Command Prompt and Run PowerShell, using the active local folder. The former External tools submenu and development-only project-folder shortcut are no longer shown.

Network combines Windows discovery, mapped drives, saved network connections and bounded IPv4 file-service probes on attached private networks. It checks SMB (445), FTP (21), SSH/SFTP (22), implicit FTPS (990) and NFS (2049), showing results progressively. SMB opens shared folders; newly found transfer services open their connection settings before connecting. A listening port indicates a possible service, not confirmed credentials or permissions. Large prefixes are bounded to the adapter's /24, with at most 1,024 candidate addresses and a time limit. Devices on other VPN subnets, IPv6-only devices, nonstandard ports or services hidden by a firewall may require a saved/manual connection. Permission-blocked probes produce a warning in the Network status tooltip.

## Layout and navigation

The toolbar cycles side-by-side → stacked → single panel and shows the active layout. View → Panel layout also selects it. Ctrl+H switches dual-panel orientation; Ctrl+Shift+H switches single/last dual layout. Single-panel mode retains the active side; the other side keeps its folder, tabs and connection for later.

The toolbar groups navigation/selection separately from preview/edit/thumbnails. Hidden files toggles hidden/system visibility in both panels while retaining name filters. Invert selection and direction arrows follow it; arrows open the source folder in the other panel. Edit uses F4. Thumbnails change the list, not the preview.

Returning from a subfolder selects that subfolder in the parent. Automatic local refresh leaves the list enabled and applies only added/removed/changed rows, preserving unchanged row instances, marks and scroll position. Network watching is separately configurable and can be disabled for slow paths; Ctrl+R refreshes manually.

The folder tree can be toggled separately in either panel from the toolbar View menu. Its root is the current folder. It shows its subfolders, loads deeper branches on expansion, and follows navigation by changing the root. The splitter adjusts its width. Each tab and saved view retains its own tree preference.

## Compare open panels

Commands → Compare panels adds a Comparison column and controls to both open folder lists. It compares the current level by name and identifies items present on one side only or differing by type, file size or modification time. The time comparison allows two seconds for file system precision. Matching folder names indicate presence on both sides. Use the filter to narrow either list, then Mark results to use the normal file commands on that selection. Compare matching files opens the content comparison for a uniquely matched local file. Navigating or refreshing either panel clears the comparison. Matching items are green, differences are amber, and entries unique to the left or right are blue or violet respectively. The comparison column and filter identify each state in text. The Compare tabs dialog can also show its results directly in the panels, opening the second location in the other panel when both chosen tabs belong to the same side.

## Include subfolders

Include subfolders in the View menu next to the hidden-files toolbar button shows files from the current folder and its descendants in the same tab. Large scans add results progressively. The Location column shows the relative parent folder. Use the same control or Backspace to return to the ordinary view. If scanning is cancelled or interrupted, the footer identifies the partial list.

## Unified preview

Preview, View → Preview, Ctrl+Q and Ctrl+Shift+Q control one feature. In dual-panel layouts it replaces the opposite panel; in single-panel mode it appears below the list. Changing layout moves the open preview appropriately and retains lower-preview height. Its header collapses/expands it, and the splitter changes height. Closing restores the opposite panel's previous contents. Hidden/collapsed previews do not continue loading files.

Images, text, syntax-highlighted code, Markdown, binary and hex views are available. Auto chooses the suitable view; Text explicitly displays source, with optional highlighting for supported languages. The formatting checkbox enables/disables code highlighting or rendered Markdown. Hex/Binary retain raw byte views. Find and encoding controls are available; text loads in bounded pages with offset navigation for larger files. Stable selection does not reload an unchanged preview after unrelated folder changes; modifying the selected file does refresh it.

Settings can use internal code/Markdown preview for F3. F4 uses the configured editor; unsupported types are not automatically executed through associations by F3. Settings → Save view and workspaces preserve layout, ratios, visible side, tree and preview state. Automatic view persistence follows its existing exit-saving setting; older settings retain compatible dual-panel defaults.

## Rendered documents

Auto mode with formatting enabled supports:

- PDF through WebView2, with pages, zoom and search.
- DOCX through docx-preview, including text styles, tables and embedded images within library limits.
- XLSX/XLS/XLSM/XLSB/ODS through SheetJS, with sheet selection and formatted values. Macros/formulas are not executed; charts and full original styling are unsupported.
- Older DOC/other unsupported document formats through text extraction where available.

The ready state is shown after rendering. ZIP/RAR/7z in Auto show a read-only archive listing, up to 10,000 items with archive limits; manual Text/Binary/Hex modes remain available.

WebView2 Runtime is neither bundled nor automatically installed. When unavailable, rendering fails or the document exceeds 64 MB, a text/source fallback remains with an official [Microsoft runtime link](https://developer.microsoft.com/microsoft-edge/webview2/#download-section). Install separately and reopen the preview.

Workbook limits: 200 sheets, 2,000 rows and 100 columns per sheet. Compressed documents: 10,000 entries and 128 MB declared expanded size. Files are processed locally, read-only, without retaining a source lock after loading. The renderer permits only the selected file and bundled scripts, blocking unrelated network requests, new windows and downloads. Libraries are bundled, not fetched from a CDN. Licenses are in `preview`. WebView2 profile/cache uses `%TEMP%/FileCompass/WebView2/<installation-id>`.

## HTML source and page preview

HTML/HTM/XHTML/SHTML in Auto provide Web page / Source code choices. Source is syntax highlighted without changing Office/PDF formatting preferences. Explicit Text mode also supports source highlighting.

Rendered HTML is static: scripts, remote requests, forms, navigation and downloads are blocked. Local CSS/images/fonts may load only from the file's directory or descendants, without traversing reparse links; auxiliary files are limited to 8 MB and unrelated file types are not exposed. If WebView2/rendering is unavailable, source code remains.

Archive/FTP/SFTP HTML previews prepare only the selected HTML file, not separate CSS/images. Code/Markdown views remain read-only.

## Archive and remote previews

The same preview works inside an open archive and on FTP/FTPS/SFTP, including the cloud SFTP bridge. Archive files extract into a separate TEMP directory. Remote documents/images download a temporary copy; remote text continues to read a bounded prefix. Preparation shows loading before preview/metadata.

Selection changes/closing cancel preparation and remove temporary copies; source archives/files remain unchanged. Prepared copies are limited to 64 MB, with a 150 ms debounce before extraction/download. SCP uses F3. Encrypted entries require an available password; missing/incorrect passwords show a preview error.

## File details and metadata columns

The expandable Details section provides type-dependent information and Copy details. In a lower single-panel image preview, expanded details move beside the image. General fields include name/type/size/creation/modification/attributes and tags.

Images include dimensions, megapixels, DPI, color/depth, transparency, frame count and color-profile presence. EXIF can include capture time, camera/lens, aperture/ISO/exposure/focal length, orientation, GPS, software and copyright; ICO/CUR include variants and dimensions.

PDF details include actual page count, version, stored properties, first-page dimensions and a text excerpt. Scanned pages without text need OCR, which this feature does not provide. Word formats expose properties, authors/editors, cached counts, paragraphs/comments, macro presence and excerpts. Excel formats expose sheets/visibility, saved ranges of the first six sheets, properties and shared-text samples; saved ranges need not equal nonempty cells. Presentation formats expose slide counts/properties/text samples. Office/ODF cached counts can be stale; formulas/macros are not run. Audio/video details depend on readable tags and available codecs.

Metadata columns load in the background; sorting by one also reads other supported files. Leaving the folder cancels work. Missing/unreadable values stay blank. Local/ordinary shared folders support these columns; archive/direct remote/aggregate/system listings do not scan them. PDF counts are read directly; Office/ODF counts are stored values.

## Automatic and saved folder views

Settings → Appearance → Automatically adapt view to folder content is off by default. On entering another folder it inspects ordinary nonhidden/nonsystem files: a 60% majority of images selects thumbnails/image columns, documents select author/pages/sheets, music artist/album/title/duration, video duration/resolution. Empty/mixed folders retain the base view. Refreshing the same folder does not override a manually selected view. Disabling the feature restores the original panel view.

After adjusting columns, widths, sorting and display mode, right-click the header to save a view for this folder or this folder and descendants. Saved views apply independently of content detection. More specific paths win, and explicit saved views take priority over content inference. The View menu beside the hidden-files button identifies the current folder view and shows the matching folder in its tooltip. Its menu saves the current columns, widths, sorting, grouping and display mode. Use default view temporarily returns to the tab's base view. Apply folder view reapplies the rule or content suggestion. Delete the saved view of this folder removes the current-path preset. Presets use `config/folder-views.xml`; the switch uses `config/settings.xml`.

## Icon libraries

Action sets are `Actions.Fluent.fvicons` (Windows 11 color), `Actions.Flat.fvicons` and `Actions.Outline.fvicons`. `FileCompass.Icons.dll` contains seven app-icon variants with ten sizes from 16 to 256 px. Settings → Icons previews action/app choices immediately; Apply saves without closing, Save saves and closes, and Cancel discards later unsaved changes. Changing the app icon updates application windows, not the embedded EXE icon or existing Windows shortcuts.

Create shortcut with selected icon writes a `.lnk` referencing the shared library. For the taskbar, unpin the old item and pin the new shortcut; Windows can cache old pinned icons. Keep the library/application at a stable path. Shortcut Properties → Change icon can browse `FileCompass.Icons.dll`; an EXE's fixed embedded icon cannot be changed this way.

File icons use Windows associations, including effective user registrations and registered icon resources, with recognizable fallbacks when needed. FVICONS are named-PNG data ZIPs, not executable assemblies. Icon DLLs are mapped as resources without executing code. Original app artwork is separate from third-party Fluent action icons; see `libraries/licenses/FLUENT-ICONS-LICENSE.txt`.


## Common locations and right-click menus

Drive buttons remain at the left of their row. The common-folders dropdown at the right contains Desktop, Downloads, Documents, Pictures, Music, Videos, the user profile and Recycle Bin; drives are not repeated in it. Drive label, free space and total capacity appear in the panel footer. Their actual redirected paths are resolved through SHGetKnownFolderPath, and selecting one bypasses remembered per-drive directories.

A short right-click toggles the item mark while preserving other marks. A stationary hold shows feedback after 250 ms and opens the File Compass command menu at 600 ms. Its final Windows menu command opens the classic native shell menu for supported local selections, using IContextMenu with IContextMenu2/3 message forwarding. Right-button dragging marks files. Remote/archive items retain application commands; unsupported commands are disabled. Holding on a marked item preserves its group; holding on an unmarked item targets that item.

The Preview context command and F3 always use the internal QuickPreviewPane in the opposite panel, including XLS and other supported document formats. Previewing an archive member or a downloaded remote file also stays inside the application. Associated opening and explicit editing remain separate commands.


## Right-button selection and menus

Short right-click toggles the target mark without clearing other marks, including Windows selection mode. Right-drag marks/unmarks a range. Holding without dragging shows delayed feedback after 250 ms, labelled File Compass menu, then opens the application menu at 600 ms. Release after opening does not toggle selection. Escape/capture loss cancels. The native Windows menu is available as the final application-menu item for supported local selections; it is not opened by the hold gesture itself. Shift+F10 and the menu key still open the application menu directly.


## Open tabs and saved bookmarks

Open tabs occupy the breadcrumb/path row itself; there is no additional tab row. The active tab fits its path and close control up to the available width. Inactive tabs show the last folder or a custom label. A long active path keeps its root and tail visible when it has to be shortened, with the full path in a tooltip. The plus button stays visible while the tabs scroll. Right-click a tab to copy or edit its path, open the location in the other panel, search, compare panels, include subfolders, or manage its saved view. Ctrl+L or the pencil edits the active path. The + button/Ctrl+T opens another tab and Ctrl+W closes one; each panel has its own open-tab set. Saved bookmarks are a separate list under the star button. Adding a bookmark saves a location without opening a tab; choosing one from the panel star menu opens a new tab in that panel. The toolbar star opens existing bookmarks and their management commands. The list supports groups and bookmark management. Closing a tab does not remove a saved bookmark. Open tabs and bookmarks are saved separately, and startup restores only the open-tab set. Locked home tabs remain a separate optional tab behavior. Missing folders, disconnected drives and remote tabs retain their saved targets and show an explicit unavailable state with Retry/connect; remote login occurs only on explicit opening.

## Current panel navigation and carousel

One blue-grey row combines location tabs and navigation. The active panel has a darker row and a stronger blue border. White tabs have rounded top corners and bare close crosses; + opens a new tab. Root and parent-folder buttons sit at the right. The last tab can be closed, leaving an empty panel; disconnected FTP/SFTP tabs remain closable. Closing a connected remote tab first offers disconnect, close the tab only, or cancel.

File headers have a bottom separator and thin vertical cell dividers. Right-click a header to toggle or reorder available columns, or drag headers. The sort arrow follows its column. Counts and widths are fitted to the available panel width; there is no horizontal file-list scrollbar. SI MB/GB, IEC MiB/GiB and exact bytes are configurable under Settings → Panels alongside hidden/system visibility.

Dragging a divider redistributes its two visible neighboring columns' width within that panel. The last column has no divider to resize. In Details view, right-click a column header and select **Group by** to group files by type (extension), modification date or size range. The group heading shows the number of visible items. The active sort still applies within each group. Grouping is stored with the tab's view.

In viewer settings, **Text and code** controls the hover preview for text and other files. Under **Images and media**, images and video frames each have their own option. Leave the pointer over the file icon or thumbnail for one second to show a temporary preview. Local videos show the six sampled frames without playing the video. Moving off the name, scrolling, or clicking closes the popup.

Preview starts with only the selected file. More/Less at the start of its information row uses down/up chevrons to show/hide the file carousel. Choosing a carousel item updates the source-panel cursor while preserving marks. Opening another folder refreshes carousel candidates from that folder. Details opens a separate popup. Wheel scrolling keeps the carousel scrollbar off the pictures, and document/archive icons remain small.

Open **Recycle Bin** from the common folders dropdown. To restore selected files or folders, open the context menu or click **Recycle Bin** in the panel footer. Windows restores items to their original locations. **Empty Recycle Bin** applies to all drives after Windows asks for confirmation.

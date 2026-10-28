# Panels, icons and previews

The Start menu contains Open in Explorer, Run Command Prompt and Run PowerShell, using the active local folder. The former External tools submenu and development-only project-folder shortcut are no longer shown.

Network combines Windows discovery, mapped drives, saved network connections and bounded IPv4 file-service probes on attached private networks. It checks SMB (445), FTP (21), SSH/SFTP (22), implicit FTPS (990) and NFS (2049), showing results progressively. SMB opens shared folders; newly found transfer services open their connection settings before connecting. A listening port indicates a possible service, not confirmed credentials or permissions. Large prefixes are bounded to the adapter's /24, with at most 1,024 candidate addresses and a time limit. Devices on other VPN subnets, IPv6-only devices, nonstandard ports or services hidden by a firewall may require a saved/manual connection. Permission-blocked probes produce a warning in the Network status tooltip.

## Layout and navigation

The toolbar cycles side-by-side → stacked → single panel and shows the active layout. View → Panel layout also selects it. Ctrl+H switches dual-panel orientation; Ctrl+Shift+H switches single/last dual layout. Single-panel mode retains the active side; the other side keeps its folder, tabs and connection for later.

The toolbar groups navigation/selection separately from preview/edit/thumbnails. Hidden files toggles hidden/system visibility in both panels while retaining name filters. Invert selection and direction arrows follow it; arrows open the source folder in the other panel. Edit uses F4. Thumbnails change the list, not the preview.

Returning from a subfolder selects that subfolder in the parent. Automatic local refresh leaves the list enabled and applies only added/removed/changed rows, preserving unchanged row instances, marks and scroll position. Network watching is separately configurable and can be disabled for slow paths; Ctrl+R refreshes manually.

Single-panel mode has an adjustable folder tree with Windows names/icons: Home/Quick Access, Desktop, profile, registered cloud folders, This PC, Network and other available system roots such as Libraries, Recycle Bin or Linux. This PC includes local/mapped drives; Network lists computers and devices discovered by Windows, with cached and partial results available during discovery. Computers open their shared folders inside File Compass; other devices open through Explorer. Ctrl+R refreshes discovery. Redirected OneDrive paths come from Windows. Branches load asynchronously on expansion and can be retried. The tree tracks navigation and retains selection/expanded branches. Open an actual filesystem folder for file operations. Dual-panel modes hide the tree, even when older tabs/views requested it.

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

After adjusting columns, widths, sorting and display mode, right-click the header to save a view for this folder or this folder and descendants. Saved views apply when automatic views are enabled. More specific paths win; explicit saved views take priority over content inference. Remove saved folder view removes the current-path preset. Presets use `config/folder-views.xml`; the switch uses `config/settings.xml`.

## Icon libraries

Action sets are `Actions.Fluent.fvicons` (Windows 11 color), `Actions.Flat.fvicons` and `Actions.Outline.fvicons`. `FileCompass.Icons.dll` contains seven app-icon variants with ten sizes from 16 to 256 px. Settings → Icons previews action/app choices immediately; Save persists them, Cancel restores saved choices. Changing the app icon updates application windows, not the embedded EXE icon or existing Windows shortcuts.

Create shortcut with selected icon writes a `.lnk` referencing the shared library. For the taskbar, unpin the old item and pin the new shortcut; Windows can cache old pinned icons. Keep the library/application at a stable path. Shortcut Properties → Change icon can browse `FileCompass.Icons.dll`; an EXE's fixed embedded icon cannot be changed this way.

File icons use Windows associations, including effective user registrations and registered icon resources, with recognizable fallbacks when needed. FVICONS are named-PNG data ZIPs, not executable assemblies. Icon DLLs are mapped as resources without executing code. Original app artwork is separate from third-party Fluent action icons; see `libraries/licenses/FLUENT-ICONS-LICENSE.txt`.


## Common locations and right-click menus

The drive selector includes Windows known folders below physical and network drives: Desktop, Downloads, Documents, Pictures, Music, Videos and the user profile. Their actual redirected paths are resolved through SHGetKnownFolderPath, and selecting one bypasses remembered per-drive directories.

A short right-click toggles the item mark while preserving other marks. A stationary hold shows feedback after 250 ms and opens the File Compass command menu at 600 ms. Its final Windows menu command opens the classic native shell menu for supported local selections, using IContextMenu with IContextMenu2/3 message forwarding. Right-button dragging marks files. Remote/archive items retain application commands; unsupported commands are disabled. Holding on a marked item preserves its group; holding on an unmarked item targets that item.

The Preview context command and F3 always use the internal QuickPreviewPane in the opposite panel, including XLS and other supported document formats. Previewing an archive member or a downloaded remote file also stays inside the application. Associated opening and explicit editing remain separate commands.


## Right-button selection and menus

Short right-click toggles the target mark without clearing other marks, including Windows selection mode. Right-drag marks/unmarks a range. Holding without dragging shows delayed feedback after 250 ms, labelled File Compass menu, then opens the application menu at 600 ms. Release after opening does not toggle selection. Escape/capture loss cancels. The native Windows menu is available as the final application-menu item for supported local selections; it is not opened by the hold gesture itself. Shift+F10 and the menu key still open the application menu directly.


## Open tabs and saved bookmarks

Open tabs occupy the breadcrumb/path row itself; there is no additional tab row. Tabs use bounded widths based on their path lengths; short drive roots stay compact. When paths do not fit, both active and inactive tabs reuse the copy dialog middle-compaction rule, keeping the drive/root and path ending visible with the full path in a tooltip. The active tab retains breadcrumb navigation when its full path fits. Ctrl+L or the pencil edits the active path. The + button/Ctrl+T opens another tab and Ctrl+W closes one; each panel has its own open-tab set. Saved bookmarks are a separate list under the star button. Adding a bookmark saves a location without opening a tab; choosing one from the panel star menu opens a new tab in that panel. The list supports groups and bookmark management. Closing a tab does not remove a saved bookmark. Open tabs and bookmarks are saved separately, and startup restores only the open-tab set. Locked home tabs remain a separate optional tab behavior. Missing folders, disconnected drives and remote tabs retain their saved targets and show an explicit unavailable state with Retry/connect; remote login occurs only on explicit opening.

# Source and distribution review — 1 November 2026

This inventory describes the checked-in application, not a new public binary release. Upstream license texts remain unchanged. The application author has not selected an open-source license for File Compass itself; third-party licenses do not grant rights to the application code.

| Area | Retained provenance and notices | Review result |
|---|---|---|
| Managed UI/editor/parser libraries | `source/ui/manifest.json`, `source/ui/licenses`, pinned restore scripts | MIT/BSD/Apache/LGPL and Microsoft SDK terms remain component-specific. SharpSevenZip and TagLibSharp remain separate replaceable DLLs. |
| Native FTP/SFTP/SCP | `source/plugins/network/BUILD-MANIFEST.txt`, `licenses`, `source/native` | curl/libssh2/LibreSSL and linked dependency notices retained. Build headers/libraries are build inputs, not disposable test output. |
| Archive engine | `source/plugins/7zip/License.txt` | 7-Zip terms retained; WinRAR is an optional separately licensed installation. ARJ/LHa download terms are documented in ARCHIVES.md. |
| SMART | `source/plugins/smart/LICENSE.txt`, `smartmontools-7.5.tar.gz` | GPLv2 notice and matching source archive retained. The executable runs as a separate process. |
| Media images | `source/plugins/media/SOURCE.json`, `MAGICK-NOTICE.txt` | Magick.NET 15.0.0 Q8 **OpenMP** x64, matching Core/native DLL and Microsoft `vcomp140.dll`. Keep the set together. |
| Media audio/video | `source/plugins/media/SOURCE.json`, `FFMPEG-LICENSE.txt`, `GPL-3.0.txt` | Shipped build is BtbN LGPL shared, not a GPL/nonfree build. Exact complete corresponding source/dependency-source distribution remains a public-release prerequisite. |
| Document renderer | `source/preview` component manifest and license files | Local JS libraries and WebView2 SDK notices retained. The WebView2 system runtime is not bundled. |
| Cloud, search and SSHFS | rclone and Everything license files; `source/plugins/sshfs/README.md` | rclone is MIT; Everything SDK is distinct from its external indexing service. WinFsp/SSHFS installers are optional external components, not bundled system drivers. |
| Icons | `source/ui/icons/LICENSE.txt`, `SOURCE.json` | Microsoft Fluent UI System Icons MIT attribution retained. |

## Items required before public binary distribution

1. Prepare an exact corresponding-source bundle, build recipes and dependency notices for the pinned FFmpeg build. A mutable `latest` URL or a link to a different revision is insufficient. Consult the [FFmpeg license checklist](https://www.ffmpeg.org/legal.html).
2. Retain the applicable Microsoft redistribution terms and publisher entitlement for the OpenMP runtime supplied in the official Magick.NET package. It is not relicensed under Apache. Consult [Microsoft's redistribution guidance](https://learn.microsoft.com/en-us/cpp/windows/determining-which-dlls-to-redistribute).
3. If the user activates a vendor component update, review the license/source obligations for that exact downloaded version. In particular, the FFmpeg updater uses gyan.dev full static builds, which are distinct from the original BtbN LGPL shared package. The shipped-build notice must not be presented as covering arbitrary replacements.
4. Keep publisher OAuth configuration, account tokens, credentials, user files and local profiles out of Git and release archives. Application update release URLs remain unconfigured; this source upload does not enable automatic application updates or publish a binary release.

## Reproducible development inputs

`source/prepare-media.ps1 -CompilerOnly` restores and SHA-256-verifies the pinned Roslyn compiler without replacing runtime binaries. The full restore verifies all pinned archives. The FFmpeg download URL rotates, so preserve the verified original archive under ignored `source/downloads/media`; never replace the expected hash merely to make a changed download pass. OpenMP and runtime versions follow the official pinned [Magick.NET package](https://github.com/dlemstra/Magick.NET).

## Documentation scope

All eight interface catalogs are maintained together with equal key sets and format placeholders. Offline help is an external HTML package shared by the native viewer and browsers, with 44 topics and 28 screenshot scenes per language. It is maintained in Czech and English; other interface languages explicitly show English help. Original third-party legal notices are not translated. Historical validation records in ignored development directories are not current product specifications.

## Current inventory and cleanup boundaries

`source/dependencies.json` classifies all 49 pinned runtime DLL/EXE/SFX files and hashes 70 original notice/provenance files. `python tools/check-dependency-notices.py` checks the inventory without changing upstream notices; `--refresh` is reserved for a reviewed dependency change. The content checker runs it automatically. The SDK project and PowerShell build both package external HTML help; publisher OAuth credentials are excluded from both.

Maintenance removes ignored historical builds, test outputs and executable backups only after checking workspace boundaries, Git tracking, running programs and nested repositories. Compact test results are retained under ignored maintenance records. The installed application, local profiles, official dependency downloads, compiler/native build inputs, license texts and matching-source archives are preserved. Vendor update rollback packages in the user profile are retained.

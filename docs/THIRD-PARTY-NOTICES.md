# Third-party notices

Paths below refer to the installation root unless a source path is stated. Original upstream license/copyright texts are retained with each component; this English inventory does not replace them.

## libcurl 8.22.0 and libssh2 1.11.1

Network operations use the native `FileCompassNetwork.dll` adapter in `plugins/network`. It statically links libcurl (curl/MIT-like license), libssh2 (BSD-3-Clause) and dependencies from the official curl 8.22.0_3 Windows x64 package. libcurl handles FTP/FTPS; libssh2 handles SFTP/SCP/SSH. LibreSSL 4.3.3 supplies cryptography. Dependencies include zlib-ng, zstd, Brotli, libpsl, the public suffix list, nghttp2, nghttp3 and ngtcp2.

Original notices are in `plugins/network/licenses`; `BUILD-MANIFEST.txt` lists exact components/source links and the plugin manifest records package provenance/hashes. Static libraries/headers/original downloads are build-time inputs under `source/downloads/network`; adapter sources/build scripts are in `source/native`. No separate runtime installation is required. FluentFTP, SSH.NET and WinSCP are no longer runtime components.

Sources: https://curl.se/windows/ and https://libssh2.org/.

## External SSHFS support

WinFsp — Windows File System Proxy, Copyright (C) Bill Zissimopoulos: https://github.com/winfsp/winfsp (GPLv3 with FLOSS exception; commercial licensing https://winfsp.dev/com/).
SSHFS-Win: https://github.com/winfsp/sshfs-win ; component licenses/sources are listed in its License.txt/repository.

File Compass does not bundle these system components; it downloads unmodified official GitHub release installers only when requested.

## 7-Zip 26.04

Unmodified `7z.exe` and `7z.dll` are distributed in `plugins/7zip` without installation. Copyright (C) 1999–2026 Igor Pavlov. LGPL and additional terms are in `plugins/7zip/License.txt`. Corresponding sources: https://github.com/ip7z/7zip/releases/tag/26.04 and https://www.7-zip.org/download.html.
File Compass uses a separate command-line process and the bundled codec. WinRAR is not bundled; users select their own installation.

## WPF UI 4.3.0

WPF UI / WPF UI Abstractions (lepo.co), MIT. The .NET Framework 4.7.2 libraries run under File Compass's .NET Framework 4.8 target. Microsoft dependencies: System.Memory 4.6.3, System.Buffers 4.6.1, System.Numerics.Vectors 4.6.1 and System.Runtime.CompilerServices.Unsafe 6.1.2 (MIT).

Libraries are in `libraries`, original notices in `libraries/licenses`. `libraries/manifest.json` records versions, official NuGet URLs and SHA-256. `CompactMenu.xaml` adapts WPF UI MenuItem for compact menus and retains its copyright notice. Other controls use library templates.
Source: https://github.com/lepoco/wpfui/releases/tag/4.3.0.

## SharpSevenZip

Managed wrapper: SharpSevenZip 2.0.128, LGPL-3.0-or-later.
Source: https://github.com/JeremyAnsel/SharpSevenZip/tree/f4c16a6a95ace333fa33f38c1d69e44d77812eee
Package: https://www.nuget.org/packages/SharpSevenZip/2.0.128
License and package metadata: libraries/licenses/SharpSevenZip.
The wrapper is distributed as a separate unmodified DLL; it uses the existing bundled native 7z.dll.
The pinned package and assembly hashes are in tools/fetch-archive-library.ps1 in the source distribution.

## Microsoft Fluent UI System Icons

Copyright Microsoft Corporation. MIT license: libraries/licenses/FLUENT-ICONS-LICENSE.txt.
Source: https://github.com/microsoft/fluentui-system-icons
Pinned revision: a347af95478f989038cccbf085978865264c048a (libraries/licenses/FLUENT-ICONS-SOURCE.json).
Original SVG and PNG sources are in source/ui/icons. Runtime action icons are bundled in icons/Actions.*.fvicons.
Color variants are preserved; regular command icons use blue, amber, green or red
according to their action. Asset import: tools/fetch-fluent-icons.cjs.


## TagLibSharp 2.3.0
Media metadata reader, LGPL-2.1. Source: https://github.com/mono/taglib-sharp/tree/TaglibSharp-2.3.0.0 . License: libraries/licenses/TAGLIBSHARP-LICENSE.txt. NuGet package SHA-256: 3C3F5B55988F69E0BC84DC760FEB8351FEF4D5C09A322D88462E683D334DFBFC. The unmodified library remains a separate DLL and can be replaced.


## AvalonEdit 6.3.1.120
MIT; source https://github.com/icsharpcode/AvalonEdit/tree/v6.3.1 . License libraries/licenses/AVALONEDIT-LICENSE.txt. NuGet package SHA-256 8AC3E5AA2BB6178D92CBE72C7900A683FAB0AEE66A8EAFA481611C444DBEE3E3.

## Markdig 0.37.0
BSD-2-Clause; source https://github.com/xoofx/markdig/tree/0.37.0 . License libraries/licenses/MARKDIG-LICENSE.txt. NuGet package SHA-256 9CFA31D3A2EB694D3164F1A3FAB41A040C698A22E784CD4D76E50489D5698335.

## Everything SDK
Copyright 2022 David Carpenter, MIT. Source and SDK https://www.voidtools.com/support/everything/sdk/ . License plugins/everything/LICENSE.txt. Downloaded SDK archive SHA-256 F5716D9513CCE6B462B5170A0A2E7E081E191D9BCAC6774F5DECC061497DF443. The optional search feature requires a separately running Everything; this distribution contains only its SDK DLL. No indexing service is installed or started.


PdfPig 0.1.16 (Apache-2.0), https://github.com/UglyToad/PdfPig; Microsoft.Bcl.HashCode 6.0.0 (MIT). License texts and package metadata are in libraries/licenses/.

## rclone 1.75.2 — cloud storage

Google Drive, OneDrive and Dropbox use official rclone for Windows x64.
Source/license: https://github.com/rclone/rclone/tree/v1.75.2
Package: https://downloads.rclone.org/v1.75.2/rclone-v1.75.2-windows-amd64.zip
MIT license: `plugins/cloud/LICENSE.txt`; original documentation: `README.txt`. The executable hash is compiled into PluginHashes.cs and checked before execution. The package was checked against official SHA256SUMS; build-time downloads are under `source/downloads/rclone`.

## Local document preview

- docx-preview 0.3.7: MIT, https://github.com/VolodymyrBaydalka/docxjs; locally patched to render symbols and stylesheet contents without HTML parsing.
- JSZip 3.10.1: MIT option, https://github.com/Stuk/jszip
- SheetJS Community Edition 0.20.3: Apache-2.0, https://cdn.sheetjs.com/; spreadsheets are rendered using text-only DOM cells.
- Microsoft.Web.WebView2 SDK 1.0.4258.31: Microsoft license; the system runtime is not bundled.

The document HTML and JavaScript are embedded in `preview/preview.dll`, together with copies of the license texts and the source component manifest. License files remain readable in `preview`. `WebView2Loader.dll` remains a separate native component. Documents render locally; packaging does not change the component licenses.

## SMART

smartmontools 7.5 / smartctl.exe: GPLv2. License and corresponding source archive are in `plugins/smart`. The tool runs as a separate process.
Source: https://github.com/smartmontools/smartmontools/releases/tag/RELEASE_7_5

## Media conversion

- Magick.NET 15.0.0 Q8 OpenMP x64 and Magick.NET.Core 15.0.0: Apache-2.0 wrapper; ImageMagick and native dependencies have their own licenses. The complete package notice is `plugins/media/MAGICK-NOTICE.txt`.
- FFmpeg `n9.0.2-35-g9a9b70bb8c-20261024`: BtbN Windows x64 **LGPL shared** build, separate processes. `FFMPEG-LICENSE.txt` and `GPL-3.0.txt` contain LGPLv3 and its incorporated GPLv3 terms. This is not the GPL/nonfree build. Runtime decoding/encoding is limited to local files by protocol/demuxer allowlists. Package SHA-256 and source links are in `plugins/media/SOURCE.json`.
- Microsoft Visual C++ OpenMP runtime `vcomp140.dll`: Microsoft-licensed native runtime supplied unchanged inside the pinned official Magick.NET Q8 OpenMP package. It is not covered by the Apache license of the wrapper. App-local redistribution is subject to the applicable Microsoft Visual Studio / Visual C++ redistribution terms. See https://learn.microsoft.com/en-us/cpp/windows/determining-which-dlls-to-redistribute . The publisher must retain the applicable redistribution entitlement/terms before public release. Managed/native Magick.NET and this runtime are updated as one set.
- Roslyn compiler 4.14.0: MIT, build dependency only, not distributed with the application.

Dependency archives are retained under `source/downloads/media` and restored/verified with `source/prepare-media.ps1`. Do not silently update rotating FFmpeg downloads. Before public distribution, prepare the exact corresponding sources/build scripts for FFmpeg and its LGPL dependencies and all dependency notices; upstream links alone are not a complete source distribution. No patent or Dolby trademark licenses are represented by this integration. There is no Dolby Atmos encoder.


## Linux filesystem reader

LTRData.DiscUtils.Ext, Core and Streams 1.0.90 are distributed under MIT. Upstream: https://github.com/LTRData/DiscUtils at commit 414c7f7b2d82c42b07bc6fdfc18a763331334697. Copyright Kenneth Bell, Quamotion and contributors; the original notice is included in libraries/licenses/DiscUtils/DISCUTILS-LICENSE.txt.

LTRData.Extensions 1.0.23: MIT, copyright 2011–2026 Olof Lagerkvist, LTR Data; source https://github.com/LTRData/Library at commit 915cdf4eaac22bffd2623de0721f7e7d18c93268. See LTRDATA-EXTENSIONS-LICENSE.txt.

System.Collections.Immutable 9.0.20, System.Text.Encoding.CodePages 9.0.20 and System.Threading.Tasks.Extensions 4.6.3: MIT, Microsoft and contributors. Package license and available third-party notices are included alongside the library notices. SOURCE.json records exact package versions, download hashes, source commits and DLL hashes. Dependencies are restored by source/prepare-ext.ps1. No filesystem driver is installed. The reader uses a version-pinned metadata adapter to inspect original inode types before file operations; upstream DLLs are unmodified.


## YamlDotNet 16.3.0

The internal editor validates YAML using the unmodified YamlDotNet net47 assembly (compatible with .NET Framework 4.8). MIT; copyright Antoine Aubry and contributors. The libyaml-derived implementation retains Kirill Simonov’s MIT notice. Both original notices and exact package/assembly provenance are in `libraries/licenses/YamlDotNet`. The runtime DLL is `libraries/YamlDotNet.dll`. Source: https://github.com/aaubry/YamlDotNet/tree/v16.3.0 . Restore with `source/prepare-editor.ps1`, which verifies pinned SHA-256 values for the package, assembly and license texts. Validation streams parser events without expanding aliases or constructing arbitrary objects.

## Scope of this inventory

Reviewed against the checked-in runtime layout on 1 November 2026. The shipped 7-Zip remains 26.04 even if a local user has activated a newer vendor package. The inventory covers 49 pinned runtime binaries and 70 original notice/provenance files; exact paths and SHA-256 values are recorded in `source/dependencies.json` and checked by `tools/check-dependency-notices.py`. It also covers the 7z.sfx module and all document-preview JavaScript notices. Original upstream notices have not been rewritten or translated. No open-source license for File Compass application code is granted by this third-party inventory. Optional tool downloads retain their own vendor terms. See [distribution audit](DISTRIBUTION-AUDIT.md) for outstanding public-release requirements and restore constraints.

# Third-party notices

Paths below refer to the packaged `config` directory unless a source path is stated. Original upstream license/copyright texts are retained with each component; this English inventory does not replace them.

## libcurl 8.22.0 and libssh2 1.11.1

Network operations use the native `CommanderNetwork.dll` adapter in `plugins/network`. It statically links libcurl (curl/MIT-like license), libssh2 (BSD-3-Clause) and dependencies from the official curl 8.22.0_3 Windows x64 package. libcurl handles FTP/FTPS; libssh2 handles SFTP/SCP/SSH. LibreSSL 4.3.3 supplies cryptography. Dependencies include zlib-ng, zstd, Brotli, libpsl, the public suffix list, nghttp2, nghttp3 and ngtcp2.

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

Libraries are in `ui`, original notices in `ui/licenses`. `ui/manifest.json` records versions, official NuGet URLs and SHA-256. `CompactMenu.xaml` adapts WPF UI MenuItem for compact menus and retains its copyright notice. Other controls use library templates.
Source: https://github.com/lepoco/wpfui/releases/tag/4.3.0.

# SharpSevenZip

Managed wrapper: SharpSevenZip 2.0.128, LGPL-3.0-or-later.
Source: https://github.com/JeremyAnsel/SharpSevenZip/tree/f4c16a6a95ace333fa33f38c1d69e44d77812eee
Package: https://www.nuget.org/packages/SharpSevenZip/2.0.128
License and package metadata: ui/licenses/SharpSevenZip.
The wrapper is distributed as a separate unmodified DLL; it uses the existing bundled native 7z.dll.
The pinned package and assembly hashes are in tools/fetch-archive-library.ps1 in the source distribution.

# Microsoft Fluent UI System Icons

Copyright Microsoft Corporation. MIT license: ui/licenses/FLUENT-ICONS-LICENSE.txt.
Source: https://github.com/microsoft/fluentui-system-icons
Pinned revision: a347af95478f989038cccbf085978865264c048a (ui/licenses/FLUENT-ICONS-SOURCE.json).
Original SVG and PNG sources are in source/ui/icons. Runtime action icons are bundled in icons/Actions.*.fvicons.
Color variants are preserved; regular command icons use blue, amber, green or red
according to their action. Asset import: tools/fetch-fluent-icons.cjs.


# TagLibSharp 2.3.0
Media metadata reader, LGPL-2.1. Source: https://github.com/mono/taglib-sharp/tree/TaglibSharp-2.3.0.0 . License: ui/licenses/TAGLIBSHARP-LICENSE.txt. NuGet package SHA-256: 3C3F5B55988F69E0BC84DC760FEB8351FEF4D5C09A322D88462E683D334DFBFC. The unmodified library remains a separate DLL and can be replaced.


# AvalonEdit 6.3.1.120
MIT; source https://github.com/icsharpcode/AvalonEdit/tree/v6.3.1 . License ui/licenses/AVALONEDIT-LICENSE.txt. NuGet package SHA-256 8AC3E5AA2BB6178D92CBE72C7900A683FAB0AEE66A8EAFA481611C444DBEE3E3.

# Markdig 0.37.0
BSD-2-Clause; source https://github.com/xoofx/markdig/tree/0.37.0 . License ui/licenses/MARKDIG-LICENSE.txt. NuGet package SHA-256 9CFA31D3A2EB694D3164F1A3FAB41A040C698A22E784CD4D76E50489D5698335.

# Everything SDK
Copyright 2022 David Carpenter, MIT. Source and SDK https://www.voidtools.com/support/everything/sdk/ . License plugins/everything/LICENSE.txt. Downloaded SDK archive SHA-256 F5716D9513CCE6B462B5170A0A2E7E081E191D9BCAC6774F5DECC061497DF443. The optional search feature requires a separately running Everything; this distribution contains only its SDK DLL. No indexing service is installed or started.


PdfPig 0.1.16 (Apache-2.0), https://github.com/UglyToad/PdfPig; Microsoft.Bcl.HashCode 6.0.0 (MIT). License texts and package metadata are in config/ui/licenses/.

## rclone 1.75.2 — cloud storage

Google Drive, OneDrive and Dropbox use official rclone for Windows x64.
Source/license: https://github.com/rclone/rclone/tree/v1.75.2
Package: https://downloads.rclone.org/v1.75.2/rclone-v1.75.2-windows-amd64.zip
MIT license: `config/plugins/cloud/LICENSE.txt`; original documentation: `README.txt`. The executable hash is compiled into PluginHashes.cs and checked before execution. The package was checked against official SHA256SUMS; build-time downloads are under `source/downloads/rclone`.

## Local document preview

- docx-preview 0.3.7: MIT, https://github.com/VolodymyrBaydalka/docxjs
- JSZip 3.10.1: MIT option, https://github.com/Stuk/jszip
- SheetJS Community Edition 0.20.3: Apache-2.0, https://cdn.sheetjs.com/
- Microsoft.Web.WebView2 SDK 1.0.4258.31: Microsoft license; the system runtime is not bundled.

Licenses are in `config/preview`. Documents render locally.

## SMART

smartmontools 7.5 / smartctl.exe: GPLv2. License and corresponding source archive are in `config/plugins/smart`. The tool runs as a separate process.
Source: https://github.com/smartmontools/smartmontools/releases/tag/RELEASE_7_5
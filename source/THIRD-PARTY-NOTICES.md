# libcurl 8.22.0 a libssh2 1.11.1

Síťové operace používají vlastní CommanderNetwork.dll v plugins/network.
Knihovna staticky spojuje libcurl (licence curl/MIT-like), libssh2 (BSD-3-Clause)
a závislosti z oficiálního balíčku curl 8.22.0_3 pro Windows x64.
Libcurl zajišťuje FTP/FTPS; libssh2 SFTP/SCP a související SSH operace.
LibreSSL 4.3.3 poskytuje kryptografii. Balíček zahrnuje také zlib-ng, zstd,
Brotli, libpsl, veřejný seznam domén, nghttp2, nghttp3 a ngtcp2.

Původní licenční texty a oznámení jsou v plugins/network/licenses.
Přesné komponenty a odkazy na jejich zdroje uvádí BUILD-MANIFEST.txt.
Manifest pluginu obsahuje původ balíčku a ověřené SHA-256.
Statické knihovny, hlavičky a původní balíček jsou v source/downloads/network;
zdroj adaptéru a sestavení v source/native. Není nutná samostatná instalace.

Zdroj: https://curl.se/windows/ a https://libssh2.org/.
FluentFTP, SSH.NET a WinSCP již nejsou součástí výsledného programu.

# Externí podpora SSHFS

WinFsp – Windows File System Proxy, Copyright (C) Bill Zissimopoulos.
https://github.com/winfsp/winfsp (GPLv3 s výjimkou FLOSS; komerční licence
https://winfsp.dev/com/). SSHFS-Win: https://github.com/winfsp/sshfs-win,
licence a zdroje jednotlivých částí v jeho License.txt a repozitáři.
Commander neobsahuje kopii těchto binárních komponent. Na žádost uživatele
stahuje nezměněné oficiální instalátory z příslušných GitHub releases.

# 7-Zip 26.04

Nezměněné 7z.exe a 7z.dll jsou distribuovány v plugins/7zip bez instalace.
Copyright (C) 1999–2026 Igor Pavlov. Licence LGPL a další podmínky v přiloženém
plugins/7zip/License.txt. Původní zdroje této verze jsou dostupné na
https://github.com/ip7z/7zip/releases/tag/26.04 a https://www.7-zip.org/download.html.
CommanderCZ komunikuje se samostatným programem přes příkazovou řádku.
WinRAR není distribuován; uživatel nastavuje cestu ke své instalaci.

# WPF UI 4.3.0

Rozhraní používá WPF UI a WPF UI Abstractions (lepo.co, licence MIT).
Přibalené knihovny pro .NET Framework 4.7.2 fungují v CommanderCZ na .NET Framework 4.8.
Závislosti Microsoftu: System.Memory 4.6.3, System.Buffers 4.6.1,
System.Numerics.Vectors 4.6.1 a System.Runtime.CompilerServices.Unsafe 6.1.2 (MIT).
Binární knihovny jsou v ui; původní licence a oznámení v ui/licenses.
ui/manifest.json zaznamenává verze, adresy oficiálních balíčků NuGet a SHA-256.
CompactMenu.xaml je upravená šablona WPF UI MenuItem pro menší výšku menu;
obsahuje původní autorské oznámení. Ostatní ovládací prvky využívají šablony knihovny.
Zdroj: https://github.com/lepoco/wpfui/releases/tag/4.3.0.

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

# rclone 1.75.2 – cloudová úložiště
Cloudové konektory Google Drive, OneDrive a Dropbox používají oficiální rclone pro Windows x64.
Zdroj a licence: https://github.com/rclone/rclone/tree/v1.75.2
Oficiální balíček: https://downloads.rclone.org/v1.75.2/rclone-v1.75.2-windows-amd64.zip
Licence MIT je přiložena v config/plugins/cloud/LICENSE.txt, uživatelská dokumentace v README.txt.
Při sestavení se kontrolní součet rclone.exe kompiluje do PluginHashes.cs a před spuštěním se ověřuje.
Stažený archiv byl ověřen vůči oficiálnímu SHA256SUMS; zdrojové podklady jsou v source/downloads/rclone.
## Local document preview
- docx-preview 0.3.7: MIT, https://github.com/VolodymyrBaydalka/docxjs
- JSZip 3.10.1: used under MIT, https://github.com/Stuk/jszip
- SheetJS Community Edition 0.20.3: Apache-2.0, https://cdn.sheetjs.com/
- Microsoft.Web.WebView2 SDK 1.0.4258.31: Microsoft license; system runtime is not bundled.
Licenses are included in config/preview. Documents are rendered locally.
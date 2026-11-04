# File Compass licensing

Original File Compass application code, build scripts and original documentation
are licensed under the MIT License. Copyright (c) 2026 Jan Rathouský.
The complete license is in `LICENSE` at the repository root and `help/LICENSE.txt`
in the portable package.

This grant covers material owned by the File Compass author. It does not
relicense third-party source code, adapted templates, icons, libraries, tools or
runtime components. Their original copyright notices and licenses remain in
force, including notices embedded in adapted files. See
[third-party notices](THIRD-PARTY-NOTICES.md), the original texts in
`libraries/licenses`, `plugins` and `preview`, and `dependencies.json`.
The MIT License does not grant rights to third-party trademarks.

Use, modification, redistribution and commercial use of the original application
are permitted under MIT. There is no royalty or requirement to publish changes
to that original code. Third-party LGPL/GPL/MPL obligations still apply to their
respective components; the entire portable distribution is not exclusively MIT.

## Rebuilding with modified LGPL libraries

The program verifies the distributed dependency hashes before loading code.
These checks protect an installed official build; they do not prohibit modifying,
debugging or rebuilding File Compass or its libraries.

To use an interface-compatible modified SharpSevenZip or TagLibSharp build:

1. Obtain the File Compass source revision corresponding to the release.
2. Build the library from its corresponding source and replace the DLL in
   `source/ui` (`SharpSevenZip.dll` or `TagLibSharp.dll`). Keep the original
   licensing notices and document your modifications.
3. Restore the pinned compiler with
   `powershell -NoProfile -ExecutionPolicy Bypass -File source/prepare-media.ps1 -CompilerOnly`.
4. Run `powershell -NoProfile -ExecutionPolicy Bypass -File source/build.ps1 -Release -OutputDirectory ai-docs/custom-build`.
   The output directory must be empty. The build regenerates `PluginHashes.cs`
   from the actual library files, then compiles the application against them.
5. Run the complete rebuilt folder. No publisher signing key, activation or
   permission from the author is required. A custom build is not an official
   publisher-signed File Compass release.

For native media-library modifications, use the matching upstream build recipes,
preserve ABI compatibility and rebuild the corresponding managed/native set as
needed. Replace its files in `source/ui` and/or `source/plugins/media` before
building File Compass. The same generated-hash process applies. Do not disable
integrity checks globally or replace binaries in another user's installation.

`tools/check-dependency-notices.py` verifies the official dependency baseline;
modified distributions must review their new license/source obligations and
explicitly refresh their own baseline. It is not a prerequisite for compiling
with an otherwise compatible replacement library.

## Corresponding sources and distribution

The source repository is https://github.com/rathousky-pro/file-compass.
Exact upstream versions and available source references are recorded in
[third-party notices](THIRD-PARTY-NOTICES.md) and package provenance records.
The matching smartmontools source archive is included in `plugins/smart`.

Public binary release preparation must also supply the required corresponding
sources for bundled LGPL/GPL components, including the exact FFmpeg build and
its covered dependencies and the covered native ImageMagick dependencies.
A link to a moving upstream branch or a different version is insufficient.
See [distribution review](DISTRIBUTION-AUDIT.md) for the remaining release work.

## Code signing

MIT is an OSI-approved license, but it does not provide a digital signature.
File Compass has not yet been approved for SignPath Foundation signing. The
7-Zip unRAR restriction and Microsoft runtime eligibility must be reviewed
against that service's conditions before applying. No signing-service approval
or antivirus clearance is implied by this license.

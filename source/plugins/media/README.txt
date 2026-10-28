Media components are local native tools. See SOURCE.json, FFMPEG-LICENSE.txt,
GPL-3.0.txt and MAGICK-NOTICE.txt for provenance and license texts.

Build preparation: powershell -NoProfile -ExecutionPolicy Bypass -File source/prepare-media.ps1
This verifies pinned archives and restores managed/native dependencies plus Roslyn.
The complete matching FFmpeg/dependency sources and notices must be packaged before
public redistribution; the current source URLs are provenance, not a complete source offer.

Image engine: Magick.NET-Q8-OpenMP-x64 15.0.0. The managed assembly, native
Magick.Native-Q8-OpenMP-x64.dll and bundled vcomp140.dll are a matched set.
They update with File Compass; standalone FFmpeg updates never replace them.

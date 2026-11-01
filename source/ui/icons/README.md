# Fluent Color action icons

`COLOR-SOURCE.json` records the current source of every action icon. Microsoft
Fluent Color assets are MIT licensed and pinned to the commit in `SOURCE.json`.
File Compass companion icons are original SVG artwork using matching gradients,
rounded shapes and layered surfaces. `LICENSE.txt` contains Microsoft's license.

The runtime reads only the local data-only `Actions.Fluent.fvicons` library.
SVG files remain editable vector masters. Import and rasterize with:

```powershell
node --use-system-ca tools/build-fluent-color.cjs
# Rebuild PNG files from local SVG masters without downloading:
node tools/build-fluent-color.cjs --offline
```

The build-time Node environment must provide `sharp`; it is never shipped or
loaded by the application. Both legacy import commands delegate to this importer.
`source/build.ps1` packages 256 x 256 PNG files without downsampling them. This
supports the existing 8-32 DIP toolbar setting, including 32 DIP at 400% DPI with
2x source resolution headroom. No runtime network access or SVG decoder is needed.

`picture` is a four-image thumbnail grid; `view` uses Fluent Color Content View.
Neither reuses a panel-layout symbol. The three hidden-file states have distinct
shapes (check, dash and strike) as well as different colors.

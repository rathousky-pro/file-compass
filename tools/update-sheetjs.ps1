param()
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$target = Join-Path $project 'source\preview\xlsx.full.min.js'
$download = $target + '.' + [Guid]::NewGuid().ToString('N') + '.download'
# Official upstream distribution. Do not use npm's obsolete xlsx package.
Invoke-WebRequest -Uri 'https://cdn.sheetjs.com/xlsx-0.20.3/package/dist/xlsx.full.min.js' -OutFile $download -TimeoutSec 60
$source = [IO.File]::ReadAllText($download)
if ($source.Length -lt 100000 -or $source -notmatch 'version\s*=\s*["'']0\.20\.3["'']') { throw 'Downloaded file is not the expected SheetJS 0.20.3 distribution. The original file was preserved.' }
[IO.File]::Replace($download,$target,$null)
$manifestPath = Join-Path $project 'source\preview\manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
foreach ($entry in $manifest) {
    $file = Join-Path $project ('source\preview\' + $entry.File)
    if (!(Test-Path -LiteralPath $file)) { $file = Join-Path $project ('source\ui\' + $entry.File) }
    $entry.Bytes = (Get-Item -LiteralPath $file).Length
    $entry.SHA256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
}
[IO.File]::WriteAllText($manifestPath,($manifest | ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
& (Join-Path $project 'source\gen-plugin-hashes.ps1')
$notices = Join-Path $project 'docs\THIRD-PARTY-NOTICES.md'
$text = [IO.File]::ReadAllText($notices)
$text = [Text.RegularExpressions.Regex]::Replace($text,'(?m)^- SheetJS Community Edition .*$','- SheetJS Community Edition 0.20.3: Apache-2.0, https://cdn.sheetjs.com/; spreadsheets are rendered using text-only DOM cells.')
[IO.File]::WriteAllText($notices,$text,[Text.UTF8Encoding]::new($false))
Write-Output 'SheetJS 0.20.3 installed. Rebuild File Compass to activate spreadsheet previews.'

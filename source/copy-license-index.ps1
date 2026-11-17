param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$destination = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'help\licenses'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$inventory = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'dependencies.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$index = [Collections.Generic.List[string]]::new()
$index.Add('# File Compass licenses and third-party notices')
$index.Add('')
$index.Add('File Compass original code and documentation: [MIT license](LICENSE.txt), copyright (c) 2026 Jan Rathousky. Third-party components retain their own terms.')
$index.Add('')
$index.Add('See [component inventory](THIRD-PARTY-NOTICES.md), [licensing guide](LICENSING.md) and [distribution audit](DISTRIBUTION-AUDIT.md). Original notices and provenance records are copied below without changes. Optional vendor updates may have different versions and terms.')
$index.Add('')
$index.Add('The smartmontools source archive is retained at [plugins/smart](../../plugins/smart/smartmontools-7.5.tar.gz); it is not duplicated here. Other corresponding-source and redistribution requirements remain open as described in the distribution audit.')
$index.Add('')
$index.Add('## Original notices and provenance')
$index.Add('')
foreach ($entry in $inventory.originalNotices) {
    $relative = [string]$entry.file
    if ($relative -eq 'plugins/smart/smartmontools-7.5.tar.gz') { continue }
    if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|/)\.\.(/|$)') { throw "Invalid notice path: $relative" }
    $source = Join-Path $PSScriptRoot $relative
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw "Notice differs from reviewed inventory: $relative" }
    $target = Join-Path (Join-Path $destination 'original') $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    $index.Add("- [$relative](original/$relative)")
}
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $destination 'LICENSE.txt') -Force
foreach ($name in @('LICENSING.md','THIRD-PARTY-NOTICES.md','DISTRIBUTION-AUDIT.md')) { Copy-Item -LiteralPath (Join-Path $root "docs\$name") -Destination (Join-Path $destination $name) -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'dependencies.json') -Destination (Join-Path $destination 'dependencies.json') -Force
[IO.File]::WriteAllLines((Join-Path $destination 'README.md'), $index, (New-Object Text.UTF8Encoding($false)))

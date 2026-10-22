$ErrorActionPreference = 'Stop'
$project = Join-Path (Split-Path -Parent $PSScriptRoot) 'source'
$download = Join-Path $project 'downloads\sharpsevenzip'
New-Item -ItemType Directory -Path $download -Force | Out-Null
$package = Join-Path $download 'package.zip'
Invoke-WebRequest -Uri 'https://api.nuget.org/v3-flatcontainer/sharpsevenzip/2.0.128/sharpsevenzip.2.0.128.nupkg' -OutFile $package
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne '30D7FA9E2BBB3E6524ED552641E4AC3C27DCB78BE9747FADCE8C84D18450CC8F') { throw 'Neočekávaný SHA-256 balíčku SharpSevenZip.' }
Expand-Archive -LiteralPath $package -DestinationPath $download -Force
$library = Join-Path $download 'lib\net48\SharpSevenZip.dll'
if ((Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash -ne 'E7A586E9342942974E79489DCCC43F99E5987343DB1F1F01DEE864E4B55FEB5C') { throw 'Neočekávaný SHA-256 SharpSevenZip.dll.' }
Copy-Item -LiteralPath $library -Destination (Join-Path $project 'ui\SharpSevenZip.dll')
$licenses = Join-Path $project 'ui\licenses\SharpSevenZip'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $download 'LICENSE') -Destination (Join-Path $licenses 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $download 'SharpSevenZip.nuspec') -Destination $licenses
Write-Output 'SharpSevenZip 2.0.128 obnoven a ověřen.'

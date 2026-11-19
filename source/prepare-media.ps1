param([switch]$ForceExtract, [switch]$CompilerOnly)
$ErrorActionPreference='Stop'
$mediaRoot=Join-Path $PSScriptRoot 'downloads\media'
New-Item -ItemType Directory -Path $mediaRoot -Force | Out-Null
$packages=@(
 @{Name='roslyn';Version='4.14.0';Url='https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/4.14.0/microsoft.net.compilers.toolset.4.14.0.nupkg';Hash='941a9cf3ea618d88d01a3dd6b1a45a06bcf07716a9f81ce4031caa3edd24a845'},
 @{Name='magick.net.core.15.1.0';Version='15.1.0';Url='https://api.nuget.org/v3-flatcontainer/magick.net.core/15.1.0/magick.net.core.15.1.0.nupkg';Hash='461232bfc8188ee3c244c7a73119a8c8d4ada46f109d7ff97952f29729bdd438'},
 @{Name='magick.net-q8-openmp-x64.15.1.0';Version='15.1.0';Url='https://api.nuget.org/v3-flatcontainer/magick.net-q8-openmp-x64/15.1.0/magick.net-q8-openmp-x64.15.1.0.nupkg';Hash='e818224f4c5e2cae6105a8313e5bc3d865b7165831843db4c47aa142d003a794'},
 @{Name='ffmpeg-lgpl';Version='n9.0.2-35-g9a9b70bb8c-20261024';Url='https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n9.0-latest-win64-lgpl-shared-9.0.zip';Hash='5a204635723d3c28365aec847b79649f340873d8cc4bde632c619187c17992fd'}
)
foreach($package in $packages){
 if($CompilerOnly -and $package.Name -ne 'roslyn'){continue}
 $archive=Join-Path $mediaRoot ($package.Name+'.zip')
 if(!(Test-Path -LiteralPath $archive)){
  $localPackage=Join-Path (Split-Path -Parent $mediaRoot) ($package.Name+'.nupkg')
  if(Test-Path -LiteralPath $localPackage){Copy-Item -LiteralPath $localPackage -Destination $archive}else{Invoke-WebRequest -UseBasicParsing $package.Url -OutFile $archive}
 }
 if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $package.Hash){throw ('Dependency hash mismatch: '+$package.Name+'. The rotating FFmpeg latest URL may have changed. Restore the retained verified archive or review an explicit version update; do not accept a new hash automatically.')}
 $directory=Join-Path $mediaRoot $(if($package.Name -eq 'ffmpeg-lgpl'){'ffmpeg'}else{$package.Name})
 if($ForceExtract -or !(Test-Path -LiteralPath $directory)){Expand-Archive -LiteralPath $archive -DestinationPath $directory -Force}
}
if($CompilerOnly){Write-Output 'Verified Roslyn compiler is ready.';return}
$native=Join-Path $PSScriptRoot 'plugins\media'
New-Item -ItemType Directory -Path $native -Force | Out-Null
foreach($legacy in @('ui\Magick.NET-Q8-x64.dll','plugins\media\Magick.Native-Q8-x64.dll')){
 $legacyPath=Join-Path $PSScriptRoot $legacy
 if(Test-Path -LiteralPath $legacyPath){Remove-Item -LiteralPath $legacyPath -Force}
}
Copy-Item -LiteralPath (Join-Path $mediaRoot 'magick.net.core.15.1.0\lib\netstandard20\Magick.NET.Core.dll'),(Join-Path $mediaRoot 'magick.net-q8-openmp-x64.15.1.0\lib\netstandard20\Magick.NET-Q8-OpenMP-x64.dll') -Destination (Join-Path $PSScriptRoot 'ui') -Force
Copy-Item -LiteralPath (Join-Path $mediaRoot 'magick.net-q8-openmp-x64.15.1.0\runtimes\win-x64\native\Magick.Native-Q8-OpenMP-x64.dll') -Destination $native -Force
Copy-Item -LiteralPath (Join-Path $mediaRoot 'magick.net-q8-openmp-x64.15.1.0\runtimes\win-x64\native\vcomp140.dll') -Destination $native -Force
Copy-Item -LiteralPath (Join-Path $mediaRoot 'magick.net-q8-openmp-x64.15.1.0\Notice.txt') -Destination (Join-Path $native 'MAGICK-NOTICE.txt') -Force
$ffmpeg=Join-Path $mediaRoot 'ffmpeg\ffmpeg-n9.0-latest-win64-lgpl-shared-9.0'
Get-ChildItem -LiteralPath (Join-Path $ffmpeg 'bin') -File | Where-Object {$_.Extension -eq '.dll' -or $_.Name -in @('ffmpeg.exe','ffprobe.exe')} | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $native -Force}
Copy-Item -LiteralPath (Join-Path $ffmpeg 'LICENSE.txt') -Destination (Join-Path $native 'FFMPEG-LICENSE.txt') -Force
Write-Output 'Verified media dependencies are ready. Native FFmpeg version and package hashes remain pinned.'

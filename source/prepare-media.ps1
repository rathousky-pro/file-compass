param([switch]$ForceExtract)
$ErrorActionPreference='Stop'
$mediaRoot=Join-Path $PSScriptRoot 'downloads\media'
New-Item -ItemType Directory -Path $mediaRoot -Force | Out-Null
$packages=@(
 @{Name='roslyn';Version='4.14.0';Url='https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/4.14.0/microsoft.net.compilers.toolset.4.14.0.nupkg';Hash='941a9cf3ea618d88d01a3dd6b1a45a06bcf07716a9f81ce4031caa3edd24a845'},
 @{Name='magick.net.core';Version='15.0.0';Url='https://api.nuget.org/v3-flatcontainer/magick.net.core/15.0.0/magick.net.core.15.0.0.nupkg';Hash='9936d4ea3ed1991a6672056e8df47b75b43c00afa884bff78e136cd2141798b3'},
 @{Name='magick.net-q8-x64';Version='15.0.0';Url='https://api.nuget.org/v3-flatcontainer/magick.net-q8-x64/15.0.0/magick.net-q8-x64.15.0.0.nupkg';Hash='96bf50fcbc586e8a149b467a97fb3d1bf09c0362b5d8ff828a9936b23fdc5374'},
 @{Name='ffmpeg-lgpl';Version='n9.0.2-35-g9a9b70bb8c-20261024';Url='https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n9.0-latest-win64-lgpl-shared-9.0.zip';Hash='5a204635723d3c28365aec847b79649f340873d8cc4bde632c619187c17992fd'}
)
foreach($package in $packages){
 $archive=Join-Path $mediaRoot ($package.Name+'.zip')
 if(!(Test-Path -LiteralPath $archive)){Invoke-WebRequest -UseBasicParsing $package.Url -OutFile $archive}
 if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $package.Hash){throw ('Dependency hash mismatch: '+$package.Name+'. The rotating FFmpeg latest URL may have changed. Restore the retained verified archive or review an explicit version update; do not accept a new hash automatically.')}
 $directory=Join-Path $mediaRoot $(if($package.Name -eq 'ffmpeg-lgpl'){'ffmpeg'}else{$package.Name})
 if($ForceExtract -or !(Test-Path -LiteralPath $directory)){Expand-Archive -LiteralPath $archive -DestinationPath $directory -Force}
}
$native=Join-Path $PSScriptRoot 'plugins\media'
New-Item -ItemType Directory -Path $native -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $mediaRoot 'magick.net.core\lib\netstandard20\Magick.NET.Core.dll'),(Join-Path $mediaRoot 'magick.net-q8-x64\lib\netstandard20\Magick.NET-Q8-x64.dll') -Destination (Join-Path $PSScriptRoot 'ui') -Force
Copy-Item -LiteralPath (Join-Path $mediaRoot 'magick.net-q8-x64\runtimes\win-x64\native\Magick.Native-Q8-x64.dll') -Destination $native -Force
Copy-Item -LiteralPath (Join-Path $mediaRoot 'magick.net-q8-x64\Notice.txt') -Destination (Join-Path $native 'MAGICK-NOTICE.txt') -Force
$ffmpeg=Join-Path $mediaRoot 'ffmpeg\ffmpeg-n9.0-latest-win64-lgpl-shared-9.0'
Get-ChildItem -LiteralPath (Join-Path $ffmpeg 'bin') -File | Where-Object {$_.Extension -eq '.dll' -or $_.Name -in @('ffmpeg.exe','ffprobe.exe')} | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $native -Force}
Copy-Item -LiteralPath (Join-Path $ffmpeg 'LICENSE.txt') -Destination (Join-Path $native 'FFMPEG-LICENSE.txt') -Force
Write-Output 'Verified media dependencies are ready. Native FFmpeg version and package hashes remain pinned.'

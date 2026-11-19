param([string]$Compiler, [string]$Libraries)
$ErrorActionPreference='Stop'
$workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(!$Compiler){$Compiler=(Get-ChildItem -LiteralPath (Join-Path $workspace 'tools/native') -Recurse -Filter 'x86_64-w64-mingw32-clang.exe' | Select-Object -First 1).FullName}
if(!$Libraries){
 $package=Join-Path $workspace 'source/downloads/curl-8.23.0_9-win64-mingw.zip'
 if(!(Test-Path -LiteralPath $package) -or (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne '89cb71930f4242d884e8e349463455932ab1d089b20f4f99318261a3cbff9167'){throw 'Missing or unverified curl 8.23.0_9 archive.'}
 $extract=Join-Path $workspace 'source/downloads/security-20261119/curl-8.23.0_9-win64-mingw'
 $Libraries=Join-Path $extract 'curl-8.23.0_9-win64-mingw'
 if(!(Test-Path -LiteralPath (Join-Path $Libraries 'lib/libcurl.a'))){Expand-Archive -LiteralPath $package -DestinationPath $extract -Force}
}
if(!$Compiler -or !(Test-Path -LiteralPath $Compiler)){throw 'Chybí LLVM-MinGW x64. Zadejte cestu parametrem -Compiler.'}
if(!(Test-Path -LiteralPath (Join-Path $Libraries 'lib/libcurl.a'))){throw 'Chybí statické knihovny curl. Zadejte cestu parametrem -Libraries.'}
$output=Join-Path (Split-Path $PSScriptRoot -Parent) 'plugins/network'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$linkArguments=@('-shared','-O2',('-I'+(Join-Path $Libraries 'include')), ('-I'+(Join-Path $workspace 'source/downloads/nfs/libnfs/include')), (Join-Path $PSScriptRoot 'bridge.c'),(Join-Path $PSScriptRoot 'nfs_io.c'),(Join-Path $PSScriptRoot 'network.def'), '-Wl,--export-all-symbols','-Wl,--whole-archive',(Join-Path $Libraries 'lib/libcurl.a'),(Join-Path $Libraries 'lib/libssh2.a'),'-Wl,--no-whole-archive','-Wl,--start-group')
$linkArguments+=Get-ChildItem -LiteralPath (Join-Path $Libraries 'lib') -Filter '*.a' | Where-Object {$_.Name -notin @('libcurl.a','libssh2.a','libcurl.dll.a')} | ForEach-Object {$_.FullName}
$nfs=Join-Path $workspace 'source/downloads/nfs/libnfs.a'
if(!(Test-Path -LiteralPath $nfs)){throw 'Missing verified libnfs static library. Build NFS first.'}
$linkArguments+= $nfs
$linkArguments+=@('-Wl,--end-group','-lws2_32','-lcrypt32','-lbcrypt','-ladvapi32','-lwldap32','-lsecur32','-lnormaliz','-liphlpapi','-o',(Join-Path $output 'FileCompassNetwork.dll'))
& $Compiler @linkArguments
if($LASTEXITCODE -ne 0){throw 'Native network plugin build failed.'}
'Native plugin built.'

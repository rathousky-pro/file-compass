param([string]$Compiler, [string]$Libraries)
$ErrorActionPreference='Stop'
$workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(!$Compiler){$Compiler=(Get-ChildItem -LiteralPath (Join-Path $workspace 'tools/native') -Recurse -Filter 'x86_64-w64-mingw32-clang.exe' | Select-Object -First 1).FullName}
if(!$Libraries){$Libraries=Join-Path $workspace 'source/downloads/network/curl-8.22.0_3-win64-mingw'}
if(!$Compiler -or !(Test-Path -LiteralPath $Compiler)){throw 'Chybí LLVM-MinGW x64. Zadejte cestu parametrem -Compiler.'}
if(!(Test-Path -LiteralPath (Join-Path $Libraries 'lib/libcurl.a'))){throw 'Chybí statické knihovny curl. Zadejte cestu parametrem -Libraries.'}
$output=Join-Path (Split-Path $PSScriptRoot -Parent) 'plugins/network'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$linkArguments=@('-shared','-O2',('-I'+(Join-Path $Libraries 'include')), (Join-Path $PSScriptRoot 'bridge.c'),(Join-Path $PSScriptRoot 'network.def'), '-Wl,--export-all-symbols','-Wl,--whole-archive',(Join-Path $Libraries 'lib/libcurl.a'),(Join-Path $Libraries 'lib/libssh2.a'),'-Wl,--no-whole-archive','-Wl,--start-group')
$linkArguments+=Get-ChildItem -LiteralPath (Join-Path $Libraries 'lib') -Filter '*.a' | Where-Object {$_.Name -notin @('libcurl.a','libssh2.a','libcurl.dll.a')} | ForEach-Object {$_.FullName}
$linkArguments+=@('-Wl,--end-group','-lws2_32','-lcrypt32','-lbcrypt','-ladvapi32','-lwldap32','-lsecur32','-lnormaliz','-liphlpapi','-o',(Join-Path $output 'CommanderNetwork.dll'))
& $Compiler @args
if($LASTEXITCODE -ne 0){throw 'Native network plugin build failed.'}
'Native plugin built.'

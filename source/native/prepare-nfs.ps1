param([string]$Compiler,[string]$Cmake,[string]$Git)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$cache=Join-Path $root 'source\downloads\nfs'
$source=Join-Path $cache 'libnfs'
$commit='331d391a9a224cca3782f782ed4835eec8fa33f8'
if(!$Compiler){$Compiler=(Get-ChildItem (Join-Path $root 'tools\native') -Recurse -Filter 'x86_64-w64-mingw32-clang.exe'|Select-Object -First 1 -ExpandProperty FullName)}
if(!$Git){$Git=(Get-Command git.exe -ErrorAction SilentlyContinue|Select-Object -First 1 -ExpandProperty Source)}
if(!$Cmake){$Cmake=(Get-Command cmake.exe -ErrorAction SilentlyContinue|Select-Object -First 1 -ExpandProperty Source)}
if(!$Compiler -or !(Test-Path $Compiler)){throw 'Missing LLVM-MinGW compiler.'}
if(!$Git -or !(Test-Path $Git)){throw 'Missing git.exe.'}
if(!$Cmake -or !(Test-Path $Cmake)){throw 'Missing cmake.exe.'}
if(!(Test-Path (Join-Path $source '.git'))){
 New-Item $cache -ItemType Directory -Force|Out-Null
 & $Git clone https://github.com/sahlberg/libnfs.git $source
 if($LASTEXITCODE -ne 0){throw 'Cannot fetch libnfs.'}
}
$actual=(& $Git -C $source rev-parse HEAD).Trim()
if($actual -ne $commit){ & $Git -C $source fetch origin $commit; if($LASTEXITCODE -ne 0){throw 'Cannot fetch pinned commit.'}; & $Git -C $source checkout --detach $commit; if($LASTEXITCODE -ne 0){throw 'Cannot checkout pinned commit.'} }
$stage=Join-Path $env:TEMP ('fc-nfs-build-'+$PID)
New-Item -ItemType Directory $stage|Out-Null
try {
 $src=Join-Path $stage 'src'
 Copy-Item $source $src -Recurse
 $tool=Join-Path $stage 'tool'
 New-Item -ItemType Junction -Path $tool -Target (Split-Path -Parent $Compiler)|Out-Null
 $clang=Join-Path $tool (Split-Path -Leaf $Compiler)
 $make=Join-Path $tool 'mingw32-make.exe'
 if(!(Test-Path $make)){throw 'Missing MinGW Make.'}
 $env:PATH=$tool+';'+$env:PATH
 & $Cmake -S $src -B (Join-Path $stage 'out') -G 'MinGW Makefiles' -DCMAKE_C_COMPILER=$clang -DCMAKE_MAKE_PROGRAM=$make -DBUILD_SHARED_LIBS=OFF -DENABLE_TLS=OFF -DENABLE_TESTS=OFF -DENABLE_UTILS=OFF -DENABLE_EXAMPLES=OFF -DENABLE_NFS4_2=ON
 if($LASTEXITCODE -ne 0){throw 'libnfs CMake configuration failed.'}
 & $Cmake --build (Join-Path $stage 'out') --parallel 6
 if($LASTEXITCODE -ne 0){throw 'libnfs compile failed.'}
 Copy-Item (Join-Path $stage 'out\lib\libnfs.a') (Join-Path $cache 'libnfs.a') -Force
 Write-Output ('Prepared pinned libnfs.a: '+(Join-Path $cache 'libnfs.a'))
} finally {Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue}

$ErrorActionPreference='Stop'
$compiler=Join-Path $PSScriptRoot 'downloads\media\roslyn\tasks\net472\csc.exe'
if(!(Test-Path -LiteralPath $compiler)){throw 'Missing Roslyn compiler. Run source/prepare-media.ps1 first.'}
$output=Join-Path $PSScriptRoot 'obj\preview'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$arguments=@('/nologo','/target:library','/platform:anycpu','/deterministic+','/optimize+',('/out:'+(Join-Path $output 'preview.dll')),(Join-Path $PSScriptRoot 'PreviewLibrary.cs'))
foreach($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'preview') -File | Where-Object {$_.Extension -in '.js','.html','.json','.txt'} | Sort-Object Name){$arguments+='/resource:'+$file.FullName+',FileCompass.Preview.'+$file.Name}
& $compiler @arguments
if($LASTEXITCODE -ne 0){throw 'Preview resource library build failed.'}

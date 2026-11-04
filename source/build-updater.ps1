param([string]$OutputFile = '')
$ErrorActionPreference = 'Stop'
if (!$OutputFile) { $OutputFile = Join-Path $PSScriptRoot 'obj\updater\FileCompass.Updater.exe' }
New-Item -ItemType Directory -Path (Split-Path -Parent $OutputFile) -Force | Out-Null
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $PSScriptRoot 'downloads\media\roslyn\tasks\net472\csc.exe'
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001',('/out:'+$OutputFile))
foreach($name in @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Windows.Forms.dll')) { $arguments += '/reference:'+(Join-Path $framework $name) }
$arguments += Join-Path $PSScriptRoot 'update\UpdateContract.cs'
$arguments += Join-Path $PSScriptRoot 'update\UpdateWorker.cs'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Update worker build failed.' }

# Compatibility entry point: never remove the entire artifacts tree, which can contain repositories.
param([switch]$Apply, [string]$GitPath = '', [string[]]$KeepArtifacts = @('maintenance-current-build','maintenance-release-current-build'))
$ErrorActionPreference = 'Stop'
if ($GitPath) { $env:PATH = (Split-Path -Parent $GitPath) + ';' + $env:PATH }
& (Join-Path $PSScriptRoot 'clean-development-builds.ps1') -Apply:$Apply -KeepArtifacts $KeepArtifacts

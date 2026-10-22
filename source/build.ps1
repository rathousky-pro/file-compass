param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'gen-plugin-hashes.ps1')
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Není dostupný kompilátor .NET Framework 4.x.' }
$bin = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { Join-Path (Split-Path -Parent $PSScriptRoot) 'bin' } else { [IO.Path]::GetFullPath($OutputDirectory) }
New-Item -ItemType Directory -Path $bin -Force | Out-Null
$references = @('System.dll', 'System.Core.dll', 'System.Security.dll', 'System.Xml.dll', 'System.Xaml.dll', 'System.Windows.Forms.dll', 'Microsoft.VisualBasic.dll', 'System.IO.Compression.dll', 'System.Web.Extensions.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll')
$compilerArgs = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001', ('/out:' + (Join-Path $bin 'File Compass.exe')))
$appIcon = Join-Path $PSScriptRoot 'assets\file-voyager\FileVoyager-FloppyOrbit.ico'
$compilerArgs += '/win32icon:' + $appIcon
$compilerArgs += '/win32manifest:' + (Join-Path $PSScriptRoot 'FileVoyager.manifest')
foreach ($library in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'ui') -Filter '*.dll') { $compilerArgs += '/reference:' + $library.FullName }
foreach ($reference in $references) { $compilerArgs += '/reference:' + (Join-Path $framework $reference) }
$compilerArgs += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Sestavení selhalo.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FileVoyager.exe.config') -Destination (Join-Path $bin 'File Compass.exe.config')
New-Item -ItemType Directory -Path (Join-Path $bin 'icons') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $bin 'ui') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'ui') | Where-Object { $_.Name -ne 'icons' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $bin 'ui') -Recurse -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ui\icons\LICENSE.txt') -Destination (Join-Path $bin 'ui\licenses\FLUENT-ICONS-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ui\icons\SOURCE.json') -Destination (Join-Path $bin 'ui\licenses\FLUENT-ICONS-SOURCE.json') -Force
$exporter = Join-Path $bin 'BuildIconLibraries.exe'
$exportArgs = @('/nologo', '/target:exe', '/platform:x64', '/optimize+', '/codepage:65001', ('/out:' + $exporter), ('/reference:' + (Join-Path $bin 'File Compass.exe')))
foreach ($reference in $references) { $exportArgs += '/reference:' + (Join-Path $framework $reference) }
$exportArgs += Join-Path $PSScriptRoot 'BuildIconLibraries.cs'
& $compiler @exportArgs
if ($LASTEXITCODE -ne 0) { throw 'Sestavení exportéru ikon selhalo.' }
$exportConfig = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'FileVoyager.exe.config') -Raw).Replace('privatePath="config\ui"', 'privatePath="ui"')
Set-Content -LiteralPath ($exporter + '.config') -Value $exportConfig -Encoding UTF8
$iconProcess = Start-Process -FilePath $exporter -ArgumentList @(('"' + (Join-Path $PSScriptRoot 'ui\icons') + '"'), ('"' + (Join-Path $bin 'icons') + '"'), ('"' + (Join-Path $PSScriptRoot 'assets\file-voyager') + '"')) -WindowStyle Hidden -PassThru -Wait
if ($iconProcess.ExitCode -ne 0) { throw 'Vytvoření knihoven ikon selhalo.' }
& $compiler '/nologo' '/target:library' '/platform:x64' ('/out:' + (Join-Path $bin 'icons\FileCompass.Icons.dll')) ('/win32res:' + (Join-Path $bin 'icons\ApplicationIcons.res')) (Join-Path $PSScriptRoot 'ApplicationIconLibrary.cs')
if ($LASTEXITCODE -ne 0) { throw 'Sestavení knihovny ikon aplikace selhalo.' }
Remove-Item -LiteralPath $exporter, ($exporter + '.config'), (Join-Path $bin 'icons\ApplicationIcons.res') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets\file-voyager\README.md') -Destination (Join-Path $bin 'icons') -Force
# Remove only the former asset directory inside this verified output directory.
$oldIcons = [IO.Path]::GetFullPath((Join-Path $bin 'ui\icons'))
if (!$oldIcons.StartsWith($bin.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Neplatná cesta ikon.' }
if (Test-Path -LiteralPath $oldIcons) { Remove-Item -LiteralPath $oldIcons -Recurse -Force }
foreach ($name in 'Compass','Panels','Route','Night') { $oldIcon = Join-Path $bin ('icons\FileVoyager-' + $name + '.ico'); if (Test-Path -LiteralPath $oldIcon) { Remove-Item -LiteralPath $oldIcon -Force } }
$oldPreview = Join-Path $bin 'icons\FileVoyager-Preview.png'; if (Test-Path -LiteralPath $oldPreview) { Remove-Item -LiteralPath $oldPreview -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md') -Destination $bin
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'NETWORK-PLUGINS.md'), (Join-Path $PSScriptRoot 'NETWORK-VALIDATION.md') -Destination $bin
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UI-VALIDATION.md') -Destination $bin
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'WORKFLOW-FEATURES.md') -Destination $bin
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ARCHIVE-PLUGINS.md') -Destination $bin
New-Item -ItemType Directory -Path (Join-Path $bin 'plugins') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugins\7zip') -Destination (Join-Path $bin 'plugins') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugins\network') -Destination (Join-Path $bin 'plugins') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugins\everything') -Destination (Join-Path $bin 'plugins') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugins\sshfs') -Destination (Join-Path $bin 'plugins') -Recurse -Force
Write-Output ('Hotovo: ' + (Join-Path $bin 'File Compass.exe'))
New-Item -ItemType Directory -Path (Join-Path $bin 'languages') -Force | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'languages\*.lng') -Destination (Join-Path $bin 'languages') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'languages\README.md') -Destination (Join-Path $bin 'languages') -Force

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ADVANCED-TOOLS.md') -Destination $bin -Force


# Portable distribution: one application executable and a config directory.
$configRoot = Join-Path $bin 'config'
New-Item -ItemType Directory -Path $configRoot -Force | Out-Null
foreach ($folder in 'icons','languages','plugins','ui') {
    $from = Join-Path $bin $folder
    $to = Join-Path $configRoot $folder
    if (Test-Path -LiteralPath $from) {
        if (Test-Path -LiteralPath $to) {
            Get-ChildItem -LiteralPath $from -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $to -Recurse -Force }
            if (![IO.Path]::GetFullPath($from).StartsWith([IO.Path]::GetFullPath($bin).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid resource directory' }
            Remove-Item -LiteralPath $from -Recurse -Force
        } else { Move-Item -LiteralPath $from -Destination $to }
    }
}
# Documentation belongs with the supporting files.
Get-ChildItem -LiteralPath $bin -Filter '*.md' | ForEach-Object { Move-Item -LiteralPath $_.FullName -Destination (Join-Path $configRoot $_.Name) -Force }

foreach ($obsolete in Get-ChildItem -LiteralPath $bin -File | Where-Object { $_.Name -match '^(FileVoyager(?:\.previous-[^\\]+)?|CommanderCZ)\.exe(?:\.config)?$' }) {
    $targetPath = [IO.Path]::GetFullPath($obsolete.FullName)
    if (![String]::Equals([IO.Path]::GetDirectoryName($targetPath), [IO.Path]::GetFullPath($bin).TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid obsolete executable path' }
    Remove-Item -LiteralPath $targetPath -Force
}

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FILE-COMPASS.md') -Destination (Join-Path $configRoot 'README.md') -Force

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FILE-DETAILS.md') -Destination (Join-Path $configRoot 'FILE-DETAILS.md') -Force

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FOLDER-VIEWS.md') -Destination (Join-Path $configRoot 'FOLDER-VIEWS.md') -Force

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'METADATA-SEARCH.md') -Destination (Join-Path $configRoot 'METADATA-SEARCH.md') -Force

New-Item -ItemType Directory -Path (Join-Path $configRoot "plugins/cloud") -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "plugins/cloud/rclone.exe"),(Join-Path $PSScriptRoot "plugins/cloud/README.txt"),(Join-Path $PSScriptRoot "plugins/cloud/LICENSE.txt") -Destination (Join-Path $configRoot "plugins/cloud") -Force
$googleOAuth = Join-Path $PSScriptRoot 'plugins/cloud/google-oauth.json'
if (Test-Path -LiteralPath $googleOAuth) { Copy-Item -LiteralPath $googleOAuth -Destination (Join-Path $configRoot 'plugins/cloud/google-oauth.json') -Force }

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CLOUD-STORAGE.md') -Destination (Join-Path $configRoot 'CLOUD-STORAGE.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'GOOGLE-OAUTH-PUBLISHER.md') -Destination (Join-Path $configRoot 'GOOGLE-OAUTH-PUBLISHER.md') -Force

New-Item -ItemType Directory -Path (Join-Path $configRoot 'preview') -Force | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'preview\*') -Destination (Join-Path $configRoot 'preview') -Force

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DOCUMENT-PREVIEW.md') -Destination (Join-Path $configRoot 'DOCUMENT-PREVIEW.md') -Force

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DUPLICATES-COMPARE.md') -Destination (Join-Path $configRoot 'DUPLICATES-COMPARE.md') -Force

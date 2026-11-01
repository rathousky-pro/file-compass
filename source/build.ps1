param([string]$OutputDirectory = '', [switch]$Release)
$ErrorActionPreference = 'Stop'
if(!(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'ui\YamlDotNet.dll'))){throw 'Missing YAML editor dependency. Run source/prepare-editor.ps1 first.'}
& (Join-Path $PSScriptRoot 'gen-plugin-hashes.ps1')
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $PSScriptRoot 'downloads\media\roslyn\tasks\net472\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Missing Roslyn compiler. Run source/prepare-media.ps1 first (.NET Framework 4.8 is required).'  }
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ($Release -and [string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $projectRoot ('dist\FileCompass-' + [Guid]::NewGuid().ToString('N')) }
$bin = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { Join-Path $projectRoot 'bin' } else { [IO.Path]::GetFullPath($OutputDirectory) }
if ($Release -and (Test-Path -LiteralPath $bin) -and @(Get-ChildItem -LiteralPath $bin -Force).Count -ne 0) { throw 'Release output must be empty. Use a fresh staging directory.' }
New-Item -ItemType Directory -Path $bin -Force | Out-Null
if (!$Release -and [String]::Equals($bin.TrimEnd('\'), (Join-Path $projectRoot 'bin'), [StringComparison]::OrdinalIgnoreCase)) {
    $profileRoot = Join-Path $projectRoot 'local-profile'
    New-Item -ItemType Directory -Path $profileRoot -Force | Out-Null
    foreach ($name in @('config','data')) {
        $from = [IO.Path]::GetFullPath((Join-Path $bin $name))
        $to = [IO.Path]::GetFullPath((Join-Path $profileRoot $name))
        if (![String]::Equals([IO.Path]::GetDirectoryName($from), $bin, [StringComparison]::OrdinalIgnoreCase) -or !$to.StartsWith($projectRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid profile migration path.' }
        if (Test-Path -LiteralPath $from) {
            if (Test-Path -LiteralPath $to) {
                # A still-running older binary can recreate its former profile. Preserve it without overwriting the migrated profile.
                $to = [IO.Path]::GetFullPath((Join-Path $profileRoot ('recovered-' + $name + '-' + [Guid]::NewGuid().ToString('N'))))
                if (![String]::Equals([IO.Path]::GetDirectoryName($to), $profileRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid recovery profile path.' }
            }
            Move-Item -LiteralPath $from -Destination $to
        }
    }
    Set-Content -LiteralPath (Join-Path $bin 'development-profile.txt') -Value 'Local development profile is outside the binary directory.' -Encoding UTF8
    $oldOAuth = Join-Path $PSScriptRoot 'plugins\cloud\google-oauth.json'
    if (Test-Path -LiteralPath $oldOAuth) {
        $oauthTarget = Join-Path $profileRoot 'config\oauth\google.json'
        New-Item -ItemType Directory -Path (Split-Path -Parent $oauthTarget) -Force | Out-Null
        if (!(Test-Path -LiteralPath $oauthTarget)) { Move-Item -LiteralPath $oldOAuth -Destination $oauthTarget }
        else { Move-Item -LiteralPath $oldOAuth -Destination (Join-Path $profileRoot ('google-oauth-preserved-' + [Guid]::NewGuid().ToString('N') + '.json')) }
    }
}
$references = @('netstandard.dll', 'System.dll', 'System.Management.dll', 'System.Core.dll', 'System.Security.dll', 'System.Xml.dll', 'System.Xaml.dll', 'System.Windows.Forms.dll', 'Microsoft.VisualBasic.dll', 'System.IO.Compression.dll', 'System.Web.Extensions.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll')
$buildInfoDirectory = Join-Path $PSScriptRoot ('obj\build-' + [Guid]::NewGuid().ToString('N'))
& (Join-Path $PSScriptRoot 'gen-build-info.ps1') -OutputDirectory $buildInfoDirectory
$compilerArgs = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001', ('/out:' + (Join-Path $bin 'File Compass.exe')))
$appIcon = Join-Path $PSScriptRoot 'assets\file-voyager\FileVoyager-FloppyOrbit.ico'
$compilerArgs += Join-Path $buildInfoDirectory 'BuildInfo.g.cs'
$compilerArgs += '/resource:' + (Join-Path $PSScriptRoot 'assets\branding\rathousky-pro.png') + ',FileCompass.Branding.RathouskyPro.png'
$compilerArgs += '/win32icon:' + $appIcon
$compilerArgs += '/win32manifest:' + (Join-Path $PSScriptRoot 'FileCompass.manifest')
foreach ($library in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'ui') -Filter '*.dll') { $compilerArgs += '/reference:' + $library.FullName }
foreach ($reference in $references) { $compilerArgs += '/reference:' + (Join-Path $framework $reference) }
if (!$Release) { $compilerArgs += '/define:APP_TESTS' }
$compilerArgs += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Where-Object { !$Release -or $_.Name -notlike '*Tests.cs' } | ForEach-Object { $_.FullName }
# Keep the compiler invocation below the Windows process argument limit.
# A response file also keeps builds reliable when the workspace path contains spaces.
$responseFile = Join-Path $buildInfoDirectory 'compile.rsp'
$responseArguments = $compilerArgs | ForEach-Object { '"' + $_ + '"' }
[IO.File]::WriteAllLines($responseFile, $responseArguments, (New-Object Text.UTF8Encoding($true)))
& $compiler ('@' + $responseFile)
if ($LASTEXITCODE -ne 0) { throw 'Sestavení selhalo.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FileCompass.exe.config') -Destination (Join-Path $bin 'File Compass.exe.config')
New-Item -ItemType Directory -Path (Join-Path $bin 'icons') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $bin 'libraries') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'ui') | Where-Object { $_.Name -ne 'icons' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $bin 'libraries') -Recurse -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ui\icons\LICENSE.txt') -Destination (Join-Path $bin 'libraries\licenses\FLUENT-ICONS-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ui\icons\SOURCE.json') -Destination (Join-Path $bin 'libraries\licenses\FLUENT-ICONS-SOURCE.json') -Force
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'ui\icons\TOOLBAR-SOURCE.json')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ui\icons\TOOLBAR-SOURCE.json') -Destination (Join-Path $bin 'libraries\licenses\FLUENT-TOOLBAR-SOURCE.json') -Force }
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'ui\icons\COLOR-SOURCE.json')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ui\icons\COLOR-SOURCE.json') -Destination (Join-Path $bin 'libraries\licenses\FLUENT-COLOR-SOURCE.json') -Force }
$exporter = Join-Path $bin 'BuildIconLibraries.exe'
$exportArgs = @('/nologo', '/target:exe', '/platform:x64', '/optimize+', '/codepage:65001', ('/out:' + $exporter), ('/reference:' + (Join-Path $bin 'File Compass.exe')))
foreach ($reference in $references) { $exportArgs += '/reference:' + (Join-Path $framework $reference) }
$exportArgs += Join-Path $PSScriptRoot 'BuildIconLibraries.cs'
& $compiler @exportArgs
if ($LASTEXITCODE -ne 0) { throw 'Sestavení exportéru ikon selhalo.' }
$exportConfig = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'FileCompass.exe.config') -Raw)
Set-Content -LiteralPath ($exporter + '.config') -Value $exportConfig -Encoding UTF8
$iconProcess = Start-Process -FilePath $exporter -ArgumentList @(('"' + (Join-Path $PSScriptRoot 'ui\icons') + '"'), ('"' + (Join-Path $bin 'icons') + '"'), ('"' + (Join-Path $PSScriptRoot 'assets\file-voyager') + '"')) -WindowStyle Hidden -PassThru -Wait
if ($iconProcess.ExitCode -ne 0) { throw 'Vytvoření knihoven ikon selhalo.' }
& $compiler '/nologo' '/target:library' '/platform:x64' ('/out:' + (Join-Path $bin 'icons\FileCompass.Icons.dll')) ('/win32res:' + (Join-Path $bin 'icons\ApplicationIcons.res')) (Join-Path $PSScriptRoot 'ApplicationIconLibrary.cs')
if ($LASTEXITCODE -ne 0) { throw 'Sestavení knihovny ikon aplikace selhalo.' }
Remove-Item -LiteralPath $exporter, ($exporter + '.config'), (Join-Path $bin 'icons\ApplicationIcons.res') -Force
# Remove only the former asset directory inside this verified output directory.
$oldIcons = [IO.Path]::GetFullPath((Join-Path $bin 'ui\icons'))
if (!$oldIcons.StartsWith($bin.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Neplatná cesta ikon.' }
if (Test-Path -LiteralPath $oldIcons) { Remove-Item -LiteralPath $oldIcons -Recurse -Force }
foreach ($name in 'Compass','Panels','Route','Night') { $oldIcon = Join-Path $bin ('icons\FileVoyager-' + $name + '.ico'); if (Test-Path -LiteralPath $oldIcon) { Remove-Item -LiteralPath $oldIcon -Force } }
$oldPreview = Join-Path $bin 'icons\FileVoyager-Preview.png'; if (Test-Path -LiteralPath $oldPreview) { Remove-Item -LiteralPath $oldPreview -Force }
New-Item -ItemType Directory -Path (Join-Path $bin 'plugins') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugins\7zip') -Destination (Join-Path $bin 'plugins') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugins\network') -Destination (Join-Path $bin 'plugins') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugins\everything') -Destination (Join-Path $bin 'plugins') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugins\sshfs') -Destination (Join-Path $bin 'plugins') -Recurse -Force
Write-Output ('Hotovo: ' + (Join-Path $bin 'File Compass.exe'))
New-Item -ItemType Directory -Path (Join-Path $bin 'languages') -Force | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'languages\*.lng') -Destination (Join-Path $bin 'languages') -Force



$configRoot = if (Test-Path -LiteralPath (Join-Path $bin 'development-profile.txt')) { Join-Path $projectRoot 'local-profile\config' } else { Join-Path $bin 'config' }
foreach ($obsolete in Get-ChildItem -LiteralPath $bin -File | Where-Object { $_.Name -match '^(FileVoyager(?:\.previous-[^\\]+)?|FileCompass)\.exe(?:\.config)?$' }) {
    $targetPath = [IO.Path]::GetFullPath($obsolete.FullName)
    if (![String]::Equals([IO.Path]::GetDirectoryName($targetPath), [IO.Path]::GetFullPath($bin).TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid obsolete executable path' }
    Remove-Item -LiteralPath $targetPath -Force
}





New-Item -ItemType Directory -Path (Join-Path $bin "plugins/cloud") -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "plugins/cloud/rclone.exe"),(Join-Path $PSScriptRoot "plugins/cloud/README.txt"),(Join-Path $PSScriptRoot "plugins/cloud/LICENSE.txt") -Destination (Join-Path $bin "plugins/cloud") -Force
# Publisher credentials are local configuration and never copied into builds.


New-Item -ItemType Directory -Path (Join-Path $bin 'preview') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'preview') -File | Where-Object { $_.Extension -in '.dll','.txt' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $bin 'preview') -Force }



New-Item -ItemType Directory -Path (Join-Path $bin 'plugins/smart') -Force | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'plugins/smart/*') -Destination (Join-Path $bin 'plugins/smart') -Force

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'obj\preview\preview.dll') -Destination (Join-Path $bin 'preview\preview.dll') -Force
foreach ($name in @('index.html','viewer.js','docx-preview.min.js','jszip.min.js','xlsx.full.min.js','manifest.json')) { $obsolete = Join-Path (Join-Path $bin 'preview') $name; if (Test-Path -LiteralPath $obsolete) { Remove-Item -LiteralPath $obsolete -Force } }

# Only curated user help is distributed. AI/development material stays at repository root.
$documentationRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs'
$helpRoot = Join-Path $bin 'help'
New-Item -ItemType Directory -Path $helpRoot -Force | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'help/site/*') -Destination $helpRoot -Recurse -Force -ErrorAction Stop
New-Item -ItemType Directory -Path (Join-Path $helpRoot 'images') -Force | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'help/images/*.png') -Destination (Join-Path $helpRoot 'images') -Force -ErrorAction Stop
Copy-Item -Path (Join-Path $documentationRoot '*.md') -Destination $helpRoot -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'dependencies.json') -Destination (Join-Path $helpRoot 'dependencies.json') -Force -ErrorAction Stop
$oldDocumentation = @('README.md','FILE-COMPASS.md','ARCHIVES.md','ADVANCED-TOOLS.md','ARCHIVE-PLUGINS.md','CLOUD-STORAGE.md','DOCUMENT-PREVIEW.md','DUPLICATES-COMPARE.md','FILE-DETAILS.md','FILE-TOOLS.md','FOLDER-VIEWS.md','GOOGLE-OAUTH-PUBLISHER.md','LIBCURL-AUDIT.md','METADATA-SEARCH.md','NETWORK-PLUGINS.md','NETWORK-VALIDATION.md','THIRD-PARTY-NOTICES.md','UI-VALIDATION.md','WORKFLOW-FEATURES.md')
foreach ($folder in @($bin,$configRoot)) {
    foreach ($name in $oldDocumentation) { $old = Join-Path $folder $name; if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Force } }
}
foreach ($relative in @('icons/README.md','languages/README.md')) { $old = Join-Path $configRoot $relative; if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Force } }
if ($Release) {
    foreach ($private in @('config','data','development-profile.txt')) { if (Test-Path -LiteralPath (Join-Path $bin $private)) { throw ('Private state in release output: ' + $private) } }
    if (@(Get-ChildItem -LiteralPath $bin -Recurse -File | Where-Object { $_.Name -match '^(google-oauth\.json|google\.json|rclone\.conf|host-key\.pem|encryption-key\.dat|key\.dat|integrity\.key)$' }).Count -gt 0) { throw 'Credentials found in release output.' }
}

New-Item -ItemType Directory -Path (Join-Path $bin 'plugins/media') -Force | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'plugins/media/*') -Destination (Join-Path $bin 'plugins/media') -Recurse -Force

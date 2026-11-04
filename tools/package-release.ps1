param(
    [string]$InputDirectory = '',
    [string]$OutputDirectory = '',
    [string]$ReleaseDirectory = '',
    [switch]$Build,
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
. (Join-Path $PSScriptRoot 'github-release.ps1')
if ($ReleaseDirectory) {
    if ($InputDirectory -or $OutputDirectory -or $Build) { throw 'Use -ReleaseDirectory by itself, optionally with -Publish. Existing ZIPs are not rebuilt.' }
    $bundle = Read-ReleaseBundle -Directory $ReleaseDirectory
    Write-Output ('Verified existing ZIP: '+$bundle.Zip)
    Write-Output ('Release: '+$bundle.Tag)
    if ($Publish) { Publish-GitHubRelease -Bundle $bundle -ProjectRoot $projectRoot }
    return
}
if (!$OutputDirectory) { $OutputDirectory = Join-Path $projectRoot ('ai-docs\releases\'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ($Build) {
    if ($InputDirectory) { throw 'Use either -Build or -InputDirectory.' }
    $InputDirectory = Join-Path $OutputDirectory 'package'
    & (Join-Path $projectRoot 'source\build.ps1') -Release -OutputDirectory $InputDirectory
}
if (!$InputDirectory) { throw 'Use -Build or specify a clean Release directory with -InputDirectory.' }
$InputDirectory = [IO.Path]::GetFullPath($InputDirectory)
$license = Join-Path $InputDirectory 'help\LICENSE.txt'
if (!(Test-Path -LiteralPath $license) -or (Get-FileHash -LiteralPath $license -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath (Join-Path $projectRoot 'LICENSE') -Algorithm SHA256).Hash) { throw 'The package must include the complete current MIT license at help/LICENSE.txt.' }
$exe = Join-Path $InputDirectory 'File Compass.exe'
$information = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion
if ($information -notmatch '^(?<version>\d+\.\d+\.\d+)(?:-(?<channel>alpha|beta|rc))?\+(?<build>\d{8}\.\d{2,9})$') { throw 'Invalid application version/build.' }
$releaseVersion = $Matches.version; $channel = $Matches.channel; $buildId = $Matches.build
$tag = 'v'+$releaseVersion+$(if($channel){'-'+$channel})+'-build.'+$buildId
$manifestName = 'file-compass-release.json'
$files = @()
foreach ($file in Get-ChildItem -LiteralPath $InputDirectory -Recurse -File | Sort-Object FullName) {
    $relative = $file.FullName.Substring($InputDirectory.TrimEnd('\').Length+1).Replace('\','/')
    if ($relative -eq $manifestName) { continue }
    if ($relative -notmatch '^(File Compass\.exe(?:\.config)?|(libraries|plugins|preview|icons|languages|help)/.+)$') { throw ('Unexpected file in Release directory: '+$relative) }
    $ancestor = $file
    while ($ancestor -and $ancestor.FullName.StartsWith($InputDirectory,[StringComparison]::OrdinalIgnoreCase)) {
        if (($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw ('Linked release path: '+$file.FullName) }
        $ancestor = if ($ancestor -is [IO.FileInfo]) { $ancestor.Directory } else { $ancestor.Parent }
    }
    if ($file.Name -match '^(google-oauth\.json|google\.json|rclone\.conf|host-key\.pem|encryption-key\.dat|key\.dat|integrity\.key)$') { throw 'Credentials in release directory.' }
    $files += [ordered]@{ Relative=$relative; Hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); Size=$file.Length }
}
$manifest = [ordered]@{ Format=1; Version=$releaseVersion; Channel=[string]$channel; Build=$buildId; Files=$files }
[IO.File]::WriteAllText((Join-Path $InputDirectory $manifestName),($manifest|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$zip = Join-Path $OutputDirectory 'FileCompass-win-x64.zip'
if(Test-Path -LiteralPath $zip){throw 'Release ZIP already exists. Use a new output directory.'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($InputDirectory,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
$digest = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($zip+'.sha256'),($digest+'  FileCompass-win-x64.zip'+[Environment]::NewLine),(New-Object Text.UTF8Encoding($false)))
$url = 'https://github.com/rathousky-pro/file-compass-web/releases/download/'+$tag+'/FileCompass-win-x64.zip'
$notes = "File Compass $information`n`nWindows x64 portable beta. Original File Compass code is licensed under MIT, copyright (c) 2026 Jan Rathousky. Third-party components retain their own licenses; see help/LICENSE.txt and help/LICENSING.md in the package.`n`nThe executable is not digitally signed yet. Includes application files and the verified update manifest.`nSHA-256: $digest`n"
$notesPath = Join-Path $OutputDirectory 'release-notes.md'; [IO.File]::WriteAllText($notesPath,$notes,(New-Object Text.UTF8Encoding($false)))
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'release-info.json'),(@{tag=$tag;url=$url;zip=$zip;sha256=$digest;prerelease=[bool]$channel}|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
if ($Publish) {
    Publish-GitHubRelease -Bundle (Read-ReleaseBundle -Directory $OutputDirectory) -ProjectRoot $projectRoot
}
Write-Output ('Release: '+$tag)
Write-Output ('ZIP: '+$zip)
Write-Output ('Download URL (after publication): '+$url)

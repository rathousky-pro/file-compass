param(
    [string]$ReleaseDirectory = '',
    [switch]$UpdateWebsite,
    [string]$WebsiteDirectory = '',
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (!$ReleaseDirectory) { $ReleaseDirectory = Join-Path $projectRoot 'ai-docs/releases/mit-20261104-final' }
if (!$WebsiteDirectory) { $WebsiteDirectory = Join-Path $projectRoot 'ai-docs/artifacts/file-compass-web-counter' }
if (![IO.Path]::IsPathRooted($ReleaseDirectory)) { $ReleaseDirectory = Join-Path $projectRoot $ReleaseDirectory }
if (![IO.Path]::IsPathRooted($WebsiteDirectory)) { $WebsiteDirectory = Join-Path $projectRoot $WebsiteDirectory }
function Resolve-PublishExecutable {
    param([string]$Name,[string[]]$Candidates)
    $command = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { return $command.Source }
    foreach ($candidate in $Candidates) { if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) { return $candidate } }
    throw ('Cannot find '+$Name+'.')
}
$userDirectory = [Environment]::GetFolderPath('UserProfile')
$runtime = Join-Path $userDirectory '.cache/codex-runtimes/codex-primary-runtime/dependencies'
$gitExe = Resolve-PublishExecutable 'git.exe' @((Join-Path $runtime 'native/git/cmd/git.exe'),(Join-Path $env:ProgramFiles 'Git/cmd/git.exe'),(Join-Path $env:LOCALAPPDATA 'Programs/Git/cmd/git.exe'))
$env:PATH = (Split-Path -Parent $gitExe)+';'+$env:PATH
if ($UpdateWebsite) {
    $nodeExe = Resolve-PublishExecutable 'node.exe' @((Join-Path $runtime 'node/bin/node.exe'),(Join-Path $env:ProgramFiles 'nodejs/node.exe'))
    $env:PATH = (Split-Path -Parent $nodeExe)+';'+$env:PATH
}
$safeRoot = $projectRoot.Replace('\','/')
function Invoke-SourceGit {
    & $gitExe --no-pager -c "safe.directory=$safeRoot" -c 'user.name=JRKladno76' -c 'user.email=jan.rathousky.kladno@gmail.com' -C $projectRoot @args
    if ($LASTEXITCODE -ne 0) { throw ('Source Git operation failed: '+$args[0]) }
}
. (Join-Path $PSScriptRoot 'github-release.ps1')
$bundle = Read-ReleaseBundle -Directory $ReleaseDirectory
if ((Invoke-SourceGit remote get-url origin) -ne 'https://github.com/rathousky-pro/file-compass.git') { throw 'Unexpected application repository.' }
if ((Invoke-SourceGit branch --show-current) -ne 'main') { throw 'Run this publication from main. No branch was changed.' }
$paths = @('LICENSE','README.md','.gitignore','.gitattributes','.github','docs','source','tools')
function Test-SourcePath([string]$Path) {
    return $Path -in @('LICENSE','README.md','.gitignore','.gitattributes') -or $Path -match '^(\.github|docs|source|tools)/'
}
$staged = @(Invoke-SourceGit diff --cached --name-only)
if (@($staged | Where-Object { !(Test-SourcePath $_) }).Count) { throw 'Unrelated staged files must be handled first.' }
# Git ignore rules exclude profiles, build output, dependency caches and credentials.
$candidates = @(Invoke-SourceGit -c core.quotepath=false ls-files --cached --others --exclude-standard -- @paths)
foreach ($relative in $candidates) {
    $file = Join-Path $projectRoot $relative
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { continue }
    if ($relative -match '(?i)(\.(pfx|p12|snk)$|(^|/)(google-oauth\.json|rclone\.conf|integrity\.key|host-key\.pem|\.env(?:\..*)?)$)') { throw ('Credential file cannot be published: '+$relative) }
    if ((Get-Item -LiteralPath $file).Length -lt 3MB) {
        $text = [IO.File]::ReadAllText($file)
        if ($text -match '\bgh[pousr]_[A-Za-z0-9]{30,}|\bgithub_pat_[A-Za-z0-9_]{30,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----\r?\n[A-Za-z0-9+/]{40,}') { throw ('Possible credential material in: '+$relative) }
    }
}
if ($UpdateWebsite) {
    if (!(Test-Path -LiteralPath (Join-Path $WebsiteDirectory 'build.mjs'))) { throw 'Website checkout is missing.' }
    $safeWebsite = $WebsiteDirectory.Replace('\','/')
    $websiteRemote = & $gitExe --no-pager -c "safe.directory=$safeWebsite" -C $WebsiteDirectory remote get-url origin
    if ($LASTEXITCODE -ne 0 -or $websiteRemote -ne 'https://github.com/rathousky-pro/file-compass-web.git') { throw 'Unexpected website checkout.' }
}
Write-Output ('Source repository: rathousky-pro/file-compass / main')
Write-Output ('Verified ZIP: '+$bundle.Zip)
Write-Output ('Release: '+$bundle.Tag)
if ($CheckOnly) { Write-Output 'Local preflight passed. No commit, network access or upload performed.'; return }
Invoke-SourceGit fetch origin main
# Never reset, force-push or silently merge an incoming change.
& $gitExe --no-pager -c "safe.directory=$safeRoot" -C $projectRoot merge-base --is-ancestor origin/main HEAD
if ($LASTEXITCODE -ne 0) { throw 'Remote main contains changes not present locally. Integrate them and run again.' }
$record = Join-Path $ReleaseDirectory 'source-commit.txt'
if (Test-Path -LiteralPath $record) {
    if (([IO.File]::ReadAllText($record)).Trim() -ne (Invoke-SourceGit rev-parse HEAD) -or @(Invoke-SourceGit status --porcelain -- @paths).Count) { throw 'Source changed after this ZIP was assigned to a commit. Prepare a new release instead of reusing its identity.' }
} else {
    Invoke-SourceGit add -A -- @paths
    Invoke-SourceGit diff --cached --check
    & $gitExe --no-pager -c "safe.directory=$safeRoot" -C $projectRoot diff --cached --quiet
    if ($LASTEXITCODE -eq 1) { Invoke-SourceGit commit -m ('License File Compass under MIT and publish beta '+$bundle.Tag) }
    elseif ($LASTEXITCODE -ne 0) { throw 'Cannot inspect staged source changes.' }
    [IO.File]::WriteAllText($record,((Invoke-SourceGit rev-parse HEAD)+[Environment]::NewLine),(New-Object Text.UTF8Encoding($false)))
}
Invoke-SourceGit push origin HEAD:main
$commit = (Invoke-SourceGit rev-parse HEAD).Trim()
$bundle.Notes += "`nApplication source: https://github.com/rathousky-pro/file-compass/tree/$commit`n"
Publish-GitHubRelease -Bundle $bundle -ProjectRoot $projectRoot
if ($UpdateWebsite) {
    & (Join-Path $PSScriptRoot 'update-website-release.ps1') -ReleaseDirectory $ReleaseDirectory -WebsiteDirectory $WebsiteDirectory -Publish
}
Write-Output ('Source published: https://github.com/rathousky-pro/file-compass/tree/'+$commit)
Write-Output 'Publication finished. GitHub CLI is not required.'

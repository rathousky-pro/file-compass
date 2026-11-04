param(
    [string]$WebsiteDirectory = (Join-Path $PSScriptRoot '../ai-docs/artifacts/file-compass-web-counter'),
    [switch]$CheckOnly,
    [string]$CommitAuthorName = 'JRKladno76',
    [string]$CommitAuthorEmail = 'jan.rathousky.kladno@gmail.com'
)
$ErrorActionPreference = 'Stop'
function Resolve-PublishTool {
    param([string]$Name, [string[]]$Candidates)
    $command = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { return $command.Source }
    foreach ($candidate in $Candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    throw "Cannot find $Name in PATH, standard installations or the Codex runtime."
}
$userDirectory = [Environment]::GetFolderPath('UserProfile')
$runtimeDependencies = Join-Path $userDirectory '.cache/codex-runtimes/codex-primary-runtime/dependencies'
$gitExe = Resolve-PublishTool 'git.exe' @(
    (Join-Path $runtimeDependencies 'native/git/cmd/git.exe'),
    (Join-Path $env:ProgramFiles 'Git/cmd/git.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs/Git/cmd/git.exe')
)
$nodeExe = Resolve-PublishTool 'node.exe' @(
    (Join-Path $runtimeDependencies 'node/bin/node.exe'),
    (Join-Path $env:ProgramFiles 'nodejs/node.exe')
)
# Child processes, including Git credential helpers, must also find these tools.
$env:PATH = (Split-Path -Parent $gitExe) + ';' + (Split-Path -Parent $nodeExe) + ';' + $env:PATH
Write-Output "Git: $gitExe"
Write-Output "Node.js: $nodeExe"
$repo = (Resolve-Path -LiteralPath $WebsiteDirectory).Path
$safeRepo = $repo.Replace('\','/')
function Invoke-WebsiteGit {
    # Use the existing project author's identity for this invocation only.
    # A regular PowerShell session may not have Codex's Git configuration.
    & $gitExe --no-pager -c "safe.directory=$safeRepo" -c "user.name=$CommitAuthorName" -c "user.email=$CommitAuthorEmail" -C $repo @args
    if ($LASTEXITCODE -ne 0) { throw "Website Git operation failed: $($args[0])" }
}
$remote = Invoke-WebsiteGit remote get-url origin
if ($remote -ne 'https://github.com/rathousky-pro/file-compass-web.git') { throw 'Unexpected website repository.' }
if ((Invoke-WebsiteGit branch --show-current) -ne 'main') { throw 'Website must be on main.' }
# Validate both identities before generation, staging or network operations.
$authorIdentity = Invoke-WebsiteGit var GIT_AUTHOR_IDENT
$committerIdentity = Invoke-WebsiteGit var GIT_COMMITTER_IDENT
Write-Output "Commit author: $authorIdentity"
$manifest = Get-Content -LiteralPath (Join-Path $repo 'assets/screenshots/manifest.json') -Raw | ConvertFrom-Json
if ($manifest.captured -ne '2026-11-04' -or $manifest.images.Count -ne 56) { throw 'Unexpected screenshot capture.' }
$files = @('README.md','gallery.mjs','index.html','cs.html','assets/panels-cs.png','assets/panels-en.png','assets/screenshots/manifest.json')
$files += @($manifest.images | ForEach-Object { 'assets/screenshots/' + $_.file })
# A previous run may already have staged this publication. Preserve unrelated work.
$staged = @(Invoke-WebsiteGit diff --cached --name-only)
$unrelated = @($staged | Where-Object { $_ -cnotin $files })
if ($unrelated.Count) { throw "Unrelated staged work must be handled first: $($unrelated -join ', ')" }
Invoke-WebsiteGit diff --cached --check
foreach ($image in $manifest.images) {
    $file = Join-Path $repo ('assets/screenshots/' + $image.file)
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $image.sha256) { throw "Screenshot hash differs: $($image.file)" }
}
Push-Location -LiteralPath $repo
try { & $nodeExe build.mjs; if ($LASTEXITCODE -ne 0) { throw 'Website generation failed.' } }
finally { Pop-Location }
if ($CheckOnly) {
    Write-Output 'Local checks passed. No commit or push performed.'
    return
}
Invoke-WebsiteGit fetch origin main
$localHead = Invoke-WebsiteGit rev-parse HEAD
$remoteHead = Invoke-WebsiteGit rev-parse origin/main
if ($localHead -ne $remoteHead) { throw 'Remote website has changed. Review and integrate it before publishing.' }
Invoke-WebsiteGit add -- @files
Invoke-WebsiteGit diff --cached --check
Invoke-WebsiteGit commit -m 'Refresh bilingual gallery with current Fluent screenshots and synthetic files'
Invoke-WebsiteGit push origin HEAD:main
Write-Output 'Published current screenshots to the GitHub Pages source branch.'

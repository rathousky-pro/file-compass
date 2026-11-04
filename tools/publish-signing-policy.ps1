param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$website = Join-Path $project 'ai-docs/artifacts/file-compass-web-counter'
$runtime = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.cache/codex-runtimes/codex-primary-runtime/dependencies'
function Find-PublishTool([string]$Name,[string[]]$Candidates) {
    $found = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { return $found.Source }
    foreach ($candidate in $Candidates) { if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate } }
    throw ('Cannot find '+$Name)
}
$gitExe = Find-PublishTool 'git.exe' @((Join-Path $runtime 'native/git/cmd/git.exe'),(Join-Path $env:ProgramFiles 'Git/cmd/git.exe'))
$nodeExe = Find-PublishTool 'node.exe' @((Join-Path $runtime 'node/bin/node.exe'),(Join-Path $env:ProgramFiles 'nodejs/node.exe'))
$env:PATH = (Split-Path -Parent $gitExe)+';'+(Split-Path -Parent $nodeExe)+';'+$env:PATH
function Invoke-PolicyGit {
    param([string]$Repository,[string[]]$Arguments)
    & $gitExe --no-pager -c ('safe.directory='+$Repository.Replace('\','/')) -c 'user.name=JRKladno76' -c 'user.email=jan.rathousky.kladno@gmail.com' -C $Repository @Arguments
    if ($LASTEXITCODE -ne 0) { throw ('Git failed: '+$Arguments[0]) }
}
$targets = @(
    @{Folder=$project;Remote='https://github.com/rathousky-pro/file-compass.git';Files=@('README.md','docs/CODE-SIGNING.md','tools/publish-signing-policy.ps1')},
    @{Folder=$website;Remote='https://github.com/rathousky-pro/file-compass-web.git';Files=@('build.mjs','index.html','cs.html')}
)
foreach ($target in $targets) {
    if ((Invoke-PolicyGit $target.Folder @('remote','get-url','origin')) -ne $target.Remote) { throw 'Unexpected repository.' }
    if ((Invoke-PolicyGit $target.Folder @('branch','--show-current')) -ne 'main') { throw 'Publication requires main; no branch was changed.' }
    $staged = @(Invoke-PolicyGit $target.Folder @('diff','--cached','--name-only'))
    if (@($staged | Where-Object { $_ -cnotin $target.Files }).Count) { throw 'Unrelated staged changes must be handled first.' }
    foreach ($relative in $target.Files) { if (!(Test-Path -LiteralPath (Join-Path $target.Folder $relative))) { throw ('Missing publication file: '+$relative) } }
    if (!$CheckOnly) {
        Invoke-PolicyGit $target.Folder @('fetch','origin','main')
        & $gitExe --no-pager -c ('safe.directory='+$target.Folder.Replace('\','/')) -C $target.Folder merge-base --is-ancestor origin/main HEAD
        if ($LASTEXITCODE -ne 0) { throw 'Remote main has incoming changes. Integrate them before publication.' }
    }
}
Push-Location -LiteralPath $website
try { & $nodeExe build.mjs; if ($LASTEXITCODE -ne 0) { throw 'Website build failed.' } }
finally { Pop-Location }
$policyUrl = 'https://github.com/rathousky-pro/file-compass/blob/main/docs/CODE-SIGNING.md'
foreach ($name in @('index.html','cs.html')) {
    $html = [IO.File]::ReadAllText((Join-Path $website $name))
    if (!$html.Contains($policyUrl) -or !$html.Contains('Code signing policy') -or !$html.Contains('0.9.8')) { throw ('Signing-policy link missing: '+$name) }
}
if ($CheckOnly) { Write-Output 'PASS: local policy, repository identities and both website links. No commit, network access or upload.'; return }
# Publish the policy before the website that links to it. Existing ZIP assets are untouched.
foreach ($target in $targets) {
    Invoke-PolicyGit $target.Folder (@('add','--')+$target.Files)
    Invoke-PolicyGit $target.Folder @('diff','--cached','--check')
    & $gitExe --no-pager -c ('safe.directory='+$target.Folder.Replace('\','/')) -C $target.Folder diff --cached --quiet
    if ($LASTEXITCODE -eq 1) { Invoke-PolicyGit $target.Folder @('commit','-m','Document planned File Compass 0.9.8 code signing policy') }
    elseif ($LASTEXITCODE -ne 0) { throw 'Cannot inspect staged changes.' }
    Invoke-PolicyGit $target.Folder @('push','origin','HEAD:main')
}
Write-Output ('Published code signing policy: '+$policyUrl)
Write-Output 'Website updated. Existing ZIP and its download URL were preserved.'

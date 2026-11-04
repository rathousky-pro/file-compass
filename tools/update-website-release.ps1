param(
    [Parameter(Mandatory=$true)][string]$ReleaseDirectory,
    [string]$WebsiteDirectory = '',
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
if (!$WebsiteDirectory) { $WebsiteDirectory = Join-Path $PSScriptRoot '../ai-docs/artifacts/file-compass-web-counter' }
. (Join-Path $PSScriptRoot 'github-release.ps1')
$bundle = Read-ReleaseBundle -Directory $ReleaseDirectory
$website = (Resolve-Path -LiteralPath $WebsiteDirectory).Path
$safeWebsite = $website.Replace('\','/')
$files = @('build.mjs','releases.json','index.html','cs.html','README.md')
function Invoke-WebsiteGit {
    & git --no-pager -c "safe.directory=$safeWebsite" -c 'user.name=JRKladno76' -c 'user.email=jan.rathousky.kladno@gmail.com' -C $website @args
    if ($LASTEXITCODE -ne 0) { throw ('Website Git operation failed: '+$args[0]) }
}
if ((Invoke-WebsiteGit remote get-url origin) -ne 'https://github.com/rathousky-pro/file-compass-web.git') { throw 'Unexpected website repository.' }
if ((Invoke-WebsiteGit branch --show-current) -ne 'main') { throw 'Website must be on main.' }
if ($Publish) {
    # Never advertise an absent, draft or different ZIP. No credentials go into the website.
    $headers = @{Accept='application/vnd.github+json';'User-Agent'='FileCompass-Website-Publisher'}
    $release = Invoke-RestMethod -Uri ('https://api.github.com/repos/rathousky-pro/file-compass-web/releases/tags/'+[Uri]::EscapeDataString($bundle.Tag)) -Headers $headers -TimeoutSec 30
    if ($release.draft -or $release.tag_name -cne $bundle.Tag) { throw 'The prepared release is not public.' }
    foreach ($file in @($bundle.Zip,$bundle.Checksum)) {
        $name = [IO.Path]::GetFileName($file)
        $assets = @($release.assets | Where-Object { $_.name -ceq $name })
        $digest = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
        $expectedUrl = 'https://github.com/rathousky-pro/file-compass-web/releases/download/'+$bundle.Tag+'/'+$name
        if ($assets.Count -ne 1 -or $assets[0].state -ne 'uploaded' -or [long]$assets[0].size -ne (Get-Item -LiteralPath $file).Length -or $assets[0].digest -cne ('sha256:'+$digest) -or $assets[0].browser_download_url -cne $expectedUrl) { throw ('Published asset does not match the local release: '+$name) }
    }
    Invoke-WebsiteGit fetch origin main
    if ((Invoke-WebsiteGit rev-parse HEAD) -ne (Invoke-WebsiteGit rev-parse origin/main)) { throw 'The remote website changed. Integrate it before publication.' }
    $staged = @(Invoke-WebsiteGit diff --cached --name-only)
    if (@($staged | Where-Object { $_ -cnotin $files }).Count) { throw 'Unrelated staged website changes must be handled first.' }
}
if ($bundle.Tag -notmatch '^v(?<version>\d+\.\d+\.\d+)(?:-(?<channel>alpha|beta|rc))?-build\.(?<build>\d{8}\.\d{2,9})$') { throw 'Unexpected release tag.' }
$version = $Matches.version; $channel = [string]$Matches.channel; $build = $Matches.build
$base = 'https://github.com/rathousky-pro/file-compass-web/releases'
$zipUrl = $base+'/download/'+$bundle.Tag+'/FileCompass-win-x64.zip'
$metadata = [ordered]@{
    version=($version+$(if($channel){' '+$channel})+' / '+$build)
    channel=$channel; build=$build; sizeBytes=(Get-Item -LiteralPath $bundle.Zip).Length
    access='public'; repository='rathousky-pro/file-compass-web'
    sourceRepository='rathousky-pro/file-compass'; license='MIT'
    zip=$zipUrl; exe=$null; notes=($base+'/tag/'+$bundle.Tag); checksum=($zipUrl+'.sha256')
}
[IO.File]::WriteAllText((Join-Path $website 'releases.json'),($metadata|ConvertTo-Json)+[Environment]::NewLine,(New-Object Text.UTF8Encoding($false)))
Push-Location -LiteralPath $website
try { & node build.mjs; if ($LASTEXITCODE -ne 0) { throw 'Website generation failed.' } }
finally { Pop-Location }
foreach ($page in @('index.html','cs.html')) {
    $html = [IO.File]::ReadAllText((Join-Path $website $page))
    if (!$html.Contains($zipUrl) -or !$html.Contains($build) -or !$html.Contains('MIT License')) { throw ('Missing release information: '+$page) }
}
if ($Publish) {
    Invoke-WebsiteGit add -- @files
    Invoke-WebsiteGit diff --cached --check
    & git --no-pager -c "safe.directory=$safeWebsite" -C $website diff --cached --quiet
    if ($LASTEXITCODE -eq 1) { Invoke-WebsiteGit commit -m ('Publish MIT beta download '+$build) }
    elseif ($LASTEXITCODE -ne 0) { throw 'Cannot inspect staged website changes.' }
    Invoke-WebsiteGit push origin HEAD:main
    Write-Output ('Website published with verified ZIP: '+$zipUrl)
} else {
    Write-Output ('Prepared locally; ZIP must be published before using -Publish: '+$zipUrl)
}

param([string]$OutputDirectory = '')
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if(!$OutputDirectory){$OutputDirectory=Join-Path $projectRoot ('ai-docs\artifacts\github-publisher-test-'+[Guid]::NewGuid().ToString('N'))}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
. (Join-Path $PSScriptRoot 'github-release.ps1')
$checks=New-Object 'Collections.Generic.List[string]'
function Check($ok,[string]$label){if(!$ok){throw $label};$checks.Add($label)}
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $package=Join-Path $OutputDirectory 'package';New-Item -ItemType Directory -Path $package -Force | Out-Null
    $manifest=@{Format=1;Version='0.9.5';Channel='beta';Build='20261104.76';Files=@()}
    [IO.File]::WriteAllText((Join-Path $package 'file-compass-release.json'),($manifest|ConvertTo-Json))
    $zip=Join-Path $OutputDirectory 'FileCompass-win-x64.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($package,$zip)
    $hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText(($zip+'.sha256'),($hash+'  FileCompass-win-x64.zip'))
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'release-info.json'),(@{tag='v0.9.5-beta-build.20261104.76';sha256=$hash}|ConvertTo-Json))
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'release-notes.md'),'Publisher fixture')
    $bundle=Read-ReleaseBundle $OutputDirectory
    Check ($bundle.Hash -ceq $hash -and $bundle.Prerelease) 'Existing ZIP is validated without rebuilding or changing its bytes'
    $rejected=$false;try{Invoke-ReleaseGitHubRequest -Method GET -Uri 'https://attacker.invalid/releases' -Headers @{}}catch{$rejected=$true}
    Check $rejected 'Transport rejects hosts outside the GitHub API before accessing credentials'

    # Test doubles replace authentication and HTTP. These tests cannot publish remotely.
    function Get-ReleaseGitHubToken {param([string]$ProjectRoot);return 'test-only-not-a-real-token'}
    $script:Release=$null;$script:Assets=@();$script:Calls=New-Object 'Collections.Generic.List[string]'
    function Invoke-ReleaseGitHubRequest {
        param([string]$Method,[string]$Uri,[hashtable]$Headers,[string]$Json,[string]$File,[switch]$AllowNotFound)
        $script:Calls.Add($Method+' '+$Uri)
        if($Method -eq 'GET' -and $Uri -like '*/releases/tags/*'){return $script:Release}
        if($Method -eq 'POST' -and $Uri.EndsWith('/releases')){
            $body=$Json|ConvertFrom-Json
            if(!$body.draft){throw 'Release must start as a draft'}
            $script:Release=[pscustomobject]@{id=123;tag_name=$body.tag_name;draft=$true;prerelease=$body.prerelease};return $script:Release
        }
        if($Method -eq 'GET' -and $Uri -like '*assets?per_page=100'){return $script:Assets}
        if($Method -eq 'POST' -and $Uri.StartsWith('https://uploads.github.com/')){
            $asset=[pscustomobject]@{name=[IO.Path]::GetFileName($File);size=(Get-Item -LiteralPath $File).Length;state='uploaded';digest=('sha256:'+(Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash.ToLowerInvariant())}
            $script:Assets+=@($asset);return $asset
        }
        if($Method -eq 'PATCH'){
            if($script:Assets.Count -ne 2){throw 'Cannot publish incomplete assets'}
            $script:Release.draft=$false;return $script:Release
        }
        throw ('Unexpected test request: '+$Method+' '+$Uri)
    }
    $output=@(Publish-GitHubRelease -Bundle $bundle -ProjectRoot $projectRoot)
    Check (!$script:Release.draft -and $script:Assets.Count -eq 2) 'API publication creates a draft, uploads the ZIP and checksum, then publishes'
    Check ($script:Calls[$script:Calls.Count-1].StartsWith('PATCH ')) 'Draft becomes public only after both uploaded asset digests were verified'
    Check ($output[-1].Contains('/releases/download/'+$bundle.Tag+'/FileCompass-win-x64.zip')) 'Publisher returns the exact URL used by the application updater'
    $script:Calls.Clear();$output=@(Publish-GitHubRelease -Bundle $bundle -ProjectRoot $projectRoot)
    Check (@($script:Calls|Where-Object{!$_.StartsWith('GET ')}).Count -eq 0) 'Rerunning publication of an identical published release makes no remote changes'
    $script:Release.draft=$true;$script:Assets=@($script:Assets[0]);$script:Calls.Clear()
    [void]@(Publish-GitHubRelease -Bundle $bundle -ProjectRoot $projectRoot)
    Check (@($script:Calls|Where-Object{$_.StartsWith('POST https://uploads.github.com/')}).Count -eq 1) 'Interrupted draft publication resumes by uploading only the missing asset'
    $script:Release.draft=$true;$script:Assets[0].digest='sha256:'+('0'*64);$script:Calls.Clear();$rejected=$false
    try{[void]@(Publish-GitHubRelease -Bundle $bundle -ProjectRoot $projectRoot)}catch{$rejected=$true}
    Check ($rejected -and @($script:Calls|Where-Object{$_.StartsWith('PATCH ') -or $_.StartsWith('DELETE ')}).Count -eq 0) 'Conflicting asset digest is rejected without publishing or deleting an existing asset'
    $content=[IO.File]::ReadAllText((Join-Path $OutputDirectory 'release-info.json'));[IO.File]::WriteAllText((Join-Path $OutputDirectory 'release-info.json'),$content.Replace($hash,('0'*64)));$rejected=$false
    try{[void](Read-ReleaseBundle $OutputDirectory)}catch{$rejected=$true}
    Check $rejected 'A changed ZIP or incorrect metadata digest is rejected before authentication'
    $checks.Add('PASS: '+$checks.Count+' checks (mocked GitHub; no network or credentials used)')
    [IO.File]::WriteAllLines((Join-Path $OutputDirectory 'result.txt'),$checks)
    $checks
} catch {
    $checks.Add('FAIL: '+$_.Exception.Message)
    [IO.File]::WriteAllLines((Join-Path $OutputDirectory 'result.txt'),$checks)
    throw
}

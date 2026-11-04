# Shared publishing helpers. Dot-sourcing this file does not access credentials or the network.
function Read-ReleaseBundle {
    param([Parameter(Mandatory=$true)][string]$Directory)
    $folder = (Resolve-Path -LiteralPath $Directory -ErrorAction Stop).Path
    $infoPath = Join-Path $folder 'release-info.json'
    $info = Get-Content -LiteralPath $infoPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $zip = Join-Path $folder 'FileCompass-win-x64.zip'
    $checksum = $zip+'.sha256'
    $notesPath = Join-Path $folder 'release-notes.md'
    if ($info.tag -notmatch '^v\d{1,4}\.\d{1,4}\.\d{1,4}(?:-(alpha|beta|rc))?-build\.\d{8}\.\d{2,9}$') { throw 'Invalid release tag.' }
    $digest = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($digest -cne $info.sha256) { throw 'ZIP hash does not match release-info.json. No upload was started.' }
    $expectedChecksum = $digest+'  FileCompass-win-x64.zip'
    if ((Get-Content -LiteralPath $checksum -Raw).Trim() -cne $expectedChecksum) { throw 'ZIP checksum file does not match the ZIP.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $entry = $archive.GetEntry('file-compass-release.json')
        if (!$entry -or $entry.Length -gt 8MB) { throw 'Missing or oversized release manifest.' }
        $reader = New-Object IO.StreamReader($entry.Open())
        try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    } finally { $archive.Dispose() }
    $channel = [string]$manifest.Channel
    $tag = 'v'+$manifest.Version+$(if($channel){'-'+$channel})+'-build.'+$manifest.Build
    if ($manifest.Format -ne 1 -or $tag -cne $info.tag) { throw 'ZIP manifest does not match the release tag.' }
    if ((Get-Item -LiteralPath $notesPath).Length -gt 1MB) { throw 'Release notes exceed their limit.' }
    return [pscustomobject]@{
        Tag=$tag; Zip=$zip; Checksum=$checksum; Hash=$digest; Prerelease=[bool]$channel
        Title=('File Compass '+$manifest.Version+$(if($channel){' '+$channel})+' / '+$manifest.Build)
        Notes=[IO.File]::ReadAllText($notesPath)
    }
}

function Get-ReleaseGitHubToken {
    param([Parameter(Mandatory=$true)][string]$ProjectRoot)
    # Prefer an existing token supplied by CI; never print it or persist it in a script.
    foreach ($name in @('GH_TOKEN','GITHUB_TOKEN')) {
        $value = [Environment]::GetEnvironmentVariable($name,'Process')
        if ($value) { return $value }
    }
    $command = Get-Command git.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    $userDirectory = [Environment]::GetFolderPath('UserProfile')
    $candidates = @(
        $(if($command){$command.Source}),
        (Join-Path $userDirectory '.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe'),
        (Join-Path $env:ProgramFiles 'Git\cmd\git.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Git\cmd\git.exe')
    )
    $git = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -First 1
    if (!$git) { throw 'Git could not be found. Use the Git installation used for the project push, or supply GITHUB_TOKEN through the process environment.' }
    $gitDirectory = Split-Path -Parent $git
    $safeRoot = $ProjectRoot.Replace('\','/')
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $git
    $start.Arguments = '--no-pager -c "safe.directory='+$safeRoot+'" -C "'+$ProjectRoot+'" credential fill'
    $start.UseShellExecute=$false; $start.CreateNoWindow=$true
    $start.RedirectStandardInput=$true; $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    $start.EnvironmentVariables['PATH']=$gitDirectory+';'+(Join-Path (Split-Path -Parent $gitDirectory) 'mingw64\bin')+';'+$env:PATH
    Write-Host 'Pouzivam prihlaseni GitHub ulozene u Gitu. Pokud chybi, Git muze otevrit prihlaseni v prohlizeci.'
    $process = New-Object Diagnostics.Process
    $process.StartInfo=$start
    $credentialText=$null; $password=$null
    try {
        [void]$process.Start()
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.Write("protocol=https`nhost=github.com`npath=rathousky-pro/file-compass-web.git`n`n")
        $process.StandardInput.Close()
        $process.WaitForExit()
        $credentialText=$output.GetAwaiter().GetResult()
        [void]$errors.GetAwaiter().GetResult() # Do not forward credential-helper output.
        if($process.ExitCode -ne 0){throw 'GitHub authentication through Git failed. Sign in with the Git credential manager used for the project.'}
        foreach($line in ($credentialText -split "`r?`n")){if($line.StartsWith('password=')){$password=$line.Substring(9)}}
        if(!$password){throw 'Git did not return a GitHub credential. No upload was started.'}
        return $password
    } finally { $credentialText=$null; $password=$null; $process.Dispose() }
}

function Invoke-ReleaseGitHubRequest {
    param([string]$Method,[string]$Uri,[hashtable]$Headers,[string]$Json,[string]$File,[switch]$AllowNotFound)
    $address = [Uri]$Uri
    if($address.Scheme -ne 'https' -or !$address.IsDefaultPort -or $address.UserInfo -or $address.Host -notin @('api.github.com','uploads.github.com')){throw 'Unexpected GitHub API address.'}
    $parameters=@{Uri=$Uri;Method=$Method;Headers=$Headers;ErrorAction='Stop';MaximumRedirection=0;TimeoutSec=600}
    if($File){$parameters.InFile=$File;$parameters.ContentType='application/octet-stream'}
    elseif($Json){$parameters.Body=[Text.Encoding]::UTF8.GetBytes($Json);$parameters.ContentType='application/json; charset=utf-8'}
    try { return Invoke-RestMethod @parameters }
    catch {
        $status=0
        if($_.Exception.Response){try{$status=[int]$_.Exception.Response.StatusCode}catch{}}
        if($AllowNotFound -and $status -eq 404){return $null}
        if($status -in @(401,403)){throw "GitHub API denied access ($status). The GitHub account/token needs Contents write permission for rathousky-pro/file-compass-web. GitHub CLI is not required."}
        throw "GitHub API request failed ($status). Any incomplete upload remains a draft; rerun the same command to resume."
    }
}

function Publish-GitHubRelease {
    param([Parameter(Mandatory=$true)]$Bundle,[Parameter(Mandatory=$true)][string]$ProjectRoot)
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
    $token=Get-ReleaseGitHubToken -ProjectRoot $ProjectRoot
    $headers=@{Accept='application/vnd.github+json';Authorization=('Bearer '+$token);'User-Agent'='FileCompass-Release-Publisher';'X-GitHub-Api-Version'='2026-03-10'}
    $base='https://api.github.com/repos/rathousky-pro/file-compass-web'
    $tag=[Uri]::EscapeDataString($Bundle.Tag)
    try {
        $release=Invoke-ReleaseGitHubRequest -Method GET -Uri ($base+'/releases/tags/'+$tag) -Headers $headers -AllowNotFound
        if(!$release){
            Write-Host ('Vytvarim koncept vydani '+$Bundle.Tag)
            $body=@{tag_name=$Bundle.Tag;name=$Bundle.Title;body=$Bundle.Notes;draft=$true;prerelease=$Bundle.Prerelease}|ConvertTo-Json -Depth 4
            $release=Invoke-ReleaseGitHubRequest -Method POST -Uri ($base+'/releases') -Headers $headers -Json $body
        }
        $releaseId=0L
        if(![long]::TryParse([string]$release.id,[ref]$releaseId) -or $releaseId -lt 1 -or $release.tag_name -cne $Bundle.Tag){throw 'GitHub returned an unexpected release identity.'}
        if([bool]$release.prerelease -ne $Bundle.Prerelease){throw 'Existing release uses a different release channel. No assets were overwritten.'}
        foreach($file in @($Bundle.Zip,$Bundle.Checksum)){
            $name=[IO.Path]::GetFileName($file)
            $digest=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
            $size=(Get-Item -LiteralPath $file).Length
            $assets=@(Invoke-ReleaseGitHubRequest -Method GET -Uri ($base+'/releases/'+$releaseId+'/assets?per_page=100') -Headers $headers)
            $found=@($assets | Where-Object { $_.name -ceq $name })
            if($found.Count -gt 1){throw ('Duplicate GitHub asset: '+$name)}
            if($found.Count -eq 1){$asset=$found[0]}
            else {
                if(!$release.draft){throw ('Published release is incomplete. Existing published assets will not be modified: '+$Bundle.Tag)}
                Write-Host ('Nahravam '+$name+' ('+[Math]::Round($size/1MB,1)+' MiB)')
                $uri='https://uploads.github.com/repos/rathousky-pro/file-compass-web/releases/'+$releaseId+'/assets?name='+[Uri]::EscapeDataString($name)
                $asset=Invoke-ReleaseGitHubRequest -Method POST -Uri $uri -Headers $headers -File $file
            }
            if($asset.name -cne $name -or [long]$asset.size -ne $size -or $asset.state -ne 'uploaded' -or $asset.digest -cne ('sha256:'+$digest)){throw ('GitHub asset does not match the local file or has no confirmed digest: '+$name+'. No published assets were overwritten.')}
        }
        if($release.draft){
            Write-Host 'Oba soubory jsou overene. Zverejnuji vydani.'
            $body=@{draft=$false;prerelease=$Bundle.Prerelease}|ConvertTo-Json
            $release=Invoke-ReleaseGitHubRequest -Method PATCH -Uri ($base+'/releases/'+$releaseId) -Headers $headers -Json $body
            if($release.draft){throw 'GitHub did not publish the prepared release.'}
        }
        Write-Output ('Published: https://github.com/rathousky-pro/file-compass-web/releases/tag/'+$Bundle.Tag)
        Write-Output ('ZIP: https://github.com/rathousky-pro/file-compass-web/releases/download/'+$Bundle.Tag+'/FileCompass-win-x64.zip')
    } finally {$headers.Authorization=$null;$token=$null}
}

param([string]$OutputDirectory, [string]$Python, [string]$Dependencies, [string]$ExecutablePath, [string]$TestSwitch = '--relay-test')
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$servers = @()
try {
    $ports = @()
    foreach ($kind in @('ftp-source','ftp-target','sftp-target','scp-target')) {
        $folder = Join-Path $output $kind
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        $portFile = Join-Path $folder 'port.txt'
        $protocol = $kind.Split('-')[0]
        $arguments = @((Join-Path $PSScriptRoot ($protocol + '_fixture.py')), [IO.Path]::GetFullPath($Dependencies), (Join-Path $folder 'server'), $portFile) | ForEach-Object { '"' + $_.Replace('"','') + '"' }
        $server = Start-Process -FilePath $Python -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $folder 'server.log') -RedirectStandardError (Join-Path $folder 'errors.log')
        $servers += $server
        $deadline = (Get-Date).AddSeconds(15)
        while (!(Test-Path -LiteralPath $portFile) -and !$server.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 100 }
        if (!(Test-Path -LiteralPath $portFile)) { throw ('Test server did not start: ' + $kind) }
        $ports += Get-Content -LiteralPath $portFile
    }
    $client = Join-Path $output 'client'
    $test = Start-Process -FilePath ([IO.Path]::GetFullPath($ExecutablePath)) -ArgumentList (@($TestSwitch, ('"' + $client + '"')) + $ports) -WindowStyle Hidden -Wait -PassThru
    Get-Content -LiteralPath (Join-Path $client 'relay-result.txt')
    if ($test.ExitCode -ne 0) { throw 'Server relay test failed.' }
}
finally { foreach ($server in $servers) { if (!$server.HasExited) { Stop-Process -Id $server.Id } } }

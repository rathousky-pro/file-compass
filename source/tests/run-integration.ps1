param(
    [ValidateSet('ftp','sftp','scp')][string]$Protocol = 'ftp',
    [string]$OutputDirectory,
    [string]$Python = 'python',
    [string]$Dependencies,
    [string]$ExecutablePath,
    [switch]$Resume,
    [switch]$Sshfs,
    [string]$FixtureMode = 'default'
)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) { $ExecutablePath = Join-Path (Split-Path -Parent $project) 'bin\File Compass.exe' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$run = Join-Path $output ($Protocol + '-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$portFile = Join-Path $run 'port.txt'
$fixture = Join-Path $PSScriptRoot ($Protocol + '_fixture.py')
$serverRoot = Join-Path $run 'server'
$clientRoot = Join-Path $run 'client'
function Quote-Argument([string]$value) { return '"' + $value.Replace('"', '') + '"' }
$fixtureArguments = @($fixture, [IO.Path]::GetFullPath($Dependencies), $serverRoot, $portFile)
if ($FixtureMode -ne 'default') { $fixtureArguments += $FixtureMode }
$arguments = $fixtureArguments | ForEach-Object { Quote-Argument $_ }
$server = Start-Process -FilePath $Python -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $run 'server.log') -RedirectStandardError (Join-Path $run 'server-errors.log')
$secondServer = $null
try {
    $deadline = (Get-Date).AddSeconds(15)
    while (!(Test-Path -LiteralPath $portFile) -and !$server.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 100 }
    if (!(Test-Path -LiteralPath $portFile)) { throw ('Server did not start. See ' + (Join-Path $run 'server-errors.log')) }
    $port = Get-Content -LiteralPath $portFile
    $testArguments = @($(if ($Sshfs) { '--sshfs-test' } else { '--' + $Protocol + $(if ($Resume) { '-resume-test' } else { '-test' }) }), (Quote-Argument $clientRoot), $port)
    if ($Protocol -eq 'sftp' -and !$Sshfs) {
        $ftpPortFile = Join-Path $run 'ftp-port.txt'
        $ftpArguments = @((Join-Path $PSScriptRoot 'ftp_fixture.py'), [IO.Path]::GetFullPath($Dependencies), (Join-Path $run 'ftp-server'), $ftpPortFile) | ForEach-Object { Quote-Argument $_ }
        $secondServer = Start-Process -FilePath $Python -ArgumentList $ftpArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $run 'ftp-server.log') -RedirectStandardError (Join-Path $run 'ftp-server-errors.log')
        $ftpDeadline = (Get-Date).AddSeconds(15)
        while (!(Test-Path -LiteralPath $ftpPortFile) -and !$secondServer.HasExited -and (Get-Date) -lt $ftpDeadline) { Start-Sleep -Milliseconds 100 }
        if (!(Test-Path -LiteralPath $ftpPortFile)) { throw 'Second FTP server did not start.' }
        $testArguments += Get-Content -LiteralPath $ftpPortFile
    }
    $test = Start-Process -FilePath ([IO.Path]::GetFullPath($ExecutablePath)) -ArgumentList $testArguments -WindowStyle Hidden -Wait -PassThru
    $resultName = $(if ($Sshfs) { 'sshfs-test-result.txt' } else { $Protocol + $(if ($Resume) { '-resume-test-result.txt' } else { '-test-result.txt' }) })
    $result = Join-Path $clientRoot $resultName
    Copy-Item -LiteralPath $result -Destination (Join-Path $output $resultName)
    Get-Content -LiteralPath $result
    if ($test.ExitCode -ne 0) { throw ($Protocol + ' integration test failed.') }
}
finally { if (!$server.HasExited) { Stop-Process -Id $server.Id }; if ($secondServer -and !$secondServer.HasExited) { Stop-Process -Id $secondServer.Id } }

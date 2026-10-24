param([string]$ExecutablePath,[string]$OutputDirectory,[string]$Python,[string]$Dependencies,[string[]]$Modes=@('list','ansi','ansi-auto','ftps','ftps-implicit','ftps-wrong-host','ftps-client','key-rsa','key-ed25519','interactive','ftp-proxy-1','ftp-proxy-2','ftp-proxy-3','sftp-proxy-1','sftp-proxy-2','sftp-proxy-3'))
$ErrorActionPreference='Stop'
$exe=[IO.Path]::GetFullPath($ExecutablePath)
$bin=Split-Path $exe -Parent
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 ('/reference:'+$exe) ('/out:'+(Join-Path $bin 'BackendFeatures.exe')) (Join-Path $PSScriptRoot 'BackendFeatures.cs')
if($LASTEXITCODE -ne 0){throw 'Feature probe build failed.'}
foreach($mode in $Modes) {
 $protocol=if($mode -in @('key-rsa','key-ed25519','interactive','agent','keepalive') -or $mode.StartsWith('sftp-')){'sftp'}else{'ftp'}
 $run=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ($mode+'-'+[Guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory -Path $run -Force | Out-Null
 $portFile=Join-Path $run 'port.txt'
 $serverMode=if($mode -eq 'agent'){'key-rsa'}elseif($mode -eq 'keepalive'){'password'}elseif($mode.Contains('-proxy-')){if($protocol -eq 'ftp'){'default'}else{'password'}}else{$mode}
 $arguments=@((Join-Path $PSScriptRoot ($protocol+'_fixture.py')),[IO.Path]::GetFullPath($Dependencies),(Join-Path $run 'server'),$portFile,$serverMode) | ForEach-Object {'"'+$_+'"'}
 $server=Start-Process -FilePath $Python -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $run 'server.log') -RedirectStandardError (Join-Path $run 'server-errors.log')
 $proxy=$null; $agentServer=$null; $originalAgentSocket=$env:SSH_AUTH_SOCK
 try {
  $deadline=(Get-Date).AddSeconds(15)
  while(!(Test-Path -LiteralPath $portFile) -and !$server.HasExited -and (Get-Date) -lt $deadline){Start-Sleep -Milliseconds 100}
  if(!(Test-Path -LiteralPath $portFile)){Get-Content -LiteralPath (Join-Path $run 'server-errors.log'); throw 'Feature server did not start.'}
  if($mode -eq 'agent') {
   $env:SSH_AUTH_SOCK='\\.\pipe\FileCompass-agent-test-'+[Guid]::NewGuid().ToString('N')
   $agentReady=Join-Path $run 'agent-ready.txt'
   $agentArguments=@((Join-Path $PSScriptRoot 'agent_fixture.py'),[IO.Path]::GetFullPath($Dependencies),(Join-Path $run 'id_test'),$env:SSH_AUTH_SOCK,$agentReady) | ForEach-Object {'"'+$_+'"'}
   $agentServer=Start-Process -FilePath $Python -ArgumentList $agentArguments -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $run 'agent-errors.txt')
   $deadline=(Get-Date).AddSeconds(10)
   while(!(Test-Path -LiteralPath $agentReady) -and !$agentServer.HasExited -and (Get-Date) -lt $deadline){Start-Sleep -Milliseconds 100}
   if(!(Test-Path -LiteralPath $agentReady)){throw 'Fixture agent did not start.'}
  }
  $probeArguments=@($protocol,$mode,('"'+$run+'"'),(Get-Content -LiteralPath $portFile))
  if($mode.Contains('-proxy-')) {
   $method=$mode.Substring($mode.Length-1); $proxyPort=Join-Path $run 'proxy-port.txt'
   $proxyArguments=@((Join-Path $PSScriptRoot 'proxy_fixture.py'),$method,$proxyPort) | ForEach-Object {'"'+$_+'"'}
   $proxy=Start-Process -FilePath $Python -ArgumentList $proxyArguments -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $run 'proxy-errors.log')
   $deadline=(Get-Date).AddSeconds(10)
   while(!(Test-Path -LiteralPath $proxyPort) -and !$proxy.HasExited -and (Get-Date) -lt $deadline){Start-Sleep -Milliseconds 100}
   if(!(Test-Path -LiteralPath $proxyPort)){throw 'Proxy did not start.'}
   $probeArguments+=@((Get-Content -LiteralPath $proxyPort),$method)
  }
  $probe=Start-Process -FilePath (Join-Path $bin 'BackendFeatures.exe') -ArgumentList $probeArguments -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $run 'result.txt') -RedirectStandardError (Join-Path $run 'errors.txt')
  Get-Content -LiteralPath (Join-Path $run 'result.txt'); Get-Content -LiteralPath (Join-Path $run 'errors.txt')
  if($probe.ExitCode -ne 0){throw ('Feature check failed: '+$mode)}
 } finally { $env:SSH_AUTH_SOCK=$originalAgentSocket; if($agentServer -and !$agentServer.HasExited){Stop-Process -Id $agentServer.Id}; if(!$server.HasExited){Stop-Process -Id $server.Id}; if($proxy -and !$proxy.HasExited){Stop-Process -Id $proxy.Id}}
}

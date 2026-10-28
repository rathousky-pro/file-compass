param()
$ErrorActionPreference='Stop'
$taskDownloads=Join-Path $PSScriptRoot 'downloads\editor'
$taskLicenses=Join-Path $PSScriptRoot 'ui\licenses\YamlDotNet'
New-Item -ItemType Directory -Path $taskDownloads,$taskLicenses -Force | Out-Null
$taskPackage=Join-Path $taskDownloads 'yamldotnet.16.3.0.nupkg'
$taskUrl='https://api.nuget.org/v3-flatcontainer/yamldotnet/16.3.0/yamldotnet.16.3.0.nupkg'
if(!(Test-Path -LiteralPath $taskPackage)){Invoke-WebRequest -UseBasicParsing -Uri $taskUrl -OutFile $taskPackage}
if((Get-FileHash -LiteralPath $taskPackage -Algorithm SHA256).Hash -ne 'e068bcc1243c46c8bfdfe2f27a026bfff03cde7c67d9f37c2cdd70bd24a9dfd4'){throw 'YamlDotNet package SHA-256 mismatch.'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskArchive=[IO.Compression.ZipFile]::OpenRead($taskPackage)
try{$taskEntry=$taskArchive.GetEntry('lib/net47/YamlDotNet.dll');if($null -eq $taskEntry){throw 'Missing net47 assembly.'};$taskStream=$taskEntry.Open();try{$taskMemory=New-Object IO.MemoryStream;$taskStream.CopyTo($taskMemory);$taskBytes=$taskMemory.ToArray()}finally{$taskStream.Dispose();if($taskMemory){$taskMemory.Dispose()}}}finally{$taskArchive.Dispose()}
$taskSha=[Security.Cryptography.SHA256]::Create()
try{$taskHash=[BitConverter]::ToString($taskSha.ComputeHash($taskBytes)).Replace('-','').ToLowerInvariant()}finally{$taskSha.Dispose()}
if($taskHash -ne '9172f0758ed76963e0ede3b5ca9a1fb5b82f64c7e7a92cb3a6de39efa933a668'){throw 'YamlDotNet assembly SHA-256 mismatch.'}
[IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'ui\YamlDotNet.dll'),$taskBytes)
$taskNotices=@(
 @{Name='LICENSE.txt';Hash='DAEA8B7F1B2BAB48EA12AD50B0CE91634669D99D7DD0CB64AED8DFFEEEF69ED3'},
 @{Name='LICENSE-libyaml.txt';Hash='CB65F46DF0D9125BA60862117200EC6631FEF3A8169CD0B4513FFCAD4EF5CB9B'}
)
foreach($taskNotice in $taskNotices){$taskPath=Join-Path $taskLicenses $taskNotice.Name;if(!(Test-Path -LiteralPath $taskPath)){Invoke-WebRequest -UseBasicParsing -Uri ('https://raw.githubusercontent.com/aaubry/YamlDotNet/v16.3.0/'+$taskNotice.Name) -OutFile $taskPath};if((Get-FileHash -LiteralPath $taskPath).Hash -ne $taskNotice.Hash){throw ('YamlDotNet license SHA-256 mismatch: '+$taskNotice.Name)}}
$taskManifest=[ordered]@{name='YamlDotNet';version='16.3.0';license='MIT';source='https://github.com/aaubry/YamlDotNet/tree/v16.3.0';package=$taskUrl;package_sha256='e068bcc1243c46c8bfdfe2f27a026bfff03cde7c67d9f37c2cdd70bd24a9dfd4';assembly='lib/net47/YamlDotNet.dll';assembly_sha256=$taskHash}
$taskManifest|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskLicenses 'SOURCE.json') -Encoding UTF8
Write-Output 'Verified YamlDotNet 16.3.0 (net47, MIT) and upstream license notices.'

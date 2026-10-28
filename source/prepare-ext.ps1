$ErrorActionPreference='Stop'
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ui\licenses\DiscUtils\SOURCE.json') -Raw | ConvertFrom-Json
$cache=Join-Path $PSScriptRoot 'downloads\ext'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
foreach($package in $manifest){
 $archive=Join-Path $cache ($package.package+'.zip')
 if(!(Test-Path -LiteralPath $archive)){Invoke-WebRequest -UseBasicParsing ('https://api.nuget.org/v3-flatcontainer/'+$package.package+'/'+$package.version+'/'+$package.package+'.'+$package.version+'.nupkg') -OutFile $archive}
 if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $package.sha256){throw ('Dependency hash mismatch: '+$package.package)}
 $expanded=Join-Path $cache $package.package
 Expand-Archive -LiteralPath $archive -DestinationPath $expanded -Force
 foreach($file in $package.files){
  $source=Join-Path $expanded ('lib\'+$package.framework+'\'+$file.name)
  if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $file.sha256){throw ('Assembly hash mismatch: '+$file.name)}
  Copy-Item -LiteralPath $source -Destination (Join-Path $PSScriptRoot ('ui\'+$file.name)) -Force
 }
}
Write-Output 'Verified MIT-licensed ext reader dependencies restored.'

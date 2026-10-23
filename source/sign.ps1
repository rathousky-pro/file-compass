param(
    [string]$Executable = '',
    [string]$CertificateFile = $env:COMMANDER_SIGN_PFX,
    [string]$CertificatePassword = $env:COMMANDER_SIGN_PASSWORD,
    [string]$TimestampServer = 'http://timestamp.digicert.com'
)
# Supply the signing certificate separately; never commit it.
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Executable)) { $Executable = Join-Path (Split-Path -Parent $PSScriptRoot) 'bin\File Compass.exe' }
if (-not (Test-Path -LiteralPath $Executable)) { throw "Soubor k podpisu neexistuje: $Executable" }
if ([string]::IsNullOrWhiteSpace($CertificateFile) -or -not (Test-Path -LiteralPath $CertificateFile)) { throw 'Zadejte -CertificateFile (nebo COMMANDER_SIGN_PFX) s platným PFX certifikátem pro podpis kódu.' }
$signtool = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $signtool) { throw 'signtool.exe nebyl nalezen (nainstalujte Windows SDK).' }
$signArguments = @('sign', '/fd', 'SHA256', '/f', $CertificateFile, '/tr', $TimestampServer, '/td', 'SHA256')
if (-not [string]::IsNullOrEmpty($CertificatePassword)) { $signArguments += @('/p', $CertificatePassword) }
& $signtool.FullName @signArguments $Executable
if ($LASTEXITCODE -ne 0) { throw 'Podpis selhal.' }
& $signtool.FullName verify /pa /v $Executable
if ($LASTEXITCODE -ne 0) { throw 'Ověření podpisu selhalo.' }
Write-Output "Podepsáno: $Executable"

param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$fixtureRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$targetDirectory = Join-Path $fixtureRoot 'target'
New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
$payload = Join-Path $fixtureRoot 'payload.txt'
[IO.File]::WriteAllText($payload, 'FileCompass installation fixture', [Text.Encoding]::ASCII)
$msi = Join-Path $fixtureRoot 'fixture.msi'
if (Test-Path -LiteralPath $msi) { Remove-Item -LiteralPath $msi }
$installer = New-Object -ComObject WindowsInstaller.Installer
function Invoke-Com($object, [string]$name, $arguments) {
    return $object.GetType().InvokeMember($name, 'InvokeMethod', $null, $object, $arguments)
}
$database = $installer.OpenDatabase([string]$msi, [int]3)
function Sql([string]$statement) {
    $view = Invoke-Com $database 'OpenView' @($statement)
    try { $null = Invoke-Com $view 'Execute' @(); $null = Invoke-Com $view 'Close' @() }
    finally { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) | Out-Null }
}
function Escape-Sql([string]$text) { return $text.Replace("'", "''") }
Sql 'CREATE TABLE `Property` (`Property` CHAR(72) NOT NULL, `Value` CHAR(0) LOCALIZABLE PRIMARY KEY `Property`)'
Sql 'CREATE TABLE `Directory` (`Directory` CHAR(72) NOT NULL, `Directory_Parent` CHAR(72), `DefaultDir` CHAR(255) NOT NULL LOCALIZABLE PRIMARY KEY `Directory`)'
Sql 'CREATE TABLE `Component` (`Component` CHAR(72) NOT NULL, `ComponentId` CHAR(38), `Directory_` CHAR(72) NOT NULL, `Attributes` SHORT NOT NULL, `Condition` CHAR(255), `KeyPath` CHAR(72) PRIMARY KEY `Component`)'
Sql 'CREATE TABLE `Feature` (`Feature` CHAR(38) NOT NULL, `Feature_Parent` CHAR(38), `Title` CHAR(64) LOCALIZABLE, `Description` CHAR(255) LOCALIZABLE, `Display` SHORT, `Level` SHORT NOT NULL, `Directory_` CHAR(72), `Attributes` SHORT NOT NULL PRIMARY KEY `Feature`)'
Sql 'CREATE TABLE `FeatureComponents` (`Feature_` CHAR(38) NOT NULL, `Component_` CHAR(72) NOT NULL PRIMARY KEY `Feature_`, `Component_`)'
Sql 'CREATE TABLE `File` (`File` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, `FileName` CHAR(255) NOT NULL LOCALIZABLE, `FileSize` LONG NOT NULL, `Version` CHAR(72), `Language` CHAR(20), `Attributes` SHORT, `Sequence` LONG NOT NULL PRIMARY KEY `File`)'
Sql 'CREATE TABLE `Media` (`DiskId` SHORT NOT NULL, `LastSequence` LONG NOT NULL, `DiskPrompt` CHAR(64) LOCALIZABLE, `Cabinet` CHAR(255), `VolumeLabel` CHAR(32), `Source` CHAR(72) PRIMARY KEY `DiskId`)'
Sql 'CREATE TABLE `InstallExecuteSequence` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)'
$properties = @{ ProductCode = ('{' + [Guid]::NewGuid().ToString().ToUpperInvariant() + '}'); ProductName = 'FileCompass isolated MSI fixture'; Manufacturer = 'FileCompass test'; ProductVersion = '1.0.0'; ProductLanguage = '1033'; INSTALLDIR = ($targetDirectory + '\'); INSTALLLEVEL='1' }
foreach ($entry in $properties.GetEnumerator()) { Sql ("INSERT INTO ``Property`` (``Property``, ``Value``) VALUES ('" + (Escape-Sql $entry.Key) + "', '" + (Escape-Sql $entry.Value) + "')") }
Sql "INSERT INTO ``Directory`` (``Directory``, ``DefaultDir``) VALUES ('TARGETDIR', 'SourceDir')"
Sql "INSERT INTO ``Directory`` (``Directory``, ``Directory_Parent``, ``DefaultDir``) VALUES ('INSTALLDIR', 'TARGETDIR', '.')"
# Deliberately no RegisterProduct, PublishProduct or registry actions: only a file in this test workspace.
Sql "INSERT INTO ``Component`` (``Component``, ``Directory_``, ``Attributes``, ``KeyPath``) VALUES ('PayloadComponent', 'INSTALLDIR', 0, 'PayloadFile')"
Sql "INSERT INTO ``Feature`` (``Feature``, ``Level``, ``Attributes``) VALUES ('Main', 1, 0)"
Sql "INSERT INTO ``FeatureComponents`` (``Feature_``, ``Component_``) VALUES ('Main', 'PayloadComponent')"
Sql ("INSERT INTO ``File`` (``File``, ``Component_``, ``FileName``, ``FileSize``, ``Attributes``, ``Sequence``) VALUES ('PayloadFile', 'PayloadComponent', 'payload.txt', " + (Get-Item -LiteralPath $payload).Length + ", 0, 1)")
Sql "INSERT INTO ``Media`` (``DiskId``, ``LastSequence``) VALUES (1, 1)"
$sequence = @{ CostInitialize=800; FileCost=900; CostFinalize=1000; InstallValidate=1400; InstallInitialize=1500; ProcessComponents=1600; InstallFiles=4000; InstallFinalize=6600 }
foreach ($entry in $sequence.GetEnumerator()) { Sql ("INSERT INTO ``InstallExecuteSequence`` (``Action``, ``Sequence``) VALUES ('" + $entry.Key + "', " + $entry.Value + ")") }
$summary = $database.GetType().InvokeMember('SummaryInformation', 'GetProperty', $null, $database, @(20))
function Summary([int]$id, $value) { $summary.GetType().InvokeMember('Property', 'SetProperty', $null, $summary, @($id,$value)) | Out-Null }
Summary 2 'FileCompass isolated fixture'
Summary 7 'Intel;1033'
Summary 9 ('{' + [Guid]::NewGuid().ToString().ToUpperInvariant() + '}')
Summary 14 200
Summary 15 8
$null = Invoke-Com $summary 'Persist' @()
$null = Invoke-Com $database 'Commit' @()
foreach ($object in @($summary,$database,$installer)) { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($object) | Out-Null }
Write-Output $msi

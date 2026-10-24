param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PSScriptRoot)
$metadata = Get-Content -LiteralPath (Join-Path $root 'version.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($metadata.version -notmatch '^\d{1,4}\.\d{1,4}\.\d{1,4}$' -or $metadata.channel -notmatch '^[a-z0-9-]*$') { throw 'Invalid application version.' }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $root 'obj' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$hash = [Security.Cryptography.SHA256]::Create()
try { $lockId = [BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($root.ToLowerInvariant()))).Replace('-','') } finally { $hash.Dispose() }
$mutex = New-Object Threading.Mutex($false, ('Local\FileCompass.Build.' + $lockId))
$locked = $false
try {
    try { $locked = $mutex.WaitOne(30000) } catch [Threading.AbandonedMutexException] { $locked = $true }
    if (!$locked) { throw 'Another build is generating version metadata.' }
    $now = [DateTime]::UtcNow
    $day = $now.ToString('yyyyMMdd', [Globalization.CultureInfo]::InvariantCulture)
    $counterRoot = Join-Path $root 'obj'
    New-Item -ItemType Directory -Path $counterRoot -Force | Out-Null
    $counterPath = Join-Path $counterRoot 'build-sequence.json'
    $daily = 1; $revision = 1
    if (Test-Path -LiteralPath $counterPath) {
        $old = Get-Content -LiteralPath $counterPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($old.day -eq $day -and $old.version -eq $metadata.version) { $daily = [int]$old.daily + 1 }
        if ($old.version -eq $metadata.version) { $revision = [int]$old.revision + 1 }
    }
    if ($revision -gt 65535) { throw 'File version revision exhausted. Increase version.json.' }
    $version = [string]$metadata.version
    $channel = [string]$metadata.channel
    $display = if ($channel) { $version + ' ' + $channel } else { $version }
    $build = $day + '.' + $daily.ToString('00', [Globalization.CultureInfo]::InvariantCulture)
    $information = $version + $(if ($channel) { '-' + $channel }) + '+' + $build
    $timestamp = $now.ToString('o', [Globalization.CultureInfo]::InvariantCulture)
    $source = @"
using System;
using System.Globalization;
using System.Reflection;
[assembly: AssemblyVersion("$version.0")]
[assembly: AssemblyFileVersion("$version.$revision")]
[assembly: AssemblyInformationalVersion("$information")]
[assembly: AssemblyMetadata("BuildTimestampUtc", "$timestamp")]
namespace FileCompass {
    internal static class BuildInfo {
        internal const string Version = "$version";
        internal const string DisplayVersion = "$display";
        internal const string Build = "$build";
        internal const string TimestampUtc = "$timestamp";
        internal static DateTime BuiltAtUtc { get { return DateTime.Parse(TimestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind); } }
    }
}
"@
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'BuildInfo.g.cs'), $source, (New-Object Text.UTF8Encoding($false)))
    $state = @{ version=$version; day=$day; daily=$daily; revision=$revision } | ConvertTo-Json
    $stateTemporary = $counterPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    [IO.File]::WriteAllText($stateTemporary, $state, (New-Object Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $stateTemporary -Destination $counterPath -Force
    Write-Output ('Build metadata: ' + $display + ' / ' + $build)
} finally { if ($locked) { $mutex.ReleaseMutex() }; $mutex.Dispose() }


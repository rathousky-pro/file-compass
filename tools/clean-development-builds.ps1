param([switch]$Apply, [string[]]$KeepWork = @('publication-20261028','design-mailer-2026-10-26','help-proposal'))
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
$record = Join-Path $root 'ai-docs\release-audit-20261028'
New-Item -ItemType Directory -Path $record -Force | Out-Null
$work = Join-Path $root 'work'
$running = @(Get-Process | ForEach-Object { try { if ($_.Path) { [IO.Path]::GetFullPath($_.Path) } } catch {} })
$tracked = @(& git -C $root ls-files)
if ($LASTEXITCODE -ne 0) { throw 'Cannot verify tracked source files.' }
$script:count = 0; $script:bytes = [long]0; $script:links = 0
function Walk([string]$path, [string]$boundary, [bool]$remove) {
    $full = [IO.Path]::GetFullPath($path)
    if (!$full.StartsWith($boundary + '\', [StringComparison]::OrdinalIgnoreCase) -and $full -ne $boundary) { throw 'Unsafe cleanup path.' }
    $attrs = [IO.File]::GetAttributes($full)
    $isDir = ($attrs -band [IO.FileAttributes]::Directory) -ne 0
    if (($attrs -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        $script:links++
        # Delete the link itself; never enumerate its target.
        if ($remove) { if ($isDir) { [IO.Directory]::Delete($full) } else { [IO.File]::Delete($full) } }
        return
    }
    if ($isDir) {
        foreach ($child in [IO.Directory]::EnumerateFileSystemEntries($full)) { Walk $child $boundary $remove }
        if ($remove) { [IO.Directory]::Delete($full) }
    } else {
        $script:count++; $script:bytes += ([IO.FileInfo]$full).Length
        if ($remove) {
            # Retain compact validation reports, never profiles or test source media.
            $relative = $full.Substring($root.Length+1)
            if ($relative -notmatch '\\(profile|config|data)\\' -and ([IO.Path]::GetFileName($full) -match '(?i)(result.*\.txt$|diagnosis\.md$|benchmark.*\.(csv|json)$)')) {
                $saved = Join-Path $record ('history\' + $relative)
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved)) | Out-Null
                [IO.File]::Copy($full,$saved,$true)
            }
            if (($attrs -band [IO.FileAttributes]::ReadOnly) -ne 0) { [IO.File]::SetAttributes($full,$attrs -band (-bnot [IO.FileAttributes]::ReadOnly)) }
            [IO.File]::Delete($full)
        }
    }
}
$targets = @()
if (Test-Path -LiteralPath $work) {
    if (([IO.File]::GetAttributes($work) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Work directory is a link.' }
    $targets += Get-ChildItem -LiteralPath $work -Directory | Where-Object { $_.Name -notin $KeepWork } | ForEach-Object { $_.FullName }
}
$targets += Get-ChildItem -LiteralPath (Join-Path $root 'bin') -File -Filter 'File Compass.before-*.exe' | ForEach-Object { $_.FullName }
$obsolete = @('ai-docs\artifacts','dist','verification','source\verification','source\bin',
    'source\downloads\manual','source\downloads\sevenzipsharp',
    'source\downloads\media\magick.net-q8-x64','source\downloads\media\magick.net-q8-x64.zip',
    'bin\libraries\Magick.NET-Q8-x64.dll','bin\plugins\media\Magick.Native-Q8-x64.dll')
foreach ($relative in $obsolete) { $candidate = Join-Path $root $relative; if (Test-Path -LiteralPath $candidate) { $targets += $candidate } }
if (Test-Path -LiteralPath (Join-Path $root 'source\obj')) {
    $targets += Get-ChildItem -LiteralPath (Join-Path $root 'source\obj') -Directory -Filter 'build-*' | ForEach-Object { $_.FullName }
}
foreach ($target in $targets) {
    $full = [IO.Path]::GetFullPath($target)
    if (!$full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Target outside workspace.' }
    $relative = $full.Substring($root.Length+1).Replace('\','/')
    if (@($tracked | Where-Object { $_ -eq $relative -or $_.StartsWith($relative+'/',[StringComparison]::OrdinalIgnoreCase) }).Count) { throw ('Tracked target: '+$relative) }
    if (@($running | Where-Object { $_ -eq $full -or $_.StartsWith($full+'\',[StringComparison]::OrdinalIgnoreCase) }).Count) { throw ('Running executable: '+$relative) }
    Write-Output ($(if($Apply){'Removing '}else{'Would remove '})+$relative)
    Walk $full $full ([bool]$Apply)
}
$summary = [pscustomobject]@{Applied=[bool]$Apply;Targets=$targets.Count;Files=$script:count;Bytes=$script:bytes;Links=$script:links;PreservedWork=$KeepWork;Utc=[DateTime]::UtcNow.ToString('o')}
$summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $record ($(if($Apply){'cleanup-applied-'}else{'cleanup-preview-'})+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'.json')) -Encoding UTF8
$summary | Format-List

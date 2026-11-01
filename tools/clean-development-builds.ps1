param([switch]$Apply, [string[]]$KeepArtifacts = @('maintenance-current-build','maintenance-release-current-build'), [string[]]$KeepWork = @())
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
$record = Join-Path $root ('ai-docs\maintenance-' + (Get-Date -Format 'yyyyMMdd'))
New-Item -ItemType Directory -Path $record -Force | Out-Null
$tracked = @(& git -C $root -c core.quotepath=false ls-files)
if ($LASTEXITCODE -ne 0) { throw 'Cannot check tracked files.' }
$running = @(Get-Process | ForEach-Object { try { if ($_.Path) { [IO.Path]::GetFullPath($_.Path) } } catch { if ($_.ProcessName -like '*Compass*') { throw 'Cannot check a running File Compass process.' } } })
$targets = @()
$artifacts = Join-Path $root 'ai-docs\artifacts'
if (Test-Path -LiteralPath $artifacts) {
    $targets += Get-ChildItem -LiteralPath $artifacts -Directory | Where-Object {
        $_.Name -notin $KeepArtifacts -and ($_.Name -match '-(build|tests?|validation|check|regression|initial|temp|installed-final|final)$' -or $_.Name -match '^panel-settings-deployment-backup-')
    } | ForEach-Object { $_.FullName }
}
$targets += Get-ChildItem -LiteralPath (Join-Path $root 'bin') -File -Filter 'File Compass.before-*.exe' | ForEach-Object { $_.FullName }
$targets += Get-ChildItem -LiteralPath (Join-Path $root 'bin') -Force -File | Where-Object { $_.Name -match '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}_File Compass\.exe$' } | ForEach-Object { $_.FullName }
$iconFolder = Join-Path $root 'bin\icons'
if (Test-Path -LiteralPath $iconFolder) {
    $targets += Get-ChildItem -LiteralPath $iconFolder -Force -File | Where-Object { $_.Name -match '^Actions\.(Fluent|Flat|Outline)\.fvicons\.before-[a-zA-Z0-9-]+$' } | ForEach-Object { $_.FullName }
}
if (Test-Path -LiteralPath (Join-Path $root 'source\obj')) {
    $targets += Get-ChildItem -LiteralPath (Join-Path $root 'source\obj') -Directory -Filter 'build-*' | ForEach-Object { $_.FullName }
}
function Entries([string]$path) {
    $item = Get-Item -LiteralPath $path -Force
    $item
    if ($item.PSIsContainer -and !($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        foreach ($child in Get-ChildItem -LiteralPath $path -Force) { Entries $child.FullName }
    }
}
# Preflight every target before any deletion; nested repositories and links are not followed.
$plan = @()
foreach ($target in $targets) {
    $full = [IO.Path]::GetFullPath($target)
    if (!$full.StartsWith($root + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Cleanup target outside workspace.' }
    $ancestor = [IO.Path]::GetDirectoryName($full)
    while ($ancestor -ne $root) {
        if ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Cleanup target has a linked parent.' }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }
    $relative = $full.Substring($root.Length + 1).Replace('\','/')
    if (@($tracked | Where-Object { $_ -eq $relative -or $_.StartsWith($relative + '/', [StringComparison]::OrdinalIgnoreCase) }).Count) { throw ('Tracked cleanup target: ' + $relative) }
    & git -C $root check-ignore --quiet -- $relative
    if ($LASTEXITCODE -ne 0) { throw ('Target is not an ignored development output: ' + $relative) }
    if (@($running | Where-Object { $_ -eq $full -or $_.StartsWith($full + '\',[StringComparison]::OrdinalIgnoreCase) }).Count) { Write-Output ('Preserving running build: ' + $relative); continue }
    $items = @(Entries $full)
    if (@($items | Where-Object { $_.Name -eq '.git' }).Count) { Write-Output ('Preserving nested repository: ' + $relative); continue }
    $files = @($items | Where-Object { !$_.PSIsContainer -and !($_.Attributes -band [IO.FileAttributes]::ReparsePoint) })
    $bytes = [long](($files | Measure-Object -Property Length -Sum).Sum)
    $plan += [pscustomobject]@{Path=$full;Relative=$relative;Bytes=$bytes;Files=$files.Count}
}
function RemoveOutput([string]$path, [string]$boundary) {
    $full = [IO.Path]::GetFullPath($path)
    if ($full -ne $boundary -and !$full.StartsWith($boundary + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe recursive cleanup path.' }
    $item = Get-Item -LiteralPath $full -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        if ($item.PSIsContainer) { [IO.Directory]::Delete($full) } else { [IO.File]::Delete($full) }
    } elseif ($item.PSIsContainer) {
        foreach ($child in Get-ChildItem -LiteralPath $full -Force) { RemoveOutput $child.FullName $boundary }
        [IO.Directory]::Delete($full)
    } else {
        if ($item.Name -match '(?i)^result.*\.txt$' -and $item.Length -le 262144 -and $full -notmatch '\\(profile|config|data)\\') {
            $saved = Join-Path $record ('history\' + $full.Substring($root.Length + 1))
            New-Item -ItemType Directory -Path (Split-Path -Parent $saved) -Force | Out-Null
            Copy-Item -LiteralPath $full -Destination $saved -Force
        }
        if ($item.IsReadOnly) { $item.IsReadOnly = $false }
        [IO.File]::Delete($full)
    }
}
$removed = @(); $failures = @()
foreach ($entry in $plan) {
    if ($Apply) {
        try { RemoveOutput $entry.Path $entry.Path; $removed += $entry }
        catch { $failures += [pscustomobject]@{Path=$entry.Relative;Error=$_.Exception.Message} }
    }
}
$summary = [pscustomobject]@{Applied=[bool]$Apply;Targets=$plan.Count;PlannedBytes=[long](($plan | Measure-Object Bytes -Sum).Sum);RemovedTargets=$removed.Count;RemovedBytes=[long](($removed | Measure-Object Bytes -Sum).Sum);Failures=$failures;Plan=$plan;Utc=[DateTime]::UtcNow.ToString('o')}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $record ($(if($Apply){'cleanup-applied-'}else{'cleanup-preview-'}) + (Get-Date -Format 'HHmmss') + '.json')) -Encoding UTF8
[pscustomobject]@{Applied=$summary.Applied;Targets=$summary.Targets;PlannedGiB=[Math]::Round($summary.PlannedBytes/1GB,2);RemovedTargets=$summary.RemovedTargets;RemovedGiB=[Math]::Round($summary.RemovedBytes/1GB,2);Failures=$failures.Count} | Format-List
if ($failures.Count) { $failures | Format-Table -AutoSize; throw 'Some outputs could not be removed; see the cleanup report.' }

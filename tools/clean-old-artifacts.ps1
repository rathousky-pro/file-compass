param([switch]$Apply, [string]$GitPath = '')
$ErrorActionPreference = 'Stop'
$workspaceRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
# A normal Windows shell does not necessarily inherit Codex's Git PATH.
if ([string]::IsNullOrWhiteSpace($GitPath)) {
    $command = Get-Command git.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { $GitPath = $command.Source }
    else {
        $candidates = @()
        foreach ($base in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
            if ($base) { $candidates += Join-Path $base 'Git\cmd\git.exe' }
        }
        if ($env:LOCALAPPDATA) { $candidates += Join-Path $env:LOCALAPPDATA 'Programs\Git\cmd\git.exe' }
        if ($env:USERPROFILE) { $candidates += Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe' }
        if ($env:CODEX_PRIMARY_RUNTIME_ROOT) { $candidates += Join-Path $env:CODEX_PRIMARY_RUNTIME_ROOT 'dependencies\native\git\cmd\git.exe' }
        $GitPath = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    }
}
if (!$GitPath -or !(Test-Path -LiteralPath $GitPath -PathType Leaf)) {
    throw 'Git was not found. Supply its full executable path using -GitPath. No files were deleted.'
}
# Deliberately exclude source, tools, bin, local-profile and development notes.
$targets = @('ai-docs\artifacts', 'dist', 'verification')
foreach ($relative in $targets) {
    $target = [IO.Path]::GetFullPath((Join-Path $workspaceRoot $relative))
    if (!$target.StartsWith($workspaceRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe cleanup path.' }
    if (!(Test-Path -LiteralPath $target)) { continue }
    if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Cleanup root must not be a link.' }
    $tracked = @(& $GitPath -C $workspaceRoot ls-files -- $relative.Replace('\','/'))
    if ($LASTEXITCODE -ne 0 -or $tracked.Count) { throw ('Cannot remove tracked or unchecked directory: ' + $relative) }
    Write-Output ($(if ($Apply) { 'Removing ' } else { 'Would remove ' }) + $relative)
    if (!$Apply) { continue }
    # Remove junctions/symlinks themselves before recursive deletion; never follow their targets.
    $links = @(Get-ChildItem -LiteralPath $target -Recurse -Attributes ReparsePoint)
    foreach ($link in $links) {
        $linkPath = [IO.Path]::GetFullPath($link.FullName)
        if (!$linkPath.StartsWith($target + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe link path.' }
        Remove-Item -LiteralPath $linkPath -Force
    }
    Remove-Item -LiteralPath $target -Recurse -Force
}

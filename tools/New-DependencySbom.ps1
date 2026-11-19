param([string]$OutputPath = '', [switch]$Audit)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'source'
$inventory = Get-Content -LiteralPath (Join-Path $source 'dependencies.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$network = Get-Content -LiteralPath (Join-Path $source 'plugins/network/manifest.json') -Raw | ConvertFrom-Json
$components = @()
$queries = @{}
$discPackages = Get-Content -LiteralPath (Join-Path $source 'ui/licenses/DiscUtils/SOURCE.json') -Raw | ConvertFrom-Json
$nativeCpes = @{}
foreach ($entry in $inventory.binaries) {
    $path = Join-Path $source $entry.file
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $entry.sha256) { throw "Unverified dependency: $($entry.file)" }
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($path)
    $version = ($info.ProductVersion -split '\+')[0]
    $component = [ordered]@{type='file'; 'bom-ref'=$entry.file; name=$entry.file; hashes=@(@{alg='SHA-256';content=$hash}); properties=@(@{name='filecompass:component';value=$entry.component})}
    if ($version) { $component.version = $version }
    $components += $component
    if ($entry.file -like 'ui/*.dll' -and $version) {
        $name = [IO.Path]::GetFileNameWithoutExtension($entry.file)
        $provenance = $discPackages | Where-Object { $_.files.name -contains ($name+'.dll') } | Select-Object -First 1
        if ($provenance) { $name=$provenance.package; $version=$provenance.version }
        elseif ($name -eq 'ICSharpCode.AvalonEdit') { $name='AvalonEdit'; $version=($version -split '\.')[0..2] -join '.' }
        elseif ($name -like 'Microsoft.Web.WebView2.*') { $name='Microsoft.Web.WebView2' }
        elseif ($name -like 'UglyToad.PdfPig*') { $name='PdfPig' }
        elseif ($name -eq 'TagLibSharp') { $version=($version -split '\.')[0..2] -join '.' }
        $queries["NuGet:$name@$version"] = @{package=@{ecosystem='NuGet';name=$name};version=$version}
    }
    if ($entry.file -eq 'plugins/cloud/rclone.exe' -and $version) {
        $queries["Go:rclone@$version"] = @{package=@{ecosystem='Go';name='github.com/rclone/rclone'};version=$version}
    }
    if ($entry.file -eq 'plugins/media/Magick.Native-Q8-OpenMP-x64.dll' -and $version) { $nativeCpes['ImageMagick']="cpe:2.3:a:imagemagick:imagemagick:${version}:*:*:*:*:*:*:*" }
    if ($entry.file -eq 'plugins/7zip/7z.exe' -and $version) { $nativeCpes['7-Zip']="cpe:2.3:a:7-zip:7-zip:${version}:*:*:*:*:*:*:*" }
}
foreach ($item in $network.components.PSObject.Properties) {
    $components += [ordered]@{type='library';'bom-ref'="network:$($item.Name)";name=$item.Name;version=[string]$item.Value;properties=@(@{name='filecompass:contained-in';value='plugins/network/FileCompassNetwork.dll'})}
}
$nativeCpes['curl']="cpe:2.3:a:haxx:curl:$($network.components.libcurl):*:*:*:*:*:*:*"
$preview = Get-Content -LiteralPath (Join-Path $source 'preview/manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $preview | Where-Object {$_.File -like '*.js' -or $_.File -like '*.html'}) {
    $path = Join-Path (Join-Path $source 'preview') $entry.File
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $entry.SHA256.ToLowerInvariant()) { throw "Unverified preview asset: $($entry.File)" }
    $components += [ordered]@{type='file';'bom-ref'="preview/$($entry.File)";name="preview/$($entry.File)";hashes=@(@{alg='SHA-256';content=$hash})}
}
foreach ($js in @(@{name='jszip';version='3.10.1'},@{name='docx-preview';version='0.3.7'},@{name='xlsx';version='0.20.3'})) {
    $queries["npm:$($js.name)@$($js.version)"]=@{package=@{ecosystem='npm';name=$js.name};version=$js.version}
    $components += [ordered]@{type='library';'bom-ref'="npm:$($js.name)";name=$js.name;version=$js.version;purl="pkg:npm/$($js.name)@$($js.version)"}
}
$bom = [ordered]@{bomFormat='CycloneDX';specVersion='1.5';serialNumber=('urn:uuid:'+ [guid]::NewGuid());version=1;metadata=@{timestamp=[DateTime]::UtcNow.ToString('o');component=@{type='application';name='File Compass'}};components=$components}
if ($OutputPath) {
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),($bom | ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($false)))
}
Write-Output "SBOM: $($components.Count) components; $($queries.Count) package queries; $($nativeCpes.Count) native CPE queries. Other embedded dependencies still require upstream advisory review."
if ($Audit) {
    $keys = @($queries.Keys | Sort-Object)
    $request = @{queries=@($keys | ForEach-Object {$queries[$_]})} | ConvertTo-Json -Depth 8
    $response = Invoke-RestMethod -Uri 'https://api.osv.dev/v1/querybatch' -Method Post -ContentType 'application/json' -Body $request -TimeoutSec 60
    if ($response.results.Count -ne $keys.Count) { throw 'Incomplete OSV response' }
    $findings = 0
    for ($i=0;$i -lt $keys.Count;$i++) { foreach ($v in $response.results[$i].vulns) { Write-Warning "$($keys[$i]): $($v.id)"; $findings++ } }
    foreach ($native in ($nativeCpes.Keys | Sort-Object)) {
        $encoded = [uri]::EscapeDataString($nativeCpes[$native])
        $result = Invoke-RestMethod -Uri "https://services.nvd.nist.gov/rest/json/cves/2.0?cpeName=$encoded" -TimeoutSec 60
        if ($null -eq $result.totalResults) { throw "Incomplete NVD response for $native" }
        foreach ($item in $result.vulnerabilities) { Write-Warning "${native}: $($item.cve.id)"; $findings++ }
        if ($result.totalResults -gt $result.vulnerabilities.Count) { throw "NVD results need pagination for $native" }
        Start-Sleep -Seconds 6
    }
    Write-Output "OSV findings: $findings (review applicability; no finding is not proof of safety)."
    if ($findings -gt 0) { throw 'Package security advisories require review.' }
}

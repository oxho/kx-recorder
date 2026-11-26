[CmdletBinding()]
param([string]$Iscc = 'ISCC.exe')

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$out = Join-Path $root 'ScreenToGif\bin\Installer'
$payload = Join-Path $out ([guid]::NewGuid().ToString('N'))
$project = Join-Path $root 'ScreenToGif\ScreenToGif.csproj'
& dotnet publish $project -c Release -r win-x64 --self-contained true -o $payload
if ($LASTEXITCODE) { throw 'Windows publish failed.' }

Copy-Item -LiteralPath (Join-Path $root 'LICENSE.txt') -Destination $payload
Get-ChildItem -Path "$root\NOTICE*" -File | Copy-Item -Destination $payload

# Keep the metadata and notices of the packages in the actual published dependency graph.
$assets = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'ScreenToGif\obj\project.assets.json') | ConvertFrom-Json
$deps = Get-Content -Raw -Encoding UTF8 (Join-Path $payload 'ScreenToGif.deps.json') | ConvertFrom-Json
foreach ($library in $deps.libraries.PSObject.Properties) {
    if ($library.Value.type -eq 'project') { continue }
    $key = $library.Name -replace '^runtimepack\.', ''
    $restore = $assets.libraries.PSObject.Properties[$key]
    $relative = if ($restore) { $restore.Value.path } else { $key.ToLowerInvariant() }
    $dir = $assets.packageFolders.PSObject.Properties.Name | ForEach-Object { Join-Path $_ $relative } |
        Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (!$dir) { throw "Missing restored package for license inspection: $key" }
    $dest = Join-Path "$payload\ThirdParty" $key
    foreach ($file in Get-ChildItem -LiteralPath $dir -File -Recurse) {
        if ($file.Name -notmatch '(?i)licen[cs]e|copying|notice|copyright|\.nuspec$') { continue }
        $copy = Join-Path $dest $file.FullName.Substring($dir.Length).TrimStart('\','/')
        New-Item -ItemType Directory -Force -Path (Split-Path $copy) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $copy
    }
}

& $Iscc "/DPayload=$payload" "/O$out" (Join-Path $PSScriptRoot 'KxRecorder.iss')
if ($LASTEXITCODE) { throw 'Installer compilation failed.' }

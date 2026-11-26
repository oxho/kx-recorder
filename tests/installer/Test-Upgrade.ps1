param([Parameter(Mandatory)][string]$Current,[Parameter(Mandatory)][string]$Previous)
$ErrorActionPreference = 'Stop'
if ((Get-FileHash $Previous).Hash -ne 'a656afbb7b6d14b76640ca18218ff26412c27d41c359398f269f6c03332c7431') { throw 'Unexpected upgrade baseline.' }
$dir = Join-Path $env:TEMP ("中文 升级验证 " + [guid]::NewGuid())
$name = 'K-x录动'
$exe = Join-Path $dir 'ScreenToGif.exe'
$shell = New-Object -ComObject Shell.Application
function Run([string]$file,[string]$options) {
    $p = Start-Process $file -ArgumentList $options -Wait -PassThru
    if ($p.ExitCode -notin 0,3010) { throw "Installer returned $($p.ExitCode)" }
}

function Install($file,$desktop,$start) {
    $tasks = @($(if($desktop){'desktopicon'}else{'!desktopicon'}),$(if($start){'startmenu'}else{'!startmenu'})) -join ','
    Run $file "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS=`"$tasks`" /DIR=`"$dir`""
}
function Uninstall { Run "$dir\unins000.exe" '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' }
$startPath = "$name\$name.lnk"

function Check-Link($folders,$relative,$expected) {
    $found = @($folders | ForEach-Object {
        Join-Path ([Environment]::GetFolderPath($_)) $relative
    } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -Unique)
    if (($found.Count -gt 0) -ne $expected) { throw "Unexpected shortcut: $relative (expected $expected)" }
    foreach ($link in $found) {
        $folder = $shell.NameSpace((Split-Path $link))
        $actual = $folder.ParseName((Split-Path $link -Leaf)).GetLink.Path
        $fs = New-Object -ComObject Scripting.FileSystemObject
        if (!$actual -or $fs.GetFile($actual).ShortPath -ne $fs.GetFile($exe).ShortPath) {
            throw "Shortcut $link resolved to '$actual', expected '$exe'."
        }
    }
}
function Check($desktop,$start) {
    Check-Link @('DesktopDirectory','CommonDesktopDirectory') "$name.lnk" $desktop
    Check-Link @('Programs','CommonPrograms') $startPath $start
}
Check $false $false
foreach ($desktop in @($false,$true)) {
    foreach ($start in @($false,$true)) {
        Install $Current $desktop $start
        try { Check $desktop $start } finally { Uninstall }
        Check $false $false
        if (Test-Path -LiteralPath $exe) { throw 'Uninstall left the application behind.' }
    }
}
Install $Previous $false $true
$oldVersion = (Get-Item $exe).VersionInfo.ProductVersion

$settings = Join-Path $env:APPDATA 'ScreenToGif\Settings.xaml'
New-Item -Type Directory -Force (Split-Path $settings) | Out-Null
Set-Content -Encoding UTF8 $settings '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:s="clr-namespace:System;assembly=mscorlib"><s:String x:Key="LanguageCode">zh-CN</s:String></ResourceDictionary>'
$before = (Get-FileHash $settings).Hash
Install $Current $false $true
if ((Get-FileHash $settings).Hash -ne $before) { throw 'User settings changed during upgrade.' }

try {
    $newVersion = (Get-Item $exe).VersionInfo.ProductVersion
    if ($newVersion -eq $oldVersion) { throw "Expected to upgrade from $oldVersion to a newer product version." }
    Check $false $true
} finally { Uninstall }
Check $false $false
Write-Output "Upgrade $oldVersion to $newVersion preserved user settings and shortcut choices."

# Run on a clean Windows test account with no existing K-x录动 installation.
param([Parameter(Mandatory)][string]$Installer)
$ErrorActionPreference = 'Stop'
$installerPath = (Resolve-Path -LiteralPath $Installer).Path
$installDir = Join-Path $env:TEMP ("中文 安装验证\" + [guid]::NewGuid())
$shortcut = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'K-x录动.lnk'
$registration = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{DF940D12-E19B-4CDD-A45D-96ABB94524EE}_is1'
if ((Test-Path $registration) -or (Test-Path $shortcut)) { throw 'Use a clean Windows test account.' }

function Run-Setup([string]$File, [string]$Arguments) {
    $proc = Start-Process -FilePath $File -ArgumentList $Arguments -PassThru -Wait
    if ($proc.ExitCode -ne 0) { throw "Setup returned $($proc.ExitCode)" }
}
foreach ($withShortcut in @($false, $true)) {
    $task = if ($withShortcut) { 'desktopicon' } else { '!desktopicon' }
    Run-Setup $installerPath "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS=$task /DIR=`"$installDir`""
    $exe = Join-Path $installDir 'ScreenToGif.exe'
    try {
        if (!(Test-Path -LiteralPath $exe) -or !(Test-Path -LiteralPath "$installDir\LICENSE.txt")) { throw 'Missing payload.' }
        if ((Test-Path -LiteralPath $shortcut) -ne $withShortcut) { throw 'Shortcut option failed.' }
        if ($withShortcut) {
            Start-Process -FilePath $shortcut
            $app = $null
            $deadline = (Get-Date).AddSeconds(25)
            do {
                Start-Sleep -Milliseconds 300
                $app = Get-Process -Name 'ScreenToGif' -ErrorAction SilentlyContinue |
                    Where-Object { $_.Path -eq $exe } | Select-Object -First 1
            } until ($app -or (Get-Date) -gt $deadline)
            if (!$app) { throw "Shortcut did not start the installed application: $exe" }
        } else {
            $app = Start-Process -FilePath $exe -ArgumentList "-open startup" -WorkingDirectory $installDir -PassThru
        }
        try {
            if (!$app.WaitForInputIdle(30000) -or $app.HasExited) { throw 'WPF startup failed.' }
            $deadline = (Get-Date).AddSeconds(30)
            do {
                Start-Sleep -Milliseconds 250
                $app.Refresh()
            } until ($app.MainWindowTitle -like '*K-x录动*' -or $app.HasExited -or (Get-Date) -ge $deadline)
            if ($app.MainWindowTitle -notlike '*K-x录动*') { throw 'Branded startup window did not appear.' }
        } finally {
            if (!$app.HasExited) { $app.Kill(); $app.WaitForExit() }
        }
    } finally {
        Run-Setup "$installDir\unins000.exe" '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
    }
    if ((Test-Path $exe) -or (Test-Path $shortcut)) { throw 'Uninstall left installed files.' }
}
Write-Output 'Chinese directory, application startup, desktop shortcut and uninstall checks passed.'

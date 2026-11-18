# Устанавливает IP Watch в профиль пользователя и включает автозапуск.
# Запуск: powershell -ExecutionPolicy Bypass -File install.ps1
$ErrorActionPreference = 'Stop'

$repo = 'Mmitekk/IPWatch'
$targetDir = Join-Path $env:LOCALAPPDATA 'Programs\IPWatch'
$target = Join-Path $targetDir 'IPWatch.exe'

Get-Process IPWatch -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

$localExe = if ($PSScriptRoot) {
    Join-Path (Split-Path -Parent $PSScriptRoot) 'dist\IPWatch.exe'
} else {
    ''
}

if ($localExe -and (Test-Path $localExe)) {
    Copy-Item $localExe $target -Force
} else {
    $url = "https://github.com/$repo/releases/latest/download/IPWatch.exe"
    Write-Host "Скачиваю $url"
    Invoke-WebRequest -Uri $url -OutFile $target -UseBasicParsing
}

# автозапуск при входе в Windows
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Set-ItemProperty -Path $runKey -Name 'IPWatch' -Value ('"' + $target + '" --tray')

# ярлык в меню «Пуск»
$startMenu = [Environment]::GetFolderPath('Programs')
$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut((Join-Path $startMenu 'IP Watch.lnk'))
$lnk.TargetPath = $target
$lnk.WorkingDirectory = $targetDir
$lnk.IconLocation = $target
$lnk.Description = 'IP Watch — внешний IP, страна, время, пинг, шлюз'
$lnk.Save()

Start-Process $target -ArgumentList '--tray'

Write-Host "Установлено: $target"
Write-Host 'Автозапуск включён. Приложение свёрнуто в трей.'

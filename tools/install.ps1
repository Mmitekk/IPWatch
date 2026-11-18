# Устанавливает IP Watch в профиль пользователя и включает автозапуск.
# Запуск: powershell -ExecutionPolicy Bypass -File install.ps1
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'dist\IPWatch.exe'
if (-not (Test-Path $exe)) {
    throw "Не найден $exe — сначала выполните publish (см. README)."
}

$targetDir = Join-Path $env:LOCALAPPDATA 'Programs\IPWatch'
$target = Join-Path $targetDir 'IPWatch.exe'

Get-Process IPWatch -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
Copy-Item $exe $target -Force

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
$lnk.Description = 'Внешний IP, страна, время, пинг и шлюз'
$lnk.Save()

Start-Process $target -ArgumentList '--tray'

Write-Host "Установлено: $target"
Write-Host 'Автозапуск включён. Приложение свёрнуто в трей.'

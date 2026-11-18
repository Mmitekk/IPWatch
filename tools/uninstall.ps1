# Полностью удаляет IP Watch: автозапуск, ярлык, файлы.
$ErrorActionPreference = 'SilentlyContinue'

Get-Process IPWatch | Stop-Process -Force
Start-Sleep -Milliseconds 500

Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'IPWatch'
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'IP Watch.lnk') -Force
Remove-Item (Join-Path $env:LOCALAPPDATA 'Programs\IPWatch') -Recurse -Force

Write-Host 'IP Watch удалён.'

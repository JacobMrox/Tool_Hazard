# Game Boost (user-level, no admin) - Tool Hazard
# Run with: powershell -ExecutionPolicy Bypass -File Apply-GameBoost.ps1
# Safe + reversible. Pairs with Apply-GameBoost-Elevated.ps1 for HAGS/power/service tweaks.
Write-Host "=== Game Boost (user) ===" -ForegroundColor Cyan
Write-Host "Backs up current values to %TEMP%\gameboost-backup.txt for manual revert." -ForegroundColor Yellow

$backup = Join-Path $env:TEMP "gameboost-backup.txt"
"Backup taken: $(Get-Date -Format o)" | Out-File $backup
"VisualFXSetting: $((Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects' -ErrorAction SilentlyContinue).VisualFXSetting)" | Out-File $backup -Append
"EnableTransparency: $((Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -ErrorAction SilentlyContinue).EnableTransparency)" | Out-File $backup -Append

# Game Mode ON, DVR OFF
$gdvr = "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR"
New-Item $gdvr -Force | Out-Null
Set-ItemProperty $gdvr -Name GameModeEnabled -Value 1 -Type DWord
Set-ItemProperty $gdvr -Name AppCaptureEnabled -Value 0 -Type DWord
Set-ItemProperty $gdvr -Name HistoricalCaptureEnabled -Value 0 -Type DWord
Set-ItemProperty $gdvr -Name HistoricalCaptureOnBatteryAllowed -Value 0 -Type DWord
Set-ItemProperty $gdvr -Name HistoricalCaptureOnWirelessDisplayAllowed -Value 0 -Type DWord
Set-ItemProperty $gdvr -Name AudioCaptureEnabled -Value 0 -Type DWord

New-Item "HKCU:\Software\Microsoft\GameBar" -Force | Out-Null
Set-ItemProperty "HKCU:\Software\Microsoft\GameBar" -Name AllowAutoGameMode -Value 1 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\GameBar" -Name AutoGameModeEnabled -Value 1 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\GameBar" -Name UseNexusForGameBarEnabled -Value 0 -Type DWord
Write-Host "Game Mode ON, Game DVR backup capture OFF"

# Visual FX best performance + transparency off + animations off
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" -Name VisualFXSetting -Value 3 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" -Name EnableTransparency -Value 0 -Type DWord
Set-ItemProperty "HKCU:\Control Panel\Desktop" -Name MenuShowDelay -Value "0"
New-Item "HKCU:\Control Panel\Desktop\WindowMetrics" -Force | Out-Null
Set-ItemProperty "HKCU:\Control Panel\Desktop\WindowMetrics" -Name MinAnimate -Value "0"
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name TaskbarAnimations -Value 0 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name ListviewAlphaSelect -Value 0 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name ListviewShadow -Value 0 -Type DWord
Write-Host "Visual effects -> Best performance, transparency/animations OFF"

# Mouse raw input (1:1 aim)
Set-ItemProperty "HKCU:\Control Panel\Mouse" -Name MouseSpeed -Value "0"
Set-ItemProperty "HKCU:\Control Panel\Mouse" -Name MouseThreshold1 -Value "0"
Set-ItemProperty "HKCU:\Control Panel\Mouse" -Name MouseThreshold2 -Value "0"
Set-ItemProperty "HKCU:\Control Panel\Mouse" -Name MouseAccelLevel -Value "0" -ErrorAction SilentlyContinue
Write-Host "Mouse pointer precision OFF (raw input)"

# Taskbar bloat off (Search/Widgets/Chat)
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Search" -Name SearchboxTaskbarMode -Value 0 -Type DWord -ErrorAction SilentlyContinue
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name TaskbarDa -Value 0 -Type DWord -ErrorAction SilentlyContinue
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name TaskbarMn -Value 0 -Type DWord -ErrorAction SilentlyContinue
Write-Host "Taskbar Search/Widgets/Chat OFF"

# Background apps off, power throttling off
New-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications" -Force | Out-Null
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications" -Name GlobalUserDisabled -Value 1 -Type DWord
New-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\PowerCfg" -Force | Out-Null
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\PowerCfg" -Name PowerThrottlingOff -Value 1 -Type DWord
Write-Host "Background apps OFF, power throttling OFF"

# Clean %TEMP%
$before = (Get-ChildItem $env:TEMP -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
Get-ChildItem $env:TEMP -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
$after = 0; try { $after = (Get-ChildItem $env:TEMP -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum } catch {}
Write-Host ("Cleaned %TEMP%: freed {0:N0} MB" -f (($before - $after) / 1MB)) -ForegroundColor Green

Write-Host "`nUser boost done! Restart Explorer (taskmgr) or reboot for full effect." -ForegroundColor Green
Write-Host "Next: run Apply-GameBoost-Elevated.ps1 for HAGS + Ultimate Performance + services." -ForegroundColor Yellow
Pause

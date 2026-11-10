# Game Boost (user-level, no admin) - Tool Hazard
# Run: powershell -ExecutionPolicy Bypass -File Apply-GameBoost.ps1
# Safe + reversible. Every value is written to %TEMP%\gameboost-backup.txt first.
#
# Note: user-level tweaks never require elevation. System-wide tweaks live in
# Apply-GameBoost-Elevated.ps1.

Write-Host "=== Game Boost (user) ===" -ForegroundColor Cyan

$backup = Join-Path $env:TEMP "gameboost-backup.txt"
if (Test-Path $backup) { Rename-Item $backup "$backup.old" -Force }
"Game Boost backup - $(Get-Date -Format o)" | Out-File $backup
function Save($path, $name) {
    $value = (Get-ItemProperty $path -Name $name -ErrorAction SilentlyContinue).$name
    "$path\$name = $value" | Out-File $backup -Append
}
Save "HKCU:\System\GameConfigStore" "GameDVR_Enabled"
Save "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR" "AppCaptureEnabled"
Save "HKCU:\Software\Microsoft\GameBar" "AutoGameModeEnabled"
Save "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" "VisualFXSetting"
Save "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" "EnableTransparency"
Save "HKCU:\Control Panel\Mouse" "MouseSpeed"
Save "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications" "GlobalUserDisabled"
Write-Host "Backup written to $backup" -ForegroundColor Yellow

# Game Mode lives under GameBar, not GameDVR.
New-Item "HKCU:\Software\Microsoft\GameBar" -Force | Out-Null
Set-ItemProperty "HKCU:\Software\Microsoft\GameBar" -Name AutoGameModeEnabled -Value 1 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\GameBar" -Name AllowAutoGameMode   -Value 1 -Type DWord

# Game DVR background capture off (Game Mode stays on).
New-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR" -Force | Out-Null
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR" -Name AppCaptureEnabled -Value 0 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR" -Name HistoricalCaptureEnabled -Value 0 -Type DWord
New-Item "HKCU:\System\GameConfigStore" -Force | Out-Null
Set-ItemProperty "HKCU:\System\GameConfigStore" -Name GameDVR_Enabled -Value 0 -Type DWord

# VisualFXSetting: 0 = let Windows choose, 1 = best appearance, 2 = best performance, 3 = custom.
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" -Name VisualFXSetting -Value 2 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" -Name EnableTransparency -Value 0 -Type DWord

# Windowed-game swap-chain upgrade.
$dx = "HKCU:\Software\Microsoft\DirectX\UserGpuPreferences"
New-Item $dx -Force | Out-Null
$current = (Get-ItemProperty $dx -Name DirectXUserGlobalSettings -ErrorAction SilentlyContinue).DirectXUserGlobalSettings
$parts = @($current -split ';' | Where-Object { $_ -and $_ -notmatch '^SwapEffectUpgradeEnable=' })
$parts += 'SwapEffectUpgradeEnable=1'
Set-ItemProperty $dx -Name DirectXUserGlobalSettings -Value (($parts -join ';') + ';')

# Mouse acceleration off (1:1 aim).
Set-ItemProperty "HKCU:\Control Panel\Mouse" -Name MouseSpeed      -Value "0"
Set-ItemProperty "HKCU:\Control Panel\Mouse" -Name MouseThreshold1 -Value "0"
Set-ItemProperty "HKCU:\Control Panel\Mouse" -Name MouseThreshold2 -Value "0"

# Background UWP activity off.
New-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications" -Force | Out-Null
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications" -Name GlobalUserDisabled -Value 1 -Type DWord

Write-Host "User boost applied. Sign out or restart Explorer for visual changes." -ForegroundColor Green
Write-Host "Next: run Apply-GameBoost-Elevated.ps1 for HAGS, power plan and throttling." -ForegroundColor Yellow
Pause

# Self-elevating Game Boost REVERT (admin) - Tool Hazard
# Restores the Windows defaults for the elevated Game Boost tweaks only.

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs -Wait
    exit
}

Write-Host "=== Reverting elevated Game Boost ===" -ForegroundColor Cyan

# 2 = HAGS enabled, which is the Windows 11 default on supported GPUs.
try { Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name HwSchMode -Value 2 -Type DWord } catch { Write-Warning $_ }
try { Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling" -Name PowerThrottlingOff -Value 0 -Type DWord } catch { Write-Warning $_ }
try {
    $mm = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
    Set-ItemProperty $mm -Name NetworkThrottlingIndex -Value 10 -Type DWord
    Set-ItemProperty $mm -Name SystemResponsiveness  -Value 20 -Type DWord
} catch { Write-Warning $_ }

try { sc.exe config DiagTrack start= auto | Out-Host } catch { Write-Warning $_ }
powercfg /SETACTIVE 381b4222-f694-41f0-9685-ff5bb260df2e

Write-Host "Reverted to Windows defaults. Reboot if HAGS was changed." -ForegroundColor Green
Write-Host "User-level changes: use Tool Hazard > Extras > Game Boost > Revert Everything." -ForegroundColor Yellow
Pause

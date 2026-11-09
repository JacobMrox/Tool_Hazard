# Self-elevating Game Boost REVERT (admin) - Tool Hazard
# Restores Windows defaults for the admin-level Game Boost changes.
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Requesting Administrator rights..." -ForegroundColor Yellow
    Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs -Wait
    exit
}

Write-Host "Elevated - reverting admin Game Boost..." -ForegroundColor Cyan

try {
    Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name HwSchMode -Value 2 -Type DWord
    Write-Host "HAGS OFF (HwSchMode=2) - reboot required"
} catch { Write-Warning $_ }

try {
    Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl" -Name Win32PrioritySeparation -Value 2 -Type DWord
    Write-Host "Win32PrioritySeparation=2 (default)"
} catch { Write-Warning $_ }

foreach ($svc in @("SysMain","WSearch","DiagTrack")) {
    try {
        sc.exe config $svc start= auto | Write-Host
        Write-Host "$svc -> Automatic"
    } catch { Write-Warning $_ }
}

# Restore High Performance + PCIe ASPM moderate
powercfg /SETACTIVE 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c
powercfg /SETACVALUEINDEX SCHEME_MIN SUB_PCIEXPRESS ASPM 1
powercfg /SETACTIVE SCHEME_CURRENT
powercfg /GETACTIVESCHEME

Write-Host "`nReverted admin changes. Reboot for HAGS OFF. For user-level revert, run Apply-GameBoost.ps1 in reverse (or use Tool Hazard > Game Boost > Revert)." -ForegroundColor Green
Pause

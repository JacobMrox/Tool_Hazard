# Self-elevating Game Boost (admin) - Tool Hazard
# Run by double-clicking, or: powershell -ExecutionPolicy Bypass -File Apply-GameBoost-Elevated.ps1
#
# HwSchMode: 1 = hardware GPU scheduling DISABLED, 2 = ENABLED. (Older builds of this
# script had these reversed, which disabled HAGS when it claimed to enable it.)

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Requesting Administrator rights..." -ForegroundColor Yellow
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs -Wait
    exit
}

Write-Host "=== Game Boost (elevated) ===" -ForegroundColor Cyan

# 1. Hardware-accelerated GPU scheduling ON (2 = enabled). Needs a reboot.
try {
    Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name HwSchMode -Value 2 -Type DWord
    Write-Host "HAGS ON (HwSchMode=2) - reboot required" -ForegroundColor Green
} catch { Write-Warning $_ }

# 2. Power throttling OFF (machine scope; the old HKCU value was a no-op).
try {
    New-Item "HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling" -Force | Out-Null
    Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling" -Name PowerThrottlingOff -Value 1 -Type DWord
    Write-Host "Power throttling OFF"
} catch { Write-Warning $_ }

# 3. Multimedia network throttling OFF.
try {
    $mm = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
    Set-ItemProperty $mm -Name NetworkThrottlingIndex -Value 0xFFFFFFFF -Type DWord
    Set-ItemProperty $mm -Name SystemResponsiveness  -Value 10 -Type DWord
    Write-Host "Network throttling OFF, system responsiveness 10%"
} catch { Write-Warning $_ }

# 4. High performance power plan.
#    Ultimate Performance is intentionally not used: on a laptop it raises idle clocks
#    and heat for no measurable gain over High performance.
#    NOTE: OEM laptops (Dell, HP, ...) usually keep the built-in scheme registered but
#    HIDDEN, so "powercfg /LIST" does not show it. It can still be activated by GUID,
#    and duplicating a hidden scheme only produces another hidden copy - so try the
#    GUID first and fall back to a copy only if that fails.
try {
    $high = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"
    $activated = $false
    powercfg /SETACTIVE $high
    if ($LASTEXITCODE -eq 0) { $activated = $true }

    if (-not $activated) {
        $copy = powercfg /DUPLICATESCHEME $high
        $copyGuid = ([regex]::Match($copy, 'GUID:\s*([0-9a-fA-F-]{36})')).Groups[1].Value
        if ($copyGuid) {
            powercfg /SETACTIVE $copyGuid
            if ($LASTEXITCODE -eq 0) { $activated = $true }
        }
    }

    if ($activated) { Write-Host "High performance plan active" -ForegroundColor Green }
    else { Write-Warning "Could not activate the High performance plan." }
} catch { Write-Warning $_ }

# 5. Telemetry service disabled (does not affect Windows Update or Defender).
try {
    sc.exe config DiagTrack start= disabled | Out-Host
    Write-Host "DiagTrack -> Disabled"
} catch { Write-Warning $_ }

# Optional (commented): smaller foreground time slice. Marginal on Windows 11 and some
# benchmarks show no difference, so it is not applied by default.
# Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl" -Name Win32PrioritySeparation -Value 38 -Type DWord

Write-Host "`n=== Result ===" -ForegroundColor Cyan
powercfg /GETACTIVESCHEME
Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" | Format-List HwSchMode
Write-Host "`nReboot for HAGS to take effect, then check Settings > System > Display > Graphics > Default graphics settings." -ForegroundColor Green
Pause

# Self-elevating Game Boost (admin) - Tool Hazard
# Run by double-clicking, or: powershell -ExecutionPolicy Bypass -File Apply-GameBoost-Elevated.ps1
# Enables HAGS + Ultimate Performance + PCIe ASPM off + CPU scheduler boost + services to Manual.
# Requires reboot for HAGS. Pair with Apply-GameBoost.ps1 (user-level).
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Requesting Administrator rights..." -ForegroundColor Yellow
    Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs -Wait
    exit
}

Write-Host "Elevated - applying admin Game Boost..." -ForegroundColor Cyan

# 1. HAGS (Hardware GPU Scheduling) - GTX 1650 Ti supports it. Needs reboot.
try {
    Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name HwSchMode -Value 1 -Type DWord
    Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name PlatformSupportMiracast -Value 1 -Type DWord
    Write-Host "HAGS ON (HwSchMode=1) - reboot required" -ForegroundColor Green
    Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" | Format-List HwSchMode, PlatformSupportMiracast
} catch { Write-Warning $_ }

# 2. CPU scheduler foreground boost (short quantum for the focused app)
try {
    New-Item "HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl" -Force | Out-Null
    Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl" -Name Win32PrioritySeparation -Value 38 -Type DWord
    Write-Host "Win32PrioritySeparation=38 (foreground boost)"
} catch { Write-Warning $_ }

# 3. Ultimate Performance power plan
try {
    $ultimate = "e9a42b02-d5df-448d-aa00-03f14749eb61"
    if (-not (powercfg /LIST | Select-String $ultimate)) {
        Write-Host "Duplicating Ultimate Performance plan..."
        powercfg /DUPLICATESCHEME $ultimate
    }
    powercfg /SETACTIVE $ultimate
    # Disable PCIe ASPM on AC for lowest latency (NVMe + GPU link)
    powercfg /SETACVALUEINDEX SCHEME_MIN SUB_PCIEXPRESS ASPM 0
    powercfg /SETACTIVE SCHEME_CURRENT
    Write-Host "Ultimate Performance active, PCIe ASPM OFF on AC" -ForegroundColor Green
    powercfg /GETACTIVESCHEME
} catch { Write-Warning $_ }

# 4. Services -> Manual (Start on demand, not Disabled - safe for gaming)
foreach ($svc in @("SysMain","WSearch","DiagTrack")) {
    try {
        sc.exe config $svc start= demand | Write-Host
        Write-Host "$svc -> Manual" -ForegroundColor Green
    } catch { Write-Warning $_ }
}
Get-Service SysMain, WSearch, DiagTrack -ErrorAction SilentlyContinue | Select-Object Name, Status, StartType | Format-Table -AutoSize

# 5. Telemetry policy (best-effort on Home edition)
try {
    New-Item "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection" -Force | Out-Null
    Set-ItemProperty "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection" -Name AllowTelemetry -Value 0 -Type DWord
    Set-ItemProperty "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection" -Name DoNotShowFeedbackNotifications -Value 1 -Type DWord
    Write-Host "Telemetry policy set (AllowTelemetry=0)"
} catch { Write-Warning $_ }

# 6. Delivery Optimization off (stops background Windows Update sharing / throttling)
try {
    New-Item "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization" -Force | Out-Null
    Set-ItemProperty "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization" -Name DODownloadMode -Value 0 -Type DWord
    Write-Host "Delivery Optimization OFF"
} catch { Write-Warning $_ }

Write-Host "`n=== Done ===" -ForegroundColor Cyan
Write-Host "Reboot now for HAGS to take effect, then verify in Settings > System > Display > Graphics > Default graphics settings > Hardware-accelerated GPU scheduling = ON." -ForegroundColor Green
Pause

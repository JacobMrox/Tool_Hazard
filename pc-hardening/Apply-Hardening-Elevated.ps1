# Self-elevating hardening script — run this file (double-click works too)
# It will ask for Administrator approval once via UAC.

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Requesting Administrator rights..." -ForegroundColor Yellow
    Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs -Wait
    exit
}

Write-Host "Elevated — applying hardening..." -ForegroundColor Cyan

# --- 1. Defender hardening ---
Set-MpPreference -EnableNetworkProtection 1 -CloudBlockLevel 2 -MAPSReporting 2 -SubmitSamplesConsent 1 -DisableRemovableDriveScanning $false -PUAProtection 1 -DisableRestorePoint $false
Set-MpPreference -EnableControlledFolderAccess 2  # Audit mode — safe, logs blocks
Write-Host "Defender: NetworkProtection=1, Cloud=High, ControlledFolder=Audit, RemovableScan=ON" -ForegroundColor Green

# --- 2. SMB hardening ---
try { Set-SmbServerConfiguration -EncryptData $true -Force; Set-SmbServerConfiguration -AnnounceServer $false -Force; Write-Host "SMB: EncryptData ON" -ForegroundColor Green } catch { Write-Warning $_ }

# --- 3. Password policy ---
try {
    net accounts /minpwlen:12 /lockoutthresh:5 /lockoutduration:15 /lockoutwindow:15
    Write-Host "Policy: minlen 12, lockout 5" -ForegroundColor Green
} catch { Write-Warning $_ }

# --- 4. Defender signatures ---
try { Update-MpSignature -UpdateSource MicrosoftUpdateServer; Write-Host "Signatures: updated" -ForegroundColor Green } catch { Write-Warning $_ }

# --- 5. Disable obsolete MSN Messenger firewall rules (Any profile) ---
try {
    Get-NetFirewallRule -DisplayName "*Windows Live Messenger*" | ForEach-Object { Disable-NetFirewallRule -Name $_.Name; Write-Host "Disabled firewall: $($_.DisplayName)" -ForegroundColor Green }
    Write-Host "Note: qBittorrent/Zoom/emulators left as-is (set them to Private only in Firewall GUI if you want). Skipped to avoid breaking your setup." -ForegroundColor Yellow
} catch { Write-Warning $_ }

Write-Host "`n=== RESULT ===" -ForegroundColor Cyan
Get-MpPreference | Select-Object EnableNetworkProtection, CloudBlockLevel, EnableControlledFolderAccess, DisableRemovableDriveScanning, PUAProtection, DisableRestorePoint | Format-List
Get-MpComputerStatus | Select-Object AntivirusEnabled, RealTimeProtectionEnabled, IsTamperProtected, AntivirusSignatureLastUpdated | Format-List
Get-SmbServerConfiguration | Select-Object EnableSMB1Protocol, EncryptData | Format-List
net accounts

Write-Host "`nDone! Reboot not required. Controlled Folder Access is in AUDIT for 7 days — check Protection History in Windows Security, then switch to Block if no false positives." -ForegroundColor Green
Pause

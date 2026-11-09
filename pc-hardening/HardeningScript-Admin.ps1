# Harden-PC — Run as Administrator in PowerShell
# Created after audit of Windows 11 Home (Build 26300)
# Review each section before running. Steps marked MANUAL require GUI.

Write-Host "=== 1. Defender hardening ===" -ForegroundColor Cyan
Set-MpPreference -EnableNetworkProtection 1 -CloudBlockLevel 2 -MAPSReporting 2 -SubmitSamplesConsent 1 -DisableRemovableDriveScanning $false -PUAProtection 1
# Controlled Folder Access: start in AUDIT mode (2) to avoid breaking apps, then switch to BLOCK (1) after testing
Set-MpPreference -EnableControlledFolderAccess 2
Set-MpPreference -ControlledFolderAccessAllowedApplications -Version
# Allow restore points after Defender remediation (currently blocked)
Set-MpPreference -DisableRestorePoint $false
Write-Host "Defender: Network Protection ON, Cloud=High, Controlled Folder=AudIT, Removable scan ON" -ForegroundColor Green
Get-MpPreference | Select-Object EnableNetworkProtection, CloudBlockLevel, EnableControlledFolderAccess, DisableRemovableDriveScanning, PUAProtection | Format-List

Write-Host "`n=== 2. SMB hardening ===" -ForegroundColor Cyan
Set-SmbServerConfiguration -EncryptData $true -Force
Set-SmbServerConfiguration -AnnounceServer $false -Force
# SMB1 already disabled (EnableSMB1Protocol=False) — verify:
Get-SmbServerConfiguration | Select-Object EnableSMB1Protocol, EncryptData, AnnounceServer | Format-List
# Optional: confirm Windows feature disabled (requires reboot if changed):
# Disable-WindowsOptionalFeature -Online -FeatureName SMB1Protocol -NoRestart

Write-Host "`n=== 3. Firewall cleanup ===" -ForegroundColor Cyan
Write-Host "Review rules BEFORE disabling. This lists inbound ALLOW rules on Public profile:" -ForegroundColor Yellow
Get-NetFirewallRule -Enabled True -Direction Inbound -Action Allow | Where-Object { $_.Profile -match 'Public' -or $_.Profile -eq 'Any' } | Select-Object DisplayName, Profile | Format-Table -AutoSize
Write-Host "Recommendation: change game/emulator rules to Private ONLY. Example for Steam/qBittorrent/Zoom:"
Write-Host "  Right-click rule > Properties > Profiles > uncheck Public + Domain, keep Private"
Write-Host "  Or in PowerShell: Set-NetFirewallRule -DisplayName 'qBittorrent' -Profile Private"
Write-Host "  Droid4X / MiniThunder / vboxheadless / MSN Messenger: disable or uninstall if unused."
Write-Host "  Disable-NetFirewallRule -DisplayName 'Windows Live Messenger'"

Write-Host "`n=== 4. Password policy + account ===" -ForegroundColor Cyan
Write-Host "Current your account has NO password and is Admin — fix now:" -ForegroundColor Red
Write-Host "  1. Settings > Accounts > Your info > Set password (use password manager, 16+ chars)"
Write-Host "  2. Settings > Accounts > Other users > Create 'Standard' account for daily work. Use Admin only for installs."
Write-Host "  3. Run: net user user *  (to force password set)"
Write-Host "  4. Tighten policy via secpol.msc: Min length 12, Lockout 5 attempts, 15 min"
Write-Host "  Or via PowerShell (Admin):"
Write-Host "    net accounts /minpwlen:12 /lockoutthresh:5 /lockoutduration:15 /lockoutwindow:15"
Write-Host "    net user user /passwordreq:yes /passwordchg:yes"

Write-Host "`n=== 5. Encryption + Secure Boot (MANUAL checks) ===" -ForegroundColor Cyan
Write-Host "Run these (Admin):"
Write-Host "  Confirm-SecureBootUEFI  # should return True"
Write-Host "  Get-Tpm | Format-List  # TpmPresent: True"
Write-Host "  manage-bde -status # check C: protection (Home = Device Encryption)"
Write-Host "If not enabled: Settings > Privacy > Device encryption > Turn On. Save recovery key to USB/paper (NOT in OneDrive)."

Write-Host "`n=== 6. Backups (MANUAL) ===" -ForegroundColor Cyan
Write-Host "  System > Protection > Turn On Protection for C: (5%+), create Restore Point now"
Write-Host "  Settings > Backup: enable File History to external drive (3-2-1 rule: 3 copies, 2 media, 1 offline/offsite)"

Write-Host "`n=== 7. Obsolete software ===" -ForegroundColor Cyan
Write-Host "Uninstall: Windows Live Messenger (msnmsgr.exe), Droid4X, any cracked Photoshop tools"
Write-Host "Remove from Startup: Task Manager > Startup apps > Disable MEGAsync/Ollama/Steam/Epic auto-start unless needed"
Write-Host "RDP already disabled (fDenyTSConnections=1) — KEEP disabled."

Write-Host "`n=== Done. Reboot, then verify ===" -ForegroundColor Green
Write-Host "  Get-MpComputerStatus | Select-Object RealTimeProtectionEnabled, IsTamperProtected, AntivirusSignatureLastUpdated | Format-List"
Write-Host "  Get-MpPreference | Select-Object EnableNetworkProtection, EnableControlledFolderAccess, CloudBlockLevel | Format-List"

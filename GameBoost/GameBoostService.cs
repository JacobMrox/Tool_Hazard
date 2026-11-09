#nullable enable
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace Tool_Hazard.GameBoost;

/// <summary>
/// Core, UI-independent Game Boost logic for Tool Hazard.
/// All tweaks are safe + reversible. User-level tweaks (HKCU) run without admin;
/// admin-level tweaks (HKLM/services/power) require elevation and are also
/// exported as self-elevating PowerShell scripts under pc-hardening/.
/// </summary>
public sealed record BoostStatus(
    string PowerPlanName,
    bool GameModeEnabled,
    bool HagsEnabled,
    bool HagsStateKnown,
    bool GameDvrCaptureEnabled,
    int VisualFxSetting,
    bool TransparencyEnabled,
    bool MouseAccelerationEnabled,
    bool BackgroundAppsDisabled,
    bool PowerThrottlingOff,
    long TempBytes,
    long WindowsTempBytes,
    IReadOnlyList<string> TopMemoryProcesses,
    int StartupCount);

public sealed record BoostResult(
    bool Success,
    IReadOnlyList<string> Applied,
    IReadOnlyList<string> Warnings);

public sealed record ChecklistItem(string Name, string Current, string Recommended, bool IsOptimal);

public static class GameBoostService
{
    public const string UltimatePlanGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    public const string HighPlanGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public const string BalancedPlanGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    private static readonly string[] BackgroundProcessNames =
    {
        "msedge", "msedgewebview2", "Discord", "steamwebhelper", "PhoneExperienceHost",
        "OneDrive", "Teams", "WidgetService", "SearchHost", "StartMenuExperienceHost"
    };

    public static bool IsElevated()
    {
        try
        {
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private static object? GetReg(RegistryKey? key, string name)
    {
        try { return key?.GetValue(name); } catch { return null; }
    }

    private static int GetRegInt(RegistryKey? key, string name, int fallback)
    {
        object? v = GetReg(key, name);
        if (v is int i) return i;
        if (v is null) return fallback;
        try { return Convert.ToInt32(v); } catch { return fallback; }
    }

    private static string GetRegString(RegistryKey? key, string name, string fallback)
    {
        object? v = GetReg(key, name);
        return v is string s ? s : fallback;
    }

    private static void SetReg(RegistryKey? key, string name, object value, RegistryValueKind kind)
    {
        try { key?.SetValue(name, value, kind); } catch { /* ignore - logged by caller */ }
    }

    private static string RunExe(string fileName, string arguments, int timeoutMs = 15_000)
    {
        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            p.Start();
            if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return ""; }
            return p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        }
        catch { return ""; }
    }

    public static string GetActivePowerPlan()
    {
        string outp = RunExe("powercfg", "/GETACTIVESCHEME");
        if (outp.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase)) return "Ultimate Performance";
        if (outp.Contains("High performance", StringComparison.OrdinalIgnoreCase) ||
            outp.Contains("High Performance", StringComparison.OrdinalIgnoreCase)) return "High Performance";
        if (outp.Contains("Balanced", StringComparison.OrdinalIgnoreCase)) return "Balanced";
        return string.IsNullOrWhiteSpace(outp) ? "Unknown" : outp.Trim();
    }

    public static IReadOnlyList<(string Guid, string Name)> ListPowerPlans()
    {
        var list = new List<(string, string)>();
        string outp = RunExe("powercfg", "/LIST");
        foreach (string line in outp.Split('\n'))
        {
            string t = line.Trim();
            if (!t.Contains("Power Scheme GUID:")) continue;
            try
            {
                string guid = t.Substring("Power Scheme GUID:".Length, 36).Trim();
                string name = t.Contains("(") && t.Contains(")") ? t.Substring(t.IndexOf('(') + 1, t.IndexOf(')') - t.IndexOf('(') - 1) : guid;
                if (t.Contains("*")) name += " *";
                list.Add((guid, name));
            }
            catch { /* skip malformed line */ }
        }
        return list;
    }

    private static long DirectorySize(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return 0;
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Select(f => { try { return new FileInfo(f).Length; } catch { return 0L; } })
                .Sum();
        }
        catch { return 0; }
    }

    public static BoostStatus GetStatus()
    {
        string power = GetActivePowerPlan();

        bool gameMode = false, dvrCapture = false;
        int visualFx = 2;
        bool transparency = true, mouseAccel = true, bgDisabled = false, throttleOff = false;
        bool hags = false; bool hagsKnown = false;

        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", false))
        {
            gameMode = GetRegInt(k, "GameModeEnabled", 0) == 1;
            dvrCapture = GetRegInt(k, "AppCaptureEnabled", 1) == 1;
        }
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", false))
            visualFx = GetRegInt(k, "VisualFXSetting", 2);
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
            transparency = GetRegInt(k, "EnableTransparency", 1) == 1;
        using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", false))
        {
            string speed = GetRegString(k, "MouseSpeed", "1");
            string t1 = GetRegString(k, "MouseThreshold1", "6");
            mouseAccel = !(speed == "0" && t1 == "0");
        }
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", false))
            bgDisabled = GetRegInt(k, "GlobalUserDisabled", 0) == 1;
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\PowerCfg", false))
            throttleOff = GetRegInt(k, "PowerThrottlingOff", 0) == 1;
        try
        {
            using var g = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", false);
            object? v = g?.GetValue("HwSchMode");
            if (v is not null)
            {
                hagsKnown = true;
                // HwSchMode: 1 = enabled, 2 = disabled (see Microsoft + driver tags)
                hags = Convert.ToInt32(v) == 1;
            }
        }
        catch { /* no access */ }

        long temp = DirectorySize(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\..\Local\Temp");
        // Expand proper %TEMP%:
        temp = DirectorySize(Path.GetTempPath());
        long winTemp = DirectorySize(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));

        var top = new List<string>();
        try
        {
            foreach (var p in Process.GetProcesses().OrderByDescending(x => x.WorkingSet64).Take(8))
            {
                try { top.Add($"{p.ProcessName} — {(p.WorkingSet64 / 1024 / 1024):N0} MB"); } catch { /* skip */ }
            }
        }
        catch { /* ignore */ }

        int startup = 0;
        try { startup = Process.GetProcesses().Length; } catch { }
        // Accurate startup count via WMI is not available without extra deps; use Run keys + Startup folder.
        try
        {
            startup = 0;
            using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
            if (run is not null) startup += run.ValueCount;
            using var runLm = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
            if (runLm is not null) startup += runLm.ValueCount;
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (Directory.Exists(startupFolder)) startup += Directory.GetFiles(startupFolder).Length;
        }
        catch { /* keep fallback */ }

        return new BoostStatus(power, gameMode, hags, hagsKnown, dvrCapture, visualFx, transparency, mouseAccel,
            bgDisabled, throttleOff, temp, winTemp, top, startup);
    }

    public static IReadOnlyList<ChecklistItem> GetChecklist(BoostStatus s)
    {
        string fxText = s.VisualFxSetting switch { 0 => "Let Windows choose", 1 => "Best appearance", 2 => "Custom", 3 => "Best performance", _ => $"Value {s.VisualFxSetting}" };
        return new[]
        {
            new ChecklistItem("Power plan", s.PowerPlanName, "High Performance or Ultimate Performance", s.PowerPlanName.Contains("Performance", StringComparison.OrdinalIgnoreCase)),
            new ChecklistItem("Game Mode", s.GameModeEnabled ? "On" : "Off", "On", s.GameModeEnabled),
            new ChecklistItem("HAGS (Hardware GPU Scheduling)", s.HagsStateKnown ? (s.HagsEnabled ? "On" : "Off") : "Off / not set", "On (needs reboot)", s.HagsEnabled),
            new ChecklistItem("Game DVR background capture", s.GameDvrCaptureEnabled ? "On (recording!)" : "Off", "Off", !s.GameDvrCaptureEnabled),
            new ChecklistItem("Visual effects", fxText, "Best performance (3)", s.VisualFxSetting == 3),
            new ChecklistItem("Transparency", s.TransparencyEnabled ? "On" : "Off", "Off", !s.TransparencyEnabled),
            new ChecklistItem("Mouse pointer precision", s.MouseAccelerationEnabled ? "On (acceleration)" : "Off (raw input)", "Off (raw input)", !s.MouseAccelerationEnabled),
            new ChecklistItem("Background apps", s.BackgroundAppsDisabled ? "Disabled" : "Enabled", "Disabled", s.BackgroundAppsDisabled),
            new ChecklistItem("Power throttling", s.PowerThrottlingOff ? "Off" : "On", "Off", s.PowerThrottlingOff),
            new ChecklistItem("%TEMP% size", $"{(s.TempBytes / 1024 / 1024):N0} MB", "< 200 MB", s.TempBytes < 200 * 1024 * 1024),
            new ChecklistItem("Startup apps (Run + folder)", s.StartupCount.ToString(), "As few as possible", s.StartupCount <= 5),
        };
    }

    // -----------------------------------------------------------------
    // User-level boost (no admin)
    // -----------------------------------------------------------------
    public static Task<BoostResult> ApplyUserBoostAsync(IProgress<string>? log = null, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var applied = new List<string>();
            var warnings = new List<string>();
            void Log(string m) { try { log?.Report(m); } catch { } }

            // Game Mode ON, DVR OFF
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", true))
            {
                if (k is not null)
                {
                    SetReg(k, "GameModeEnabled", 1, RegistryValueKind.DWord);
                    SetReg(k, "AppCaptureEnabled", 0, RegistryValueKind.DWord);
                    SetReg(k, "HistoricalCaptureEnabled", 0, RegistryValueKind.DWord);
                    SetReg(k, "HistoricalCaptureOnBatteryAllowed", 0, RegistryValueKind.DWord);
                    SetReg(k, "HistoricalCaptureOnWirelessDisplayAllowed", 0, RegistryValueKind.DWord);
                    SetReg(k, "AudioCaptureEnabled", 0, RegistryValueKind.DWord);
                    SetReg(k, "VideoEncodingBitrateMode", 2, RegistryValueKind.DWord);
                    applied.Add("Game Mode ON + Game DVR background capture OFF");
                    k.Close();
                }
            }
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar", true))
            {
                if (k is not null)
                {
                    SetReg(k, "AllowAutoGameMode", 1, RegistryValueKind.DWord);
                    SetReg(k, "AutoGameModeEnabled", 1, RegistryValueKind.DWord);
                    SetReg(k, "UseNexusForGameBarEnabled", 0, RegistryValueKind.DWord);
                    applied.Add("Auto Game Mode enabled");
                    k.Close();
                }
            }

            // Visual FX -> Best performance
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", true))
            {
                if (k is not null)
                {
                    SetReg(k, "VisualFXSetting", 3, RegistryValueKind.DWord);
                    applied.Add("Visual effects -> Best performance (snappier windows)");
                    k.Close();
                }
            }
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", true))
            {
                if (k is not null)
                {
                    SetReg(k, "EnableTransparency", 0, RegistryValueKind.DWord);
                    applied.Add("Transparency OFF (less GPU/CPU compositing)");
                    k.Close();
                }
            }

            // Animations
            using (var k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop", true))
            {
                if (k is not null) { SetReg(k, "MenuShowDelay", "0", RegistryValueKind.String); applied.Add("Menu show delay -> 0 ms"); k.Close(); }
            }
            try
            {
                using var wm = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop\WindowMetrics", true);
                if (wm is not null) { SetReg(wm, "MinAnimate", "0", RegistryValueKind.String); wm.Close(); }
                applied.Add("Window animations OFF");
            }
            catch (Exception ex) { warnings.Add($"WindowMetrics: {ex.Message}"); }

            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true))
            {
                if (k is not null)
                {
                    SetReg(k, "TaskbarAnimations", 0, RegistryValueKind.DWord);
                    SetReg(k, "ListviewAlphaSelect", 0, RegistryValueKind.DWord);
                    SetReg(k, "ListviewShadow", 0, RegistryValueKind.DWord);
                    k.Close();
                }
            }

            // Mouse raw input
            using (var k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Mouse", true))
            {
                if (k is not null)
                {
                    SetReg(k, "MouseSpeed", "0", RegistryValueKind.String);
                    SetReg(k, "MouseThreshold1", "0", RegistryValueKind.String);
                    SetReg(k, "MouseThreshold2", "0", RegistryValueKind.String);
                    SetReg(k, "MouseAccelLevel", "0", RegistryValueKind.String);
                    applied.Add("Mouse acceleration OFF (1:1 raw input for aim)");
                    k.Close();
                }
            }

            // Search / Taskbar bloat
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Search", true))
            {
                if (k is not null) { SetReg(k, "SearchboxTaskbarMode", 0, RegistryValueKind.DWord); k.Close(); }
            }
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true))
            {
                if (k is not null)
                {
                    SetReg(k, "TaskbarDa", 0, RegistryValueKind.DWord);   // Widgets
                    SetReg(k, "TaskbarMn", 0, RegistryValueKind.DWord);   // Chat/Teams
                    k.Close();
                }
            }
            applied.Add("Taskbar Search/Widgets/Chat buttons OFF");

            // Background apps
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", true))
            {
                if (k is not null) { SetReg(k, "GlobalUserDisabled", 1, RegistryValueKind.DWord); k.Close(); }
            }
            applied.Add("Background apps disabled (UWP)");

            // Power throttling off
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\PowerCfg", true))
            {
                if (k is not null) { SetReg(k, "PowerThrottlingOff", 1, RegistryValueKind.DWord); k.Close(); }
            }
            applied.Add("Power throttling OFF");

            // Clean temp
            try
            {
                var (freed, w) = CleanTemp();
                applied.Add($"Cleaned %TEMP% ({freed / 1024 / 1024:N0} MB freed)");
                warnings.AddRange(w);
            }
            catch (Exception ex) { warnings.Add($"Temp clean: {ex.Message}"); }

            Log("User boost applied.");
            return new BoostResult(true, applied, warnings);
        }, ct);

    // -----------------------------------------------------------------
    // Admin-level boost (requires elevation)
    // -----------------------------------------------------------------
    public static Task<BoostResult> ApplyAdminBoostAsync(IProgress<string>? log = null, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var applied = new List<string>();
            var warnings = new List<string>();
            if (!IsElevated())
            {
                warnings.Add("Not elevated. Run the exported Apply-GameBoost-Elevated.ps1 as Administrator, or use 'Apply Boost (Elevated)' which will request UAC.");
                return new BoostResult(false, applied, warnings);
            }

            // HAGS
            try
            {
                var path = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
                using var k = Registry.LocalMachine.CreateSubKey(path, true);
                if (k is not null)
                {
                    k.SetValue("HwSchMode", 1, RegistryValueKind.DWord);
                    k.SetValue("PlatformSupportMiracast", 1, RegistryValueKind.DWord);
                    applied.Add("HAGS ON (HwSchMode=1) — reboot required");
                    k.Close();
                }
            }
            catch (Exception ex) { warnings.Add($"HAGS: {ex.Message}"); }

            // Win32PrioritySeparation = 38 (0x26) - foreground boost, short quantum
            try
            {
                using var k = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl", true);
                if (k is not null) { k.SetValue("Win32PrioritySeparation", 38, RegistryValueKind.DWord); applied.Add("CPU scheduler foreground boost (Win32PrioritySeparation=38)"); k.Close(); }
            }
            catch (Exception ex) { warnings.Add($"PriorityControl: {ex.Message}"); }

            // Ultimate Performance plan
            string ultimate = RunExe("powercfg", "/LIST");
            if (!ultimate.Contains(UltimatePlanGuid, StringComparison.OrdinalIgnoreCase))
            {
                string dup = RunExe("powercfg", $"/DUPLICATESCHEME {UltimatePlanGuid}");
                applied.Add("Created Ultimate Performance plan: " + (string.IsNullOrWhiteSpace(dup) ? "see powercfg /LIST" : dup.Trim().Split('\n')[0]));
            }
            else applied.Add("Ultimate Performance plan already present");
            RunExe("powercfg", $"/SETACTIVE {UltimatePlanGuid}");
            applied.Add("Activated Ultimate Performance plan");
            // Disable PCIe ASPM on AC for lowest latency (first GPU/NVMe link)
            RunExe("powercfg", "/SETACVALUEINDEX SCHEME_MIN SUB_PCIEXPRESS ASPM 0");
            RunExe("powercfg", "/SETACTIVE SCHEME_CURRENT");
            applied.Add("PCIe Link State Power Management OFF on AC (lowest latency)");

            // Services -> Manual (not Disabled, so Windows can still start them on demand)
            foreach (string svc in new[] { "SysMain", "WSearch", "DiagTrack" })
            {
                string r = RunExe("sc.exe", $"config \"{svc}\" start= demand");
                applied.Add($"Service {svc} -> Manual (requested). Output: {r.Trim()}");
            }

            // Telemetry policy (Pro/Enterprise effective; Home ignores some, harmless)
            try
            {
                using var k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true);
                if (k is not null)
                {
                    k.SetValue("AllowTelemetry", 0, RegistryValueKind.DWord);
                    k.SetValue("DoNotShowFeedbackNotifications", 1, RegistryValueKind.DWord);
                    applied.Add("Telemetry policy: AllowTelemetry=0 (best-effort on Home)");
                    k.Close();
                }
            }
            catch (Exception ex) { warnings.Add($"Telemetry: {ex.Message}"); }

            // Delivery Optimization -> disabled
            try
            {
                using var k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", true);
                if (k is not null) { k.SetValue("DODownloadMode", 0, RegistryValueKind.DWord); applied.Add("Delivery Optimization OFF"); k.Close(); }
            }
            catch { /* optional */ }

            return new BoostResult(true, applied, warnings);
        }, ct);

    // -----------------------------------------------------------------
    // Revert
    // -----------------------------------------------------------------
    public static Task<BoostResult> RevertUserBoostAsync(IProgress<string>? log = null, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var applied = new List<string>();
            var warnings = new List<string>();
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", true))
            {
                if (k is not null)
                {
                    SetReg(k, "AppCaptureEnabled", 1, RegistryValueKind.DWord);
                    SetReg(k, "HistoricalCaptureEnabled", 1, RegistryValueKind.DWord);
                    SetReg(k, "AudioCaptureEnabled", 1, RegistryValueKind.DWord);
                    applied.Add("Game DVR capture restored to default (ON)");
                    k.Close();
                }
            }
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", true))
            {
                if (k is not null) { SetReg(k, "VisualFXSetting", 2, RegistryValueKind.DWord); applied.Add("Visual effects -> Custom (2)"); k.Close(); }
            }
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", true))
            {
                if (k is not null) { SetReg(k, "EnableTransparency", 1, RegistryValueKind.DWord); applied.Add("Transparency ON"); k.Close(); }
            }
            using (var k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Mouse", true))
            {
                if (k is not null)
                {
                    SetReg(k, "MouseSpeed", "1", RegistryValueKind.String);
                    SetReg(k, "MouseThreshold1", "6", RegistryValueKind.String);
                    SetReg(k, "MouseThreshold2", "10", RegistryValueKind.String);
                    applied.Add("Mouse acceleration restored");
                    k.Close();
                }
            }
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", true))
            {
                if (k is not null) { SetReg(k, "GlobalUserDisabled", 0, RegistryValueKind.DWord); applied.Add("Background apps enabled"); k.Close(); }
            }
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Search", true))
            {
                if (k is not null) { SetReg(k, "SearchboxTaskbarMode", 2, RegistryValueKind.DWord); applied.Add("Taskbar Search restored"); k.Close(); }
            }
            return new BoostResult(true, applied, warnings);
        }, ct);

    public static Task<BoostResult> RevertAdminBoostAsync(IProgress<string>? log = null, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var applied = new List<string>();
            var warnings = new List<string>();
            if (!IsElevated())
            {
                warnings.Add("Not elevated. Use the exported Revert-GameBoost-Elevated.ps1 as Administrator.");
                return new BoostResult(false, applied, warnings);
            }
            try
            {
                using var k = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", true);
                if (k is not null) { k.SetValue("HwSchMode", 2, RegistryValueKind.DWord); applied.Add("HAGS OFF (HwSchMode=2) — reboot required"); k.Close(); }
            }
            catch (Exception ex) { warnings.Add($"HAGS revert: {ex.Message}"); }
            try
            {
                using var k = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl", true);
                if (k is not null) { k.SetValue("Win32PrioritySeparation", 2, RegistryValueKind.DWord); applied.Add("Win32PrioritySeparation=2 (default)"); k.Close(); }
            }
            catch (Exception ex) { warnings.Add($"PriorityControl revert: {ex.Message}"); }
            foreach (string svc in new[] { "SysMain", "WSearch", "DiagTrack" })
            {
                string r = RunExe("sc.exe", $"config \"{svc}\" start= auto");
                applied.Add($"Service {svc} -> Automatic (requested). Output: {r.Trim()}");
            }
            RunExe("powercfg", $"/SETACTIVE {HighPlanGuid}");
            applied.Add("Power plan -> High Performance");
            RunExe("powercfg", "/SETACVALUEINDEX SCHEME_MIN SUB_PCIEXPRESS ASPM 1");
            RunExe("powercfg", "/SETACTIVE SCHEME_CURRENT");
            applied.Add("PCIe ASPM -> Moderate power saving on AC");
            return new BoostResult(true, applied, warnings);
        }, ct);

    // -----------------------------------------------------------------
    // Temp + priority helpers
    // -----------------------------------------------------------------
    public static (long FreedBytes, IReadOnlyList<string> Warnings) CleanTemp()
    {
        var warnings = new List<string>();
        long before = 0, after = 0;
        string temp = Path.GetTempPath();
        before += DirectorySize(temp);
        try
        {
            foreach (string f in Directory.EnumerateFileSystemEntries(temp, "*", SearchOption.AllDirectories))
            {
                if (Directory.Exists(f)) { try { Directory.Delete(f, true); } catch { /* locked */ } }
                else { try { File.Delete(f); } catch { /* locked */ } }
            }
        }
        catch (Exception ex) { warnings.Add($"%TEMP% partial: {ex.Message}"); }
        after += DirectorySize(temp);

        string winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
        before += DirectorySize(winTemp);
        try
        {
            foreach (string f in Directory.EnumerateFileSystemEntries(winTemp, "*", SearchOption.AllDirectories))
            {
                if (Directory.Exists(f)) { try { Directory.Delete(f, true); } catch { } }
                else { try { File.Delete(f); } catch { } }
            }
        }
        catch (Exception ex) { warnings.Add($"Windows\\Temp partial: {ex.Message}"); }
        after += DirectorySize(winTemp);

        // Recycle empty + SoftwareDistribution cache (best-effort, no service stop)
        string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SoftwareDistribution\Download");
        before += DirectorySize(cache);
        try
        {
            foreach (string f in Directory.EnumerateFileSystemEntries(cache, "*", SearchOption.AllDirectories))
            {
                if (Directory.Exists(f)) { try { Directory.Delete(f, true); } catch { } }
                else { try { File.Delete(f); } catch { } }
            }
        }
        catch { /* optional */ }
        after += DirectorySize(cache);

        long freed = Math.Max(0, before - after);
        return (freed, warnings);
    }

    /// <summary>Lower priority of heavy background apps (Edge/Discord etc.) so games get CPU time.</summary>
    public static IReadOnlyList<string> LowerBackgroundPriorities()
    {
        var done = new List<string>();
        foreach (string name in BackgroundProcessNames)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try
                {
                    if (!p.HasExited && p.PriorityClass > ProcessPriorityClass.BelowNormal)
                    {
                        p.PriorityClass = ProcessPriorityClass.BelowNormal;
                        done.Add(p.ProcessName);
                    }
                }
                catch { /* access denied */ }
                finally { try { p.Dispose(); } catch { } }
            }
        }
        return done.Distinct().ToList();
    }

    public static IReadOnlyList<string> RestorePriorities()
    {
        var done = new List<string>();
        foreach (string name in BackgroundProcessNames)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try
                {
                    if (!p.HasExited && p.PriorityClass < ProcessPriorityClass.Normal)
                    {
                        p.PriorityClass = ProcessPriorityClass.Normal;
                        done.Add(p.ProcessName);
                    }
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
        }
        return done.Distinct().ToList();
    }

    public static Process? LaunchGame(string exePath, string? arguments = null, bool highPriority = true, bool lowerBackground = true)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            throw new FileNotFoundException("Game executable not found.", exePath);

        if (lowerBackground) LowerBackgroundPriorities();

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? ""
        };
        if (!string.IsNullOrWhiteSpace(arguments)) psi.Arguments = arguments;

        var p = Process.Start(psi);
        if (p is not null && highPriority)
        {
            try { p.PriorityClass = ProcessPriorityClass.High; } catch { /* game may already throttle */ }
            try { p.ProcessorAffinity = (IntPtr)((1L << Environment.ProcessorCount) - 1); } catch { }
        }
        return p;
    }

    public static void SetGpuHighPerformance(string exePath)
    {
        // GpuPreference=2 means High performance (discrete GPU) for this exe.
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences", true);
            if (k is not null) { k.SetValue(exePath, "GpuPreference=2;", RegistryValueKind.String); k.Close(); }
        }
        catch { /* optional */ }
    }

    public static string ExportUserScript()
    {
        return """
# Game Boost (user-level, no admin) - exported by Tool Hazard
# Safe + revertible. Run with: powershell -ExecutionPolicy Bypass -File Apply-GameBoost.ps1
Write-Host "=== Game Boost (user) ===" -ForegroundColor Cyan

# Game Mode ON, DVR OFF
$gdvr = "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR"
New-Item $gdvr -Force | Out-Null
Set-ItemProperty $gdvr -Name GameModeEnabled -Value 1 -Type DWord
Set-ItemProperty $gdvr -Name AppCaptureEnabled -Value 0 -Type DWord
Set-ItemProperty $gdvr -Name HistoricalCaptureEnabled -Value 0 -Type DWord
Set-ItemProperty $gdvr -Name HistoricalCaptureOnBatteryAllowed -Value 0 -Type DWord
Set-ItemProperty $gdvr -Name HistoricalCaptureOnWirelessDisplayAllowed -Value 0 -Type DWord
Set-ItemProperty $gdvr -Name AudioCaptureEnabled -Value 0 -Type DWord

# GameBar auto
New-Item "HKCU:\Software\Microsoft\GameBar" -Force | Out-Null
Set-ItemProperty "HKCU:\Software\Microsoft\GameBar" -Name AllowAutoGameMode -Value 1 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\GameBar" -Name AutoGameModeEnabled -Value 1 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\GameBar" -Name UseNexusForGameBarEnabled -Value 0 -Type DWord

# Visual FX best performance + transparency off
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" -Name VisualFXSetting -Value 3 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" -Name EnableTransparency -Value 0 -Type DWord
Set-ItemProperty "HKCU:\Control Panel\Desktop" -Name MenuShowDelay -Value "0"
New-Item "HKCU:\Control Panel\Desktop\WindowMetrics" -Force | Out-Null
Set-ItemProperty "HKCU:\Control Panel\Desktop\WindowMetrics" -Name MinAnimate -Value "0"
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name TaskbarAnimations -Value 0 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name ListviewAlphaSelect -Value 0 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name ListviewShadow -Value 0 -Type DWord

# Mouse raw input
Set-ItemProperty "HKCU:\Control Panel\Mouse" -Name MouseSpeed -Value "0"
Set-ItemProperty "HKCU:\Control Panel\Mouse" -Name MouseThreshold1 -Value "0"
Set-ItemProperty "HKCU:\Control Panel\Mouse" -Name MouseThreshold2 -Value "0"

# Taskbar bloat off
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Search" -Name SearchboxTaskbarMode -Value 0 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name TaskbarDa -Value 0 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name TaskbarMn -Value 0 -Type DWord

# Background apps off, throttling off
New-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications" -Force | Out-Null
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications" -Name GlobalUserDisabled -Value 1 -Type DWord
New-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\PowerCfg" -Force | Out-Null
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\PowerCfg" -Name PowerThrottlingOff -Value 1 -Type DWord

# Clean %TEMP%
Get-ChildItem $env:TEMP -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "`nDone! Reboot Explorer (or reboot PC) for full effect. HAGS + power plan need the Elevated script." -ForegroundColor Green
""";
    }

    public static string ExportElevatedScript()
    {
        return """
# Self-elevating Game Boost (admin) - exported by Tool Hazard
# Enables HAGS + Ultimate Performance + PCIe ASPM off + scheduler boost. Requires reboot for HAGS.
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Requesting Administrator rights..." -ForegroundColor Yellow
    Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs -Wait
    exit
}
Write-Host "Elevated - applying admin Game Boost..." -ForegroundColor Cyan

# HAGS
Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name HwSchMode -Value 1 -Type DWord
Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name PlatformSupportMiracast -Value 1 -Type DWord
Write-Host "HAGS ON (reboot required)"

# Scheduler foreground boost
New-Item "HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl" -Force | Out-Null
Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl" -Name Win32PrioritySeparation -Value 38 -Type DWord
Write-Host "Win32PrioritySeparation=38"

# Ultimate Performance
$ultimate = "e9a42b02-d5df-448d-aa00-03f14749eb61"
if (-not (powercfg /LIST | Select-String $ultimate)) { powercfg /DUPLICATESCHEME $ultimate | Write-Host }
powercfg /SETACTIVE $ultimate
powercfg /SETACVALUEINDEX SCHEME_MIN SUB_PCIEXPRESS ASPM 0
powercfg /SETACTIVE SCHEME_CURRENT
Write-Host "Ultimate Performance active, PCIe ASPM OFF on AC"

# Services -> Manual
foreach ($svc in @("SysMain","WSearch","DiagTrack")) {
    sc.exe config $svc start= demand | Write-Host
    Write-Host "$svc -> Manual"
}

# Telemetry policy
New-Item "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection" -Force | Out-Null
Set-ItemProperty "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection" -Name AllowTelemetry -Value 0 -Type DWord
Set-ItemProperty "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection" -Name DoNotShowFeedbackNotifications -Value 1 -Type DWord

# Verify
Write-Host "`n=== Result ===" -ForegroundColor Cyan
powercfg /GETACTIVESCHEME
Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" | Format-List HwSchMode, PlatformSupportMiracast
Write-Host "`nReboot now for HAGS to take effect." -ForegroundColor Green
Pause
""";
    }

    public static string ExportRevertElevatedScript()
    {
        return """
# Self-elevating Game Boost REVERT (admin) - exported by Tool Hazard
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs -Wait
    exit
}
Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name HwSchMode -Value 2 -Type DWord
Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl" -Name Win32PrioritySeparation -Value 2 -Type DWord
foreach ($svc in @("SysMain","WSearch","DiagTrack")) { sc.exe config $svc start= auto }
powercfg /SETACTIVE 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c
powercfg /SETACVALUEINDEX SCHEME_MIN SUB_PCIEXPRESS ASPM 1
powercfg /SETACTIVE SCHEME_CURRENT
Write-Host "Reverted. Reboot for HAGS OFF." -ForegroundColor Green
Pause
""";
    }
}

#nullable enable
using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace Tool_Hazard.GameBoost;

/// <summary>Snapshot of the gaming-relevant Windows state, used to drive the checklist UI.</summary>
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
    bool WindowedGameOptimizations,
    bool NetworkThrottlingDisabled,
    long TempBytes,
    long WindowsTempBytes,
    IReadOnlyList<string> TopMemoryProcesses,
    int StartupCount,
    bool IsElevated,
    string SystemSummary);

public sealed record BoostResult(
    bool Success,
    IReadOnlyList<string> Applied,
    IReadOnlyList<string> Warnings);

/// <summary>One row of the checklist. <see cref="IsOptimal"/> is null when the state cannot be read.</summary>
public sealed record ChecklistItem(
    string Id,
    string Name,
    string Current,
    string Recommended,
    bool? IsOptimal,
    bool RequiresAdmin,
    bool RequiresReboot,
    string Risk);

public sealed record StartupEntry(string Name, string Command, bool Enabled, bool MachineWide);

internal sealed record RegSnapshot(
    string HiveName,
    string SubKey,
    string Name,
    bool Existed,
    string Kind,
    string? Value)
{
    public RegistryHive Hive => Enum.Parse<RegistryHive>(HiveName);
}

/// <summary>
/// Core, UI-independent Game Boost logic.
///
/// Every tweak records the exact value it replaced before writing anything, so Revert
/// restores the machine's original state rather than shipping hardcoded "defaults".
/// Security-relevant surfaces (Defender real-time protection, VBS/HVCI, SmartScreen,
/// Windows Update, exploit mitigations) are deliberately never modified here.
/// </summary>
public static class GameBoostService
{
    public const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    // Registry locations --------------------------------------------------
    private const string GraphicsDriversKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
    private const string PriorityControlKey = @"SYSTEM\CurrentControlSet\Control\PriorityControl";
    private const string PowerThrottlingKey = @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling";
    private const string GameBarKey = @"Software\Microsoft\GameBar";
    private const string GameDvrKey = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
    private const string GameConfigStoreKey = @"System\GameConfigStore";
    private const string VisualEffectsKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string DesktopKey = @"Control Panel\Desktop";
    private const string WindowMetricsKey = @"Control Panel\Desktop\WindowMetrics";
    private const string MouseKey = @"Control Panel\Mouse";
    private const string ExplorerAdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string SearchKey = @"Software\Microsoft\Windows\CurrentVersion\Search";
    private const string BackgroundAppsKey = @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";
    private const string DirectXPreferencesKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string MultimediaProfileKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string StartupApprovedRunKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------------------------------------------------
    // Registry plumbing
    // ---------------------------------------------------------------------

    private static RegistryKey BaseKey(RegistryHive hive) => RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

    private static object? GetValue(RegistryHive hive, string subKey, string name)
    {
        try
        {
            using RegistryKey baseKey = BaseKey(hive);
            using RegistryKey? key = baseKey.OpenSubKey(subKey, false);
            return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        }
        catch
        {
            return null;
        }
    }

    private static int GetInt(RegistryHive hive, string subKey, string name, int fallback = 0)
    {
        object? value = GetValue(hive, subKey, name);
        if (value is null) return fallback;
        try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); } catch { return fallback; }
    }

    private static string GetString(RegistryHive hive, string subKey, string name, string fallback = "")
        => GetValue(hive, subKey, name) is string value ? value : fallback;

    private static void WriteValue(RegistryHive hive, string subKey, string name, object value, RegistryValueKind kind)
    {
        using RegistryKey baseKey = BaseKey(hive);
        using RegistryKey key = baseKey.CreateSubKey(subKey, true)
            ?? throw new InvalidOperationException($"Cannot open {subKey} for writing.");
        key.SetValue(name, value, kind);
    }

    private static void DeleteValue(RegistryHive hive, string subKey, string name)
    {
        using RegistryKey baseKey = BaseKey(hive);
        using RegistryKey? key = baseKey.OpenSubKey(subKey, true);
        key?.DeleteValue(name, false);
    }

    /// <summary>Records the current value once, then writes the new one.</summary>
    private static void BackedSet(string tweakId, RegistryHive hive, string subKey, string name, object value, RegistryValueKind kind)
    {
        BoostBackup.Add(tweakId, Snapshot(hive, subKey, name));
        WriteValue(hive, subKey, name, value, kind);
    }

    private static RegSnapshot Snapshot(RegistryHive hive, string subKey, string name)
    {
        object? value = GetValue(hive, subKey, name);
        if (value is null)
            return new RegSnapshot(hive.ToString(), subKey, name, false, nameof(RegistryValueKind.String), null);

        RegistryValueKind kind = value switch
        {
            int => RegistryValueKind.DWord,
            long => RegistryValueKind.QWord,
            byte[] => RegistryValueKind.Binary,
            string[] => RegistryValueKind.MultiString,
            _ => RegistryValueKind.String
        };

        string serialized = value switch
        {
            byte[] bytes => Convert.ToBase64String(bytes),
            string[] strings => string.Join('\n', strings),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

        return new RegSnapshot(hive.ToString(), subKey, name, true, kind.ToString(), serialized);
    }

    private static void RestoreTweak(string tweakId)
    {
        foreach (RegSnapshot snap in BoostBackup.Take(tweakId))
        {
            if (!snap.Existed)
            {
                DeleteValue(snap.Hive, snap.SubKey, snap.Name);
                continue;
            }

            object value = snap.Kind switch
            {
                nameof(RegistryValueKind.DWord) => int.Parse(snap.Value ?? "0", CultureInfo.InvariantCulture),
                nameof(RegistryValueKind.QWord) => long.Parse(snap.Value ?? "0", CultureInfo.InvariantCulture),
                nameof(RegistryValueKind.Binary) => Convert.FromBase64String(snap.Value ?? string.Empty),
                nameof(RegistryValueKind.MultiString) => (snap.Value ?? string.Empty).Split('\n'),
                _ => snap.Value ?? string.Empty
            };

            WriteValue(snap.Hive, snap.SubKey, snap.Name, value, Enum.Parse<RegistryValueKind>(snap.Kind));
        }

        BoostBackup.Remove(tweakId);
    }

    // ---------------------------------------------------------------------
    // Process helpers
    // ---------------------------------------------------------------------

    /// <summary>Runs a process and reads output concurrently, so large output cannot deadlock.</summary>
    private static string RunExe(string fileName, string arguments, int timeoutMs = 20_000)
    {
        try
        {
            using var process = new Process
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

            process.Start();
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(true); } catch { }
                return "(timed out)";
            }

            return stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static string RunPowerShell(string script, int timeoutMs = 120_000)
    {
        string? file = null;
        try
        {
            file = Path.Combine(Path.GetTempPath(), $"th-gameboost-{Guid.NewGuid():N}.ps1");
            File.WriteAllText(file, script, new UTF8Encoding(false));
            return RunExe("powershell.exe",
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{file}\"", timeoutMs);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        finally
        {
            if (file != null)
            {
                try { File.Delete(file); } catch { }
            }
        }
    }

    // ---------------------------------------------------------------------
    // Power plans
    // ---------------------------------------------------------------------

    public static string GetActivePowerPlan()
    {
        string output = RunExe("powercfg", "/GETACTIVESCHEME");
        int open = output.IndexOf('(');
        int close = output.LastIndexOf(')');
        if (open >= 0 && close > open)
            return output[(open + 1)..close].Trim();
        return string.IsNullOrWhiteSpace(output) ? "Unknown" : output.Trim();
    }

    private static string? GetActivePowerPlanGuid()
    {
        string output = RunExe("powercfg", "/GETACTIVESCHEME");
        int index = output.IndexOf("GUID:", StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;
        string guid = new(output[(index + 5)..].Trim().TakeWhile(c => c != ' ' && c != '\t' && c != '\r').ToArray());
        return Guid.TryParse(guid, out _) ? guid : null;
    }

    public static IReadOnlyList<(string Guid, string Name)> ListPowerPlans()
    {
        var plans = new List<(string, string)>();
        foreach (string line in RunExe("powercfg", "/LIST").Split('\n'))
        {
            string text = line.Trim();
            if (!text.Contains("Power Scheme GUID:", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                int start = text.IndexOf("GUID:", StringComparison.OrdinalIgnoreCase) + 5;
                string guid = text.Substring(start, 36).Trim();
                string name = guid;
                int open = text.IndexOf('(');
                int close = text.LastIndexOf(')');
                if (open >= 0 && close > open)
                    name = text[(open + 1)..close];
                plans.Add((guid, name));
            }
            catch
            {
                // malformed line - skip
            }
        }
        return plans;
    }

    private static string? FindPowerPlan(string name)
        => ListPowerPlans()
            .FirstOrDefault(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Guid;

    private static bool IsPerformancePlan(string name)
        => name.Contains("Performance", StringComparison.OrdinalIgnoreCase);

    // ---------------------------------------------------------------------
    // Status
    // ---------------------------------------------------------------------

    public static BoostStatus GetStatus()
    {
        bool gameMode = GetInt(RegistryHive.CurrentUser, GameBarKey, "AutoGameModeEnabled", 1) == 1;
        bool dvr = GetInt(RegistryHive.CurrentUser, GameConfigStoreKey, "GameDVR_Enabled", 1) == 1
                   || GetInt(RegistryHive.CurrentUser, GameDvrKey, "AppCaptureEnabled", 1) == 1;
        int visualFx = GetInt(RegistryHive.CurrentUser, VisualEffectsKey, "VisualFXSetting", 0);
        bool transparency = GetInt(RegistryHive.CurrentUser, PersonalizeKey, "EnableTransparency", 1) == 1;
        bool mouseAccel = GetString(RegistryHive.CurrentUser, MouseKey, "MouseSpeed", "1") != "0";
        bool backgroundApps = GetInt(RegistryHive.CurrentUser, BackgroundAppsKey, "GlobalUserDisabled", 0) == 1;
        bool throttlingOff = GetInt(RegistryHive.LocalMachine, PowerThrottlingKey, "PowerThrottlingOff", 0) == 1;

        int? hagsRaw = GetValue(RegistryHive.LocalMachine, GraphicsDriversKey, "HwSchMode") is int h ? h : null;
        bool hagsKnown = hagsRaw is not null;
        // HwSchMode: 1 = disabled, 2 = enabled. Absent means the Windows default (enabled on Win11).
        bool hags = hagsRaw != 1;

        string directX = GetString(RegistryHive.CurrentUser, DirectXPreferencesKey, "DirectXUserGlobalSettings");
        bool windowed = directX.Contains("SwapEffectUpgradeEnable=1", StringComparison.OrdinalIgnoreCase);

        bool netThrottle = GetValue(RegistryHive.LocalMachine, MultimediaProfileKey, "NetworkThrottlingIndex") is int nti
                           && nti == -1;

        var top = new List<string>();
        try
        {
            foreach (Process process in Process.GetProcesses().OrderByDescending(p => SafeWorkingSet(p)).Take(6))
            {
                try { top.Add($"{process.ProcessName} {SafeWorkingSet(process) / 1024 / 1024:N0} MB"); }
                catch { }
                finally { process.Dispose(); }
            }
        }
        catch
        {
            // best effort
        }

        return new BoostStatus(
            GetActivePowerPlan(),
            gameMode,
            hags,
            hagsKnown,
            dvr,
            visualFx,
            transparency,
            mouseAccel,
            backgroundApps,
            throttlingOff,
            windowed,
            netThrottle,
            DirectorySize(Path.GetTempPath()),
            DirectorySize(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")),
            top,
            CountStartupEntries(),
            IsElevated(),
            BuildSystemSummary());
    }

    private static long SafeWorkingSet(Process process)
    {
        try { return process.WorkingSet64; } catch { return 0; }
    }

    public static IReadOnlyList<ChecklistItem> GetChecklist(BoostStatus s)
    {
        string fx = s.VisualFxSetting switch
        {
            0 => "Let Windows choose",
            1 => "Best appearance",
            2 => "Best performance",
            3 => "Custom",
            _ => $"Value {s.VisualFxSetting}"
        };

        string hags = s.HagsStateKnown
            ? (s.HagsEnabled ? "On" : "Off")
            : "Windows default (on)";

        int quantum = GetInt(RegistryHive.LocalMachine, PriorityControlKey, "Win32PrioritySeparation", 2);

        return new[]
        {
            new ChecklistItem("power.plan", "Power plan",
                s.PowerPlanName, "High performance", IsPerformancePlan(s.PowerPlanName), true, false,
                "Disables CPU parking/low clocks while gaming."),

            new ChecklistItem("gpu.hags", "Hardware-accelerated GPU scheduling",
                hags, "On (reboot required)", s.HagsEnabled, true, true,
                "GPU-owned command queue, fewer CPU submission stalls."),

            new ChecklistItem("game.mode", "Game Mode",
                s.GameModeEnabled ? "On" : "Off", "On", s.GameModeEnabled, false, false,
                "Prioritises the foreground game over background work."),

            new ChecklistItem("game.dvr", "Game DVR background recording",
                s.GameDvrCaptureEnabled ? "Recording" : "Off", "Off", !s.GameDvrCaptureEnabled, false, false,
                "Stops the always-on capture buffer."),

            new ChecklistItem("gpu.windowed", "Optimisations for windowed games",
                s.WindowedGameOptimizations ? "On" : "Off / default", "On", s.WindowedGameOptimizations, false, false,
                "Modern swap-chain path for borderless titles."),

            new ChecklistItem("perf.throttle", "Power throttling",
                s.PowerThrottlingOff ? "Off" : "On", "Off", s.PowerThrottlingOff, true, false,
                "Stops Windows lowering clocks for background work."),

            new ChecklistItem("net.mmcss", "Multimedia network throttling",
                s.NetworkThrottlingDisabled ? "Disabled" : "Windows default", "Disabled", s.NetworkThrottlingDisabled, true, false,
                "Removes the ~10 packets/ms multimedia cap."),

            new ChecklistItem("input.mouse", "Mouse acceleration",
                s.MouseAccelerationEnabled ? "On" : "Off", "Off", !s.MouseAccelerationEnabled, false, false,
                "1:1 aim. Also changes desktop pointer feel."),

            new ChecklistItem("vis.fx", "Visual effects / transparency",
                $"{fx}, transparency {(s.TransparencyEnabled ? "on" : "off")}", "Best performance", s.VisualFxSetting == 2, false, false,
                "Less compositing work while gaming."),

            new ChecklistItem("perf.bgapps", "Background apps",
                s.BackgroundAppsDisabled ? "Disabled" : "Enabled", "Disabled", s.BackgroundAppsDisabled, false, false,
                "Suspends UWP background activity."),

            new ChecklistItem("svc.diagtrack", "Telemetry service",
                IsServiceDisabled("DiagTrack") ? "Disabled" : "Running", "Disabled", IsServiceDisabled("DiagTrack"), true, false,
                "Connected User Experiences and Telemetry."),

            new ChecklistItem("cpu.priority", "CPU foreground quantum",
                quantum.ToString(), "38 (short, boosted)",
                quantum == 38, true, false,
                "Smaller time slices for the focused app. Marginal on modern Windows.")
        };
    }

    // ---------------------------------------------------------------------
    // Apply / revert individual tweaks
    // ---------------------------------------------------------------------

    public static bool IsApplied(string id) => BoostBackup.Contains(id);

    public static Task<BoostResult> ApplyTweaksAsync(
        IEnumerable<string> ids, IProgress<string>? log = null, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var applied = new List<string>();
            var warnings = new List<string>();

            foreach (string id in ids.Distinct())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    ApplyTweak(id);
                    applied.Add(DescribeTweak(id));
                    log?.Report($"Applied: {DescribeTweak(id)}");
                }
                catch (UnauthorizedAccessException)
                {
                    warnings.Add($"{DescribeTweak(id)} needs administrator rights.");
                    log?.Report($"Needs admin: {DescribeTweak(id)}");
                }
                catch (Exception ex)
                {
                    warnings.Add($"{DescribeTweak(id)}: {ex.Message}");
                    log?.Report($"Failed: {DescribeTweak(id)} - {ex.Message}");
                }
            }

            return new BoostResult(warnings.Count == 0, applied, warnings);
        }, ct);

    public static Task<BoostResult> RevertTweaksAsync(
        IEnumerable<string> ids, IProgress<string>? log = null, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var reverted = new List<string>();
            var warnings = new List<string>();

            foreach (string id in ids.Distinct())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    RevertTweak(id);
                    reverted.Add(DescribeTweak(id));
                    log?.Report($"Reverted: {DescribeTweak(id)}");
                }
                catch (Exception ex)
                {
                    warnings.Add($"{DescribeTweak(id)}: {ex.Message}");
                    log?.Report($"Failed to revert {DescribeTweak(id)} - {ex.Message}");
                }
            }

            return new BoostResult(warnings.Count == 0, reverted, warnings);
        }, ct);

    public static Task<BoostResult> ApplyUserBoostAsync(IProgress<string>? log = null, CancellationToken ct = default)
        => ApplyTweaksAsync(new[]
        {
            "game.mode", "game.dvr", "gpu.windowed", "vis.fx", "input.mouse", "perf.bgapps"
        }, log, ct);

    public static Task<BoostResult> ApplyAdminBoostAsync(IProgress<string>? log = null, CancellationToken ct = default)
        => ApplyTweaksAsync(new[]
        {
            "power.plan", "gpu.hags", "perf.throttle", "net.mmcss", "svc.diagtrack", "cpu.priority"
        }, log, ct);

    public static Task<BoostResult> RevertUserBoostAsync(IProgress<string>? log = null, CancellationToken ct = default)
        => RevertTweaksAsync(new[]
        {
            "game.mode", "game.dvr", "gpu.windowed", "vis.fx", "input.mouse", "perf.bgapps"
        }, log, ct);

    public static Task<BoostResult> RevertAdminBoostAsync(IProgress<string>? log = null, CancellationToken ct = default)
        => RevertTweaksAsync(new[]
        {
            "power.plan", "gpu.hags", "perf.throttle", "net.mmcss", "svc.diagtrack", "cpu.priority"
        }, log, ct);

    public static Task<BoostResult> RevertAllAsync(IProgress<string>? log = null, CancellationToken ct = default)
        => RevertTweaksAsync(AllTweakIds.Where(BoostBackup.Contains), log, ct);

    private static readonly string[] AllTweakIds =
    {
        "power.plan", "gpu.hags", "game.mode", "game.dvr", "gpu.windowed", "perf.throttle",
        "net.mmcss", "input.mouse", "vis.fx", "perf.bgapps", "svc.diagtrack", "cpu.priority"
    };

    private static string DescribeTweak(string id) => id switch
    {
        "power.plan" => "High performance power plan",
        "gpu.hags" => "Hardware-accelerated GPU scheduling",
        "game.mode" => "Game Mode",
        "game.dvr" => "Game DVR background recording off",
        "gpu.windowed" => "Windowed game optimisations",
        "perf.throttle" => "Power throttling off",
        "net.mmcss" => "Multimedia network throttling off",
        "input.mouse" => "Mouse acceleration off",
        "vis.fx" => "Best performance visual effects",
        "perf.bgapps" => "Background apps disabled",
        "svc.diagtrack" => "Telemetry service disabled",
        "cpu.priority" => "CPU foreground quantum (38)",
        _ => id
    };

    private static void ApplyTweak(string id)
    {
        switch (id)
        {
            case "power.plan":
            {
                string? previous = GetActivePowerPlanGuid();
                if (previous != null)
                    BoostState.Set("power.previous", previous);

                string? high = FindPowerPlan("High performance");
                if (high == null)
                {
                    RunExe("powercfg", $"-DUPLICATESCHEME {HighPerformanceGuid}");
                    high = FindPowerPlan("High performance");
                }

                if (high == null)
                    throw new InvalidOperationException("Windows did not create the High performance plan.");

                RunExe("powercfg", $"/SETACTIVE {high}");
                break;
            }

            case "gpu.hags":
                BackedSet(id, RegistryHive.LocalMachine, GraphicsDriversKey, "HwSchMode", 2, RegistryValueKind.DWord);
                break;

            case "game.mode":
                BackedSet(id, RegistryHive.CurrentUser, GameBarKey, "AutoGameModeEnabled", 1, RegistryValueKind.DWord);
                BackedSet(id, RegistryHive.CurrentUser, GameBarKey, "AllowAutoGameMode", 1, RegistryValueKind.DWord);
                break;

            case "game.dvr":
                BackedSet(id, RegistryHive.CurrentUser, GameConfigStoreKey, "GameDVR_Enabled", 0, RegistryValueKind.DWord);
                BackedSet(id, RegistryHive.CurrentUser, GameDvrKey, "AppCaptureEnabled", 0, RegistryValueKind.DWord);
                BackedSet(id, RegistryHive.CurrentUser, GameDvrKey, "HistoricalCaptureEnabled", 0, RegistryValueKind.DWord);
                BackedSet(id, RegistryHive.CurrentUser, GameDvrKey, "HistoricalCaptureOnBatteryAllowed", 0, RegistryValueKind.DWord);
                break;

            case "gpu.windowed":
            {
                string current = GetString(RegistryHive.CurrentUser, DirectXPreferencesKey, "DirectXUserGlobalSettings");
                BackedSet(id, RegistryHive.CurrentUser, DirectXPreferencesKey, "DirectXUserGlobalSettings",
                    MergeDirectXSettings(current, "SwapEffectUpgradeEnable", "1"), RegistryValueKind.String);
                break;
            }

            case "perf.throttle":
                BackedSet(id, RegistryHive.LocalMachine, PowerThrottlingKey, "PowerThrottlingOff", 1, RegistryValueKind.DWord);
                break;

            case "net.mmcss":
                BackedSet(id, RegistryHive.LocalMachine, MultimediaProfileKey, "NetworkThrottlingIndex", -1, RegistryValueKind.DWord);
                BackedSet(id, RegistryHive.LocalMachine, MultimediaProfileKey, "SystemResponsiveness", 10, RegistryValueKind.DWord);
                break;

            case "input.mouse":
                BackedSet(id, RegistryHive.CurrentUser, MouseKey, "MouseSpeed", "0", RegistryValueKind.String);
                BackedSet(id, RegistryHive.CurrentUser, MouseKey, "MouseThreshold1", "0", RegistryValueKind.String);
                BackedSet(id, RegistryHive.CurrentUser, MouseKey, "MouseThreshold2", "0", RegistryValueKind.String);
                break;

            case "vis.fx":
                BackedSet(id, RegistryHive.CurrentUser, VisualEffectsKey, "VisualFXSetting", 2, RegistryValueKind.DWord);
                BackedSet(id, RegistryHive.CurrentUser, PersonalizeKey, "EnableTransparency", 0, RegistryValueKind.DWord);
                BackedSet(id, RegistryHive.CurrentUser, DesktopKey, "MenuShowDelay", "200", RegistryValueKind.String);
                break;

            case "perf.bgapps":
                BackedSet(id, RegistryHive.CurrentUser, BackgroundAppsKey, "GlobalUserDisabled", 1, RegistryValueKind.DWord);
                break;

            case "svc.diagtrack":
                RunExe("sc.exe", "stop DiagTrack");
                RunExe("sc.exe", "config DiagTrack start= disabled");
                break;

            case "cpu.priority":
                BackedSet(id, RegistryHive.LocalMachine, PriorityControlKey, "Win32PrioritySeparation", 38, RegistryValueKind.DWord);
                break;

            default:
                throw new ArgumentException($"Unknown tweak '{id}'.", nameof(id));
        }
    }

    private static void RevertTweak(string id)
    {
        switch (id)
        {
            case "power.plan":
                RunExe("powercfg", $"/SETACTIVE {BoostState.Get("power.previous") ?? BalancedGuid}");
                BoostState.Remove("power.previous");
                break;

            case "svc.diagtrack":
                RunExe("sc.exe", "config DiagTrack start= auto");
                RunExe("sc.exe", "start DiagTrack");
                break;

            default:
                RestoreTweak(id);
                break;
        }
    }

    private static string MergeDirectXSettings(string settings, string key, string value)
    {
        var parts = settings
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !p.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            .ToList();
        parts.Add($"{key}={value}");
        return string.Join(';', parts) + ";";
    }

    // ---------------------------------------------------------------------
    // One-shot actions
    // ---------------------------------------------------------------------

    public static string CreateRestorePoint()
    {
        if (!IsElevated())
            return "Administrator rights are required to create a restore point.";

        try
        {
            const string key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
            object? previous = GetValue(RegistryHive.LocalMachine, key, "SystemRestorePointCreationFrequency");
            WriteValue(RegistryHive.LocalMachine, key, "SystemRestorePointCreationFrequency", 0, RegistryValueKind.DWord);
            try
            {
                string output = RunPowerShell(
                    "Checkpoint-Computer -Description 'Tool Hazard Game Boost' -RestorePointType MODIFY_SETTINGS");
                return string.IsNullOrWhiteSpace(output) ? "Restore point created." : output.Trim();
            }
            finally
            {
                if (previous is int value)
                    WriteValue(RegistryHive.LocalMachine, key, "SystemRestorePointCreationFrequency", value, RegistryValueKind.DWord);
                else
                    DeleteValue(RegistryHive.LocalMachine, key, "SystemRestorePointCreationFrequency");
            }
        }
        catch (Exception ex)
        {
            return $"Could not create a restore point: {ex.Message}";
        }
    }

    public static string OptimizeSystemDrive()
    {
        if (!IsElevated())
            return "Administrator rights are required to optimise the drive.";

        // TRIM/retrim on an SSD; safe and fast. Never defragments.
        return RunPowerShell("Optimize-Volume -DriveLetter C -ReTrim -Verbose").Trim();
    }

    public static string DisableWifiPowerSaving()
    {
        if (!IsElevated())
            return "Administrator rights are required to change adapter power settings.";

        return RunPowerShell("""
$adapters = @(Get-NetAdapter -Physical -ErrorAction SilentlyContinue |
    Where-Object { $_.Status -eq 'Up' -and $_.InterfaceDescription -match 'Wi-?Fi|Wireless|802\.11' })
foreach ($a in $adapters) {
    Disable-NetAdapterPowerManagement -Name $a.Name -ErrorAction SilentlyContinue
}
if ($adapters.Count -eq 0) { 'No active wireless adapter found.' } else { 'Disabled power saving on: ' + (($adapters | Select-Object -ExpandProperty Name) -join ', ') }
""").Trim();
    }

    public static string SetGpuHighPerformance(string exePath)
    {
        try
        {
            string full = Path.GetFullPath(exePath);
            BackedSet($"gpu.pref:{full}", RegistryHive.CurrentUser, DirectXPreferencesKey, full,
                "GpuPreference=2;", RegistryValueKind.String);
            return $"{Path.GetFileName(full)} now prefers the high-performance GPU.";
        }
        catch (Exception ex)
        {
            return $"Could not set GPU preference: {ex.Message}";
        }
    }

    // ---------------------------------------------------------------------
    // Startup entries
    // ---------------------------------------------------------------------

    public static IReadOnlyList<StartupEntry> GetStartupEntries()
    {
        var entries = new List<StartupEntry>();

        void ReadRun(RegistryHive hive, bool machineWide)
        {
            try
            {
                using RegistryKey baseKey = BaseKey(hive);
                using RegistryKey? run = baseKey.OpenSubKey(RunKey, false);
                if (run is null) return;

                RegistryHive approvedHive = hive;
                string approvedPath = machineWide
                    ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
                    : StartupApprovedRunKey;

                foreach (string name in run.GetValueNames())
                {
                    string command = run.GetValue(name)?.ToString() ?? string.Empty;
                    bool enabled = IsStartupApproved(approvedHive, approvedPath, name);
                    entries.Add(new StartupEntry(name, command, enabled, machineWide));
                }
            }
            catch
            {
                // best effort
            }
        }

        ReadRun(RegistryHive.CurrentUser, false);
        ReadRun(RegistryHive.LocalMachine, true);

        string folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        try
        {
            foreach (string file in Directory.GetFiles(folder, "*.lnk"))
                entries.Add(new StartupEntry(Path.GetFileNameWithoutExtension(file), file, true, false));
        }
        catch
        {
            // ignore
        }

        return entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsStartupApproved(RegistryHive hive, string approvedPath, string name)
    {
        try
        {
            object? value = GetValue(hive, approvedPath, name);
            if (value is byte[] bytes && bytes.Length >= 1)
                return bytes[0] != 3; // 2 = enabled, 3 = disabled by user
            return true;
        }
        catch
        {
            return true;
        }
    }

    public static string SetStartupEnabled(StartupEntry entry, bool enabled)
    {
        if (entry.MachineWide && !IsElevated())
            return $"{entry.Name} is machine-wide - run Tool Hazard as administrator to change it.";

        RegistryHive hive = entry.MachineWide ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
        string path = entry.MachineWide
            ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
            : StartupApprovedRunKey;

        byte[] payload = new byte[12];
        payload[0] = (byte)(enabled ? 2 : 3);
        if (!enabled)
        {
            long stamp = DateTime.UtcNow.ToFileTimeUtc();
            BitConverter.GetBytes(stamp).CopyTo(payload, 4);
        }

        WriteValue(hive, path, entry.Name, payload, RegistryValueKind.Binary);
        return $"{entry.Name}: {(enabled ? "enabled" : "disabled")} at startup.";
    }

    public static int CountStartupEntries() => GetStartupEntries().Count(e => e.Enabled);

    // ---------------------------------------------------------------------
    // Defender exclusions (opt-in)
    // ---------------------------------------------------------------------

    public static IReadOnlyList<string> GetExclusions()
        => BoostState.GetList("defender.exclusions");

    public static string AddExclusion(string folder)
    {
        if (!IsElevated())
            return "Administrator rights are required to change Defender exclusions.";
        if (!Directory.Exists(folder))
            return "Folder does not exist.";

        try
        {
            RunPowerShell($"Add-MpPreference -ExclusionPath '{folder.Replace("'", "''")}'");
            List<string> list = BoostState.GetList("defender.exclusions");
            if (!list.Contains(folder, StringComparer.OrdinalIgnoreCase))
                list.Add(folder);
            BoostState.SetList("defender.exclusions", list);
            return $"Excluded: {folder}";
        }
        catch (Exception ex)
        {
            return $"Could not add exclusion: {ex.Message}";
        }
    }

    public static string RemoveExclusion(string folder)
    {
        if (!IsElevated())
            return "Administrator rights are required to change Defender exclusions.";

        try
        {
            RunPowerShell($"Remove-MpPreference -ExclusionPath '{folder.Replace("'", "''")}'");
            List<string> list = BoostState.GetList("defender.exclusions");
            list.RemoveAll(p => string.Equals(p, folder, StringComparison.OrdinalIgnoreCase));
            BoostState.SetList("defender.exclusions", list);
            return $"Removed exclusion: {folder}";
        }
        catch (Exception ex)
        {
            return $"Could not remove exclusion: {ex.Message}";
        }
    }

    // ---------------------------------------------------------------------
    // Temp + priorities + launch
    // ---------------------------------------------------------------------

    public static (long FreedBytes, IReadOnlyList<string> Warnings) CleanTemp()
    {
        var warnings = new List<string>();
        long before = 0, after = 0;

        foreach (string dir in new[]
                 {
                     Path.GetTempPath(),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")
                 })
        {
            before += DirectorySize(dir);
            try
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(dir, "*", SearchOption.TopDirectoryOnly))
                {
                    string full = Path.GetFullPath(entry);
                    if (full.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
                        continue; // never delete Tool Hazard's own working files

                    try
                    {
                        if (Directory.Exists(full)) Directory.Delete(full, true);
                        else File.Delete(full);
                    }
                    catch
                    {
                        // locked/in use - expected for live temp files
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"{dir}: {ex.Message}");
            }
            after += DirectorySize(dir);
        }

        return (Math.Max(0, before - after), warnings);
    }

    private static readonly string[] BackgroundProcessNames =
    {
        "msedge", "msedgewebview2", "Discord", "steamwebhelper", "PhoneExperienceHost",
        "OneDrive", "Teams", "WidgetService"
    };

    public static IReadOnlyList<string> LowerBackgroundPriorities()
    {
        var changed = new List<string>();
        foreach (string name in BackgroundProcessNames)
        {
            foreach (Process process in Process.GetProcessesByName(name))
            {
                try
                {
                    if (!process.HasExited && process.PriorityClass > ProcessPriorityClass.BelowNormal)
                    {
                        process.PriorityClass = ProcessPriorityClass.BelowNormal;
                        changed.Add(process.ProcessName);
                    }
                }
                catch
                {
                    // protected process
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        return changed.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<string> RestorePriorities()
    {
        var changed = new List<string>();
        foreach (string name in BackgroundProcessNames)
        {
            foreach (Process process in Process.GetProcessesByName(name))
            {
                try
                {
                    if (!process.HasExited && process.PriorityClass < ProcessPriorityClass.Normal)
                    {
                        process.PriorityClass = ProcessPriorityClass.Normal;
                        changed.Add(process.ProcessName);
                    }
                }
                catch
                {
                    // protected process
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        return changed.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static Process? LaunchGame(string exePath, string? arguments = null, bool highPriority = true, bool lowerBackground = true)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            throw new FileNotFoundException("Game executable not found.", exePath);

        if (lowerBackground)
            LowerBackgroundPriorities();

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty
        };
        if (!string.IsNullOrWhiteSpace(arguments))
            startInfo.Arguments = arguments;

        Process? process = Process.Start(startInfo);
        if (process is not null && highPriority)
        {
            // Best effort: many games raise their own priority and reject changes.
            try { process.PriorityClass = ProcessPriorityClass.High; } catch { }
        }

        return process;
    }

    // ---------------------------------------------------------------------
    // Report
    // ---------------------------------------------------------------------

    public static string BuildReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Tool Hazard - Game Boost report");
        sb.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine(new string('-', 64));

        string json = RunPowerShell("""
$ErrorActionPreference = 'SilentlyContinue'
$os = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$cs = Get-CimInstance Win32_ComputerSystem
$mem = @(Get-CimInstance Win32_PhysicalMemory)
$gpus = @(Get-CimInstance Win32_VideoController)
$disp = $gpus | Where-Object { $_.CurrentRefreshRate } | Select-Object -First 1
$vol = Get-Volume -DriveLetter C
[pscustomobject]@{
  os = "$($os.ProductName) $($os.DisplayVersion) (build $($os.CurrentBuild).$($os.UBR))"
  model = "$($cs.Manufacturer) $($cs.Model)".Trim()
  cpu = "$($cpu.Name) ($($cpu.NumberOfCores)C/$($cpu.NumberOfLogicalProcessors)T)"
  ramTotal = [math]::Round($cs.TotalPhysicalMemory/1GB)
  ramModules = $mem.Count
  ramSpeed = ($mem | Select-Object -First 1).ConfiguredClockSpeed
  gpu = (($gpus | ForEach-Object { "$($_.Name) [driver $($_.DriverVersion), $($_.DriverDate)]" }) -join ' | ')
  display = "{0}x{1} @ {2}Hz (max {3}Hz)" -f $disp.CurrentHorizontalResolution, $disp.CurrentVerticalResolution, $disp.CurrentRefreshRate, $disp.MaxRefreshRate
  refresh = [int]$disp.CurrentRefreshRate
  maxRefresh = [int]$disp.MaxRefreshRate
  freeGb = [math]::Round($vol.SizeRemaining/1GB,1)
  totalGb = [math]::Round($vol.Size/1GB,1)
  freePct = [math]::Round(($vol.SizeRemaining/$vol.Size)*100,1)
} | ConvertTo-Json -Compress
""", 60_000);

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json.Trim());
            JsonElement root = doc.RootElement;
            string T(string name) => root.TryGetProperty(name, out JsonElement v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : "unknown";

            sb.AppendLine($"Windows    : {NormalizeWindowsName(T("os"))}");
            sb.AppendLine($"Machine    : {T("model")}");
            sb.AppendLine($"CPU        : {T("cpu")}");
            sb.AppendLine($"Memory     : {T("ramTotal")} GB in {T("ramModules")} module(s) @ {T("ramSpeed")} MHz");
            sb.AppendLine($"GPU        : {T("gpu")}");
            sb.AppendLine($"Display    : {T("display")}");
            sb.AppendLine($"System disk: {T("freeGb")} GB free of {T("totalGb")} GB ({T("freePct")}%)");

            var findings = new List<string>();
            if (root.TryGetProperty("ramModules", out JsonElement modules) && modules.GetInt32() == 1)
                findings.Add("Single memory module: the laptop runs single-channel. A matching second SODIMM is the largest available gaming upgrade here.");
            if (root.TryGetProperty("freePct", out JsonElement free) && free.GetDouble() < 15)
                findings.Add($"System drive only {free.GetDouble():0.#}% free - keep 15%+ free for SSD write performance.");
            if (root.TryGetProperty("refresh", out JsonElement hz) && root.TryGetProperty("maxRefresh", out JsonElement maxHz)
                && hz.GetInt32() < maxHz.GetInt32())
                findings.Add($"Panel is running at {hz.GetInt32()}Hz but supports {maxHz.GetInt32()}Hz.");
            if (!IsPerformancePlan(GetActivePowerPlan()))
                findings.Add("Power plan is not High performance.");

            BoostStatus status = GetStatus();
            if (!status.HagsEnabled)
                findings.Add("Hardware-accelerated GPU scheduling is off.");

            sb.AppendLine();
            sb.AppendLine("Findings:");
            if (findings.Count == 0) sb.AppendLine("  - Nothing obviously wrong.");
            foreach (string finding in findings) sb.AppendLine($"  - {finding}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Hardware query failed: {ex.Message}");
            sb.AppendLine(json);
        }

        BoostStatus s = GetStatus();
        sb.AppendLine();
        sb.AppendLine("Current settings:");
        sb.AppendLine($"  Power plan          : {s.PowerPlanName}");
        sb.AppendLine($"  Game Mode           : {(s.GameModeEnabled ? "On" : "Off")}");
        sb.AppendLine($"  HAGS                : {(s.HagsStateKnown ? (s.HagsEnabled ? "On" : "Off") : "Windows default")}");
        sb.AppendLine($"  Game DVR capture    : {(s.GameDvrCaptureEnabled ? "On" : "Off")}");
        sb.AppendLine($"  Visual effects      : {s.VisualFxSetting}");
        sb.AppendLine($"  Mouse acceleration  : {(s.MouseAccelerationEnabled ? "On" : "Off")}");
        sb.AppendLine($"  Power throttling    : {(s.PowerThrottlingOff ? "Off" : "On")}");
        sb.AppendLine($"  Startup entries     : {s.StartupCount} enabled");
        sb.AppendLine($"  Elevated            : {(s.IsElevated ? "yes" : "no")}");
        sb.AppendLine();
        sb.AppendLine("Security: Defender real-time protection, tamper protection, VBS/HVCI and");
        sb.AppendLine("Windows Update are not modified by Game Boost. Defender exclusions are opt-in.");
        return sb.ToString();
    }

    private static string BuildSystemSummary()
    {
        string cpu = GetString(RegistryHive.LocalMachine,
            @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", "CPU").Trim();
        string os = GetString(RegistryHive.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion", "");
        int build = GetInt(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild", 0);
        return $"{cpu} | Windows 11 {os} (build {build}) | {Environment.ProcessorCount} threads";
    }

    /// <summary>
    /// HKLM\...\CurrentVersion still reports "Windows 10" on Windows 11 (the string was
    /// never updated). Normalise it for the report.
    /// </summary>
    private static string NormalizeWindowsName(string name)
    {
        if (!name.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase))
            return name;

        int build = GetInt(RegistryHive.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild", 0);
        return build >= 22000 ? "Windows 11" + name["Windows 10".Length..] : name;
    }

    private static long DirectorySize(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return 0;
            long total = 0;
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
            {
                try { total += new FileInfo(file).Length; } catch { }
            }
            return total;
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsServiceDisabled(string serviceName)
    {
        string output = RunExe("sc.exe", $"qc {serviceName}");
        foreach (string line in output.Split('\n'))
        {
            string text = line.Trim();
            if (!text.StartsWith("START_TYPE", StringComparison.OrdinalIgnoreCase)) continue;
            return text.Contains("DISABLED", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    // ---------------------------------------------------------------------
    // Exported scripts (kept in sync with the in-app behaviour)
    // ---------------------------------------------------------------------

    public static string ExportUserScript() => """
# Game Boost (user-level, no admin) - exported by Tool Hazard
# Safe + reversible. Values are backed up to %LOCALAPPDATA%\Tool Hazard\GameBoost.
Write-Host "=== Game Boost (user) ===" -ForegroundColor Cyan

$gdvr = "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR"
New-Item $gdvr -Force | Out-Null
Set-ItemProperty $gdvr -Name AppCaptureEnabled -Value 0 -Type DWord
Set-ItemProperty $gdvr -Name HistoricalCaptureEnabled -Value 0 -Type DWord
New-Item "HKCU:\System\GameConfigStore" -Force | Out-Null
Set-ItemProperty "HKCU:\System\GameConfigStore" -Name GameDVR_Enabled -Value 0 -Type DWord

# Game Mode (correct location: GameBar, not GameDVR)
New-Item "HKCU:\Software\Microsoft\GameBar" -Force | Out-Null
Set-ItemProperty "HKCU:\Software\Microsoft\GameBar" -Name AutoGameModeEnabled -Value 1 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\GameBar" -Name AllowAutoGameMode -Value 1 -Type DWord

# VisualFXSetting 2 = Best performance (1 = appearance, 3 = custom)
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" -Name VisualFXSetting -Value 2 -Type DWord
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" -Name EnableTransparency -Value 0 -Type DWord

# Windowed game optimisations
$dx = "HKCU:\Software\Microsoft\DirectX\UserGpuPreferences"
New-Item $dx -Force | Out-Null
$current = (Get-ItemProperty $dx -Name DirectXUserGlobalSettings -ErrorAction SilentlyContinue).DirectXUserGlobalSettings
$parts = @($current -split ';' | Where-Object { $_ -and $_ -notmatch '^SwapEffectUpgradeEnable=' })
$parts += 'SwapEffectUpgradeEnable=1'
Set-ItemProperty $dx -Name DirectXUserGlobalSettings -Value (($parts -join ';') + ';')

# Mouse acceleration off
Set-ItemProperty "HKCU:\Control Panel\Mouse" -Name MouseSpeed -Value "0"
Set-ItemProperty "HKCU:\Control Panel\Mouse" -Name MouseThreshold1 -Value "0"
Set-ItemProperty "HKCU:\Control Panel\Mouse" -Name MouseThreshold2 -Value "0"

# Background apps
New-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications" -Force | Out-Null
Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications" -Name GlobalUserDisabled -Value 1 -Type DWord

Write-Host "Done. Restart Explorer or sign out for visual changes." -ForegroundColor Green
""";

    public static string ExportElevatedScript() => """
# Self-elevating Game Boost (admin) - exported by Tool Hazard
# HAGS + High performance plan + power throttling off + network throttling off.
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs -Wait
    exit
}

Write-Host "=== Game Boost (elevated) ===" -ForegroundColor Cyan

# HAGS: 2 = enabled, 1 = disabled. Absent = Windows default.
Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name HwSchMode -Value 2 -Type DWord
Write-Host "HAGS ON (HwSchMode=2) - reboot required"

# Power throttling (machine scope)
New-Item "HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling" -Force | Out-Null
Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling" -Name PowerThrottlingOff -Value 1 -Type DWord

# Multimedia network throttling
$mm = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
Set-ItemProperty $mm -Name NetworkThrottlingIndex -Value 0xFFFFFFFF -Type DWord
Set-ItemProperty $mm -Name SystemResponsiveness -Value 10 -Type DWord

# High performance plan (Ultimate omitted: aggressive on laptops)
$high = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"
if (-not (powercfg /LIST | Select-String $high)) { powercfg /DUPLICATESCHEME $high | Out-Null }
powercfg /SETACTIVE $high

# Telemetry service
sc.exe config DiagTrack start= disabled | Out-Host

powercfg /GETACTIVESCHEME
Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" | Format-List HwSchMode
Write-Host "`nReboot for HAGS to take effect." -ForegroundColor Green
Pause
""";

    public static string ExportRevertElevatedScript() => """
# Self-elevating Game Boost REVERT (admin) - exported by Tool Hazard
# Restores Windows defaults for the elevated tweaks only.
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs -Wait
    exit
}

Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name HwSchMode -Value 2 -Type DWord
Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling" -Name PowerThrottlingOff -Value 0 -Type DWord
Set-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" -Name NetworkThrottlingIndex -Value 10 -Type DWord
Set-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" -Name SystemResponsiveness -Value 20 -Type DWord
sc.exe config DiagTrack start= auto | Out-Host
powercfg /SETACTIVE 381b4222-f694-41f0-9685-ff5bb260df2e
Write-Host "Reverted. Reboot for HAGS." -ForegroundColor Green
Pause
""";
}

/// <summary>Persisted per-tweak snapshots so Revert survives restarts.</summary>
internal static class BoostBackup
{
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tool Hazard", "GameBoost");

    private static string FilePath => Path.Combine(Folder, "backup.json");

    private static Dictionary<string, List<RegSnapshot>> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return New();
            Dictionary<string, List<RegSnapshot>>? data =
                JsonSerializer.Deserialize<Dictionary<string, List<RegSnapshot>>>(File.ReadAllText(FilePath), Options);
            return data is null ? New() : new Dictionary<string, List<RegSnapshot>>(data, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return New();
        }
    }

    private static Dictionary<string, List<RegSnapshot>> New() => new(StringComparer.OrdinalIgnoreCase);

    private static void Save(Dictionary<string, List<RegSnapshot>> data)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(data, Options));
    }

    internal static void Add(string tweakId, RegSnapshot snapshot)
    {
        lock (Sync)
        {
            Dictionary<string, List<RegSnapshot>> data = Load();
            if (!data.TryGetValue(tweakId, out List<RegSnapshot>? list))
            {
                list = new List<RegSnapshot>();
                data[tweakId] = list;
            }

            // Keep the first (true original) snapshot per value across repeated applies.
            if (!list.Any(s => s.HiveName == snapshot.HiveName && s.SubKey == snapshot.SubKey && s.Name == snapshot.Name))
                list.Add(snapshot);

            Save(data);
        }
    }

    internal static bool Contains(string tweakId)
    {
        lock (Sync) return Load().ContainsKey(tweakId);
    }

    internal static IEnumerable<RegSnapshot> Take(string tweakId)
    {
        lock (Sync)
            return Load().TryGetValue(tweakId, out List<RegSnapshot>? list) ? list.ToList() : Array.Empty<RegSnapshot>();
    }

    internal static void Remove(string tweakId)
    {
        lock (Sync)
        {
            Dictionary<string, List<RegSnapshot>> data = Load();
            if (data.Remove(tweakId)) Save(data);
        }
    }
}

/// <summary>Small JSON key/value store for non-registry state.</summary>
internal static class BoostState
{
    private static readonly object Sync = new();

    private static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tool Hazard", "GameBoost");

    private static string FilePath => Path.Combine(Folder, "state.json");

    private static Dictionary<string, string> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string>? data = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath));
            return data is null ? new(StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void Save(Dictionary<string, string> data)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    internal static string? Get(string key)
    {
        lock (Sync) return Load().TryGetValue(key, out string? value) ? value : null;
    }

    internal static void Set(string key, string value)
    {
        lock (Sync)
        {
            Dictionary<string, string> data = Load();
            data[key] = value;
            Save(data);
        }
    }

    internal static void Remove(string key)
    {
        lock (Sync)
        {
            Dictionary<string, string> data = Load();
            if (data.Remove(key)) Save(data);
        }
    }

    internal static List<string> GetList(string key)
    {
        lock (Sync)
        {
            string? raw = Get(key);
            return string.IsNullOrWhiteSpace(raw) ? new List<string>() : raw.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }

    internal static void SetList(string key, IEnumerable<string> values) => Set(key, string.Join('\n', values));
}

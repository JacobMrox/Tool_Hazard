#nullable enable
using Tool_Hazard.GameBoost;

namespace Tool_Hazard.Forms;

/// <summary>
/// Game Boost panel — analyzes Windows 11 gaming overhead and applies
/// safe, reversible FPS / snappiness tweaks (user + admin sides).
/// </summary>
public sealed class GameBoostForm : Form
{
    private readonly ListView _checklist = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
    private readonly TextBox _logBox = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, WordWrap = false, Font = new Font("Consolas", 9) };
    private readonly Label _summary = new() { Dock = DockStyle.Top, Height = 52, AutoEllipsis = true, Padding = new Padding(8), BackColor = Color.WhiteSmoke };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Bottom, Height = 10, Visible = false };
    private readonly CheckBox _chkLowerBg = new() { Text = "Lower Edge/Discord priority while playing", AutoSize = true, Checked = true };
    private readonly CheckBox _chkHighPrio = new() { Text = "Launch game at High priority", AutoSize = true, Checked = true };
    private readonly CheckBox _chkGpuHigh = new() { Text = "Prefer High-performance GPU for this game", AutoSize = true, Checked = true };
    private readonly TextBox _exeBox = new() { Dock = DockStyle.Fill, ReadOnly = true };

    public GameBoostForm()
    {
        Text = "Game Boost — PC Performance & Gaming Optimizer";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 620);
        Size = new Size(1020, 700);

        _checklist.Columns.Add("Setting", 260);
        _checklist.Columns.Add("Current", 200);
        _checklist.Columns.Add("Recommended", 280);
        _checklist.Columns.Add("Status", 90);

        var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 96, Padding = new Padding(8), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        var btnRow1 = new FlowLayoutPanel { AutoSize = true };
        var btnAnalyze = new Button { Text = "Analyze Now", Width = 140 };
        var btnApplyUser = new Button { Text = "Apply Boost (User)", Width = 160 };
        var btnApplyAdmin = new Button { Text = "Apply Boost (Elevated)", Width = 190 };
        var btnClean = new Button { Text = "Clean Temp", Width = 110 };
        var btnRevert = new Button { Text = "Revert", Width = 90 };
        var btnRestorePrio = new Button { Text = "Restore Priorities", Width = 140 };
        btnRow1.Controls.AddRange(new Control[] { btnAnalyze, btnApplyUser, btnApplyAdmin, btnClean, btnRevert, btnRestorePrio });

        var btnRow2 = new FlowLayoutPanel { AutoSize = true };
        var btnBrowse = new Button { Text = "Browse game .exe...", Width = 160 };
        var btnLaunch = new Button { Text = "Launch Game with Boost", Width = 200 };
        var btnExport = new Button { Text = "Export PowerShell Scripts", Width = 220 };
        btnRow2.Controls.AddRange(new Control[] { btnBrowse, btnLaunch, btnExport });
        topPanel.Controls.Add(btnRow1);
        topPanel.Controls.Add(btnRow2);
        topPanel.Controls.Add(_chkLowerBg);
        _summary.Text = "Analyzing...";

        var middle = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 520 };
        middle.Panel1.Text = "Checklist";
        var checklistHost = new Panel { Dock = DockStyle.Fill };
        checklistHost.Controls.Add(_checklist);
        middle.Panel1.Controls.Add(checklistHost);
        middle.Panel2.Text = "Log";
        middle.Panel2.Controls.Add(_logBox);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Top, Height = 78, ColumnCount = 3, RowCount = 2 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(new Label { Text = "Game .exe:", AutoSize = true, Padding = new Padding(6) }, 0, 0);
        bottom.Controls.Add(_exeBox, 1, 0);
        bottom.SetColumnSpan(_exeBox, 2);
        bottom.Controls.Add(_chkHighPrio, 0, 1);
        bottom.Controls.Add(_chkGpuHigh, 1, 1);
        bottom.Controls.Add(new Label { Text = "User tweaks take effect immediately; HAGS needs reboot.", AutoSize = true }, 2, 1);

        Controls.Add(middle);
        Controls.Add(bottom);
        Controls.Add(topPanel);
        Controls.Add(_summary);
        Controls.Add(_progress);

        btnAnalyze.Click += async (_, _) => await AnalyzeAsync();
        btnApplyUser.Click += async (_, _) => await ApplyUserAsync();
        btnApplyAdmin.Click += async (_, _) => await ApplyAdminAsync();
        btnClean.Click += async (_, _) => await CleanAsync();
        btnRevert.Click += async (_, _) => await RevertAsync();
        btnRestorePrio.Click += (_, _) => RestorePrio();
        btnBrowse.Click += (_, _) => BrowseExe();
        btnLaunch.Click += (_, _) => Launch();
        btnExport.Click += (_, _) => ExportScripts();

        Shown += async (_, _) => await AnalyzeAsync();
    }

    private void Log(string msg)
    {
        try
        {
            if (InvokeRequired) Invoke(new Action(() => _logBox.AppendText(msg + Environment.NewLine)));
            else _logBox.AppendText(msg + Environment.NewLine);
        }
        catch { /* form closing */ }
    }

    private async Task AnalyzeAsync()
    {
        Log("--- Analyze ---");
        var status = await Task.Run(GameBoostService.GetStatus);
        _checklist.Items.Clear();
        foreach (var item in GameBoostService.GetChecklist(status))
        {
            var li = new ListViewItem(new[] { item.Name, item.Current, item.Recommended, item.IsOptimal ? "✓ OK" : "✗ Fix" });
            li.BackColor = item.IsOptimal ? Color.Honeydew : Color.MistyRose;
            _checklist.Items.Add(li);
        }
        _summary.Text =
            $"Power: {status.PowerPlanName}  •  Game Mode: {(status.GameModeEnabled ? "ON" : "OFF")}  •  " +
            $"HAGS: {(status.HagsEnabled ? "ON" : "OFF")}  •  DVR capture: {(status.GameDvrCaptureEnabled ? "ON" : "OFF")}  •  " +
            $"%TEMP%: {(status.TempBytes / 1024 / 1024):N0} MB  •  Top RAM: {string.Join(", ", status.TopMemoryProcesses.Take(3))}";
        Log($"Power={status.PowerPlanName}, GameMode={status.GameModeEnabled}, HAGS={status.HagsEnabled} (known={status.HagsStateKnown}), DVR={status.GameDvrCaptureEnabled}, FX={status.VisualFxSetting}, Temp={(status.TempBytes / 1024 / 1024):N0}MB");
    }

    private async Task ApplyUserAsync()
    {
        _progress.Visible = true; _progress.Style = ProgressBarStyle.Marquee;
        Log("Applying user-level boost (no admin needed)...");
        try
        {
            var prog = new Progress<string>(Log);
            var r = await GameBoostService.ApplyUserBoostAsync(prog);
            foreach (string a in r.Applied) Log("✓ " + a);
            foreach (string w in r.Warnings) Log("! " + w);
            Log(r.Success ? "User boost done. Restart Explorer / reboot for full visual effect." : "User boost finished with warnings.");
        }
        catch (Exception ex) { Log("Error: " + ex.Message); }
        finally { _progress.Visible = false; await AnalyzeAsync(); }
    }

    private async Task ApplyAdminAsync()
    {
        Log("Elevated boost requested. This will pop a UAC prompt for the admin script (HAGS + Ultimate Performance).");
        // Try in-process first (useful when Tool Hazard itself is already elevated)
        if (GameBoostService.IsElevated())
        {
            var r = await GameBoostService.ApplyAdminBoostAsync(new Progress<string>(Log));
            foreach (string a in r.Applied) Log("✓ " + a);
            foreach (string w in r.Warnings) Log("! " + w);
            await AnalyzeAsync();
            return;
        }
        // Otherwise launch the bundled elevated powershell script via UAC
        string script = FindScript("Apply-GameBoost-Elevated.ps1") ?? FindScript("pc-hardening\\Apply-GameBoost-Elevated.ps1");
        if (script is null)
        {
            // Fallback: export + open folder for manual run
            ExportScripts(openFolder: true);
            MessageBox.Show(this,
                "Admin script not found next to the exe.\n\nExported scripts to your Desktop/GameBoost folder — right-click the Elevated .ps1 and choose 'Run with PowerShell' (it will ask for Admin).",
                "Game Boost", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
                Verb = "runas",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
            Log("Launched elevated script — approve the UAC prompt on your desktop. Reboot after it finishes for HAGS.");
        }
        catch (Exception ex) { Log("Elevated launch failed: " + ex.Message); }
    }

    private async Task CleanAsync()
    {
        Log("Cleaning temp...");
        var (freed, w) = await Task.Run(GameBoostService.CleanTemp);
        Log($"✓ Freed {(freed / 1024 / 1024):N0} MB");
        foreach (string x in w) Log("! " + x);
        await AnalyzeAsync();
    }

    private async Task RevertAsync()
    {
        var confirm = MessageBox.Show(this, "Revert user-level Game Boost changes? (Visual FX, mouse, DVR, background apps)", "Revert", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;
        var r = await GameBoostService.RevertUserBoostAsync(new Progress<string>(Log));
        foreach (string a in r.Applied) Log("✓ " + a);
        foreach (string w in r.Warnings) Log("! " + w);
        await AnalyzeAsync();
    }

    private void RestorePrio()
    {
        var done = GameBoostService.RestorePriorities();
        Log(done.Count == 0 ? "No lowered priorities to restore." : "Restored: " + string.Join(", ", done));
    }

    private void BrowseExe()
    {
        using var dlg = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*", Title = "Select game .exe" };
        if (dlg.ShowDialog(this) == DialogResult.OK) _exeBox.Text = dlg.FileName;
    }

    private void Launch()
    {
        if (string.IsNullOrWhiteSpace(_exeBox.Text)) { BrowseExe(); if (string.IsNullOrWhiteSpace(_exeBox.Text)) return; }
        try
        {
            if (_chkGpuHigh.Checked) GameBoostService.SetGpuHighPerformance(_exeBox.Text.Trim());
            var p = GameBoostService.LaunchGame(_exeBox.Text.Trim(), null, _chkHighPrio.Checked, _chkLowerBg.Checked);
            Log($"Launched: {_exeBox.Text} (PID {(p?.Id.ToString() ?? "?")})" + (_chkLowerBg.Checked ? " + background lowered." : ""));
            MessageBox.Show(this, "Game launched with Boost.\n\nEdge/Discord lowered to BelowNormal if they were running.\nUse 'Restore Priorities' when done.", "Game Boost", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Launch failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ExportScripts(bool openFolder = false)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ToolHazard-GameBoost");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Apply-GameBoost.ps1"), GameBoostService.ExportUserScript());
        File.WriteAllText(Path.Combine(folder, "Apply-GameBoost-Elevated.ps1"), GameBoostService.ExportElevatedScript());
        File.WriteAllText(Path.Combine(folder, "Revert-GameBoost-Elevated.ps1"), GameBoostService.ExportRevertElevatedScript());
        Log("Exported scripts to " + folder);
        if (openFolder) { try { System.Diagnostics.Process.Start("explorer.exe", $"\"{folder}\""); } catch { } }
        else MessageBox.Show(this, $"Exported to:\n{folder}\n\n- Apply-GameBoost.ps1 (user, no admin)\n- Apply-GameBoost-Elevated.ps1 (admin: HAGS + Ultimate)\n- Revert-GameBoost-Elevated.ps1", "Game Boost", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string? FindScript(string name)
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, name),
                Path.Combine(baseDir, "pc-hardening", Path.GetFileName(name)),
                Path.Combine(Environment.CurrentDirectory, name),
                Path.Combine(Environment.CurrentDirectory, "pc-hardening", Path.GetFileName(name)),
                Path.Combine(@"C:\Users\user\source\repos\Tool Hazard\pc-hardening", Path.GetFileName(name))
            };
            return candidates.FirstOrDefault(File.Exists);
        }
        catch { return null; }
    }
}

#nullable enable
using System.Diagnostics;
using Tool_Hazard.GameBoost;

namespace Tool_Hazard.Forms;

/// <summary>
/// Game Boost panel: analyses Windows 11 gaming overhead, then applies each tweak
/// individually. Every change is snapshotted and can be reverted exactly.
/// </summary>
public sealed class GameBoostForm : Form
{
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.None,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        EditMode = DataGridViewEditMode.EditOnEnter
    };

    private readonly TextBox _log = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        WordWrap = false,
        Font = new Font("Consolas", 9f),
        BorderStyle = BorderStyle.None
    };

    private readonly Label _summary = new()
    {
        Dock = DockStyle.Top,
        Height = 46,
        Padding = new Padding(10, 8, 10, 4),
        AutoEllipsis = true
    };

    private readonly Label _elevation = new()
    {
        Dock = DockStyle.Top,
        Height = 34,
        Padding = new Padding(10, 0, 10, 6),
        AutoEllipsis = true
    };

    private readonly ProgressBar _progress = new()
    {
        Dock = DockStyle.Bottom,
        Height = 4,
        Style = ProgressBarStyle.Marquee,
        Visible = false
    };

    private readonly ListView _startup = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        CheckBoxes = true,
        BorderStyle = BorderStyle.None
    };

    private readonly TextBox _report = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Font = new Font("Consolas", 9f),
        BorderStyle = BorderStyle.None
    };

    private readonly TextBox _exeBox = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly CheckBox _chkGpu = new() { Text = "Prefer high-performance GPU", AutoSize = true, Checked = true };
    private readonly CheckBox _chkPriority = new() { Text = "High priority", AutoSize = true, Checked = true };
    private readonly CheckBox _chkBackground = new() { Text = "Lower Edge/Discord priority", AutoSize = true, Checked = true };

    private bool _loadingStartup;

    public GameBoostForm(string? initialTab = null)
    {
        Text = "Game Boost - PC Performance & Gaming Optimizer";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(940, 640);
        Size = new Size(1080, 740);
        KeyPreview = true;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildOptimizeTab());
        tabs.TabPages.Add(BuildStartupTab());
        tabs.TabPages.Add(BuildReportTab());

        Controls.Add(tabs);
        Controls.Add(_elevation);
        Controls.Add(_summary);
        Controls.Add(_progress);

        Shown += async (_, _) =>
        {
            if (string.Equals(initialTab, "report", StringComparison.OrdinalIgnoreCase))
                tabs.SelectedIndex = 2;
            await AnalyzeAsync();
        };

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F5)
            {
                _ = AnalyzeAsync();
                e.Handled = true;
            }
        };
    }

    // ---------------------------------------------------------------------
    // Tabs
    // ---------------------------------------------------------------------

    private TabPage BuildOptimizeTab()
    {
        var page = new TabPage("Optimize") { Padding = new Padding(8) };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 76,
            AutoScroll = true,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 6)
        };

        buttons.Controls.Add(MakeButton("Analyze (F5)", 110, async () => await AnalyzeAsync()));
        buttons.Controls.Add(MakeButton("Apply Selected", 130, async () => await ApplySelectedAsync()));
        buttons.Controls.Add(MakeButton("Revert Selected", 130, async () => await RevertSelectedAsync()));
        buttons.Controls.Add(MakeButton("Apply Recommended", 160, async () => await ApplyRecommendedAsync()));
        buttons.Controls.Add(MakeButton("Revert Everything", 150, async () => await RevertAllAsync()));
        buttons.Controls.Add(MakeButton("Create Restore Point", 160, CreateRestorePoint));
        buttons.Controls.Add(MakeButton("TRIM system drive", 140, TrimDrive));
        buttons.Controls.Add(MakeButton("Fix Wi-Fi power saving", 160, FixWifi));

        ConfigureGrid();

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 320
        };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(_log);

        page.Controls.Add(split);
        page.Controls.Add(buttons);
        page.Controls.Add(BuildLaunchBar());
        return page;
    }

    private Control BuildLaunchBar()
    {
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 68,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(0, 6, 0, 0)
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        bar.Controls.Add(new Label { Text = "Game .exe:", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 6, 0) }, 0, 0);
        bar.Controls.Add(_exeBox, 1, 0);
        bar.Controls.Add(MakeButton("Browse...", 82, BrowseExe), 2, 0);
        bar.Controls.Add(MakeButton("Launch", 90, LaunchGame), 3, 0);

        var checks = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        checks.Controls.Add(_chkGpu);
        checks.Controls.Add(_chkPriority);
        checks.Controls.Add(_chkBackground);
        bar.Controls.Add(checks, 0, 1);
        bar.SetColumnSpan(checks, 4);
        return bar;
    }

    private TabPage BuildStartupTab()
    {
        var page = new TabPage("Startup apps") { Padding = new Padding(8) };

        _startup.Columns.Add("App", 280);
        _startup.Columns.Add("Scope", 90);
        _startup.Columns.Add("Command", 640);

        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(2, 6, 2, 6),
            Text = "Untick an entry to stop it launching with Windows. Machine-wide entries need administrator rights. " +
                   "Changes apply immediately and are recorded by Windows under StartupApproved."
        };

        var refresh = MakeButton("Refresh list", 110, RefreshStartupAsync);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false };
        bar.Controls.Add(refresh);
        bar.Controls.Add(new Label { Text = "   Tip: keep only your launcher, audio and input drivers.", AutoSize = true, Padding = new Padding(0, 10, 0, 0) });

        page.Controls.Add(_startup);
        page.Controls.Add(bar);
        page.Controls.Add(hint);
        _startup.ItemChecked += StartupItemChecked;
        return page;
    }

    private TabPage BuildReportTab()
    {
        var page = new TabPage("Report") { Padding = new Padding(8) };

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false };
        bar.Controls.Add(MakeButton("Generate", 100, async () => await GenerateReportAsync()));
        bar.Controls.Add(MakeButton("Save as...", 100, SaveReport));
        bar.Controls.Add(MakeButton("Copy", 90, () => Clipboard.SetText(_report.Text)));

        page.Controls.Add(_report);
        page.Controls.Add(bar);
        return page;
    }

    // ---------------------------------------------------------------------
    // Grid
    // ---------------------------------------------------------------------

    private void ConfigureGrid()
    {
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Selected",
            HeaderText = "",
            Width = 34,
            FillWeight = 12,
            ReadOnly = false
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Setting", HeaderText = "Setting", FillWeight = 90, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Current", HeaderText = "Current", FillWeight = 80, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Recommended", HeaderText = "Recommended", FillWeight = 90, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 50, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Needs", HeaderText = "Needs", FillWeight = 55, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", HeaderText = "Why", FillWeight = 190, ReadOnly = true });

        _grid.CellFormatting += GridCellFormatting;
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
    }

    private void GridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Rows[e.RowIndex].Tag is not ChecklistItem item)
            return;

        if (_grid.Columns[e.ColumnIndex].Name == "Status")
        {
            e.Value = item.IsOptimal switch
            {
                true => "OK",
                false => "Improve",
                null => "Unknown"
            };
            e.CellStyle.ForeColor = item.IsOptimal switch
            {
                true => Color.FromArgb(0, 120, 70),
                false => Color.FromArgb(180, 70, 30),
                _ => SystemColors.GrayText
            };
        }
        else if (_grid.Columns[e.ColumnIndex].Name == "Needs")
        {
            var needs = new List<string>();
            if (item.RequiresAdmin) needs.Add("admin");
            if (item.RequiresReboot) needs.Add("reboot");
            if (GameBoostService.IsApplied(item.Id)) needs.Add("applied");
            e.Value = string.Join(", ", needs);
        }
    }

    // ---------------------------------------------------------------------
    // Actions
    // ---------------------------------------------------------------------

    private async Task AnalyzeAsync()
    {
        UseWaitCursor = true;
        _progress.Visible = true;
        try
        {
            BoostStatus status = await Task.Run(GameBoostService.GetStatus);
            IReadOnlyList<ChecklistItem> checklist = GameBoostService.GetChecklist(status);

            _grid.Rows.Clear();
            foreach (ChecklistItem item in checklist)
            {
                int index = _grid.Rows.Add();
                DataGridViewRow row = _grid.Rows[index];
                row.Tag = item;
                row.Cells["Setting"].Value = item.Name;
                row.Cells["Current"].Value = item.Current;
                row.Cells["Recommended"].Value = item.Recommended;
                row.Cells["Notes"].Value = item.Risk;
                row.Cells["Needs"].Value = "";
                row.Cells["Selected"].Value = item.IsOptimal == false;

                if (item.RequiresAdmin && !status.IsElevated)
                    row.DefaultCellStyle.ForeColor = SystemColors.GrayText;
            }

            _summary.Text =
                $"{status.SystemSummary}\n" +
                $"Power: {status.PowerPlanName}   |   Game Mode: {(status.GameModeEnabled ? "ON" : "OFF")}   |   " +
                $"HAGS: {(status.HagsStateKnown ? (status.HagsEnabled ? "ON" : "OFF") : "default")}   |   " +
                $"DVR: {(status.GameDvrCaptureEnabled ? "recording" : "off")}   |   " +
                $"Startup: {status.StartupCount}   |   Temp: {(status.TempBytes / 1024 / 1024):N0} MB";

            _elevation.Text = status.IsElevated
                ? "Running as administrator - every tweak can be applied."
                : "Not elevated. User-level tweaks work now; power plan, HAGS, throttling and telemetry need UAC.";
            _elevation.ForeColor = status.IsElevated ? Color.FromArgb(0, 120, 70) : Color.FromArgb(170, 90, 20);

            await RefreshStartupAsync();
        }
        catch (Exception ex)
        {
            Log("Analyze failed: " + ex.Message);
        }
        finally
        {
            _progress.Visible = false;
            UseWaitCursor = false;
        }
    }

    private async Task ApplySelectedAsync()
    {
        List<string> ids = SelectedIds();
        if (ids.Count == 0)
        {
            Log("Nothing selected.");
            return;
        }

        if (!await EnsureElevatedForAsync(ids))
            return;

        await RunAsync(async () =>
        {
            var result = await GameBoostService.ApplyTweaksAsync(ids, new Progress<string>(Log));
            foreach (string warning in result.Warnings) Log("! " + warning);
            if (ids.Contains("gpu.hags")) Log("HAGS changed - sign out or reboot to apply.");
            if (ids.Contains("input.mouse")) Log("Mouse acceleration off - re-apply if the desktop pointer feels wrong.");
        });
    }

    private async Task RevertSelectedAsync()
    {
        List<string> ids = SelectedIds();
        if (ids.Count == 0)
        {
            Log("Nothing selected.");
            return;
        }

        await RunAsync(async () =>
        {
            var result = await GameBoostService.RevertTweaksAsync(ids, new Progress<string>(Log));
            foreach (string warning in result.Warnings) Log("! " + warning);
        });
    }

    private async Task ApplyRecommendedAsync()
    {
        var ids = new List<string>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is ChecklistItem item && item.IsOptimal == false)
                ids.Add(item.Id);
        }

        if (ids.Count == 0)
        {
            Log("Everything is already at the recommended value.");
            return;
        }

        if (!await EnsureElevatedForAsync(ids))
            return;

        await RunAsync(async () =>
        {
            var result = await GameBoostService.ApplyTweaksAsync(ids, new Progress<string>(Log));
            foreach (string warning in result.Warnings) Log("! " + warning);
        });
    }

    private async Task RevertAllAsync()
    {
        if (MessageBox.Show(this,
                "Restore every value Game Boost has changed back to what it was before?\n\n" +
                "Only values recorded in the backup are touched.",
                "Revert everything", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        await RunAsync(async () =>
        {
            var result = await GameBoostService.RevertAllAsync(new Progress<string>(Log));
            if (result.Applied.Count == 0) Log("No recorded changes to revert.");
            foreach (string warning in result.Warnings) Log("! " + warning);
        });
    }

    private void CreateRestorePoint()
    {
        Log("Creating a restore point...");
        string result = GameBoostService.CreateRestorePoint();
        Log(result);
    }

    private void TrimDrive() => Log(GameBoostService.OptimizeSystemDrive());

    private void FixWifi() => Log(GameBoostService.DisableWifiPowerSaving());

    private async Task RunAsync(Func<Task> action)
    {
        _progress.Visible = true;
        UseWaitCursor = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Log("Error: " + ex.Message);
        }
        finally
        {
            _progress.Visible = false;
            UseWaitCursor = false;
            await AnalyzeAsync();
        }
    }

    private async Task<bool> EnsureElevatedForAsync(IEnumerable<string> ids)
    {
        if (GameBoostService.IsElevated())
            return true;

        IReadOnlyList<ChecklistItem> checklist = await Task.Run(() =>
            GameBoostService.GetChecklist(GameBoostService.GetStatus()));

        List<string> adminIds = checklist
            .Where(item => item.RequiresAdmin)
            .Select(item => item.Id)
            .Intersect(ids)
            .ToList();

        if (adminIds.Count == 0)
            return true;

        DialogResult answer = MessageBox.Show(this,
            $"{adminIds.Count} selected tweak(s) need administrator rights.\n\n" +
            "Restart Tool Hazard elevated? Unsaved work in other tabs is not affected.",
            "Game Boost", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
            return false;

        try
        {
            Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--gameboost")
            {
                UseShellExecute = true,
                Verb = "runas"
            });
            Log("Relaunched elevated - continue in the new window.");
        }
        catch (Exception ex)
        {
            Log("Elevation cancelled: " + ex.Message);
        }

        return false;
    }

    private List<string> SelectedIds()
    {
        _grid.EndEdit();
        var ids = new List<string>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is ChecklistItem item && row.Cells["Selected"].Value is true)
                ids.Add(item.Id);
        }
        return ids;
    }

    // ---------------------------------------------------------------------
    // Startup
    // ---------------------------------------------------------------------

    private Task RefreshStartupAsync()
    {
        _loadingStartup = true;
        try
        {
            _startup.Items.Clear();
            foreach (StartupEntry entry in GameBoostService.GetStartupEntries())
            {
                var item = new ListViewItem(entry.Name)
                {
                    Tag = entry,
                    Checked = entry.Enabled
                };
                item.SubItems.Add(entry.MachineWide ? "Machine" : "User");
                item.SubItems.Add(entry.Command);
                _startup.Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            Log("Startup list failed: " + ex.Message);
        }
        finally
        {
            _loadingStartup = false;
        }
        return Task.CompletedTask;
    }

    private void StartupItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        if (_loadingStartup || e.Item.Tag is not StartupEntry entry)
            return;
        if (e.Item.Checked == entry.Enabled)
            return;

        Log(GameBoostService.SetStartupEnabled(entry, e.Item.Checked));
        e.Item.Tag = entry with { Enabled = e.Item.Checked };
    }

    // ---------------------------------------------------------------------
    // Report
    // ---------------------------------------------------------------------

    private async Task GenerateReportAsync()
    {
        _progress.Visible = true;
        UseWaitCursor = true;
        try
        {
            _report.Text = await Task.Run(GameBoostService.BuildReport);
            Log("Report generated.");
        }
        catch (Exception ex)
        {
            Log("Report failed: " + ex.Message);
        }
        finally
        {
            _progress.Visible = false;
            UseWaitCursor = false;
        }
    }

    private void SaveReport()
    {
        if (string.IsNullOrWhiteSpace(_report.Text))
        {
            Log("Generate a report first.");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Text (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"tool-hazard-gameboost-{DateTime.Now:yyyyMMdd-HHmm}.txt"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        File.WriteAllText(dialog.FileName, _report.Text);
        Log("Saved: " + dialog.FileName);
    }

    // ---------------------------------------------------------------------
    // Game launch
    // ---------------------------------------------------------------------

    private void BrowseExe()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select the game executable"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _exeBox.Text = dialog.FileName;
    }

    private void LaunchGame()
    {
        if (string.IsNullOrWhiteSpace(_exeBox.Text))
        {
            BrowseExe();
            if (string.IsNullOrWhiteSpace(_exeBox.Text))
                return;
        }

        try
        {
            if (_chkGpu.Checked)
                Log(GameBoostService.SetGpuHighPerformance(_exeBox.Text.Trim()));

            Process? process = GameBoostService.LaunchGame(
                _exeBox.Text.Trim(), null, _chkPriority.Checked, _chkBackground.Checked);

            Log($"Launched {Path.GetFileName(_exeBox.Text)} (PID {process?.Id.ToString() ?? "?"}).");
        }
        catch (Exception ex)
        {
            Log("Launch failed: " + ex.Message);
        }
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static Button MakeButton(string text, int width, Action action)
    {
        var button = new Button { Text = text, Width = width, Height = 30, Margin = new Padding(0, 0, 6, 6) };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button MakeButton(string text, int width, Func<Task> action)
    {
        var button = new Button { Text = text, Width = width, Height = 30, Margin = new Padding(0, 0, 6, 6) };
        button.Click += async (_, _) => await action();
        return button;
    }

    private void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            if (InvokeRequired)
                Invoke(() => Append(message));
            else
                Append(message);
        }
        catch
        {
            // form is closing
        }
    }

    private void Append(string message)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}

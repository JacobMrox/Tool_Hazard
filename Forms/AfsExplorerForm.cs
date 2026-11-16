using AFSLib;
using AFSLib.Enums;
using System.Text;

namespace Tool_Hazard.Forms;

// AFS archive explorer for PS2 / Dreamcast / Code Veronica style archives.
// Open, browse entries, extract all (or one), replace an entry and rebuild the archive.
public sealed class AfsExplorerForm : Form
{
    private readonly ListView _list = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        GridLines = true
    };

    private readonly ToolStripStatusLabel _status = new();
    private readonly ToolStripMenuItem _openButton = new("Open...");
    private readonly ToolStripMenuItem _extractEntryButton = new("Extract Selected...");
    private readonly ToolStripMenuItem _extractButton = new("Extract All...");
    private readonly ToolStripMenuItem _replaceButton = new("Replace Entry...");
    private readonly ToolStripMenuItem _saveButton = new("Save As...");

    private AfsArchive? _archive;
    private string _sourcePath = "";
    private bool _isModified;

    public AfsExplorerForm()
    {
        Text = "AFS Explorer";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(760, 480);
        MinimumSize = new Size(560, 320);

        _list.Columns.AddRange(new ColumnHeader[]
        {
            new() { Text = "Entry", Width = 300 },
            new() { Text = "Size (bytes)", Width = 110, TextAlign = HorizontalAlignment.Right },
            new() { Text = "Archived", Width = 170 }
        });

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        _openButton.ShortcutKeys = Keys.Control | Keys.O;
        _extractEntryButton.ShortcutKeys = Keys.Control | Keys.E;
        _saveButton.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
        file.DropDownItems.AddRange(new ToolStripItem[]
        {
            _openButton,
            new ToolStripSeparator(),
            _extractEntryButton,
            _extractButton,
            new ToolStripSeparator(),
            _replaceButton,
            _saveButton,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Close", null, (_, _) => Close())
        });
        menu.Items.Add(file);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Extract Selected...", null, (_, _) => ExtractEntry());
        contextMenu.Items.Add("Replace Entry...", null, (_, _) => ReplaceEntry());
        contextMenu.Opening += (_, _) =>
        {
            var hasSelection = _archive is not null && _list.SelectedItems.Count > 0;
            contextMenu.Items[0].Enabled = hasSelection;
            contextMenu.Items[1].Enabled = hasSelection;
        };
        _list.ContextMenuStrip = contextMenu;

        var status = new StatusStrip();
        status.Items.Add(_status);

        Controls.Add(_list);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;

        _openButton.Click += (_, _) => PromptOpen();
        _extractEntryButton.Click += (_, _) => ExtractEntry();
        _extractButton.Click += (_, _) => ExtractAll();
        _replaceButton.Click += (_, _) => ReplaceEntry();
        _saveButton.Click += (_, _) => SaveAs();
        _list.DoubleClick += (_, _) => ExtractEntry();
        _list.SelectedIndexChanged += (_, _) => UpdateState();
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter || _list.SelectedItems.Count == 0)
                return;
            e.Handled = true;
            ExtractEntry();
        };
        FormClosing += AfsExplorerForm_FormClosing;
        Shown += (_, _) => PromptOpen();
        UpdateState();
    }

    private void PromptOpen()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open AFS archive",
            Filter = "AFS archives (*.afs)|*.afs|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            LoadAfs(dialog.FileName);
    }

    private void LoadAfs(string path)
    {
        try
        {
            if (!AfsArchive.TryFromFile(File.ReadAllBytes(path), out var archive))
                throw new InvalidDataException("The selected file is not a valid AFS archive.");

            _archive = archive;
            _sourcePath = path;
            _isModified = false;
            Populate();
            UpdateState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AFS open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Populate()
    {
        _list.Items.Clear();
        if (_archive is null)
            return;

        for (int i = 0; i < _archive.Files.Count; i++)
        {
            var file = _archive.Files[i];
            var item = new ListViewItem(file.Name);
            item.SubItems.Add(file.Data.Length.ToString("N0"));
            item.SubItems.Add(file.ArchiveTime?.ToString("g") ?? "-");
            item.Tag = i;
            _list.Items.Add(item);
        }
    }

    private void ExtractAll()
    {
        if (_archive is null)
            return;

        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the folder to extract the AFS entries into"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            UseWaitCursor = true;
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int written = 0;

            foreach (AFSLib.Structs.File file in _archive.Files)
            {
                var name = SanitizeFileName(file.Name, usedNames);
                File.WriteAllBytes(Path.Combine(dialog.SelectedPath, name), file.Data);
                written++;
            }

            UpdateState();
            MessageBox.Show(this,
                $"Extracted {written} entr{(written == 1 ? "y" : "ies")} to:\n\n{dialog.SelectedPath}",
                "AFS extract",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowOperationError("AFS extraction failed", ex);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ExtractEntry()
    {
        if (_archive is null || _list.SelectedItems.Count == 0)
            return;

        int index = (int)_list.SelectedItems[0].Tag!;
        var entry = _archive.Files[index];

        using var dialog = new SaveFileDialog
        {
            Title = "Extract AFS entry",
            FileName = SanitizeFileName(entry.Name, new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            Filter = "All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            File.WriteAllBytes(dialog.FileName, entry.Data);
            _status.Text = $"Extracted {entry.Name} to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            ShowOperationError("AFS entry extraction failed", ex);
        }
    }

    private void ReplaceEntry()
    {
        if (_archive is null || _list.SelectedItems.Count == 0)
            return;

        int index = (int)_list.SelectedItems[0].Tag!;

        using var dialog = new OpenFileDialog
        {
            Title = $"Select the replacement for {_archive.Files[index].Name}",
            Filter = "All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            _archive.Files[index].Data = File.ReadAllBytes(dialog.FileName);
            _isModified = true;
            Populate();
            if (index < _list.Items.Count)
            {
                _list.Items[index].Selected = true;
                _list.Items[index].EnsureVisible();
            }
            UpdateState();
        }
        catch (Exception ex)
        {
            ShowOperationError("AFS entry replacement failed", ex);
        }
    }

    private void SaveAs()
    {
        if (_archive is null)
            return;

        using var dialog = new SaveFileDialog
        {
            Title = "Save AFS archive",
            Filter = "AFS archive (*.afs)|*.afs",
            FileName = Path.GetFileName(_sourcePath)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            UseWaitCursor = true;

            // Keep the source layout: metadata-carrying archives are rebuilt with
            // metadata (entry names live there), metadata-free ones stay metadata-free.
            var mode = _archive.Files.Any(f => f.ArchiveTime.HasValue)
                ? FileCreationMode.CreateWithMetadata
                : FileCreationMode.CreateNoMetadata;
            var rebuilt = _archive.ToBytes(2048, mode);

            if (!AfsArchive.TryFromFile(rebuilt, out var verified) ||
                verified.Files.Count != _archive.Files.Count ||
                verified.Files.Where((file, index) =>
                    !file.Data.AsSpan().SequenceEqual(_archive.Files[index].Data)).Any())
            {
                throw new InvalidDataException("The rebuilt archive failed verification and was not saved.");
            }

            var fullTargetPath = Path.GetFullPath(dialog.FileName);
            var targetDirectory = Path.GetDirectoryName(fullTargetPath)!;
            Directory.CreateDirectory(targetDirectory);
            var temporaryPath = Path.Combine(
                targetDirectory,
                $".{Path.GetFileName(fullTargetPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllBytes(temporaryPath, rebuilt);
                File.Move(temporaryPath, fullTargetPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }

            _sourcePath = fullTargetPath;
            _isModified = false;
            UpdateState();
            _status.Text = $"Saved and verified {Path.GetFileName(fullTargetPath)}";
        }
        catch (Exception ex)
        {
            ShowOperationError("AFS save failed", ex);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private static string SanitizeFileName(string name, HashSet<string> usedNames)
    {
        var builder = new StringBuilder(name);
        foreach (char c in Path.GetInvalidFileNameChars())
            builder.Replace(c, '_');

        builder.Replace(Path.DirectorySeparatorChar, '_');
        builder.Replace(Path.AltDirectorySeparatorChar, '_');

        var sanitized = builder.ToString();
        if (sanitized.Length == 0)
            sanitized = "entry";

        if (!usedNames.Add(sanitized))
        {
            string candidate;
            int suffix = 1;
            do
            {
                candidate = $"{Path.GetFileNameWithoutExtension(sanitized)}_{suffix}{Path.GetExtension(sanitized)}";
                suffix++;
            }
            while (!usedNames.Add(candidate));
            sanitized = candidate;
        }

        return sanitized;
    }

    private void UpdateState()
    {
        bool hasArchive = _archive is not null;
        bool hasSelection = hasArchive && _list.SelectedItems.Count > 0;
        _extractButton.Enabled = hasArchive;
        _extractEntryButton.Enabled = hasSelection;
        _replaceButton.Enabled = hasSelection;
        _saveButton.Enabled = hasArchive;

        if (!hasArchive)
        {
            Text = "AFS Explorer";
            _status.Text = "No archive open";
            return;
        }

        var totalBytes = _archive!.Files.Sum(file => (long)file.Data.Length);
        var modifiedMarker = _isModified ? "*" : "";
        Text = $"{modifiedMarker}AFS Explorer - {Path.GetFileName(_sourcePath)} ({_archive.Files.Count} entries)";
        _status.Text = hasSelection
            ? $"Selected: {_list.SelectedItems[0].Text} ({_archive.Files[(int)_list.SelectedItems[0].Tag!].Data.Length:N0} bytes)"
            : $"{_archive.Files.Count:N0} entries - {FormatByteSize(totalBytes)} - {Path.GetFileName(_sourcePath)}";
    }

    private void AfsExplorerForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isModified)
            return;

        var result = MessageBox.Show(this,
            "This archive has unsaved replacements. Close without saving?",
            "Unsaved AFS changes",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (result == DialogResult.No)
            e.Cancel = true;
    }

    private static string FormatByteSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }

    private void ShowOperationError(string title, Exception ex) =>
        MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
}

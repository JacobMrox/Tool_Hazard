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
    private readonly ToolStripButton _openButton = new("Open...");
    private readonly ToolStripButton _extractButton = new("Extract All...");
    private readonly ToolStripButton _replaceButton = new("Replace Entry...");
    private readonly ToolStripButton _saveButton = new("Save As...");

    private AfsArchive? _archive;
    private string _sourcePath = "";

    public AfsExplorerForm()
    {
        Text = "AFS Explorer";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(760, 480);
        MinimumSize = new Size(560, 320);

        _list.Columns.AddRange(new ColumnHeader[]
        {
            new("Name") { Width = 280 },
            new("Size") { Width = 90 },
            new("Archived") { Width = 150 }
        });

        var menu = new MenuStrip();
        var file = new ToolStripDropDownButton("File");
        file.DropDownItems.AddRange(new ToolStripItem[]
        {
            _openButton,
            _extractButton,
            new ToolStripSeparator(),
            _replaceButton,
            _saveButton,
            new ToolStripSeparator(),
            new ToolStripButton("Close", null, (_, _) => Close())
        });
        menu.Items.Add(file);

        var status = new StatusStrip();
        status.Items.Add(_status);

        Controls.Add(_list);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;

        _openButton.Click += (_, _) => PromptOpen();
        _extractButton.Click += (_, _) => ExtractAll();
        _replaceButton.Click += (_, _) => ReplaceEntry();
        _saveButton.Click += (_, _) => SaveAs();
        _list.DoubleClick += (_, _) => ExtractEntry();
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
            Populate();

            Text = $"AFS Explorer - {Path.GetFileName(path)} ({archive.Files.Count} entries)";
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

        File.WriteAllBytes(dialog.FileName, entry.Data);
        UpdateState();
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

        _archive.Files[index].Data = File.ReadAllBytes(dialog.FileName);
        Populate();
        UpdateState();
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

        // Keep the source layout: metadata-carrying archives are rebuilt with
        // metadata (entry names live there), metadata-free ones stay metadata-free.
        var mode = _archive.Files.Any(f => f.ArchiveTime.HasValue)
            ? FileCreationMode.CreateWithMetadata
            : FileCreationMode.CreateNoMetadata;

        File.WriteAllBytes(dialog.FileName, _archive.ToBytes(2048, mode));
        _sourcePath = dialog.FileName;
        UpdateState();
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
        _extractButton.Enabled = hasArchive;
        _replaceButton.Enabled = hasArchive && _list.SelectedItems.Count > 0;
        _saveButton.Enabled = hasArchive;

        _status.Text = hasArchive
            ? $"{_archive!.Files.Count} entries - {Path.GetFileName(_sourcePath)}"
            : "No archive open";
    }
}

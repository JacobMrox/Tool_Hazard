using System.ComponentModel;
using Tool_Hazard.Biohazard.RE4;

namespace Tool_Hazard.Forms;

public sealed class TplExplorerForm : Form
{
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    private readonly ToolStripMenuItem _save = new("Save As...");
    private readonly ToolStripMenuItem _export = new("Export CSV...");
    private TplDocument? _document;

    public TplExplorerForm(string? initialPath = null)
    {
        Text = "RE4 TPL Explorer";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(900, 520);
        MinimumSize = new Size(700, 360);

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        var open = new ToolStripMenuItem("Open...");
        file.DropDownItems.AddRange(new ToolStripItem[] { open, _save, _export });
        menu.Items.Add(file);
        Controls.Add(_grid);
        Controls.Add(menu);
        MainMenuStrip = menu;

        open.Click += (_, _) => PromptOpen();
        _save.Click += (_, _) => SaveAs();
        _export.Click += (_, _) => ExportCsv();
        Shown += (_, _) => { if (initialPath != null) LoadTpl(initialPath); else PromptOpen(); };
        UpdateState();
    }

    private void PromptOpen()
    {
        using var dialog = new OpenFileDialog { Filter = "TPL files (*.tpl)|*.tpl|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadTpl(dialog.FileName);
    }

    private void LoadTpl(string path)
    {
        try
        {
            _document = TplExplorerService.Load(path);
            _grid.DataSource = new BindingList<TplEntryInfo>(_document.Entries);
            bool editable = _document.Variant == TplVariant.UhdReferenceTable;
            _grid.ReadOnly = !editable;
            foreach (DataGridViewColumn column in _grid.Columns)
                column.ReadOnly = !editable || column.DataPropertyName is nameof(TplEntryInfo.Index) or nameof(TplEntryInfo.HasPalette);
            Text = $"RE4 TPL Explorer - {_document.Variant} - {Path.GetFileName(path)} ({_document.Entries.Count} entries)";
            UpdateState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "TPL open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveAs()
    {
        if (_document == null) return;
        _grid.EndEdit();
        using var dialog = new SaveFileDialog { Filter = "TPL files (*.tpl)|*.tpl", FileName = Path.GetFileName(_document.SourcePath) };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        TplExplorerService.SaveUhd(_document, dialog.FileName);
    }

    private void ExportCsv()
    {
        if (_document == null) return;
        using var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = Path.GetFileNameWithoutExtension(_document.SourcePath) + ".csv" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var lines = new List<string> { "Index,Width,Height,PixelFormat,PackID,TextureID,HasPalette" };
        lines.AddRange(_document.Entries.Select(x => $"{x.Index},{x.Width},{x.Height},0x{x.PixelFormat:X8},0x{x.PackId:X8},{x.TextureId},{x.HasPalette}"));
        File.WriteAllLines(dialog.FileName, lines);
    }

    private void UpdateState()
    {
        _save.Enabled = _document?.Variant == TplVariant.UhdReferenceTable;
        _export.Enabled = _document != null;
    }
}

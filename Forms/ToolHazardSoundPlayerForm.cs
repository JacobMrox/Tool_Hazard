#nullable enable
using NAudio.Wave;
using NVorbis;
using Tool_Hazard.Biohazard.Audio;
using Tool_Hazard.Biohazard.SAP;

namespace Tool_Hazard.Forms;

public class ToolHazardSoundPlayerForm : Form
{
    private sealed class PlayerEntry
    {
        public required int Index { get; init; }
        public required string Name { get; init; }
        public required string Type { get; set; }
        public required int DataLength { get; set; }
        public required Func<byte[]> DataFactory { get; set; }
        public byte[] GetData() => DataFactory();
        public required bool IsOgg { get; set; }
        public required TimeSpan Duration { get; set; }
        public SampleHeader? VhSample { get; init; }
    }

    private readonly ListView _list = new();
    private readonly Button _play = new() { Text = "Play / Stop", Width = 135, Height = 30 };
    private readonly Button _seek = new() { Text = "Seek", Width = 135, Height = 30 };
    private readonly Button _export = new() { Text = "Export selected", Width = 135, Height = 30 };
    private readonly Button _extract = new() { Text = "Extract all", Width = 135, Height = 30 };
    private readonly Button _replace = new() { Text = "Replace selected", Width = 135, Height = 30 };
    private readonly Button _save = new() { Text = "Save bank", Width = 135, Height = 30 };
    private readonly TrackBar _seekBar = new() { Minimum = 0, Maximum = 1000, TickStyle = TickStyle.None, AutoSize = false, Height = 32 };
    private readonly Label _positionLabel = new() { Text = "00:00.000 / 00:00.000", AutoSize = true };
    private readonly System.Windows.Forms.Timer _positionTimer = new() { Interval = 200 };
    private bool _seeking;
    private VorbisReader? _vorbisReader;
    private WaveFileReader? _wavReader;
    private readonly ToolStripMenuItem _openMenu = new("Open...");
    private readonly ToolStripMenuItem _saveMenu = new("Save");
    private readonly ToolStripMenuItem _saveAsMenu = new("Save As...");
    private IWavePlayer? _waveOut;
    private WaveStream? _waveStream;
    private List<PlayerEntry> _entries = new();
    private string? _sourcePath;
    private string? _pairedVbPath;
    private string _containerType = string.Empty;
    private SapBank? _sapBank;
    private XwbBank? _xwbBank;
    private readonly Dictionary<int, byte[]> _xwbReplacements = new();
    private string? _workingVhPath;
    private string? _workingVbPath;
    private string? _workingDirectory;
    private bool _dirty;

    public ToolHazardSoundPlayerForm(string? initialPath = null)
    {
        Text = "Tool Hazard Sound Player";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(680, 390);
        Size = new Size(820, 520);

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        _openMenu.ShortcutKeys = Keys.Control | Keys.O;
        _saveMenu.ShortcutKeys = Keys.Control | Keys.S;
        _saveAsMenu.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
        file.DropDownItems.Add(_openMenu);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(_saveMenu);
        file.DropDownItems.Add(_saveAsMenu);
        file.DropDownItems.Add(new ToolStripSeparator());
        var exitMenu = new ToolStripMenuItem("Exit");
        file.DropDownItems.Add(exitMenu);
        menu.Items.Add(file);
        MainMenuStrip = menu;

        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.GridLines = true;
        _list.MultiSelect = false;
        _list.Columns.Add("Index", 60);
        _list.Columns.Add("Name", 210);
        _list.Columns.Add("Type", 100);
        _list.Columns.Add("Size", 110);
        _list.Columns.Add("Duration", 100);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 160,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(8),
            WrapContents = false
        };
        buttons.Controls.AddRange(new Control[] { _play, _seek, _replace, _save, _export, _extract });

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(4) };
        _positionLabel.Dock = DockStyle.Top;
        _seekBar.Dock = DockStyle.Fill;
        bottom.Controls.Add(_seekBar);
        bottom.Controls.Add(_positionLabel);
        content.Controls.Add(bottom);
        content.Controls.Add(_list);
        content.Controls.Add(buttons);
        Controls.Add(content);
        Controls.Add(menu);

        _openMenu.Click += (_, _) => PromptOpen();
        _saveMenu.Click += (_, _) => Save(false);
        _saveAsMenu.Click += (_, _) => Save(true);
        exitMenu.Click += (_, _) => Close();
        _play.Click += (_, _) => TogglePlayback();
        _seek.Click += (_, _) => SeekToBar();
        _seekBar.Scroll += (_, _) =>
        {
            if (_waveOut == null) return;
            PlayerEntry? entry = Selected;
            if (entry == null || entry.Duration.TotalSeconds <= 0) return;
            _seeking = true;
            SeekTo(TimeSpan.FromSeconds(_seekBar.Value / 1000.0 * entry.Duration.TotalSeconds));
            UpdatePosition();
            _seeking = false;
        };
        _positionTimer.Tick += (_, _) => UpdatePosition();
        _export.Click += (_, _) => ExportSelected();
        _extract.Click += (_, _) => ExtractAll();
        _replace.Click += (_, _) => ReplaceSelected();
        _save.Click += (_, _) => Save(false);
        _list.DoubleClick += (_, _) => TogglePlayback();
        _list.SelectedIndexChanged += (_, _) => UpdateUi();

        Shown += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(initialPath))
                LoadPath(initialPath);
            else
                PromptOpen();
        };
        UpdateUi();
    }

    private PlayerEntry? Selected => _list.SelectedItems.Count == 0
        ? null
        : _list.SelectedItems[0].Tag as PlayerEntry;

    private void PromptOpen()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open Biohazard audio bank",
            Filter = "Supported audio banks (*.sap;*.xwb;*.vh)|*.sap;*.xwb;*.vh|SAP (*.sap)|*.sap|XACT WaveBank (*.xwb)|*.xwb|PS1 VAB header (*.vh)|*.vh|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            LoadPath(dialog.FileName);
    }

    private void LoadPath(string path)
    {
        try
        {
            if (!ConfirmDiscardChanges()) return;
            StopPlayback();
            CleanupWorkingVh();
            _sapBank = null;
            _xwbBank = null;
            _xwbReplacements.Clear();
            string extension = Path.GetExtension(path).ToLowerInvariant();
            _entries = extension switch
            {
                ".sap" => LoadSap(path),
                ".xwb" => LoadXwb(path),
                ".vh" => LoadVh(path),
                _ => throw new NotSupportedException("Supported formats are SAP, XWB, and paired VH/VB banks.")
            };
            _sourcePath = path;
            _dirty = false;
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Open failed:\n{ex.Message}", "Tool Hazard Sound Player", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private List<PlayerEntry> LoadSap(string path)
    {
        SapBank bank = SapService.Load(path);
        _sapBank = bank;
        _containerType = "SAP";
        _pairedVbPath = null;
        return bank.Entries.OrderBy(x => x.Index).Select(entry => new PlayerEntry
        {
            Index = entry.Index,
            Name = entry.Kind == SapEntryKind.Ogg ? Path.GetFileNameWithoutExtension(path) : $"{entry.Index:D2}",
            Type = entry.Kind == SapEntryKind.Ogg ? "OGG" : "WAV",
            DataLength = entry.Data.Length,
            DataFactory = () => entry.Data,
            IsOgg = entry.Kind == SapEntryKind.Ogg,
            Duration = GetDuration(entry.Data, entry.Kind == SapEntryKind.Ogg)
        }).ToList();
    }

    private List<PlayerEntry> LoadXwb(string path)
    {
        XwbBank bank = XwbService.Load(path);
        _xwbBank = bank;
        _containerType = "XWB";
        _pairedVbPath = null;
        return bank.Entries.Select(entry => new PlayerEntry
        {
            Index = entry.Index,
            Name = entry.Name,
            Type = $"PCM {entry.BitsPerSample}-bit / {entry.SampleRate:N0} Hz",
            DataLength = entry.DataLength + 44,
            DataFactory = entry.GetWaveData,
            IsOgg = false,
            Duration = entry.Duration
        }).ToList();
    }

    private List<PlayerEntry> LoadVh(string vhPath)
    {
        string suggested = Path.ChangeExtension(vhPath, ".vb");
        string vbPath = suggested;
        if (!File.Exists(vbPath))
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select the corresponding VB file",
                Filter = "Voice body (*.vb)|*.vb|All files (*.*)|*.*",
                FileName = Path.GetFileName(suggested),
                InitialDirectory = Path.GetDirectoryName(vhPath)
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                throw new OperationCanceledException("A VH file requires its matching VB file.");
            vbPath = dialog.FileName;
        }

        var service = new PS1AudioService();
        _workingDirectory = Path.Combine(Path.GetTempPath(), "ToolHazardAudio_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workingDirectory);
        _workingVhPath = Path.Combine(_workingDirectory, Path.GetFileName(vhPath));
        _workingVbPath = Path.Combine(_workingDirectory, Path.GetFileName(vbPath));
        File.Copy(vhPath, _workingVhPath);
        File.Copy(vbPath, _workingVbPath);
        List<SampleHeader> samples = service.ReadVhFile(_workingVhPath, _workingVbPath);
        _containerType = "VH/VB";
        _pairedVbPath = vbPath;
        return samples.Select(sample => new PlayerEntry
        {
            Index = sample.Index,
            Name = $"VAG {sample.VabNumber:D3}",
            Type = $"PS1 ADPCM / {sample.SampleRate:N0} Hz",
            DataLength = sample.PcmSampleCount * sizeof(short) + 44,
            DataFactory = () => service.CreateSampleWav(_workingVbPath!, sample),
            IsOgg = false,
            Duration = sample.Duration,
            VhSample = sample
        }).ToList();
    }

    private void RefreshList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (PlayerEntry entry in _entries)
        {
            var item = new ListViewItem(entry.Index.ToString("D4"));
            item.SubItems.Add(entry.Name);
            item.SubItems.Add(entry.Type);
            item.SubItems.Add(entry.DataLength.ToString("N0"));
            item.SubItems.Add(FormatDuration(entry.Duration));
            item.Tag = entry;
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        Text = $"Tool Hazard Sound Player - {_containerType} - {Path.GetFileName(_sourcePath)} ({_entries.Count} entries){(_dirty ? " *" : string.Empty)}";
        UpdateUi();
    }

    private void UpdateUi()
    {
        bool selected = Selected != null;
        _play.Enabled = selected;
        _seek.Enabled = _waveOut != null;
        _export.Enabled = selected;
        _extract.Enabled = _entries.Count > 0;
        _replace.Enabled = selected;
        _save.Enabled = _sourcePath != null && (_dirty || _containerType == "VH/VB");
        _saveMenu.Enabled = _sourcePath != null && (_dirty || _containerType == "VH/VB");
        _saveAsMenu.Enabled = _sourcePath != null;
    }

    private void TogglePlayback()
    {
        if (_waveOut != null)
        {
            StopPlayback();
            return;
        }
        PlayerEntry? entry = Selected;
        if (entry == null) return;

        try
        {
            byte[] data = entry.GetData();
            if (entry.IsOgg)
            {
                _vorbisReader = new VorbisReader(new MemoryStream(data, false), closeOnDispose: true);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(new VorbisProvider(_vorbisReader));
            }
            else
            {
                _wavReader = new WaveFileReader(new MemoryStream(data, false));
                _waveStream = WaveFormatConversionStream.CreatePcmStream(_wavReader);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_waveStream);
            }
            _waveOut.PlaybackStopped += (_, _) => { if (!IsDisposed) BeginInvoke(new Action(StopPlayback)); };
            _waveOut.Play();
            _play.Text = "Stop";
            _seekBar.Value = 0;
            _positionTimer.Start();
            UpdateUi();
        }
        catch (Exception ex)
        {
            StopPlayback();
            MessageBox.Show(this, $"Playback failed:\n{ex.Message}", "Tool Hazard Sound Player", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopPlayback()
    {
        _positionTimer.Stop();
        try { _waveOut?.Stop(); } catch { }
        _waveStream?.Dispose();
        _waveStream = null;
        _wavReader?.Dispose();
        _wavReader = null;
        _vorbisReader?.Dispose();
        _vorbisReader = null;
        _waveOut?.Dispose();
        _waveOut = null;
        _play.Text = "Play / Stop";
        _seekBar.Value = 0;
        _positionLabel.Text = "00:00.000 / 00:00.000";
        UpdateUi();
    }

    private void SeekToBar()
    {
        PlayerEntry? entry = Selected;
        if (entry == null || _waveOut == null)
        {
            MessageBox.Show(this, "Start playback before seeking.", "Tool Hazard Sound Player", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        double total = entry.Duration.TotalSeconds;
        if (total <= 0) return;

        using var dialog = new Form
        {
            Text = "Seek",
            Width = 300,
            Height = 130,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var label = new Label { Text = $"Seek to (0 - {total:0.###} seconds):", Left = 12, Top = 12, AutoSize = true };
        var box = new TextBox { Left = 12, Top = 36, Width = 260, Text = (_seekBar.Value / 1000.0 * total).ToString("0.###") };
        var ok = new Button { Text = "OK", Left = 112, Top = 64, Width = 75, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 197, Top = 64, Width = 75, DialogResult = DialogResult.Cancel };
        dialog.Controls.AddRange(new Control[] { label, box, ok, cancel });
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!double.TryParse(box.Text, out double seconds))
        {
            MessageBox.Show(this, "Invalid seek position.", "Tool Hazard Sound Player", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        seconds = Math.Clamp(seconds, 0, total);
        int barValue = (int)(seconds / total * 1000.0);
        _seekBar.Value = Math.Clamp(barValue, 0, 1000);
        SeekTo(TimeSpan.FromSeconds(seconds));
        UpdatePosition();
    }

    private void SeekTo(TimeSpan position)
    {
        try
        {
            if (_vorbisReader != null)
                _vorbisReader.TimePosition = position;
            else if (_waveStream != null)
                _waveStream.CurrentTime = position;
            else if (_wavReader != null)
                _wavReader.CurrentTime = position;
        }
        catch { }
    }

    private void UpdatePosition()
    {
        if (_waveOut == null)
        {
            _positionTimer.Stop();
            return;
        }
        PlayerEntry? entry = Selected;
        TimeSpan duration = entry?.Duration ?? TimeSpan.Zero;
        TimeSpan current = TimeSpan.Zero;
        try
        {
            if (_vorbisReader != null) current = _vorbisReader.TimePosition;
            else if (_waveStream != null) current = _waveStream.CurrentTime;
            else if (_wavReader != null) current = _wavReader.CurrentTime;
        }
        catch { }
        if (duration > TimeSpan.Zero)
        {
            int value = (int)(current.TotalSeconds / duration.TotalSeconds * 1000.0);
            value = Math.Clamp(value, 0, 1000);
            if (!_seeking) _seekBar.Value = value;
        }
        _positionLabel.Text = $"{FormatDuration(current)} / {FormatDuration(duration)}";
    }

    private void ExportSelected()
    {
        PlayerEntry? entry = Selected;
        if (entry == null) return;
        string ext = entry.IsOgg ? "ogg" : "wav";
        using var dialog = new SaveFileDialog
        {
            Filter = entry.IsOgg ? "Ogg Vorbis (*.ogg)|*.ogg" : "Wave audio (*.wav)|*.wav",
            FileName = SafeName(entry.Name) + "." + ext,
            DefaultExt = ext
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            File.WriteAllBytes(dialog.FileName, entry.GetData());
    }

    private void ExtractAll()
    {
        using var dialog = new FolderBrowserDialog { Description = "Select a folder for the extracted audio" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        foreach (PlayerEntry entry in _entries)
        {
            string ext = entry.IsOgg ? "ogg" : "wav";
            string name = $"{entry.Index:D4}_{SafeName(entry.Name)}.{ext}";
            File.WriteAllBytes(Path.Combine(dialog.SelectedPath, name), entry.GetData());
        }
        MessageBox.Show(this, $"Extracted {_entries.Count} audio entries.", "Tool Hazard Sound Player", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ReplaceSelected()
    {
        PlayerEntry? entry = Selected;
        if (entry == null) return;
        using var dialog = new OpenFileDialog
        {
            Title = $"Replace {entry.Name}",
            Filter = "Wave audio (*.wav)|*.wav|Ogg Vorbis (*.ogg)|*.ogg|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            StopPlayback();
            byte[] replacement = File.ReadAllBytes(dialog.FileName);
            if (_containerType == "SAP")
            {
                if (_sapBank == null) throw new InvalidOperationException("No SAP bank is loaded.");
                SapEntry sapEntry = _sapBank.Entries.Single(x => x.Index == entry.Index);
                bool isOgg = SapService.IsOgg(replacement);
                bool isWav = SapService.IsRiffWav(replacement);
                if (!isOgg && !isWav)
                    throw new InvalidDataException("Replacement must be a RIFF WAV or an Ogg Vorbis file.");

                if (isOgg)
                {
                    if (_sapBank.Entries.Count > 1)
                        throw new InvalidDataException("OGG SAPs support a single audio track. This bank has multiple entries.");
                    if (MessageBox.Show(this, "OGG SAPs are only supported by Classic Rebirth installations. The original game requires WAV banks. Continue?",
                            "Tool Hazard Sound Player", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        return;
                    _sapBank.PayloadKind = SapPayloadKind.OggSingle;
                    sapEntry.Kind = SapEntryKind.Ogg;
                }
                else
                {
                    if (_sapBank.PayloadKind == SapPayloadKind.OggSingle)
                        _sapBank.PayloadKind = SapPayloadKind.WavBank;
                    sapEntry.Kind = SapEntryKind.Wav;
                }

                sapEntry.Data = replacement;
                entry.IsOgg = isOgg;
                entry.Type = isOgg ? "OGG" : "WAV";
            }
            else if (_containerType == "XWB")
            {
                if (_xwbBank == null) throw new InvalidOperationException("No XWB bank is loaded.");
                _xwbReplacements[entry.Index] = replacement;
            }
            else if (_containerType == "VH/VB")
            {
                if (_workingVhPath == null || _workingVbPath == null || entry.VhSample == null)
                    throw new InvalidOperationException("No VH/VB bank is loaded.");
                var service = new PS1AudioService();
                service.ReplaceSampleWithWav(_workingVhPath, _workingVbPath, entry.VhSample, dialog.FileName);
                ReloadWorkingVh(entry.Index);
                entry = _entries.Single(x => x.Index == entry.Index);
                replacement = entry.GetData();
            }

            entry.DataFactory = () => replacement;
            entry.DataLength = replacement.Length;
            entry.Duration = GetDuration(replacement, entry.IsOgg);
            _dirty = true;
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Replacement failed:\n{ex.Message}", "Tool Hazard Sound Player", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ReloadWorkingVh(int selectedIndex)
    {
        if (_workingVhPath == null || _workingVbPath == null) return;
        var service = new PS1AudioService();
        _entries = service.ReadVhFile(_workingVhPath, _workingVbPath).Select(sample => new PlayerEntry
        {
            Index = sample.Index,
            Name = $"VAG {sample.VabNumber:D3}",
            Type = $"PS1 ADPCM / {sample.SampleRate:N0} Hz",
            DataLength = sample.PcmSampleCount * sizeof(short) + 44,
            DataFactory = () => service.CreateSampleWav(_workingVbPath, sample),
            IsOgg = false,
            Duration = sample.Duration,
            VhSample = sample
        }).ToList();
    }

    private void Save(bool saveAs)
    {
        if (_sourcePath == null) return;
        string destination = _sourcePath;
        if (saveAs)
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Save audio bank as",
                Filter = _containerType switch
                {
                    "SAP" => "SAP bank (*.sap)|*.sap",
                    "XWB" => "XACT WaveBank (*.xwb)|*.xwb",
                    _ => "VAB header (*.vh)|*.vh"
                },
                FileName = Path.GetFileName(_sourcePath)
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            destination = dialog.FileName;
        }

        try
        {
            StopPlayback();
            if (_containerType == "SAP" && _sapBank != null)
                SapService.Save(_sapBank, destination);
            else if (_containerType == "XWB" && _xwbBank != null)
                XwbService.Save(_xwbBank, destination, _xwbReplacements);
            else if (_containerType == "VH/VB" && _workingVhPath != null && _workingVbPath != null)
            {
                string destinationVb = Path.ChangeExtension(destination, ".vb");
                File.Copy(_workingVhPath, destination, true);
                File.Copy(_workingVbPath, destinationVb, true);
                _pairedVbPath = destinationVb;
            }
            else throw new InvalidOperationException("No editable audio bank is loaded.");

            _sourcePath = destination;
            _dirty = false;
            if (_containerType == "XWB")
            {
                _xwbReplacements.Clear();
                _xwbBank = XwbService.Load(destination);
                _entries = LoadXwb(destination);
            }
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Save failed:\n{ex.Message}", "Tool Hazard Sound Player", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_dirty) return true;
        DialogResult result = MessageBox.Show(this, "Save changes before continuing?", "Tool Hazard Sound Player",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (result == DialogResult.Cancel) return false;
        if (result == DialogResult.Yes)
        {
            Save(false);
            return !_dirty;
        }
        return true;
    }

    private void CleanupWorkingVh()
    {
        if (_workingDirectory != null && Directory.Exists(_workingDirectory))
        {
            try { Directory.Delete(_workingDirectory, true); } catch { }
        }
        _workingDirectory = null;
        _workingVhPath = null;
        _workingVbPath = null;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        StopPlayback();
        CleanupWorkingVh();
        base.OnFormClosed(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!ConfirmDiscardChanges()) e.Cancel = true;
        base.OnFormClosing(e);
    }

    private static TimeSpan GetDuration(byte[] data, bool ogg)
    {
        try
        {
            using var stream = new MemoryStream(data, false);
            if (ogg)
            {
                using var reader = new VorbisReader(stream, false);
                return reader.TotalTime;
            }
            using var wave = new WaveFileReader(stream);
            return wave.TotalTime;
        }
        catch { return TimeSpan.Zero; }
    }

    private static string FormatDuration(TimeSpan value) => $"{(int)value.TotalMinutes:00}:{value.Seconds:00}.{value.Milliseconds:000}";
    private static string SafeName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(value) ? "audio" : value;
    }

    private sealed class VorbisProvider : IWaveProvider
    {
        private readonly VorbisReader _reader;
        private readonly float[] _samples = new float[8192];
        public VorbisProvider(VorbisReader reader)
        {
            _reader = reader;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(reader.SampleRate, reader.Channels);
        }
        public WaveFormat WaveFormat { get; }
        public int Read(byte[] buffer, int offset, int count)
        {
            int samplesToRead = count / sizeof(float);
            int totalRead = 0;
            while (totalRead < samplesToRead)
            {
                int chunk = Math.Min(_samples.Length, samplesToRead - totalRead);
                int read = _reader.ReadSamples(_samples, 0, chunk);
                if (read <= 0) break;
                Buffer.BlockCopy(_samples, 0, buffer, offset + totalRead * sizeof(float), read * sizeof(float));
                totalRead += read;
            }
            return totalRead * sizeof(float);
        }
    }
}

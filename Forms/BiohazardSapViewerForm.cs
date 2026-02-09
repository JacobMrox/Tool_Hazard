#nullable enable
using NAudio.Wave;
using NVorbis;
using Tool_Hazard.Biohazard.SAP;

namespace Tool_Hazard.Forms
{
    public partial class BiohazardSapViewerForm : Form
    {
        private SapBank? _bank;

        private IWavePlayer? _waveOut;
        private WaveStream? _waveStream;

        public BiohazardSapViewerForm()
        {
            InitializeComponent();

            // wire up handlers
            listView1.SelectedIndexChanged += listView1_SelectedIndexChanged;

            // Make sure ListView behaves the way we need
            listView1.FullRowSelect = true;
            listView1.MultiSelect = false;

            UpdateUiState();
        }

        // -----------------------------
        // ListView helpers
        // -----------------------------

        private SapEntry? GetSelectedEntry()
        {
            if (_bank == null) return null;
            if (listView1.SelectedItems.Count == 0) return null;
            return listView1.SelectedItems[0].Tag as SapEntry;
        }

        private void RefreshList()
        {
            listView1.Items.Clear();

            if (_bank == null)
            {
                Text = "SAP Viewer";
                UpdateUiState();
                return;
            }

            foreach (var entry in _bank.Entries.OrderBy(x => x.Index))
            {
                var kindText = entry.Kind == SapEntryKind.Wav ? "WAV" : "OGG";

                //Get duration of sample
                var duration = GetDurationText(entry);

                // This fills: [Index] [Type] [Size]
                var lvi = new ListViewItem(entry.Index.ToString("00"));
                lvi.SubItems.Add(kindText);
                lvi.SubItems.Add(entry.Data.Length.ToString("N0"));
                lvi.SubItems.Add(duration);
                lvi.Tag = entry;

                listView1.Items.Add(lvi);
            }

            Text = $"{_bank.DisplayName} - SAP Viewer" + (_bank.IsDirty ? " *" : "");
            UpdateUiState();
        }

        private void UpdateUiState()
        {
            bool hasBank = _bank != null;
            bool hasSelection = hasBank && listView1.SelectedItems.Count > 0;

            button1.Enabled = hasSelection;
            button2.Enabled = hasSelection;
            button3.Enabled = hasSelection;

            button4.Enabled = hasBank;
            button5.Enabled = hasBank;
            saveToolStripMenuItem.Enabled = hasBank;

            button6.Enabled = hasBank && _bank!.PayloadKind == SapPayloadKind.WavBank;
        }

        private void MarkDirty()
        {
            if (_bank == null) return;
            _bank.IsDirty = true;
            Text = $"{_bank.DisplayName} - SAP Viewer *";
            UpdateUiState();
        }

        private bool ConfirmLoseChanges()
        {
            if (_bank == null || !_bank.IsDirty) return true;

            var r = MessageBox.Show(
                "You have unsaved changes. Discard them?",
                "SAP Viewer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            return r == DialogResult.Yes;
        }

        private static string GetDurationText(SapEntry entry)
        {
            try
            {
                TimeSpan t;

                if (entry.Kind == SapEntryKind.Wav)
                {
                    using var ms = new MemoryStream(entry.Data, writable: false);
                    using var r = new WaveFileReader(ms);
                    using var pcm = WaveFormatConversionStream.CreatePcmStream(r);
                    t = pcm.TotalTime;
                }
                else
                {
                    using var ms = new MemoryStream(entry.Data, writable: false);
                    using var v = new NVorbis.VorbisReader(ms, closeOnDispose: false);
                    t = v.TotalTime;
                }

                // mm:ss.mmm
                return $"{(int)t.TotalMinutes:00}:{t.Seconds:00}.{t.Milliseconds:000}";
            }
            catch
            {
                return "";
            }
        }

        // -----------------------------
        // Playback: WAV via NAudio, OGG via NVorbis decode -> WaveProvider
        // -----------------------------
        private void StopPlayback()
        {
            try { _waveOut?.Stop(); } catch { }

            _waveStream?.Dispose();
            _waveStream = null;

            _waveOut?.Dispose();
            _waveOut = null;

            button1.Text = "Play";
        }

        private void PlayEntry(SapEntry entry)
        {
            StopPlayback();

            if (entry.Kind == SapEntryKind.Wav)
            {
                var ms = new MemoryStream(entry.Data, writable: false);
                var reader = new WaveFileReader(ms);

                // Convert anything (PCM/ADPCM/etc.) into a WaveStream WaveOut likes
                WaveStream pcmStream = WaveFormatConversionStream.CreatePcmStream(reader);
                var blockAligned = new BlockAlignReductionStream(pcmStream);

                _waveStream = blockAligned;
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_waveStream);
            }
            else
            {
                // OGG: decode with NVorbis -> float PCM -> NAudio playback
                var ms = new MemoryStream(entry.Data, writable: false);
                var vorbis = new VorbisReader(ms, closeOnDispose: true);

                var provider = new VorbisWaveProvider(vorbis);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(provider);
            }

            _waveOut.PlaybackStopped += (_, __) =>
            {
                if (!IsDisposed)
                {
                    BeginInvoke(new Action(StopPlayback));
                }
            };

            _waveOut.Play();
            button1.Text = "Stop";
        }

        /// <summary>
        /// Minimal adapter: NVorbis -> NAudio WaveProvider (float PCM)
        /// </summary>
        private sealed class VorbisWaveProvider : IWaveProvider
        {
            private readonly VorbisReader _reader;
            private readonly float[] _buffer;

            public VorbisWaveProvider(VorbisReader reader, int bufferFrames = 4096)
            {
                _reader = reader;
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(reader.SampleRate, reader.Channels);
                _buffer = new float[bufferFrames * reader.Channels];
            }

            public WaveFormat WaveFormat { get; }

            public int Read(byte[] buffer, int offset, int count)
            {
                int floatsRequested = count / sizeof(float);
                if (floatsRequested <= 0) return 0;

                int floatsReadTotal = 0;

                while (floatsReadTotal < floatsRequested)
                {
                    int need = Math.Min(_buffer.Length, floatsRequested - floatsReadTotal);
                    int got = _reader.ReadSamples(_buffer, 0, need);
                    if (got <= 0) break;

                    Buffer.BlockCopy(_buffer, 0, buffer, offset + floatsReadTotal * sizeof(float), got * sizeof(float));
                    floatsReadTotal += got;
                }

                return floatsReadTotal * sizeof(float);
            }
        }

        // -----------------------------
        // Saving
        // -----------------------------
        private void SaveCommon(bool saveAs)
        {
            if (_bank == null) return;

            string? outPath = _bank.SourcePath;

            if (saveAs || string.IsNullOrWhiteSpace(outPath))
            {
                using var sfd = new SaveFileDialog
                {
                    Filter = "SAP files (*.sap)|*.sap|All files (*.*)|*.*",
                    FileName = string.IsNullOrWhiteSpace(_bank.SourcePath) ? "new.sap" : Path.GetFileName(_bank.SourcePath)
                };
                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return;

                outPath = sfd.FileName;
            }

            try
            {
                SapService.Save(_bank, outPath!);
                RefreshList();
                MessageBox.Show("SAP saved successfully.", "SAP Viewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save SAP:\n{ex.Message}", "SAP Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // -----------------------------
        // Your handlers
        // -----------------------------
        private void button1_Click(object sender, EventArgs e)
        {
            var entry = GetSelectedEntry();
            if (entry == null) return;

            if (_waveOut != null)
            {
                StopPlayback();
                return;
            }

            try
            {
                PlayEntry(entry);
            }
            catch (Exception ex)
            {
                StopPlayback();
                MessageBox.Show($"Playback failed:\n{ex.Message}", "SAP Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var entry = GetSelectedEntry();
            if (entry == null || _bank == null) return;

            var ext = entry.Kind == SapEntryKind.Wav ? "wav" : "ogg";
            var baseName = Path.GetFileNameWithoutExtension(_bank.SourcePath ?? "sap");
            var defaultName = entry.Kind == SapEntryKind.Ogg
                ? $"{baseName}.ogg"
                : $"{baseName}.{entry.Index:00}.wav";

            using var sfd = new SaveFileDialog
            {
                Filter = entry.Kind == SapEntryKind.Wav
                    ? "WAV files (*.wav)|*.wav|All files (*.*)|*.*"
                    : "OGG files (*.ogg)|*.ogg|All files (*.*)|*.*",
                FileName = defaultName,
                DefaultExt = ext
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                File.WriteAllBytes(sfd.FileName, entry.Data);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "SAP Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            var entry = GetSelectedEntry();
            if (entry == null || _bank == null) return;

            using var ofd = new OpenFileDialog
            {
                Filter = entry.Kind == SapEntryKind.Wav
                    ? "WAV files (*.wav)|*.wav|All files (*.*)|*.*"
                    : "OGG files (*.ogg)|*.ogg|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var newData = File.ReadAllBytes(ofd.FileName);

                if (entry.Kind == SapEntryKind.Wav && !SapService.IsRiffWav(newData))
                    throw new InvalidDataException("Selected file is not a valid RIFF WAV.");

                if (entry.Kind == SapEntryKind.Ogg && !SapService.IsOgg(newData))
                    throw new InvalidDataException("Selected file is not a valid OGG stream (missing OggS).");

                entry.Data = newData;

                if (_bank.PayloadKind == SapPayloadKind.OggSingle)
                {
                    _bank.Entries.Clear();
                    entry.Index = 0;
                    _bank.Entries.Add(entry);
                }

                MarkDirty();
                RefreshList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Replace failed:\n{ex.Message}", "SAP Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (_bank == null) return;

            using var fbd = new FolderBrowserDialog
            {
                Description = "Select a folder to extract SAP contents"
            };

            if (fbd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var baseName = Path.GetFileNameWithoutExtension(_bank.SourcePath ?? "sap");

                if (_bank.PayloadKind == SapPayloadKind.OggSingle)
                {
                    var outPath = Path.Combine(fbd.SelectedPath, $"{baseName}.ogg");
                    File.WriteAllBytes(outPath, _bank.Entries[0].Data);
                }
                else
                {
                    foreach (var entry in _bank.Entries.OrderBy(x => x.Index))
                    {
                        var outPath = Path.Combine(fbd.SelectedPath, $"{baseName}.{entry.Index:00}.wav");
                        File.WriteAllBytes(outPath, entry.Data);
                    }
                }

                MessageBox.Show("Extract completed.", "SAP Viewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Extract failed:\n{ex.Message}", "SAP Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            SaveCommon(saveAs: false);
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!ConfirmLoseChanges())
                return;

            using var ofd = new OpenFileDialog
            {
                Filter = "SAP files (*.sap)|*.sap|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                StopPlayback();
                _bank = SapService.Load(ofd.FileName);
                RefreshList();
            }
            catch (Exception ex)
            {
                _bank = null;
                RefreshList();
                MessageBox.Show($"Open failed:\n{ex.Message}", "SAP Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveCommon(saveAs: false);
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!ConfirmLoseChanges())
                return;

            var r = MessageBox.Show(
                "Create which SAP type?\n\nYes = WAV bank\nNo = OGG SAP (Classic Rebirth)\nCancel = abort",
                "New SAP",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (r == DialogResult.Cancel)
                return;

            try
            {
                StopPlayback();

                if (r == DialogResult.Yes)
                {
                    using var ofd = new OpenFileDialog
                    {
                        Filter = "WAV files (*.wav)|*.wav|All files (*.*)|*.*",
                        Multiselect = true,
                        Title = "Select one or more WAV files"
                    };
                    if (ofd.ShowDialog(this) != DialogResult.OK)
                        return;

                    var entries = ofd.FileNames.Select((fn, i) =>
                    {
                        var bytes = File.ReadAllBytes(fn);
                        if (!SapService.IsRiffWav(bytes))
                            throw new InvalidDataException($"Not a RIFF WAV: {Path.GetFileName(fn)}");

                        return new SapEntry { Index = i, Kind = SapEntryKind.Wav, Data = bytes };
                    }).ToList();

                    _bank = new SapBank
                    {
                        SourcePath = null,
                        Header = 1UL,
                        PayloadKind = SapPayloadKind.WavBank,
                        IsDirty = true
                    };
                    _bank.Entries.AddRange(entries);
                }
                else
                {
                    using var ofd = new OpenFileDialog
                    {
                        Filter = "OGG files (*.ogg)|*.ogg|All files (*.*)|*.*",
                        Multiselect = false,
                        Title = "Select one OGG file"
                    };
                    if (ofd.ShowDialog(this) != DialogResult.OK)
                        return;

                    var bytes = File.ReadAllBytes(ofd.FileName);
                    if (!SapService.IsOgg(bytes))
                        throw new InvalidDataException("Selected file is not a valid OGG stream (missing OggS).");

                    _bank = new SapBank
                    {
                        SourcePath = null,
                        Header = 1UL,
                        PayloadKind = SapPayloadKind.OggSingle,
                        IsDirty = true
                    };
                    _bank.Entries.Add(new SapEntry { Index = 0, Kind = SapEntryKind.Ogg, Data = bytes });
                }

                RefreshList();
                SaveCommon(saveAs: true);
            }
            catch (Exception ex)
            {
                _bank = null;
                RefreshList();
                MessageBox.Show($"New SAP failed:\n{ex.Message}", "SAP Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (_bank == null) return;
            if (_bank.PayloadKind != SapPayloadKind.WavBank)
            {
                MessageBox.Show("Add sample is only supported for WAV bank SAPs.", "SAP Viewer",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var ofd = new OpenFileDialog
            {
                Filter = "WAV files (*.wav)|*.wav|All files (*.*)|*.*",
                Multiselect = false
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var bytes = File.ReadAllBytes(ofd.FileName);
                if (!SapService.IsRiffWav(bytes))
                    throw new InvalidDataException("Selected file is not a valid RIFF WAV.");

                int nextIndex = _bank.Entries.Count == 0 ? 0 : _bank.Entries.Max(x => x.Index) + 1;

                _bank.Entries.Add(new SapEntry
                {
                    Index = nextIndex,
                    Kind = SapEntryKind.Wav,
                    Data = bytes
                });

                MarkDirty();
                RefreshList();

                // select last row
                if (listView1.Items.Count > 0)
                    listView1.Items[listView1.Items.Count - 1].Selected = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Add failed:\n{ex.Message}", "SAP Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateUiState();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopPlayback();

            if (!ConfirmLoseChanges())
            {
                e.Cancel = true;
                return;
            }

            base.OnFormClosing(e);
        }
    }
}

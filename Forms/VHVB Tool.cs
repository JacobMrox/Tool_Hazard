namespace Tool_Hazard.Forms
{
    using NAudio.Wave;

    public class SampleHeader
    {
        public int Index { get; set; }
        public int VabNumber { get; set; }
        public int Offset { get; set; }
        public int Size { get; set; }
        public int SampleRate { get; set; } = 44100;
        public int PcmSampleCount { get; set; }
        public int LoopStartSample { get; set; } = -1;
        public int LoopEndSample { get; set; } = -1;
        public int ToneReferenceCount { get; set; }

        public bool IsLooping => LoopStartSample >= 0 && LoopEndSample > LoopStartSample;
        public TimeSpan Duration => SampleRate > 0
            ? TimeSpan.FromSeconds(PcmSampleCount / (double)SampleRate)
            : TimeSpan.Zero;
    }
    public partial class VHVB_Tool : Form
    {
        private string? _vhPath;
        private string? _vbPath;
        private readonly PS1AudioService _audioService;
        private List<SampleHeader> _sampleHeaders = new();
        private IWavePlayer? _waveOut;
        private WaveStream? _waveStream;
        private bool _isInitialLoad = true;
        public VHVB_Tool()
        {
            InitializeComponent();
            _audioService = new PS1AudioService();
            this.Shown += SampleManagerForm_Shown;
            SetupListView(); // CRITICAL: Sets up columns
        }
        public VHVB_Tool(string vhPath, string vbPath) : this()
        {
            _vhPath = vhPath;
            _vbPath = vbPath;
            _isInitialLoad = false;
            this.Shown -= SampleManagerForm_Shown;
            LoadAudioSamples();
        }

        private void SetupListView()
        {
            // Force Details view so columns appear
            listViewSamples.View = View.Details;
            listViewSamples.GridLines = true;
            listViewSamples.FullRowSelect = true;
            listViewSamples.MultiSelect = false;
            listViewSamples.DoubleClick += (_, _) => PlaySelectedSample();

            // Add columns if they don't exist in Designer
            if (listViewSamples.Columns.Count == 0)
            {
                listViewSamples.Columns.Add("Sample", 70);
                listViewSamples.Columns.Add("Offset", 100);
                listViewSamples.Columns.Add("Size", 100);
                listViewSamples.Columns.Add("Rate", 80);
                listViewSamples.Columns.Add("Duration", 90);
                listViewSamples.Columns.Add("Loop", 70);
                listViewSamples.Columns.Add("Tones", 65);
            }
        }

        private void SampleManagerForm_Shown(object? sender, EventArgs e)
        {
            if (_isInitialLoad)
            {
                PromptAndLoadFiles(true);
                _isInitialLoad = false;
            }
        }

        private void PromptAndLoadFiles(bool closeOnCancel = false)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "PS1 Sound Bank Header (*.vh)|*.vh";
                ofd.Title = "Select the VH Sound Header File";
                if (ofd.ShowDialog() != DialogResult.OK) { if (closeOnCancel) this.Close(); return; }
                _vhPath = ofd.FileName;
            }

            try
            {
                string dir = Path.GetDirectoryName(_vhPath) ?? string.Empty;
                string name = Path.GetFileNameWithoutExtension(_vhPath);
                string suggestedVb = Path.Combine(dir, name + ".vb");

                if (File.Exists(suggestedVb))
                {
                    _vbPath = suggestedVb;
                }
                else
                {
                    using OpenFileDialog ofd = new OpenFileDialog
                    {
                        Filter = "VB Voice Body File (*.vb)|*.vb|All files (*.*)|*.*",
                        Title = "Select the corresponding VB Voice Body File",
                        FileName = suggestedVb
                    };
                    if (ofd.ShowDialog() != DialogResult.OK) { if (closeOnCancel) Close(); return; }
                    _vbPath = ofd.FileName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting up file paths: {ex.Message}");
                if (closeOnCancel) this.Close();
                return;
            }

            LoadAudioSamples();
        }

        private void LoadAudioSamples()
        {
            if (string.IsNullOrEmpty(_vhPath) || string.IsNullOrEmpty(_vbPath)) return;

            listViewSamples.Items.Clear();

            try
            {
                StopPlayback();
                _sampleHeaders = _audioService.ReadVhFile(_vhPath, _vbPath);

                for (int i = 0; i < _sampleHeaders.Count; i++)
                {
                    var header = _sampleHeaders[i];
                    var item = new ListViewItem(header.Index.ToString("D3"));
                    item.SubItems.Add($"0x{header.Offset:X8}");
                    item.SubItems.Add($"{header.Size:N0} Bytes");
                    item.SubItems.Add($"{header.SampleRate:N0} Hz");
                    item.SubItems.Add($"{header.Duration.TotalSeconds:0.000} s");
                    item.SubItems.Add(header.IsLooping ? "Yes" : "No");
                    item.SubItems.Add(header.ToneReferenceCount.ToString());
                    item.Tag = header;
                    listViewSamples.Items.Add(item);
                }
                Text = $"RE1-3 VH/VB Player - {Path.GetFileName(_vhPath)} ({_sampleHeaders.Count} samples)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading samples: {ex.Message}", "File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (_isInitialLoad) this.Close();
            }
        }

        //Buttons
        private void button1_Click(object sender, EventArgs e)
        {
            //Import VB/VH
            PromptAndLoadFiles(false);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (_waveOut != null)
            {
                StopPlayback();
                return;
            }

            PlaySelectedSample();
        }

        private void PlaySelectedSample()
        {
            if (listViewSamples.SelectedItems.Count == 0 || string.IsNullOrEmpty(_vbPath)) return;
            if (listViewSamples.SelectedItems[0].Tag is not SampleHeader h) return;

            try
            {
                StopPlayback();
                var wavData = _audioService.CreateSampleWav(_vbPath, h);
                var memory = new MemoryStream(wavData, writable: false);
                _waveStream = new WaveFileReader(memory);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_waveStream);
                _waveOut.PlaybackStopped += (_, _) =>
                {
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke(new Action(StopPlayback));
                };
                _waveOut.Play();
                button2.Text = "Stop";
            }
            catch (Exception ex)
            {
                StopPlayback();
                MessageBox.Show($"Play failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopPlayback()
        {
            try { _waveOut?.Stop(); } catch { }

            _waveStream?.Dispose();
            _waveStream = null;

            _waveOut?.Dispose();
            _waveOut = null;

            button2.Text = "Play Sample";
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopPlayback();
            base.OnFormClosed(e);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            //Extract Sample ADPCM or Wav (user can choose)
            if (listViewSamples.SelectedItems.Count == 0 || string.IsNullOrEmpty(_vbPath)) return;
            var h = listViewSamples.SelectedItems[0].Tag as SampleHeader;
            if (h == null) return;

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "WAV Audio (*.wav)|*.wav|PSX ADPCM Raw (*.adpcm)|*.adpcm";
                sfd.Title = $"Extract Sample {h.Index}";
                sfd.FileName = $"{Path.GetFileNameWithoutExtension(_vhPath)}_{h.Index:D5}.wav";

                if (sfd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    string ext = Path.GetExtension(sfd.FileName).ToLowerInvariant();
                    if (ext == ".adpcm")
                        _audioService.ExtractSampleToAdpcm(_vbPath, h, sfd.FileName);
                    else
                        _audioService.ExtractSampleToWav(_vbPath, h, sfd.FileName);

                    MessageBox.Show("Extracted successfully.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Extraction failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            //Replace sample with ADPC or Wav (user can choose)
            if (listViewSamples.SelectedItems.Count == 0 || string.IsNullOrEmpty(_vhPath) || string.IsNullOrEmpty(_vbPath)) return;
            var h = listViewSamples.SelectedItems[0].Tag as SampleHeader;
            if (h == null) return;

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "WAV Audio (*.wav)|*.wav|PSX ADPCM Raw (*.adpcm)|*.adpcm";
                ofd.Title = $"Replace Sample {h.Index}";

                if (ofd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    string ext = Path.GetExtension(ofd.FileName).ToLowerInvariant();
                    if (ext == ".adpcm")
                    {
                        _audioService.ReplaceSampleWithAdpcmSameLength(_vbPath, h, ofd.FileName);
                    }
                    else
                    {
                        // current WAV replacement is in-place and may require <= original size
                        _audioService.ReplaceSampleWithWav(_vhPath, _vbPath, h, ofd.FileName);
                    }

                    MessageBox.Show("Replaced successfully.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Replacement failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}

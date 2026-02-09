namespace Tool_Hazard.Forms
{
    public class SampleHeader { public int Index { get; set; } public int Offset { get; set; } public int Size { get; set; } }
    public partial class VHVB_Tool : Form
    {
        private string _vhPath;
        private string _vbPath;
        private readonly PS1AudioService _audioService;
        private List<SampleHeader> _sampleHeaders;
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

            // Add columns if they don't exist in Designer
            if (listViewSamples.Columns.Count == 0)
            {
                listViewSamples.Columns.Add("Index", 60);
                listViewSamples.Columns.Add("Offset", 100);
                listViewSamples.Columns.Add("Size", 100);
            }
        }

        private void SampleManagerForm_Shown(object sender, EventArgs e)
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
                string dir = Path.GetDirectoryName(_vhPath);
                string name = Path.GetFileNameWithoutExtension(_vhPath);
                string suggestedVb = Path.Combine(dir, name + ".vb");

                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "VB Voice Body File (*.vb)|*.vb|All files (*.*)|*.*";
                    ofd.Title = "Select the corresponding VB Voice Body File";
                    if (File.Exists(suggestedVb)) ofd.FileName = suggestedVb;

                    if (ofd.ShowDialog() != DialogResult.OK) { if (closeOnCancel) this.Close(); return; }
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
                _sampleHeaders = _audioService.ReadVhFile(_vhPath, _vbPath);

                for (int i = 0; i < _sampleHeaders.Count; i++)
                {
                    var header = _sampleHeaders[i];
                    var item = new ListViewItem(header.Index.ToString());
                    item.SubItems.Add($"0x{header.Offset:X8}");
                    item.SubItems.Add($"{header.Size:N0} Bytes");
                    item.Tag = header;
                    listViewSamples.Items.Add(item);
                }
                this.Text = $"Sample Manager: {Path.GetFileName(_vhPath)} ({_sampleHeaders.Count} Samples)";
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
            //Play Sample [in form]
            if (listViewSamples.SelectedItems.Count == 0) return;
            var h = listViewSamples.SelectedItems[0].Tag as SampleHeader;
            if (h == null) return;

            try
            {
                var pcm = _audioService.DecodeSampleToPcm(_vbPath, h);

                // temp wav
                string temp = Path.Combine(Path.GetTempPath(), $"ToolHazard_{Guid.NewGuid():N}.wav");
                // NOTE: you currently hardcode 22050 in ExtractSampleToWav; keep it consistent for now.
                // Later we’ll add per-sample rate or pitch extraction.
                typeof(PS1AudioService)
                    .GetMethod("WriteWavFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(_audioService, new object[] { pcm, temp, 22050 });

                using (var sp = new System.Media.SoundPlayer(temp))
                {
                    sp.Play();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Play failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            //Extract Sample ADPCM or Wav (user can choose)
            if (listViewSamples.SelectedItems.Count == 0) return;
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
            if (listViewSamples.SelectedItems.Count == 0) return;
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

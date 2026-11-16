using Tool_Hazard.Biohazard.MSG;

namespace Tool_Hazard.Forms
{
    public partial class ClassicREmsgTool : Form
    {
        private EncodingDictionary? _dict;
        private string? _dictPath;
        private MsgFontRenderer? _renderer;

        private string? _currentMsgPath;
        private byte[]? _currentMsgBytes;
        private bool _dirty;
        private bool _suppressSync;

        // Default resource paths
        private static string DefaultEncodingPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Biohazard", "encoding.xml");
        private static string DefaultFontPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Biohazard", "font.png");
        private static string DefaultFontEncodingPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Biohazard", "encoding_file.xml");

        // Available font textures (dumped from RE2 PC font*.tim files)
        private static readonly string[] FontPaths =
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Biohazard", "font.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Biohazard", "font_japanese_0.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Biohazard", "font_japanese_1.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Biohazard", "font_japanese_2.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Biohazard", "font_japanese_3.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Biohazard", "font_japanese_4.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Biohazard", "font_japanese_5.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Biohazard", "font_japanese_6.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Biohazard", "font_japanese_7.png"),
        };

        private static readonly string[] FontNames =
        {
            "Default (font.png)",
            "Japanese font 0",
            "Japanese font 1",
            "Japanese font 2",
            "Japanese font 3",
            "Japanese font 4",
            "Japanese font 5",
            "Japanese font 6",
            "Japanese font 7",
        };

        // Available encoding dictionaries
        private static readonly string[] EncodingPaths =
        {
            DefaultEncodingPath,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Biohazard", "encoding_japanese.xml"),
        };

        private static readonly string[] EncodingNames =
        {
            "Default (encoding.xml)",
            "Japanese (encoding_japanese.xml)",
        };

        public ClassicREmsgTool()
        {
            InitializeComponent();
            HookEvents();

            // Populate font dropdown
            cboFont.Items.AddRange(FontNames);
            cboFont.SelectedIndex = 0;

            // Populate encoding dropdown
            cboEncoding.Items.AddRange(EncodingNames);
            cboEncoding.SelectedIndex = 0;
        }

        private void HookEvents()
        {
            mnuLoadXmlDictionary.Click += (_, __) => LoadDictionaryViaDialog();
            mnuOpenMsg.Click += (_, __) => OpenMsgViaDialog();
            mnuSaveMsg.Click += (_, __) => SaveMsg(overwrite: true);
            mnuSaveMsgAs.Click += (_, __) => SaveMsg(overwrite: false);

            txtMsg.TextChanged += (_, __) =>
            {
                if (_currentMsgBytes != null)
                {
                    _dirty = true;
                    UpdateTitle();
                }
                UpdatePreview();
            };

            cboFont.SelectedIndexChanged += (_, __) =>
            {
                if (_suppressSync) return;
                EnsureRendererLoaded();
                SyncEncodingToFont();
            };

            cboEncoding.SelectedIndexChanged += (_, __) =>
            {
                if (_suppressSync) return;
                ReloadDictionaryFromSelected();
                SyncFontToEncoding();
            };

            this.FormClosing += (_, e) =>
            {
                if (!PromptSaveIfDirty())
                    e.Cancel = true;
            };

            this.FormClosed += (_, _) =>
            {
                _renderer?.Dispose();
                _renderer = null;
            };

            UpdateTitle();
        }

        // When a Japanese font is picked, auto-select the Japanese encoding (if available).
        private void SyncEncodingToFont()
        {
            if (cboFont.SelectedIndex >= 1 && cboFont.SelectedIndex <= FontNames.Length - 1)
            {
                // Japanese font selected -> switch to Japanese encoding
                _suppressSync = true;
                try { cboEncoding.SelectedIndex = 1; } finally { _suppressSync = false; }
            }
        }

        // When the Japanese encoding is picked, auto-select a Japanese font (if available).
        private void SyncFontToEncoding()
        {
            if (cboEncoding.SelectedIndex == 1)
            {
                _suppressSync = true;
                try { cboFont.SelectedIndex = 2; } finally { _suppressSync = false; } // Japanese font 1 as default
            }
        }

        private void EnsureDictionaryLoaded()
        {
            if (_dict != null) return;

            string path = _dictPath ?? DefaultEncodingPath;
            if (!File.Exists(path))
            {
                MessageBox.Show(this,
                    "No encoding dictionary found.\n\n" +
                    $"Expected at:\n{path}",
                    "No Encoding Dictionary",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                LoadDictionaryViaDialog();
                return;
            }

            _dictPath = path;
            _dict = EncodingDictionary.Load(_dictPath);
            EnsureRendererLoaded();
        }

        private void ReloadDictionaryFromSelected()
        {
            int idx = cboEncoding.SelectedIndex;
            string path = idx >= 0 && idx < EncodingPaths.Length ? EncodingPaths[idx] : DefaultEncodingPath;

            if (!File.Exists(path))
            {
                MessageBox.Show(this,
                    $"Encoding file not found:\n{path}",
                    "Encoding File Missing",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _dictPath = path;
            _dict = EncodingDictionary.Load(_dictPath);

            // Re-decode current msg if one is open
            if (_currentMsgBytes != null)
                txtMsg.Text = MsgCodec.Decode(_currentMsgBytes, _dict, stopAtZero: false);

            EnsureRendererLoaded();
            _dirty = false;
            UpdateTitle();
        }

        private void LoadDictionaryViaDialog()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Load XML Encoding Dictionary",
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            _dictPath = ofd.FileName;
            _dict = EncodingDictionary.Load(_dictPath);

            // Re-decode current msg if one is open
            if (_currentMsgBytes != null)
                txtMsg.Text = MsgCodec.Decode(_currentMsgBytes, _dict, stopAtZero: false);

            EnsureRendererLoaded();

            // Mark encoding dropdown as custom
            _suppressSync = true;
            try
            {
                cboEncoding.SelectedIndex = -1;
                cboEncoding.Text = Path.GetFileName(ofd.FileName);
            }
            finally { _suppressSync = false; }

            _dirty = false;
            UpdateTitle();
        }

        private void EnsureRendererLoaded()
        {
            if (_dict == null) return;

            string fontPath = DefaultFontPath;
            if (cboFont.SelectedIndex >= 0 && cboFont.SelectedIndex < FontPaths.Length)
                fontPath = FontPaths[cboFont.SelectedIndex];
            else if (!string.IsNullOrEmpty(cboFont.Text) && File.Exists(cboFont.Text))
                fontPath = cboFont.Text;

            if (!File.Exists(fontPath))
                return;

            // font.png has its own 8x8 atlas ordering (encoding_file.xml),
            // which is different from the byte ordering used to serialize MSG
            // files (encoding.xml). Keep the two mappings independent.
            var rendererDictionary = cboFont.SelectedIndex == 0 && File.Exists(DefaultFontEncodingPath)
                ? EncodingDictionary.Load(DefaultFontEncodingPath)
                : _dict;

            _renderer?.Dispose();
            _renderer = new MsgFontRenderer(rendererDictionary);
            _renderer.LoadFont(fontPath);
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            // Ensure the dictionary + renderer are initialized (in case the form
            // was shown without going through the normal open/save flow).
            EnsureDictionaryLoaded();
            if (_renderer == null || picPreview == null) return;

            try
            {
                var text = txtMsg.Text;
                using var frame = _renderer.RenderPreviewFrame(text);
                var bmp = new Bitmap(frame);
                var old = picPreview.Image;
                picPreview.Image = bmp;
                old?.Dispose();
                picPreview.Refresh();
            }
            catch
            {
                // Preview failures are non-fatal.
            }
        }

        private void OpenMsgViaDialog()
        {
            if (!PromptSaveIfDirty())
                return;

            EnsureDictionaryLoaded();

            using var ofd = new OpenFileDialog
            {
                Title = "Open MSG",
                Filter = "MSG Files (*.msg)|*.msg|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            _currentMsgPath = ofd.FileName;
            _currentMsgBytes = File.ReadAllBytes(_currentMsgPath);

            txtMsg.Text = MsgCodec.Decode(_currentMsgBytes, _dict!, stopAtZero: false);

            _dirty = false;
            UpdateTitle();
        }

        private void SaveMsg(bool overwrite)
        {
            EnsureDictionaryLoaded();

            if (_currentMsgBytes == null && string.IsNullOrEmpty(_currentMsgPath))
            {
                overwrite = false;
            }

            string? path = _currentMsgPath;

            if (!overwrite || string.IsNullOrEmpty(path))
            {
                using var sfd = new SaveFileDialog
                {
                    Title = "Save MSG",
                    Filter = "MSG Files (*.msg)|*.msg|All Files (*.*)|*.*",
                    FileName = string.IsNullOrEmpty(_currentMsgPath) ? "message.msg" : Path.GetFileName(_currentMsgPath)
                };

                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return;

                path = sfd.FileName;
            }

            try
            {
                var bytes = MsgCodec.Encode(txtMsg.Text, _dict!);
                File.WriteAllBytes(path!, bytes);

                _currentMsgPath = path;
                _currentMsgBytes = bytes;
                _dirty = false;

                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    ex.Message,
                    "Save failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private bool PromptSaveIfDirty()
        {
            if (!_dirty) return true;

            var result = MessageBox.Show(this,
                "You have unsaved changes. Save now?",
                "Unsaved changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Cancel) return false;
            Close();

            if (result == DialogResult.Yes)
                SaveMsg(overwrite: true);

            return !_dirty;
        }

        private void UpdateTitle()
        {
            var filePart = string.IsNullOrEmpty(_currentMsgPath) ? "No MSG" : Path.GetFileName(_currentMsgPath);
            var dictPart = string.IsNullOrEmpty(_dictPath) ? "Default dictionary" : Path.GetFileName(_dictPath);

            Text = $"MSG Tool - {filePart} - {dictPart}" + (_dirty ? " *" : "");
        }
    }
}

using System.Diagnostics;
using System.Drawing.Imaging;
using White_Day_Mod_Tool;

namespace Tool_Hazard
{
    public partial class WhiteDayFontEditor : Form
    {
        private FontParameters _font = new();
        private byte[] _workingGlyph = Array.Empty<byte>();
        private string? _loadedFontPath;
        private int _currentGlyphIndex = -1;
        private bool _loadingGlyph;
        private bool _symbolDirty;
        private bool _fontDirty;

        public WhiteDayFontEditor()
        {
            InitializeComponent();
            SetEditorEnabled(false);
        }

        private void Button_OpenFont_Click(object? sender, EventArgs e)
        {
            if (!ConfirmDiscardFontChanges())
                return;

            using var dialog = new OpenFileDialog
            {
                Title = "Open White Day Font",
                Filter = "White Day fonts (*.fnt;*.wft)|*.fnt;*.wft|FNT fonts (*.fnt)|*.fnt|WFT fonts (*.wft)|*.wft|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var loadedFont = new FontParameters();
                loadedFont.Load(dialog.FileName);
                _font = loadedFont;
                _loadedFontPath = dialog.FileName;
                _fontDirty = false;
                _symbolDirty = false;

                _loadingGlyph = true;
                CharSelect.Minimum = 1;
                CharSelect.Maximum = _font.GlyphCount;
                CharSelect.Value = 1;
                CharWidthSetting.Minimum = 0;
                CharWidthSetting.Maximum = _font.PixelWidth - 1;
                _loadingGlyph = false;

                SetEditorEnabled(true);
                LoadGlyph(0);
                fontInfoLabel.Text =
                    $"{Path.GetFileName(dialog.FileName)}  •  {_font.PixelWidth}×{_font.PixelWidth}  •  {_font.GlyphCount:N0} symbols";
                SetStatus($"Opened {Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                SetEditorEnabled(_font.GlyphCount > 0);
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Unable to Open Font",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void Button_SaveFont_Click(object? sender, EventArgs e)
        {
            SaveFontAs();
        }

        private bool SaveFontAs()
        {
            if (_currentGlyphIndex < 0 || string.IsNullOrEmpty(_loadedFontPath))
                return false;

            if (_symbolDirty)
                ApplyCurrentGlyph(showStatus: false);

            string extension = Path.GetExtension(_loadedFontPath);
            string suggestedName =
                Path.GetFileNameWithoutExtension(_loadedFontPath) + "_edited" + extension;

            using var dialog = new SaveFileDialog
            {
                Title = "Export White Day Font",
                Filter = "White Day fonts (*.fnt;*.wft)|*.fnt;*.wft|All files (*.*)|*.*",
                FileName = suggestedName,
                InitialDirectory = Path.GetDirectoryName(_loadedFontPath),
                DefaultExt = extension.TrimStart('.'),
                AddExtension = true
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return false;

            try
            {
                _font.Save(dialog.FileName);
                _fontDirty = false;
                SetStatus($"Exported font to {Path.GetFileName(dialog.FileName)}", dialog.FileName);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"The font could not be saved.\n\n{ex.Message}",
                    "Save Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        private void Button_ImportBMP_Click(object? sender, EventArgs e)
        {
            if (_currentGlyphIndex < 0)
                return;

            using var dialog = new OpenFileDialog
            {
                Title = "Import Symbol",
                Filter = "Images (*.bmp;*.png)|*.bmp;*.png|Bitmap (*.bmp)|*.bmp|PNG (*.png)|*.png|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                using Image image = Image.FromFile(dialog.FileName);
                _workingGlyph = BitmapExtensions.EncodeWhiteDayGlyph(image, _font.PixelWidth);
                _symbolDirty = true;
                RefreshPreview();
                SetStatus(
                    $"Imported {Path.GetFileName(dialog.FileName)} as symbol {CurrentSymbolNumber}. Click Apply Symbol to keep it.",
                    dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"The image could not be imported.\n\n{ex.Message}",
                    "Import Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void Button_ExportBMP_Click(object? sender, EventArgs e)
        {
            if (_currentGlyphIndex < 0)
                return;

            using var dialog = new SaveFileDialog
            {
                Title = "Export Symbol",
                Filter = "Bitmap (*.bmp)|*.bmp",
                FileName = $"symbol_{CurrentSymbolNumber:D4}.bmp",
                DefaultExt = "bmp",
                AddExtension = true
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                using Bitmap bitmap = BitmapExtensions.CreateIndexedGlyphBitmap(
                    _workingGlyph,
                    _font.PixelWidth);
                bitmap.Save(dialog.FileName, ImageFormat.Bmp);
                SetStatus(
                    $"Exported symbol {CurrentSymbolNumber} to {Path.GetFileName(dialog.FileName)}",
                    dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"The symbol could not be exported.\n\n{ex.Message}",
                    "Export Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void Button_SaveSymbol_Click(object? sender, EventArgs e)
        {
            ApplyCurrentGlyph(showStatus: true);
        }

        private void ApplyCurrentGlyph(bool showStatus)
        {
            if (_currentGlyphIndex < 0)
                return;

            _font.SetGlyph(
                _currentGlyphIndex,
                _workingGlyph,
                (int)CharWidthSetting.Value);
            _symbolDirty = false;
            _fontDirty = true;
            Button_SaveFont.ForeColor = Color.DarkGreen;

            if (showStatus)
                SetStatus($"Applied changes to symbol {CurrentSymbolNumber}");
        }

        private void CharSelect_ValueChanged(object? sender, EventArgs e)
        {
            if (_loadingGlyph || _font.GlyphCount == 0)
                return;

            int requestedIndex = (int)CharSelect.Value - 1;
            if (requestedIndex == _currentGlyphIndex)
                return;

            if (_symbolDirty)
            {
                DialogResult result = MessageBox.Show(
                    this,
                    $"Apply the changes to symbol {CurrentSymbolNumber} before moving on?",
                    "Unsaved Symbol",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Cancel)
                {
                    _loadingGlyph = true;
                    CharSelect.Value = _currentGlyphIndex + 1;
                    _loadingGlyph = false;
                    return;
                }

                if (result == DialogResult.Yes)
                    ApplyCurrentGlyph(showStatus: false);
            }

            LoadGlyph(requestedIndex);
        }

        private void LoadGlyph(int index)
        {
            _currentGlyphIndex = index;
            _workingGlyph = _font.GetGlyph(index);
            _symbolDirty = false;

            _loadingGlyph = true;
            CharSelect.Value = index + 1;
            CharWidthSetting.Value = Math.Clamp(
                _font.GetGlyphWidth(index),
                (int)CharWidthSetting.Minimum,
                (int)CharWidthSetting.Maximum);
            _loadingGlyph = false;

            symbolNumberLabel.Text = $"of {_font.GlyphCount:N0}";
            widthValueLabel.Text = CharWidthSetting.Value.ToString();
            RefreshPreview();
            SetStatus($"Viewing symbol {CurrentSymbolNumber}");
        }

        private void CharWidthSetting_ValueChanged(object? sender, EventArgs e)
        {
            widthValueLabel.Text = CharWidthSetting.Value.ToString();
            if (!_loadingGlyph && _currentGlyphIndex >= 0)
                _symbolDirty = true;
        }

        private void RefreshPreview()
        {
            Image? oldImage = ViewCharBox.Image;
            ViewCharBox.Image = BitmapExtensions.DecodeWhiteDayGlyph(
                _workingGlyph,
                _font.PixelWidth,
                displayScale: 6);
            oldImage?.Dispose();
        }

        private int CurrentSymbolNumber => _currentGlyphIndex + 1;

        private void SetEditorEnabled(bool enabled)
        {
            CharSelect.Enabled = enabled;
            CharWidthSetting.Enabled = enabled;
            Button_ImportBMP.Enabled = enabled;
            Button_ExportBMP.Enabled = enabled;
            Button_SaveSymbol.Enabled = enabled;
            Button_SaveFont.Enabled = enabled;

            if (!enabled)
            {
                fontInfoLabel.Text = "Open an 18×18 FNT or 36×36 WFT font to begin";
                statusLabel.Text = "Ready";
                Image? oldImage = ViewCharBox.Image;
                ViewCharBox.Image = null;
                oldImage?.Dispose();
            }
        }

        private void SetStatus(string message, string? filePath = null)
        {
            statusLabel.Text = message;
            string? sourcePath = filePath ?? _loadedFontPath;
            statusLabel.Tag = string.IsNullOrEmpty(sourcePath)
                ? null
                : Path.GetDirectoryName(sourcePath);
        }

        private void statusLabel_Click(object? sender, EventArgs e)
        {
            string? directory = statusLabel.Tag as string;
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });
        }

        private void WhiteDayFontEditor_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.O)
            {
                Button_OpenFont.PerformClick();
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.S && Button_SaveFont.Enabled)
            {
                Button_SaveFont.PerformClick();
                e.SuppressKeyPress = true;
            }
            else if (CharSelect.Enabled && (e.KeyCode == Keys.W || e.KeyCode == Keys.Up))
            {
                if (CharSelect.Value < CharSelect.Maximum)
                    CharSelect.Value++;
                e.SuppressKeyPress = true;
            }
            else if (CharSelect.Enabled && (e.KeyCode == Keys.S || e.KeyCode == Keys.Down))
            {
                if (CharSelect.Value > CharSelect.Minimum)
                    CharSelect.Value--;
                e.SuppressKeyPress = true;
            }
            else if (CharWidthSetting.Enabled && (e.KeyCode == Keys.D || e.KeyCode == Keys.Right))
            {
                if (CharWidthSetting.Value < CharWidthSetting.Maximum)
                    CharWidthSetting.Value++;
                e.SuppressKeyPress = true;
            }
            else if (CharWidthSetting.Enabled && (e.KeyCode == Keys.A || e.KeyCode == Keys.Left))
            {
                if (CharWidthSetting.Value > CharWidthSetting.Minimum)
                    CharWidthSetting.Value--;
                e.SuppressKeyPress = true;
            }
        }

        private bool ConfirmDiscardFontChanges()
        {
            if (!_fontDirty && !_symbolDirty)
                return true;

            DialogResult result = MessageBox.Show(
                this,
                "The current font has unsaved changes. Export it before continuing?",
                "Unsaved Font",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            return result switch
            {
                DialogResult.Yes => SaveFontAs(),
                DialogResult.No => true,
                _ => false
            };
        }

        private void WhiteDayFontEditor_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!ConfirmDiscardFontChanges())
                e.Cancel = true;
        }
    }
}

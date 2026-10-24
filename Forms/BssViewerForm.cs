using System.Drawing.Imaging;
using Tool_Hazard.Biohazard.BSS;

namespace Tool_Hazard.Forms
{
    public partial class BssViewerForm : Form
    {
        public BssViewerForm()
        {
            InitializeComponent();
            btnImportReplace.Enabled = false;
            btnReencode.Enabled = false;
            importImagePNGBMPToolStripMenuItem.Enabled = false;
            btnPrev.Enabled = false;
            btnNext.Enabled = false;
        }

        private string _currentBssPath = "";
        private byte[]? _currentBssBytes = null;
        private Bitmap? _currentBitmap = null; private int _bssType = 0;               // 1 = RE1, 2 = RE2/RE3
        private byte[][] _frames = Array.Empty<byte[]>();
        private int _currentFrame = 0;

        //Helper Methods

        private void LoadBssContainer(byte[] allBytes, bool? forceType2 = null)
        {
            _currentBssBytes = allBytes;

            _bssType = forceType2 == true ? 2 : DetectBssType(allBytes);
            int chunkSize = _bssType * 0x8000;

            if (allBytes.Length % chunkSize != 0)
                throw new InvalidDataException($"BSS size ({allBytes.Length:X}) not divisible by chunk ({chunkSize:X}). Wrong type?");

            int count = allBytes.Length / chunkSize;
            _frames = new byte[count][];

            for (int i = 0; i < count; i++)
            {
                _frames[i] = new byte[chunkSize];
                Buffer.BlockCopy(allBytes, i * chunkSize, _frames[i], 0, chunkSize);
            }

            _currentFrame = 0;
            ShowFrame(_currentFrame);

            btnPrev.Enabled = false;
            btnNext.Enabled = count > 1;

            SetStatus($"Loaded: {Path.GetFileName(_currentBssPath)} | {count} background(s) | Type={_bssType} | Chunk={chunkSize:X}");
        }

        private void ShowFrame(int index)
        {
            if (_frames.Length == 0) return;
            if (index < 0) index = 0;
            if (index >= _frames.Length) index = _frames.Length - 1;

            _currentFrame = index;
            btnPrev.Enabled = index > 0;
            btnNext.Enabled = index < _frames.Length - 1;

            var (w, h, _) = GetSettings();

            // Decode ONE frame chunk (not the whole file)
            _currentBitmap?.Dispose();
            _currentBitmap = BssCodec.DecodeToBitmap(_frames[_currentFrame], w, h);
            pictureBoxPreview.Image = _currentBitmap;

            //if (lblInfo != null)
            //    lblInfo.Text = $"Loaded: {Path.GetFileName(_currentBssPath)} | {w}x{h} | Image {_currentFrame + 1}/{_frames.Length}";

            SetStatus($"Loaded: {Path.GetFileName(_currentBssPath)} | {w}x{h} | Image {_currentFrame + 1}/{_frames.Length}");
        }

        private static int DetectBssType(byte[] data)
        {
            if (data.Length == 0x8000) return 1;
            if (HasBsHeader(data, 0x8000)) return 1;
            if (data.Length >= 0x10000 && data.Length % 0x10000 == 0) return 2;
            if (data.Length % 0x8000 == 0) return 1;
            throw new InvalidDataException("The file is not aligned as a Resident Evil BSS container.");
        }

        private static bool HasBsHeader(byte[] data, int offset) =>
            offset + 8 <= data.Length && BitConverter.ToUInt16(data, offset + 2) == 0x3800;

        private void SetStatus(string text)
        {
            // Change this to your actual status label name if needed
            if (toolStripStatusLabel2 != null)
                toolStripStatusLabel2.Text = text;
            //toolStripStatusLabel1
        }

        //BSS Load/Save/Export/Import Methods

        private (int w, int h, int q) GetSettings()
        {
            int w = (int)nudWidth.Value;
            int h = (int)nudHeight.Value;
            int q = (int)nudQuant.Value;
            if (w <= 0) w = 320;
            if (h <= 0) h = 240;
            q = Math.Clamp(q, 1, 63);
            return (w, h, q);
        }

        private void LoadBssBytesAndPreview(byte[] bytes, string sourcePathForLabel = "")
        {
            var (w, h, _) = GetSettings();

            // Decode → bitmap
            Bitmap bmp = BssCodec.DecodeToBitmap(bytes, w, h);

            // Swap current bitmap safely
            _currentBitmap?.Dispose();
            _currentBitmap = bmp;

            // Show
            pictureBoxPreview.Image = _currentBitmap;

            _currentBssBytes = bytes;

            if (!string.IsNullOrWhiteSpace(sourcePathForLabel))
                _currentBssPath = sourcePathForLabel;

            // Info
            //if (lblInfo != null)
            //{
            //    lblInfo.Text = $"Loaded: {Path.GetFileName(_currentBssPath)} | {w}x{h}";
            //}

            SetStatus($"Loaded BSS: {Path.GetFileName(_currentBssPath)} | ({bytes.Length} bytes) @ {w}x{h}");
        }

        private void ExportCurrentBitmapTo(string outPath)
        {
            if (_currentBitmap == null)
                throw new InvalidOperationException("No image loaded.");

            var ext = Path.GetExtension(outPath).ToLowerInvariant();
            if (ext == ".png")
                _currentBitmap.Save(outPath, ImageFormat.Png);
            else
                _currentBitmap.Save(outPath, ImageFormat.Bmp);
        }

        private void ReplaceWithImportedImage(string imagePath)
        {
            var (w, h, q) = GetSettings();

            using var imported = new Bitmap(imagePath);

            // Encode imported bitmap to BSS bytes
            byte[] bss = BssCodec.EncodeFromBitmap(imported, w, h, q);

            // Update preview by decoding what we just encoded (truth source = bytes)
            LoadBssBytesAndPreview(bss, _currentBssPath);

            SetStatus($"Imported & encoded ({w}x{h}, quant={q}) from {Path.GetFileName(imagePath)}");
        }

        private void openBSSToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Open BSS",
                Filter = "BSS files (*.bss)|*.bss|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var bytes = File.ReadAllBytes(ofd.FileName);
                _currentBssPath = ofd.FileName;

                bool forceType2 = chkTreatAsType2.Checked; // if you have this checkbox
                LoadBssContainer(bytes, forceType2 ? true : (bool?)null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Open BSS failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Open failed.");
            }

        }

        private void saveBSSAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_currentBssBytes == null)
            {
                MessageBox.Show(this, "No BSS data to save.", "Save BSS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Save BSS As",
                Filter = "BSS files (*.bss)|*.bss|All files (*.*)|*.*",
                FileName = string.IsNullOrWhiteSpace(_currentBssPath)
                    ? "background.bss"
                    : Path.GetFileName(_currentBssPath)
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                File.WriteAllBytes(sfd.FileName, _currentBssBytes);
                _currentBssPath = sfd.FileName;

                //if (lblInfo != null)
                //{
                //    var (w, h, _) = GetSettings();
                //    lblInfo.Text = $"Saved: {Path.GetFileName(_currentBssPath)} | {w}x{h}";
                //}

                SetStatus($"Saved BSS: {Path.GetFileName(sfd.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save BSS failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Save failed.");
            }
        }

        private void exportImagePNGBMPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // same as btnExportPng_Click but allows PNG or BMP
            if (_currentBitmap == null)
            {
                MessageBox.Show(this, "No image to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Export Image",
                Filter = "PNG (*.png)|*.png|Bitmap (*.bmp)|*.bmp|All files (*.*)|*.*",
                FileName = string.IsNullOrWhiteSpace(_currentBssPath)
                    ? "export.png"
                    : Path.GetFileNameWithoutExtension(_currentBssPath) + ".png"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                ExportCurrentBitmapTo(sfd.FileName);
                SetStatus($"Exported: {Path.GetFileName(sfd.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Export failed.");
            }
        }

        private void importImagePNGBMPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // same as btnImportReplace_Click but allows PNG/BMP
            if (string.IsNullOrWhiteSpace(_currentBssPath) && _currentBssBytes == null)
            {
                // Allow import even if no BSS loaded? If you want that, remove this guard.
                MessageBox.Show(this, "Load a BSS first (so we know what you are replacing).", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var ofd = new OpenFileDialog
            {
                Title = "Import Image (PNG/BMP)",
                Filter = "Images (*.png;*.bmp)|*.png;*.bmp|PNG (*.png)|*.png|Bitmap (*.bmp)|*.bmp|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                ReplaceWithImportedImage(ofd.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Import failed.");
            }
        }

        private void btnExportPng_Click(object sender, EventArgs e)
        {
            if (_currentBitmap == null)
            {
                MessageBox.Show(this, "No image to export.", "Export PNG", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Export PNG",
                Filter = "PNG (*.png)|*.png|All files (*.*)|*.*",
                FileName = string.IsNullOrWhiteSpace(_currentBssPath)
                    ? "export.png"
                    : Path.GetFileNameWithoutExtension(_currentBssPath) + ".png"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                _currentBitmap.Save(sfd.FileName, ImageFormat.Png);
                SetStatus($"Exported PNG: {Path.GetFileName(sfd.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Export PNG failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Export failed.");
            }
        }

        private void btnImportReplace_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentBssPath) && _currentBssBytes == null)
            {
                MessageBox.Show(this, "Load a BSS first (so we know what you are replacing).", "Import/Replace", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var ofd = new OpenFileDialog
            {
                Title = "Import/Replace Image",
                Filter = "Images (*.png;*.bmp)|*.png;*.bmp|PNG (*.png)|*.png|Bitmap (*.bmp)|*.bmp|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                ReplaceWithImportedImage(ofd.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Import/Replace failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Import failed.");
            }
        }

        private void btnReencode_Click(object sender, EventArgs e)
        {
            if (_currentBitmap == null)
            {
                MessageBox.Show(this, "Nothing loaded to re-encode.", "Re-encode", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var (w, h, q) = GetSettings();

                // Encode current preview bitmap -> bytes
                byte[] bss = BssCodec.EncodeFromBitmap(_currentBitmap, w, h, q);

                // Decode back to sanity-check (replace preview with the decoded result)
                LoadBssBytesAndPreview(bss, _currentBssPath);

                SetStatus($"Re-encoded OK (quant={q})");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Re-encode failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Re-encode failed.");
            }
        }
        private void btnNext_Click(object sender, EventArgs e)
        {
            if (_frames.Length == 0) return;
            ShowFrame(_currentFrame + 1);
        }

        private void btnPrev_Click(object sender, EventArgs e)
        {
            if (_frames.Length == 0) return;
            ShowFrame(_currentFrame - 1);
        }
    }
}

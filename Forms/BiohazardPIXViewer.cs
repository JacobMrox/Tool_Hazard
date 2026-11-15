using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Tool_Hazard.Biohazard.PIX;
using Tool_Hazard.UI;

namespace Tool_Hazard.Forms
{
    public partial class Biohazard_PIX_Viewer : Form
    {
        private enum ViewerMode
        {
            None,
            TimPack,
            Sheet,
            Background
        }

        private enum ZoomMode
        {
            Fit = 0,
            X1 = 1,
            X2 = 2,
            X3 = 3,
            X4 = 4,
            X6 = 6,
            X8 = 8
        }

        private static readonly ZoomMode[] ZoomOptions =
        {
            ZoomMode.Fit, ZoomMode.X1, ZoomMode.X2, ZoomMode.X3, ZoomMode.X4, ZoomMode.X6, ZoomMode.X8
        };

        // ---------- document state ----------
        private ViewerMode _mode = ViewerMode.None;
        private List<TimBlock>? _blocks;
        private PsxTimImage? _currentTim;
        private TimImageInfo? _currentInfo;
        private int _timIndex;
        private int _clutIndex;
        private int _clutCount = 1;
        private ItemSheet? _sheet;
        private int _iconIndex;
        private string? _documentPath;
        private bool _documentIsRawPix;
        private bool _dirty;

        // ---------- view state ----------
        private ZoomMode _zoom = ZoomMode.Fit;
        private bool _perPageCluts;
        private bool _showGrid = true;
        private int _currentScale = 1;
        private bool _updatingUi;
        private bool _adjusting;

        private Bitmap? _image;
        private Bitmap? _display;

        private readonly byte[] _palette512 =
            BorlandResReader.ReadPalette512FromEmbeddedRes("Tool_Hazard.Biohazard.PIX.Res.res");

        public Biohazard_PIX_Viewer()
        {
            InitializeComponent();
            InitViewerUi();
        }

        private void InitViewerUi()
        {
            _updatingUi = true;
            foreach (var zoom in ZoomOptions)
                cmbZoom.Items.Add(zoom == ZoomMode.Fit ? "Fit" : $"{(int)zoom}x");
            cmbZoom.SelectedIndex = 0;

            cmbClut.Items.Add("CLUT 1/1");
            cmbClut.SelectedIndex = 0;
            _updatingUi = false;

            previewPanel.Paint += PreviewPanel_Paint;
            previewPanel.Resize += PreviewPanel_Resize;

            ApplyZoomMenuChecks();
            UpdateUi();
            UpdateDisplay();
            SetStatus("Ready - open a TIM or PIX file (Ctrl+O) or drop one on the window.");
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            pictureBox1.Image = null;
            _display?.Dispose();
            _display = null;
            _image?.Dispose();
            _image = null;
            base.OnFormClosed(e);
        }

        // ------------------------------
        // Status / info
        // ------------------------------

        private void SetStatus(string text)
        {
            if (toolStripStatusLabel1 != null)
                toolStripStatusLabel1.Text = text;
        }

        private string DocName() => _documentPath is null ? "(unsaved)" : Path.GetFileName(_documentPath);

        private string DocBaseName() =>
            _documentPath is null ? "TIM" : Path.GetFileNameWithoutExtension(_documentPath);

        private void Info(string text, string title) =>
            MessageBox.Show(this, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

        private void Error(string text, string title) =>
            MessageBox.Show(this, text, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

        private void UpdateInfo()
        {
            string left;
            string right;

            switch (_mode)
            {
                case ViewerMode.TimPack:
                    left = $"{DocName()} - TIM {_timIndex + 1}/{_blocks?.Count ?? 0}" + (_dirty ? "  (modified)" : "");
                    right = _currentInfo?.Describe() ?? string.Empty;

                    if (_currentInfo is { ClutCount: > 1 })
                    {
                        right += $"  |  CLUT {_clutIndex + 1}/{_clutCount}";
                        if (_perPageCluts && _currentInfo.IsAtlas)
                            right += " (one per page)";
                    }

                    if (_image != null)
                        right += $"  |  {_currentScale}x view";
                    break;

                case ViewerMode.Sheet:
                    left = $"{DocName()} - icon {_iconIndex + 1}/{_sheet?.IconCount ?? 0}" + (_dirty ? "  (modified)" : "");
                    right = $"{ItemSheet.IconW}x{ItemSheet.IconH} icons  |  {_sheet?.IconCount ?? 0} icons  |  " +
                            $"{(_sheet?.IconCount ?? 0) * ItemSheet.IconBytes:N0} bytes  |  {_currentScale}x view";
                    break;

                case ViewerMode.Background:
                    left = $"{DocName()} - background" + (_dirty ? "  (modified)" : "");
                    right = _documentIsRawPix
                        ? "raw PS1 background  |  320x240  |  16bpp BGR555  |  153,600 bytes"
                        : _image != null
                            ? $"image  |  {_image.Width}x{_image.Height}" + (IsBackgroundPixSize() ? "  |  can be saved as PIX" : "  |  PIX needs 320x240")
                            : "image";
                    break;

                default:
                    left = "No file loaded - use File > Open, or drop a TIM / PIX file here.";
                    right = string.Empty;
                    break;
            }

            infoLeftLabel.Text = left;
            infoRightLabel.Text = right;

            Text = _mode == ViewerMode.None
                ? "Biohazard PIX & PS TIM Viewer"
                : $"Biohazard PIX & PS TIM Viewer - {DocName()}";
        }

        private bool IsBackgroundPixSize() => _image is { Width: 320, Height: 240 };

        private string BuildTimStatus()
        {
            var parts = new List<string>
            {
                $"TIM {_timIndex + 1}/{_blocks?.Count ?? 0}",
                $"CLUT {_clutIndex + 1}/{_clutCount}"
            };

            if (_currentInfo != null)
            {
                parts.Add(_currentInfo.BppText);
                parts.Add($"{_currentInfo.Width}x{_currentInfo.Height}");

                if (_perPageCluts && _currentInfo.IsAtlas)
                    parts.Add("per-page CLUTs");
            }

            if (_dirty)
                parts.Add("modified");

            return string.Join("  |  ", parts);
        }

        private void UpdateUi()
        {
            bool hasDocument = _mode != ViewerMode.None;
            bool tim = _mode == ViewerMode.TimPack;
            bool sheet = _mode == ViewerMode.Sheet;

            btnPrev.Enabled = btnNext.Enabled = tim || sheet;
            prevItemToolStripMenuItem.Enabled = nextItemToolStripMenuItem.Enabled = tim || sheet;
            firstItemToolStripMenuItem.Enabled = lastItemToolStripMenuItem.Enabled = tim || sheet;

            bool multiClut = tim && _clutCount > 1;
            bool perPage = tim && chkPerPageClut.Checked;
            lblClut.Enabled = cmbClut.Enabled = multiClut && !perPage;
            chkPerPageClut.Enabled = tim && (_currentInfo?.IsAtlas ?? false);
            perPageClutToolStripMenuItem.Enabled = chkPerPageClut.Enabled;

            prevClutToolStripMenuItem.Enabled = nextClutToolStripMenuItem.Enabled = multiClut;
            cmbZoom.Enabled = hasDocument;

            saveToolStripMenuItem.Enabled = hasDocument;
            saveAsToolStripMenuItem.Enabled = hasDocument;
            exportSelectedToolStripMenuItem.Enabled = hasDocument;
            exportAllToolStripMenuItem.Enabled = tim || sheet;
            replaceSelectedToolStripMenuItem.Enabled = hasDocument;
            copyImageToolStripMenuItem.Enabled = _image != null;

            exportIconToolStripMenuItem.Enabled = sheet;
            replaceIconToolStripMenuItem.Enabled = sheet;
            saveSheetToolStripMenuItem.Enabled = sheet;

            UpdateInfo();
        }

        // ------------------------------
        // Preview rendering
        // ------------------------------

        private void SetSourceImage(Bitmap? bmp)
        {
            var old = _image;
            _image = bmp;
            old?.Dispose();
            UpdateDisplay();
            previewPanel.Invalidate();
        }

        private void UpdateDisplay()
        {
            if (_adjusting)
            {
                UpdateInfo();
                return;
            }

            _adjusting = true;

            try
            {
                var old = _display;
                _display = null;

                if (_image != null)
                {
                    _currentScale = ComputeScale(_image.Size);
                    _display = Compose(_image, _currentScale);
                    pictureBox1.Size = _display.Size;
                }

                pictureBox1.Image = _display;
                pictureBox1.Visible = _display != null;
                old?.Dispose();

                LayoutPicture();
            }
            finally
            {
                _adjusting = false;
            }

            UpdateInfo();
        }

        private int ComputeScale(Size image)
        {
            int fixedScale = (int)_zoom;
            if (fixedScale > 0)
                return fixedScale;

            if (image.Width <= 0 || image.Height <= 0)
                return 1;

            int availableWidth = Math.Max(32, previewPanel.ClientSize.Width - 20);
            int availableHeight = Math.Max(32, previewPanel.ClientSize.Height - 20);

            int scale = Math.Min(availableWidth / image.Width, availableHeight / image.Height);
            return Math.Clamp(scale, 1, 8);
        }

        private Bitmap Compose(Bitmap source, int scale)
        {
            var bmp = new Bitmap(source.Width * scale, source.Height * scale, PixelFormat.Format32bppPArgb);

            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.SmoothingMode = SmoothingMode.None;

                if (_showGrid)
                    DrawCheckerboard(g, bmp.Width, bmp.Height);
                else
                    g.Clear(FluentTheme.IsDarkMode ? Color.FromArgb(28, 28, 30) : Color.FromArgb(246, 246, 246));

                g.DrawImage(source, new Rectangle(0, 0, bmp.Width, bmp.Height));
            }

            return bmp;
        }

        private static void DrawCheckerboard(Graphics g, int width, int height)
        {
            const int cell = 8;

            using var light = new SolidBrush(FluentTheme.IsDarkMode
                ? Color.FromArgb(64, 64, 68)
                : Color.FromArgb(214, 214, 214));
            using var dark = new SolidBrush(FluentTheme.IsDarkMode
                ? Color.FromArgb(48, 48, 52)
                : Color.FromArgb(176, 176, 176));

            for (int y = 0; y < height; y += cell)
            {
                for (int x = 0; x < width; x += cell)
                {
                    var brush = (((x / cell) + (y / cell)) & 1) == 0 ? light : dark;
                    g.FillRectangle(brush, x, y, cell, cell);
                }
            }
        }

        private void LayoutPicture()
        {
            if (pictureBox1.Image == null)
            {
                pictureBox1.Location = Point.Empty;
                return;
            }

            var size = pictureBox1.Image.Size;
            int x = Math.Max(0, (previewPanel.ClientSize.Width - size.Width) / 2);
            int y = Math.Max(0, (previewPanel.ClientSize.Height - size.Height) / 2);
            pictureBox1.Location = new Point(x, y);
        }

        private void PreviewPanel_Resize(object? sender, EventArgs e)
        {
            if (_image == null || _adjusting)
            {
                previewPanel.Invalidate();
                return;
            }

            if (_zoom == ZoomMode.Fit && ComputeScale(_image.Size) != _currentScale)
                UpdateDisplay();
            else
                LayoutPicture();

            previewPanel.Invalidate();
        }

        private void PreviewPanel_Paint(object? sender, PaintEventArgs e)
        {
            if (_image != null)
                return;

            TextRenderer.DrawText(
                e.Graphics,
                "Open a TIM / PIX / PNG file to begin - or drop one here.",
                Font,
                previewPanel.ClientRectangle,
                FluentTheme.IsDarkMode ? Color.FromArgb(170, 170, 170) : Color.FromArgb(110, 110, 110),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }

        // ------------------------------
        // Zoom / view options
        // ------------------------------

        private void SetZoom(ZoomMode zoom)
        {
            _zoom = zoom;

            _updatingUi = true;
            int index = Array.IndexOf(ZoomOptions, zoom);
            if (index >= 0)
                cmbZoom.SelectedIndex = index;
            _updatingUi = false;

            ApplyZoomMenuChecks();
            UpdateDisplay();
        }

        private void ApplyZoomMenuChecks()
        {
            zoomFitToolStripMenuItem.Checked = _zoom == ZoomMode.Fit;
            zoom1xToolStripMenuItem.Checked = _zoom == ZoomMode.X1;
            zoom2xToolStripMenuItem.Checked = _zoom == ZoomMode.X2;
            zoom4xToolStripMenuItem.Checked = _zoom == ZoomMode.X4;
            zoom8xToolStripMenuItem.Checked = _zoom == ZoomMode.X8;
        }

        private void zoomToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem { Tag: string tag })
                return;

            SetZoom(tag == "Fit" ? ZoomMode.Fit : (ZoomMode)int.Parse(tag));
        }

        private void cmbZoom_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_updatingUi)
                return;

            int index = cmbZoom.SelectedIndex;
            if (index < 0 || index >= ZoomOptions.Length)
                return;

            _zoom = ZoomOptions[index];
            ApplyZoomMenuChecks();
            UpdateDisplay();
        }

        private void SetPerPageMode(bool value)
        {
            if (_updatingUi)
                return;

            _perPageCluts = value;

            _updatingUi = true;
            chkPerPageClut.Checked = value;
            perPageClutToolStripMenuItem.Checked = value;
            _updatingUi = false;

            if (_mode == ViewerMode.TimPack)
                RefreshTimDisplay();
            else
                UpdateUi();
        }

        private void chkPerPageClut_CheckedChanged(object? sender, EventArgs e) => SetPerPageMode(chkPerPageClut.Checked);

        private void perPageClutToolStripMenuItem_CheckedChanged(object? sender, EventArgs e) =>
            SetPerPageMode(perPageClutToolStripMenuItem.Checked);

        private void transparencyGridToolStripMenuItem_CheckedChanged(object? sender, EventArgs e)
        {
            _showGrid = transparencyGridToolStripMenuItem.Checked;
            UpdateDisplay();
        }

        // ------------------------------
        // Opening
        // ------------------------------

        private void openToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Open TIM / PIX / image",
                Filter =
                    "TIM, PIX and images (*.tim;*.pix;*.png;*.bmp)|*.tim;*.pix;*.png;*.bmp|" +
                    "PlayStation TIM / PIX (*.tim;*.pix)|*.tim;*.pix|" +
                    "Images (*.png;*.bmp)|*.png;*.bmp|" +
                    "All files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
                OpenPath(ofd.FileName);
        }

        private bool ConfirmDiscardChanges()
        {
            if (!_dirty)
                return true;

            var result = MessageBox.Show(this,
                "This document has unsaved changes.\n\nDiscard them?",
                "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
                return false;

            return result == DialogResult.Yes;
        }

        private void OpenPath(string path)
        {
            if (!ConfirmDiscardChanges())
                return;

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                Error($"Could not read the file:\n{ex.Message}", "Open");
                return;
            }

            string ext = Path.GetExtension(path).ToLowerInvariant();

            try
            {
                // TIM data (a single TIM, or a pack of them - also inside .pix files).
                if (PsxTimImage.LooksLikeTim(bytes))
                {
                    var blocks = TimPack.Parse(bytes);
                    if (blocks.Count == 0)
                        throw new InvalidDataException("A TIM header was found, but no complete TIM block could be read.");

                    LoadTimPack(blocks, path);
                    return;
                }

                if (ext is ".png" or ".bmp")
                {
                    LoadBackgroundFromImage(path);
                    return;
                }

                switch (PixLoader.Detect(bytes))
                {
                    case PixKind.RawBgr555_320x240:
                        LoadRawBackground(bytes, path);
                        break;

                    case PixKind.ItemSheet40x30_8bpp:
                        LoadSheet(path);
                        break;

                    default:
                        Error("Unsupported file.\n\nExpected a PlayStation TIM (also inside .pix), a raw 320x240 background PIX, " +
                              "an item sheet PIX, or a PNG/BMP image.", "Open");
                        break;
                }
            }
            catch (Exception ex)
            {
                Error($"Failed to open the file:\n{ex.Message}", "Open");
            }
        }

        private void LoadTimPack(List<TimBlock> blocks, string path)
        {
            _mode = ViewerMode.TimPack;
            _blocks = blocks;
            _sheet = null;
            _documentPath = path;
            _documentIsRawPix = false;
            _dirty = false;
            _timIndex = 0;
            _clutIndex = 0;

            ShowTim(0, resetClut: true);
        }

        private void LoadRawBackground(byte[] bytes, string path)
        {
            var bmp = Psx1555.DecodeRaw320x240(bytes);

            _mode = ViewerMode.Background;
            _blocks = null;
            _sheet = null;
            _documentPath = path;
            _documentIsRawPix = true;
            _dirty = false;

            SetSourceImage(bmp);
            SetStatus($"Loaded raw background: {Path.GetFileName(path)}");
            UpdateUi();
        }

        private void LoadBackgroundFromImage(string path)
        {
            using var source = new Bitmap(path);

            var bmp = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                g.DrawImage(source, 0, 0, source.Width, source.Height);

            _mode = ViewerMode.Background;
            _blocks = null;
            _sheet = null;
            _documentPath = path;
            _documentIsRawPix = false;
            _dirty = false;

            SetSourceImage(bmp);

            SetStatus(bmp is { Width: 320, Height: 240 }
                ? $"Loaded image background: {Path.GetFileName(path)}"
                : $"Loaded image ({bmp.Width}x{bmp.Height}) - only 320x240 can be saved as PIX.");

            UpdateUi();
        }

        private void LoadSheet(string path)
        {
            var loaded = ItemSheet.Load(path, _palette512);

            _mode = ViewerMode.Sheet;
            _blocks = null;
            _sheet = loaded;
            _documentPath = path;
            _documentIsRawPix = false;
            _dirty = false;
            _iconIndex = 0;

            ShowIcon();
        }

        private void closeToolStripMenuItem_Click(object? sender, EventArgs e) => Close();

        private void Biohazard_PIX_Viewer_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!ConfirmDiscardChanges())
                e.Cancel = true;
        }

        private void Biohazard_PIX_Viewer_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        }

        private void Biohazard_PIX_Viewer_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                OpenPath(files[0]);
        }

        // ------------------------------
        // TIM display
        // ------------------------------

        private void ShowTim(int index, bool resetClut)
        {
            if (_blocks == null || _blocks.Count == 0)
                return;

            _timIndex = Wrap(index, _blocks.Count);

            var parsed = PsxTimImage.Parse(_blocks[_timIndex].Data);
            _currentTim = parsed;
            _currentInfo = parsed.Info;
            _clutCount = Math.Max(1, parsed.Info.ClutCount);

            if (resetClut)
                _clutIndex = 0;

            _clutIndex = Math.Clamp(_clutIndex, 0, _clutCount - 1);

            _updatingUi = true;
            chkPerPageClut.Enabled = parsed.Info.IsAtlas;

            if (resetClut)
                chkPerPageClut.Checked = parsed.Info.IsAtlas;
            else if (!parsed.Info.IsAtlas)
                chkPerPageClut.Checked = false;

            _updatingUi = false;

            _perPageCluts = chkPerPageClut.Checked;

            BuildClutCombo();
            RefreshTimDisplay();
        }

        private void BuildClutCombo()
        {
            _updatingUi = true;

            cmbClut.Items.Clear();
            if (_clutCount <= 1)
            {
                cmbClut.Items.Add("CLUT 1/1");
            }
            else
            {
                for (int i = 0; i < _clutCount; i++)
                    cmbClut.Items.Add($"CLUT {i + 1}/{_clutCount}");
            }

            cmbClut.SelectedIndex = Math.Clamp(_clutIndex, 0, cmbClut.Items.Count - 1);
            _updatingUi = false;
        }

        private void SyncClutCombo()
        {
            _updatingUi = true;

            if (cmbClut.Items.Count != (_clutCount <= 1 ? 1 : _clutCount))
            {
                _updatingUi = false;
                BuildClutCombo();
                _updatingUi = true;
            }

            if (cmbClut.Items.Count > 0)
                cmbClut.SelectedIndex = Math.Clamp(_clutIndex, 0, cmbClut.Items.Count - 1);

            _updatingUi = false;
        }

        private Bitmap RenderTimBitmap()
        {
            if (_currentTim == null)
                throw new InvalidOperationException("No TIM is loaded.");

            var mode = _perPageCluts && _currentInfo?.IsAtlas == true
                ? TimRenderMode.PerPageClut
                : TimRenderMode.SelectedClut;

            return _currentTim.Render(mode, _clutIndex);
        }

        private void RefreshTimDisplay()
        {
            if (_currentTim == null)
                return;

            try
            {
                SetSourceImage(RenderTimBitmap());
                SetStatus(BuildTimStatus());
            }
            catch (Exception ex)
            {
                SetSourceImage(null);
                SetStatus("Render failed: " + ex.Message);
            }

            UpdateUi();
        }

        private void cmbClut_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_updatingUi || _mode != ViewerMode.TimPack)
                return;

            int index = cmbClut.SelectedIndex;
            if (index < 0 || index >= _clutCount)
                return;

            _clutIndex = index;

            if (_perPageCluts)
                SetPerPageMode(false);
            else
                RefreshTimDisplay();
        }

        // ------------------------------
        // PIX sheet display
        // ------------------------------

        private void ShowIcon()
        {
            if (_sheet == null || _sheet.IconCount == 0)
                return;

            _iconIndex = Wrap(_iconIndex, _sheet.IconCount);
            SetSourceImage(_sheet.RenderIcon(_iconIndex));
            SetStatus($"Icon {_iconIndex + 1}/{_sheet.IconCount} from PIX sheet");
            UpdateUi();
        }

        private void openSheetToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (!ConfirmDiscardChanges())
                return;

            using var ofd = new OpenFileDialog
            {
                Title = "Open PIX sheet",
                Filter = "PIX files (*.pix)|*.pix|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                LoadSheet(ofd.FileName);
            }
            catch (Exception ex)
            {
                Error($"Failed to open the sheet:\n{ex.Message}", "Open sheet");
            }
        }

        // ------------------------------
        // Navigation
        // ------------------------------

        private static int Wrap(int value, int count) => count <= 0 ? 0 : ((value % count) + count) % count;

        private void PrevItem()
        {
            if (_mode == ViewerMode.TimPack && _blocks is { Count: > 0 })
            {
                ShowTim(_timIndex - 1, resetClut: true);
                return;
            }

            if (_mode == ViewerMode.Sheet && _sheet is { IconCount: > 0 })
            {
                _iconIndex = Wrap(_iconIndex - 1, _sheet.IconCount);
                ShowIcon();
            }
        }

        private void NextItem()
        {
            if (_mode == ViewerMode.TimPack && _blocks is { Count: > 0 })
            {
                ShowTim(_timIndex + 1, resetClut: true);
                return;
            }

            if (_mode == ViewerMode.Sheet && _sheet is { IconCount: > 0 })
            {
                _iconIndex = Wrap(_iconIndex + 1, _sheet.IconCount);
                ShowIcon();
            }
        }

        private void FirstItem()
        {
            if (_mode == ViewerMode.TimPack && _blocks is { Count: > 0 })
            {
                ShowTim(0, resetClut: true);
                return;
            }

            if (_mode == ViewerMode.Sheet && _sheet is { IconCount: > 0 })
            {
                _iconIndex = 0;
                ShowIcon();
            }
        }

        private void LastItem()
        {
            if (_mode == ViewerMode.TimPack && _blocks is { Count: > 0 })
            {
                ShowTim(_blocks.Count - 1, resetClut: true);
                return;
            }

            if (_mode == ViewerMode.Sheet && _sheet is { IconCount: > 0 })
            {
                _iconIndex = _sheet.IconCount - 1;
                ShowIcon();
            }
        }

        private void ChangeClut(int delta)
        {
            if (_mode != ViewerMode.TimPack || _clutCount <= 1)
                return;

            _clutIndex = Wrap(_clutIndex + delta, _clutCount);

            if (_perPageCluts)
            {
                SetPerPageMode(false);
                return;
            }

            SyncClutCombo();
            RefreshTimDisplay();
        }

        private void btnPrev_Click(object? sender, EventArgs e) => PrevItem();
        private void btnNext_Click(object? sender, EventArgs e) => NextItem();
        private void prevItemToolStripMenuItem_Click(object? sender, EventArgs e) => PrevItem();
        private void nextItemToolStripMenuItem_Click(object? sender, EventArgs e) => NextItem();
        private void firstItemToolStripMenuItem_Click(object? sender, EventArgs e) => FirstItem();
        private void lastItemToolStripMenuItem_Click(object? sender, EventArgs e) => LastItem();
        private void prevClutToolStripMenuItem_Click(object? sender, EventArgs e) => ChangeClut(-1);
        private void nextClutToolStripMenuItem_Click(object? sender, EventArgs e) => ChangeClut(1);

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Arrow keys inside the CLUT / zoom combo should behave normally.
            if (ActiveControl is ComboBox)
                return base.ProcessCmdKey(ref msg, keyData);

            switch (keyData)
            {
                case Keys.Left:
                    PrevItem();
                    return true;
                case Keys.Right:
                    NextItem();
                    return true;
                case Keys.Up:
                    ChangeClut(-1);
                    return true;
                case Keys.Down:
                    ChangeClut(1);
                    return true;
                case Keys.Home:
                    FirstItem();
                    return true;
                case Keys.End:
                    LastItem();
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------
        // Save
        // ------------------------------

        private void saveToolStripMenuItem_Click(object? sender, EventArgs e) => SaveDocument();

        private void SaveDocument()
        {
            switch (_mode)
            {
                case ViewerMode.TimPack:
                    if (string.IsNullOrEmpty(_documentPath))
                    {
                        SaveDocumentAs();
                        return;
                    }

                    try
                    {
                        File.WriteAllBytes(_documentPath, TimPack.Rebuild(_blocks!));
                        _dirty = false;
                        SetStatus($"Saved {Path.GetFileName(_documentPath)} (original padding kept)");
                        UpdateUi();
                    }
                    catch (Exception ex)
                    {
                        Error($"Could not save:\n{ex.Message}", "Save");
                    }
                    break;

                case ViewerMode.Sheet:
                    if (string.IsNullOrEmpty(_documentPath))
                    {
                        SaveDocumentAs();
                        return;
                    }

                    try
                    {
                        _sheet!.Save(_documentPath);
                        _dirty = false;
                        SetStatus($"Saved {Path.GetFileName(_documentPath)}");
                        UpdateUi();
                    }
                    catch (Exception ex)
                    {
                        Error($"Could not save:\n{ex.Message}", "Save");
                    }
                    break;

                case ViewerMode.Background when _documentIsRawPix && _documentPath != null && IsBackgroundPixSize():
                    try
                    {
                        File.WriteAllBytes(_documentPath, Psx1555.EncodeRaw320x240(_image!));
                        _dirty = false;
                        SetStatus($"Saved {Path.GetFileName(_documentPath)}");
                        UpdateUi();
                    }
                    catch (Exception ex)
                    {
                        Error($"Could not save:\n{ex.Message}", "Save");
                    }
                    break;

                default:
                    SaveDocumentAs();
                    break;
            }
        }

        private void saveAsToolStripMenuItem_Click(object? sender, EventArgs e) => SaveDocumentAs();

        private void SaveDocumentAs()
        {
            switch (_mode)
            {
                case ViewerMode.TimPack:
                {
                    using var sfd = new SaveFileDialog
                    {
                        Title = "Save TIM pack",
                        Filter = "TIM stored as PIX (*.pix)|*.pix|PlayStation TIM (*.tim)|*.tim|All files (*.*)|*.*",
                        FileName = DocBaseName()
                    };

                    if (sfd.ShowDialog(this) != DialogResult.OK)
                        return;

                    try
                    {
                        File.WriteAllBytes(sfd.FileName, TimPack.Rebuild(_blocks!));
                        _documentPath = sfd.FileName;
                        _dirty = false;
                        SetStatus($"Saved {Path.GetFileName(sfd.FileName)}");
                        UpdateUi();
                    }
                    catch (Exception ex)
                    {
                        Error($"Could not save:\n{ex.Message}", "Save as");
                    }
                    break;
                }

                case ViewerMode.Sheet:
                {
                    using var sfd = new SaveFileDialog
                    {
                        Title = "Save PIX sheet",
                        Filter = "PIX files (*.pix)|*.pix|All files (*.*)|*.*",
                        FileName = DocBaseName()
                    };

                    if (sfd.ShowDialog(this) != DialogResult.OK)
                        return;

                    try
                    {
                        _sheet!.Save(sfd.FileName);
                        _documentPath = sfd.FileName;
                        _dirty = false;
                        SetStatus($"Saved {Path.GetFileName(sfd.FileName)}");
                        UpdateUi();
                    }
                    catch (Exception ex)
                    {
                        Error($"Could not save:\n{ex.Message}", "Save as");
                    }
                    break;
                }

                case ViewerMode.Background:
                {
                    using var sfd = new SaveFileDialog
                    {
                        Title = "Save background",
                        Filter = "PIX background (*.pix)|*.pix|PNG image (*.png)|*.png|Bitmap image (*.bmp)|*.bmp|All files (*.*)|*.*",
                        FileName = DocBaseName()
                    };

                    if (sfd.ShowDialog(this) != DialogResult.OK)
                        return;

                    try
                    {
                        switch (sfd.FilterIndex)
                        {
                            case 1:
                                if (!IsBackgroundPixSize())
                                    throw new InvalidOperationException("A background PIX must be 320x240.");
                                File.WriteAllBytes(sfd.FileName, Psx1555.EncodeRaw320x240(_image!));
                                break;

                            case 2:
                                _image!.Save(sfd.FileName, ImageFormat.Png);
                                break;

                            default:
                                _image!.Save(sfd.FileName, ImageFormat.Bmp);
                                break;
                        }

                        _documentPath = sfd.FileName;
                        SetStatus($"Saved {Path.GetFileName(sfd.FileName)}");
                    }
                    catch (Exception ex)
                    {
                        Error($"Could not save:\n{ex.Message}", "Save as");
                    }
                    break;
                }

                default:
                    Info("Nothing to save - open a file first.", "Save as");
                    break;
            }
        }

        // ------------------------------
        // Export
        // ------------------------------

        private void exportSelectedToolStripMenuItem_Click(object? sender, EventArgs e) => ExportCurrent();

        private void ExportCurrent()
        {
            switch (_mode)
            {
                case ViewerMode.TimPack:
                    ExportTim();
                    break;

                case ViewerMode.Sheet:
                    ExportIcon();
                    break;

                case ViewerMode.Background:
                    ExportBackgroundImage();
                    break;

                default:
                    Info("Open a TIM, PIX sheet or background first.", "Export");
                    break;
            }
        }

        private void ExportTim()
        {
            if (_blocks == null || _currentTim == null)
                return;

            using var sfd = new SaveFileDialog
            {
                Title = "Export current TIM",
                Filter =
                    "PlayStation TIM (*.tim)|*.tim|" +
                    "TIM stored as PIX (*.pix)|*.pix|" +
                    "PNG image (*.png)|*.png|" +
                    "PNG - every CLUT stacked (*.png)|*.png|" +
                    "Bitmap image (*.bmp)|*.bmp|" +
                    "All files (*.*)|*.*",
                FilterIndex = 1,
                FileName = $"{DocBaseName()}_{_timIndex:D3}"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                switch (sfd.FilterIndex)
                {
                    case 1:
                    case 2:
                        File.WriteAllBytes(sfd.FileName, _blocks[_timIndex].Data);
                        SetStatus($"Exported {Path.GetFileName(sfd.FileName)}");
                        break;

                    case 3:
                        using (var bmp = RenderTimBitmap())
                            SaveBitmap(bmp, sfd.FileName, ImageFormat.Png);
                        SetStatus($"Exported {Path.GetFileName(sfd.FileName)}");
                        break;

                    case 4:
                        using (var bmp = _currentTim.Render(TimRenderMode.AllCluts, _clutIndex))
                            SaveBitmap(bmp, sfd.FileName, ImageFormat.Png);
                        SetStatus($"Exported {Path.GetFileName(sfd.FileName)} (all CLUTs)");
                        break;

                    default:
                        using (var bmp = RenderTimBitmap())
                            SaveBitmap(bmp, sfd.FileName, ImageFormat.Bmp);
                        SetStatus($"Exported {Path.GetFileName(sfd.FileName)}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Error($"Export failed:\n{ex.Message}", "Export");
            }
        }

        private static void SaveBitmap(Bitmap bmp, string path, ImageFormat format)
        {
            if (string.IsNullOrEmpty(Path.GetExtension(path)))
                path += format.Equals(ImageFormat.Bmp) ? ".bmp" : ".png";

            bmp.Save(path, format);
        }

        private void ExportIcon()
        {
            if (_sheet == null)
            {
                Info("Open a PIX sheet first.", "Export");
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Export icon",
                Filter =
                    "PlayStation TIM (*.tim)|*.tim|" +
                    "PNG image (*.png)|*.png|" +
                    "Bitmap image (*.bmp)|*.bmp|" +
                    "All files (*.*)|*.*",
                FileName = $"{DocBaseName()}_icon{_iconIndex:D3}"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                switch (sfd.FilterIndex)
                {
                    case 1:
                        _sheet.ExportIconAsTim(_iconIndex, sfd.FileName);
                        break;

                    case 2:
                        using (var bmp = _sheet.RenderIcon(_iconIndex))
                            SaveBitmap(bmp, sfd.FileName, ImageFormat.Png);
                        break;

                    default:
                        using (var bmp = _sheet.RenderIcon(_iconIndex))
                            SaveBitmap(bmp, sfd.FileName, ImageFormat.Bmp);
                        break;
                }

                SetStatus($"Exported icon {_iconIndex + 1}");
            }
            catch (Exception ex)
            {
                Error($"Export failed:\n{ex.Message}", "Export");
            }
        }

        private void ExportBackgroundImage()
        {
            if (_image == null)
                return;

            using var sfd = new SaveFileDialog
            {
                Title = "Export background",
                Filter = "PNG image (*.png)|*.png|Bitmap image (*.bmp)|*.bmp|PIX background (*.pix)|*.pix|All files (*.*)|*.*",
                FileName = DocBaseName()
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                switch (sfd.FilterIndex)
                {
                    case 1:
                        _image.Save(sfd.FileName, ImageFormat.Png);
                        break;

                    case 2:
                        _image.Save(sfd.FileName, ImageFormat.Bmp);
                        break;

                    default:
                        if (!IsBackgroundPixSize())
                            throw new InvalidOperationException("A background PIX must be 320x240.");
                        File.WriteAllBytes(sfd.FileName, Psx1555.EncodeRaw320x240(_image));
                        break;
                }

                SetStatus($"Exported {Path.GetFileName(sfd.FileName)}");
            }
            catch (Exception ex)
            {
                Error($"Export failed:\n{ex.Message}", "Export");
            }
        }

        private void exportAllToolStripMenuItem_Click(object? sender, EventArgs e) => ExportAll();

        private void ExportAll()
        {
            if (_mode == ViewerMode.TimPack && _blocks is { Count: > 0 })
            {
                ExportAllTims();
                return;
            }

            if (_mode == ViewerMode.Sheet && _sheet != null)
            {
                ExportAllIcons();
                return;
            }

            Info("Nothing to batch export - open a TIM pack or a PIX sheet.", "Export all");
        }

        private void ExportAllTims()
        {
            using var sfd = new SaveFileDialog
            {
                Title = "Export every TIM (a numbered file per TIM)",
                Filter = "PNG images (one file per TIM)|*.png|PlayStation TIM files (one file per TIM)|*.tim",
                FileName = $"{DocBaseName()}_all"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            string directory = Path.GetDirectoryName(sfd.FileName) ?? Directory.GetCurrentDirectory();
            string prefix = Path.GetFileNameWithoutExtension(sfd.FileName);
            bool raw = sfd.FilterIndex == 2;
            int written = 0;

            try
            {
                for (int i = 0; i < _blocks!.Count; i++)
                {
                    string target = Path.Combine(directory, $"{prefix}_{i:D3}.{(raw ? "tim" : "png")}");

                    if (raw)
                    {
                        File.WriteAllBytes(target, _blocks[i].Data);
                    }
                    else
                    {
                        var tim = PsxTimImage.Parse(_blocks[i].Data);
                        int clut = Math.Min(_clutIndex, Math.Max(1, tim.Info.ClutCount) - 1);
                        bool perPage = _perPageCluts && tim.Info.IsAtlas;

                        using var bmp = tim.Render(
                            perPage ? TimRenderMode.PerPageClut : TimRenderMode.SelectedClut,
                            clut);

                        bmp.Save(target, ImageFormat.Png);
                    }

                    written++;
                }

                SetStatus($"Exported {written} TIM(s) to {directory}");
                Info($"Exported {written} TIM(s) to:\n{directory}", "Export all");
            }
            catch (Exception ex)
            {
                Error($"Export failed after {written} TIM(s):\n{ex.Message}", "Export all");
            }
        }

        private void ExportAllIcons()
        {
            using var sfd = new SaveFileDialog
            {
                Title = "Export every icon (a numbered file per icon)",
                Filter = "PNG images (one file per icon)|*.png|PlayStation TIM files (one file per icon)|*.tim",
                FileName = $"{DocBaseName()}_icons"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            string directory = Path.GetDirectoryName(sfd.FileName) ?? Directory.GetCurrentDirectory();
            string prefix = Path.GetFileNameWithoutExtension(sfd.FileName);
            bool raw = sfd.FilterIndex == 2;
            int written = 0;

            try
            {
                for (int i = 0; i < _sheet!.IconCount; i++)
                {
                    string target = Path.Combine(directory, $"{prefix}_{i:D3}.{(raw ? "tim" : "png")}");

                    if (raw)
                    {
                        _sheet.ExportIconAsTim(i, target);
                    }
                    else
                    {
                        using var bmp = _sheet.RenderIcon(i);
                        bmp.Save(target, ImageFormat.Png);
                    }

                    written++;
                }

                SetStatus($"Exported {written} icon(s) to {directory}");
                Info($"Exported {written} icon(s) to:\n{directory}", "Export all");
            }
            catch (Exception ex)
            {
                Error($"Export failed after {written} icon(s):\n{ex.Message}", "Export all");
            }
        }

        // ------------------------------
        // Replace / import
        // ------------------------------

        private void replaceSelectedToolStripMenuItem_Click(object? sender, EventArgs e) => ReplaceCurrent();

        private void ReplaceCurrent()
        {
            switch (_mode)
            {
                case ViewerMode.TimPack:
                    ReplaceTim();
                    break;

                case ViewerMode.Sheet:
                    ReplaceIcon();
                    break;

                case ViewerMode.Background:
                    ReplaceBackground();
                    break;

                default:
                    Info("Open a file before replacing artwork.", "Replace");
                    break;
            }
        }

        private void ReplaceTim()
        {
            if (_blocks == null || _currentTim == null)
                return;

            using var ofd = new OpenFileDialog
            {
                Title = $"Replace TIM {_timIndex + 1}",
                Filter =
                    "Supported (TIM / PIX / PNG / BMP)|*.tim;*.pix;*.png;*.bmp|" +
                    "PlayStation TIM (*.tim;*.pix)|*.tim;*.pix|" +
                    "Images (*.png;*.bmp)|*.png;*.bmp|" +
                    "All files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                string ext = Path.GetExtension(ofd.FileName).ToLowerInvariant();
                byte[] replacement;

                if (ext is ".tim" or ".pix")
                {
                    var bytes = File.ReadAllBytes(ofd.FileName);
                    if (!PsxTimImage.LooksLikeTim(bytes))
                        throw new InvalidDataException("That file is not a PlayStation TIM.");

                    var inner = TimPack.Parse(bytes);
                    if (inner.Count == 0)
                        throw new InvalidDataException("No complete TIM could be read from that file.");

                    if (inner.Count > 1 && MessageBox.Show(this,
                            $"That file contains {inner.Count} TIMs.\n\nUse the first one for this slot?",
                            "Replace", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;

                    replacement = inner[0].Data;
                }
                else if (ext is ".png" or ".bmp")
                {
                    using var art = new Bitmap(ofd.FileName);
                    replacement = PsxTimImage.BuildReplacement(_blocks[_timIndex].Data, art, _clutIndex);
                }
                else
                {
                    throw new InvalidDataException("Use a TIM/PIX, or a PNG/BMP image.");
                }

                PsxTimImage.Parse(replacement); // sanity check

                _blocks[_timIndex].Data = replacement;
                _dirty = true;

                ShowTim(_timIndex, resetClut: false);
                SetStatus($"TIM {_timIndex + 1}/{_blocks.Count} replaced - Ctrl+S writes the pack back.");
            }
            catch (Exception ex)
            {
                Error($"Replace failed:\n{ex.Message}", "Replace");
            }
        }

        private void ReplaceIcon()
        {
            if (_sheet == null)
                return;

            using var ofd = new OpenFileDialog
            {
                Title = $"Replace icon {_iconIndex + 1}",
                Filter =
                    "Supported (TIM / PIX / PNG / BMP)|*.tim;*.pix;*.png;*.bmp|" +
                    "PlayStation TIM (*.tim;*.pix)|*.tim;*.pix|" +
                    "Images (*.png;*.bmp)|*.png;*.bmp|" +
                    "All files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                string ext = Path.GetExtension(ofd.FileName).ToLowerInvariant();

                if (ext is ".tim" or ".pix")
                {
                    var bytes = File.ReadAllBytes(ofd.FileName);
                    if (!PsxTimImage.LooksLikeTim(bytes))
                        throw new InvalidDataException("That file is not a PlayStation TIM.");

                    if (Tim.IsItemTim40x30_8bpp(bytes))
                    {
                        // Exact ItemEditor layout: copy the indices straight across.
                        _sheet.ReplaceIconFromTim(_iconIndex, ofd.FileName);
                    }
                    else
                    {
                        // Any other TIM: decode and requantise onto the item palette.
                        var tim = PsxTimImage.Parse(bytes);
                        using var bmp = tim.Render(TimRenderMode.SelectedClut, 0);
                        _sheet.ReplaceIconFromBitmap(_iconIndex, bmp, resizeIfNeeded: true);
                    }
                }
                else if (ext is ".png" or ".bmp")
                {
                    using var img = new Bitmap(ofd.FileName);

                    bool resize = false;
                    if (img.Width != ItemSheet.IconW || img.Height != ItemSheet.IconH)
                    {
                        resize = MessageBox.Show(this,
                            $"Image is {img.Width}x{img.Height}. Icons are {ItemSheet.IconW}x{ItemSheet.IconH}.\n\nResize automatically?",
                            "Resize", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
                    }

                    _sheet.ReplaceIconFromBitmap(_iconIndex, img, resize);
                }
                else
                {
                    throw new InvalidDataException("Use a TIM/PIX, or a PNG/BMP image.");
                }

                _dirty = true;
                ShowIcon();
                SetStatus($"Icon {_iconIndex + 1} replaced - Ctrl+S saves the sheet.");
            }
            catch (Exception ex)
            {
                Error($"Replace failed:\n{ex.Message}", "Replace");
            }
        }

        private void ReplaceBackground()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Replace background",
                Filter = "Images (*.png;*.bmp)|*.png;*.bmp|PIX background (*.pix)|*.pix|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                string ext = Path.GetExtension(ofd.FileName).ToLowerInvariant();
                Bitmap bmp;

                if (ext is ".png" or ".bmp")
                {
                    using var source = new Bitmap(ofd.FileName);
                    bmp = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
                    using var g = Graphics.FromImage(bmp);
                    g.DrawImage(source, 0, 0, source.Width, source.Height);
                }
                else
                {
                    bmp = Psx1555.DecodeRaw320x240(File.ReadAllBytes(ofd.FileName));
                }

                SetSourceImage(bmp);
                _dirty = true;
                SetStatus("Background replaced - Ctrl+S saves it.");
                UpdateUi();
            }
            catch (Exception ex)
            {
                Error($"Replace failed:\n{ex.Message}", "Replace");
            }
        }

        // ------------------------------
        // PIX sheet commands
        // ------------------------------

        private void exportIconToolStripMenuItem_Click(object? sender, EventArgs e) => ExportIcon();

        private void replaceIconToolStripMenuItem_Click(object? sender, EventArgs e) => ReplaceIcon();

        private void saveSheetToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (_sheet == null)
            {
                Info("Open a PIX sheet first.", "Save sheet");
                return;
            }

            SaveDocument();
        }

        private void copyImageToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (_image == null)
                return;

            try
            {
                using var copy = new Bitmap(_image);
                Clipboard.SetImage(copy);
                SetStatus("Image copied to the clipboard.");
            }
            catch (Exception ex)
            {
                Error($"Could not copy the image:\n{ex.Message}", "Copy image");
            }
        }
    }
}

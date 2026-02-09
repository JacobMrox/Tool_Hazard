using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Tool_Hazard.Biohazard.PIX;

namespace Tool_Hazard.Forms
{
    public partial class Biohazard_PIX_Viewer : Form
    {
        // Globals
        private string? _bgPath;
        private string? _bgLoadedExt; // ".pix" ".png" ".bmp" etc

        // Sheet state
        private ItemSheet? sheet;
        private string? _sheetPath;
        private int _iconIndex = 0;

        // TIM pack state (Multi TIM / TIM-in-PIX)
        private List<byte[]>? _timPack;
        private int _timIndex = 0;

        // CLUT (palette) viewing state for single TIMs that contain multiple CLUTs
        private int _clutIndex = 0;
        private int _clutCount = 1;

        // Alternative to [DefaultPalette512] base64 string: read from embedded resource (extracted from Biohazard/PIX/Res.res)
        // Call this once and cache it somewhere static if necessary
        byte[] palette512 = BorlandResReader.ReadPalette512FromEmbeddedRes("Tool_Hazard.Biohazard.PIX.Res.res");

        public Biohazard_PIX_Viewer()
        {
            InitializeComponent();

            // Keep preview centered when resizing the window
            this.Resize += (_, __) => CenterPictureBox();

            // Ensure nav buttons are correct on startup
            UpdateNavButtons();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            SetStatus("Why are you clicking this? This isn't Deadly Silence!");
        }

        // ----------------------
        // UI stuff
        // ----------------------
        private void SetStatus(string text)
        {
            if (toolStripStatusLabel1 != null)
                toolStripStatusLabel1.Text = text;
        }
        private void CenterPictureBox()
        {
            if (pictureBox1.Image == null)
                return;

            // make PictureBox fit image exactly
            pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
            pictureBox1.Width = pictureBox1.Image.Width;
            pictureBox1.Height = pictureBox1.Image.Height;

            // center it within its parent (panel or form)
            var parent = pictureBox1.Parent;
            if (parent == null) return;

            int x = Math.Max(0, (parent.ClientSize.Width - pictureBox1.Width) / 2);
            int y = Math.Max(0, (parent.ClientSize.Height - pictureBox1.Height) / 2);

            pictureBox1.Left = x;
            pictureBox1.Top = y;
        }

        // ----------------------
        // Helper methods
        // ----------------------

        private static int GetTimClutCount(byte[] tim)
        {
            // TIM: magic(4) flags(4) [clut block] [image block]
            if (tim.Length < 8) return 1;

            uint flags = BitConverter.ToUInt32(tim, 4);
            bool hasClut = (flags & 0x8) != 0;
            if (!hasClut) return 1;

            int pos = 8;
            if (pos + 12 > tim.Length) return 1;

            // CLUT block:
            // size(4), x(2), y(2), w(2), h(2)
            // w = number of colors per CLUT (16 for 4bpp, 256 for 8bpp)
            // h = number of CLUT rows (this is the # of CLUT sets)
            ushort h = BitConverter.ToUInt16(tim, pos + 10);
            return Math.Max(1, (int)h);
        }

        private void ClearSheetState()
        {
            sheet = null;
            _sheetPath = null;
            _iconIndex = 0;
            SetStatus("Sheet State Cleared!");
            UpdateNavButtons();
        }

        private void ClearBackgroundState()
        {
            _bgPath = null;
            _bgLoadedExt = null;
            SetStatus("Cleared Background State!");
            UpdateNavButtons();
        }

        private void ClearTimPackState()
        {
            _timPack = null;
            _timIndex = 0;
            SetStatus("Cleared Tim State Pack!");
            UpdateNavButtons();
        }

        private void UpdateNavButtons()
        {
            // Enable Next/Prev if we have either a sheet OR a multi-TIM pack loaded
            bool hasSheet = sheet != null;
            bool hasTimPack = _timPack != null && _timPack.Count > 0;

            Next.Enabled = hasSheet || hasTimPack;
            Prev.Enabled = hasSheet || hasTimPack;

            // ALSO update multipack menu items
            UpdateMultipackMenuItems();

            // ALSO update sheet menu items
            UpdateSheetMenuItems();
        }

        private bool IsMultipackActive()
        {
            return sheet == null && _timPack != null && _timPack.Count > 0;
        }

        private void UpdateMultipackMenuItems()
        {
            bool hasMultipack = sheet == null && _timPack != null && _timPack.Count > 0;

            exportSelectedMultipackToolStripMenuItem.Enabled = hasMultipack;
            replaceSelectedMultipackToolStripMenuItem.Enabled = hasMultipack;
        }

        private void UpdateSheetMenuItems()
        {
            bool hasSheet = sheet != null;

            exportToolStripMenuItem.Enabled = hasSheet;
            replaceToolStripMenuItem.Enabled = hasSheet;
            saveToolStripMenuItem1.Enabled = hasSheet;
        }


        // ----------------------
        //Background pix handling
        // ----------------------
        private static bool IsImageExt(string? ext)
        {
            ext = ext?.ToLowerInvariant();
            return ext is ".png" or ".bmp";
        }

        private void SetPictureBoxImage(Bitmap bmp)
        {
            var old = pictureBox1.Image;
            pictureBox1.Image = bmp;
            old?.Dispose();

            // Keep preview centered
            CenterPictureBox();
        }

        // TIM pack splitter: supports a file containing multiple TIM blocks concatenated (eg ITEMG.PIX)
        // (Also works for plain .tim files if you ever add that to the open filter.)
        // IMPORTANT: Some games pad between TIMs, so we scan forward for the next TIM header instead of breaking.
        private static List<byte[]> SplitTimPack(byte[] data)
        {
            var list = new List<byte[]>();
            int pos = 0;

            while (true)
            {
                int start = FindNextTimOffset(data, pos);
                if (start < 0)
                    break;

                // Parse one TIM from 'start'. If parse fails, advance and keep scanning.
                if (!TryReadOneTimBlock(data, start, out int endExclusive))
                {
                    pos = start + 4;
                    continue;
                }

                var one = new byte[endExclusive - start];
                Buffer.BlockCopy(data, start, one, 0, one.Length);
                list.Add(one);

                pos = endExclusive;
            }

            return list;
        }

        // Find next TIM header (0x10 00 00 00) starting at/after 'from'
        private static int FindNextTimOffset(byte[] data, int from)
        {
            for (int i = Math.Max(0, from); i + 4 <= data.Length; i++)
            {
                if (data[i] == 0x10 && data[i + 1] == 0x00 && data[i + 2] == 0x00 && data[i + 3] == 0x00)
                    return i;
            }
            return -1;
        }

        // Validate and compute the end offset (exclusive) of a single TIM block at 'start'
        private static bool TryReadOneTimBlock(byte[] data, int start, out int endExclusive)
        {
            endExclusive = start;

            // need at least magic + flags
            if (start + 8 > data.Length)
                return false;

            // magic
            uint magic = BitConverter.ToUInt32(data, start);
            if (magic != 0x00000010u)
                return false;

            uint flags = BitConverter.ToUInt32(data, start + 4);

            // Basic sanity: bpp mode is low 3 bits (0..3), plus optional CLUT bit (0x8)
            uint mode = flags & 0x7;
            if (mode > 3)
                return false;

            bool hasClut = (flags & 0x8) != 0;

            int pos = start + 8;

            // CLUT block
            if (hasClut)
            {
                if (pos + 4 > data.Length) return false;
                uint clutSize = BitConverter.ToUInt32(data, pos);
                if (clutSize < 12) return false; // header minimum
                if (pos + (int)clutSize > data.Length) return false;
                pos += (int)clutSize;
            }

            // IMAGE block
            if (pos + 4 > data.Length) return false;
            uint imgSize = BitConverter.ToUInt32(data, pos);
            if (imgSize < 12) return false; // header minimum
            if (pos + (int)imgSize > data.Length) return false;
            pos += (int)imgSize;

            // TIM blocks are often 4-byte aligned; we can ignore padding for slicing.
            endExclusive = pos;
            return true;
        }

        private void RenderCurrentTimFromPack()
        {
            if (_timPack == null || _timPack.Count == 0)
                return;

            // clamp tim index
            if (_timIndex < 0) _timIndex = 0;
            if (_timIndex >= _timPack.Count) _timIndex = _timPack.Count - 1;

            // update clut count for this TIM
            _clutCount = GetTimClutCount(_timPack[_timIndex]);
            if (_clutIndex < 0) _clutIndex = 0;
            if (_clutIndex >= _clutCount) _clutIndex = 0;

            // Decode current TIM using selected CLUT and show it
            var bmp = Tim.DecodeToBitmap(_timPack[_timIndex], clutIndex: _clutIndex);
            SetPictureBoxImage(bmp);

            // show "tim/clut" status
            SetStatus($"Loaded TIM {_timIndex + 1}/{_timPack.Count}  |  CLUT {_clutIndex + 1}/{_clutCount}");

            // optional: disable/enable Next/Prev
            UpdateNavButtons();
        }


        //File -> Open, Menu strip hooker 
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Clear the sheet; just in case, before a new PIX:
            ClearSheetState();

            // We are opening a background, so clear any previously loaded sheet state and allow tim pack navigation
            // (Also clear the previous tim pack, because a new file might not be TIM at all.)
            ClearTimPackState();

            // Open file dialog for background PIX or images
            using OpenFileDialog ofd = new OpenFileDialog
            {
                Filter =
    "Background PIX / Images / TIM (*.pix;*.tim;*.png;*.bmp)|*.pix;*.tim;*.png;*.bmp|" +
    "PIX / TIM files (*.pix;*.tim)|*.pix;*.tim|" +
    "PNG files (*.png)|*.png|" +
    "Bitmap files (*.bmp)|*.bmp|" +
    "All files (*.*)|*.*"

            };

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            _bgPath = ofd.FileName;
            _bgLoadedExt = Path.GetExtension(ofd.FileName)?.ToLowerInvariant();

            try
            {
                if (_bgLoadedExt == ".pix" || _bgLoadedExt == ".tim")
                {
                    // This supports raw 320x240 16bpp PIX and TIM/MULTI-TIM (both real .tim and disguised as .pix)
                    byte[] bytes = File.ReadAllBytes(ofd.FileName);

                    // TIM or MULTI-TIM (ITEMG.PIX, .tim, etc)
                    if (bytes.Length >= 4 && BitConverter.ToUInt32(bytes, 0) == 0x00000010u)
                    {
                        _timPack = SplitTimPack(bytes);
                        _timIndex = 0;

                        if (_timPack == null || _timPack.Count == 0)
                            throw new InvalidDataException("TIM file detected but no TIM blocks could be parsed.");

                        RenderCurrentTimFromPack();
                        UpdateNavButtons();
                        return;
                    }

                    // If user opened a real .tim but it doesn't start with TIM magic, it's invalid
                    if (_bgLoadedExt == ".tim")
                        throw new InvalidDataException("File has .tim extension but does not contain a valid TIM header.");

                    // Not a TIM, so decode as background PIX (raw 320x240)
                    var bmp = PixLoader.LoadAsBitmap(ofd.FileName);
                    SetPictureBoxImage(bmp);
                    UpdateNavButtons();
                }
                else if (IsImageExt(_bgLoadedExt))
                {
                    // Load PNG/BMP; normalize to 32bpp and (optionally) enforce 320x240
                    using var tmp = new Bitmap(ofd.FileName);

                    if (tmp.Width != 320 || tmp.Height != 240)
                    {
                        // Keep it strict (recommended for RE backgrounds)
                        MessageBox.Show("Background images must be 320x240 for PS1 backgrounds.", "Invalid size",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;

                        // If you prefer auto-resize instead of strict, replace the above with:
                        // var resized = new Bitmap(320, 240, PixelFormat.Format32bppArgb);
                        // using (var g = Graphics.FromImage(resized)) g.DrawImage(tmp, 0, 0, 320, 240);
                        // SetPictureBoxImage(resized);
                    }

                    var bmp = new Bitmap(320, 240, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(bmp))
                        g.DrawImage(tmp, 0, 0, 320, 240);

                    SetPictureBoxImage(bmp);
                    UpdateNavButtons();
                }
                else
                {
                    MessageBox.Show("Unsupported file type.", "Open", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open file:\n{ex.Message}", "Open error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            SetStatus("Loaded: " + ofd.FileName);
        }

        //Save As Menustrip Hook
        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image is not Bitmap bmp)
            {
                MessageBox.Show("No image loaded.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (bmp.Width != 320 || bmp.Height != 240)
            {
                MessageBox.Show("Current image is not 320x240. Cannot save as background PIX.", "Save",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                // Still allow PNG/BMP export if you want, but PIX should be blocked.
                // We'll still allow, because PNG/BMP can store any size.
            }

            using SaveFileDialog sfd = new SaveFileDialog
            {
                Filter =
                    "PIX background (*.pix)|*.pix|" +
                    "PNG image (*.png)|*.png|" +
                    "Bitmap image (*.bmp)|*.bmp|" +
                    "All files (*.*)|*.*",
                FileName = _bgPath != null ? Path.GetFileNameWithoutExtension(_bgPath) : "BACKGROUND"
            };

            if (sfd.ShowDialog() != DialogResult.OK)
                return;

            var ext = Path.GetExtension(sfd.FileName)?.ToLowerInvariant();

            try
            {
                switch (ext)
                {
                    case ".png":
                        bmp.Save(sfd.FileName, ImageFormat.Png);
                        break;

                    case ".bmp":
                        bmp.Save(sfd.FileName, ImageFormat.Bmp);
                        break;

                    case ".pix":
                        if (bmp.Width != 320 || bmp.Height != 240)
                            throw new InvalidOperationException("To save as PIX, image must be 320x240.");

                        // Raw 320x240 16-bit BGR555 (matches your background PIX)
                        var raw = Psx1555.EncodeRaw320x240(bmp);
                        File.WriteAllBytes(sfd.FileName, raw);
                        break;

                    default:
                        // If user typed no extension, default to PNG (or change to BMP if you prefer)
                        var fallback = sfd.FileName;
                        if (string.IsNullOrWhiteSpace(ext))
                        {
                            fallback += ".png";
                            bmp.Save(fallback, ImageFormat.Png);
                        }
                        else
                        {
                            throw new InvalidOperationException("Unsupported save format. Use .pix, .png, or .bmp.");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save:\n{ex.Message}", "Save error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ----------------------
        // Item sheet pix handling
        // ----------------------

        private void RenderCurrentIcon()
        {
            if (sheet == null) return;

            // clamp index
            if (_iconIndex < 0) _iconIndex = 0;
            if (_iconIndex >= sheet.IconCount) _iconIndex = sheet.IconCount - 1;

            // avoid leaking old bitmaps
            var old = pictureBox1.Image;
            pictureBox1.Image = sheet.RenderIcon(_iconIndex);
            old?.Dispose();

            // keep preview centered
            CenterPictureBox();

            // show "index/total" like multipack
            //toolStripStatusLabel1.Text = $"{_iconIndex + 1}/{sheet.IconCount}";
            SetStatus($"Loaded {_iconIndex + 1}/{sheet.IconCount} from PIX Sheet");

        }

        private void openToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            // Clear background state, because we are opening a sheet now:
            ClearBackgroundState();

            // Clear the previous TIM pack (sheet navigation and TIM-pack navigation are different modes)
            ClearTimPackState();

            // Open file dialog for item sheet PIX
            using OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "PIX files (*.pix)|*.pix|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                //sheet = ItemSheet.Load(ofd.FileName);//Old loading method that only supports raw PIX sheets without relying on Borland Res.res Reader
                sheet = Tool_Hazard.Biohazard.PIX.ItemSheet.Load(ofd.FileName, palette512);
                _sheetPath = ofd.FileName;
                _iconIndex = 0;
                RenderCurrentIcon();
                UpdateNavButtons();
            }
        }

        //Export selected icon index in sheet as its own TIM file (with TIM header, not raw)
        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sheet == null)
            {
                MessageBox.Show("Load a PIX sheet first.", "No sheet loaded",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using SaveFileDialog sfd = new SaveFileDialog
            {
                Filter =
                    "PlayStation TIM (*.tim)|*.tim|" +
                    "PNG image (*.png)|*.png|" +
                    "Bitmap image (*.bmp)|*.bmp|" +
                    "All files (*.*)|*.*",
                FileName = $"icon_{_iconIndex:D3}"
            };

            if (sfd.ShowDialog() != DialogResult.OK)
                return;

            string ext = Path.GetExtension(sfd.FileName).ToLowerInvariant();

            if (ext == ".tim")
            {
                sheet.ExportIconAsTim(_iconIndex, sfd.FileName);
                return;
            }

            // PNG/BMP export is just rendered bitmap
            using var bmp = sheet.RenderIcon(_iconIndex);

            if (ext == ".png")
                bmp.Save(sfd.FileName, ImageFormat.Png);
            else if (ext == ".bmp")
                bmp.Save(sfd.FileName, ImageFormat.Bmp);
            else
            {
                // If user typed no extension, default to PNG
                bmp.Save(sfd.FileName + ".png", ImageFormat.Png);
            }
        }

        // Replace current selected icon index in sheet with our own TIM file
        private void replaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sheet == null)
            {
                MessageBox.Show("Load a PIX sheet first.", "No sheet loaded",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using OpenFileDialog ofd = new OpenFileDialog
            {
                Filter =
                    "Supported (TIM/PNG/BMP)|*.tim;*.png;*.bmp|" +
                    "PlayStation TIM (*.tim)|*.tim|" +
                    "PNG image (*.png)|*.png|" +
                    "Bitmap image (*.bmp)|*.bmp|" +
                    "All files (*.*)|*.*"
            };

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            string ext = Path.GetExtension(ofd.FileName).ToLowerInvariant();

            try
            {
                if (ext == ".tim")
                {
                    sheet.ReplaceIconFromTim(_iconIndex, ofd.FileName);
                }
                else if (ext == ".png" || ext == ".bmp")
                {
                    using var img = new Bitmap(ofd.FileName);

                    // Strict size by default. If you want auto-resize, pass resizeIfNeeded: true
                    if (img.Width != ItemSheet.IconW ||
                        img.Height != ItemSheet.IconH)
                    {
                        var res = MessageBox.Show(
                            $"Image is {img.Width}x{img.Height}. Icon size is 40x30.\n\nResize automatically?",
                            "Resize?",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        bool resize = (res == DialogResult.Yes);
                        sheet.ReplaceIconFromBitmap(_iconIndex, img, resizeIfNeeded: resize);
                    }
                    else
                    {
                        sheet.ReplaceIconFromBitmap(_iconIndex, img, resizeIfNeeded: false);
                    }
                }
                else
                {
                    MessageBox.Show("Unsupported file type.", "Replace",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                RenderCurrentIcon();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Replace failed:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Save the entire sheet back to disk (overwriting original) or prompt for new path if we don't have one
        private void saveToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (sheet == null)
            {
                MessageBox.Show("Load a PIX sheet first.", "No sheet loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Save back to the same path by default, or prompt if we don't have one
            if (!string.IsNullOrWhiteSpace(_sheetPath) && File.Exists(_sheetPath))
            {
                sheet.Save(_sheetPath);
                MessageBox.Show("Saved.", "PIX Sheet", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "PIX files (*.pix)|*.pix|All files (*.*)|*.*",
                FileName = "ITEM_ALL.PIX"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                sheet.Save(sfd.FileName);
                _sheetPath = sfd.FileName;
                MessageBox.Show("Saved.", "PIX Sheet", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void Next_Click(object sender, EventArgs e)
        {
            // If a sheet is loaded, Next/Prev moves between icons
            if (sheet != null)
            {
                // increment index
                _iconIndex++;
                // update a label with current index
                //label1.Text = _iconIndex.ToString();
                //label1.Text = $"{_iconIndex + 1}/{sheet.IconCount}";
                if (_iconIndex >= sheet.IconCount)
                    _iconIndex = 0; // wrap

                RenderCurrentIcon();
                return;
            }

            // If no sheet is loaded, but we have a TIM pack (Multi TIM / TIM-in-PIX), Next/Prev moves between TIM blocks
            // If Shift held: change CLUT instead of TIM
            bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;

            if (_timPack != null && _timPack.Count > 0)
            {
                if (shift)
                {
                    _clutCount = GetTimClutCount(_timPack[_timIndex]);
                    _clutIndex++;
                    if (_clutIndex >= _clutCount) _clutIndex = 0;
                    RenderCurrentTimFromPack();
                    return;
                }

                _timIndex++;
                if (_timIndex >= _timPack.Count) _timIndex = 0;
                _clutIndex = 0; // reset palette when switching TIM
                RenderCurrentTimFromPack();
                return;
            }
        }

        private void Prev_Click(object sender, EventArgs e)
        {
            // If a sheet is loaded, Next/Prev moves between icons
            if (sheet != null)
            {
                _iconIndex--;
                // update a label with current index
                //label1.Text = _iconIndex.ToString();
                //label1.Text = $"{_iconIndex + 1}/{sheet.IconCount}";
                if (_iconIndex < 0)
                    _iconIndex = sheet.IconCount - 1; // wrap

                RenderCurrentIcon();
                return;
            }

            // If no sheet is loaded, but we have a TIM pack (Multi TIM / TIM-in-PIX), Next/Prev moves between TIM blocks
            if (_timPack != null && _timPack.Count > 0)
            {
                // If Shift held: change CLUT instead of TIM
                bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;

                if (shift)
                {
                    _clutCount = GetTimClutCount(_timPack[_timIndex]);

                    _clutIndex--;
                    if (_clutIndex < 0)
                        _clutIndex = _clutCount - 1; // wrap

                    RenderCurrentTimFromPack();
                    return;
                }

                _timIndex--;
                if (_timIndex < 0)
                    _timIndex = _timPack.Count - 1; // wrap

                _clutIndex = 0; // reset palette when switching TIM
                RenderCurrentTimFromPack();
                UpdateNavButtons();
            }
        }

        private void exportSelectedMultipackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Export Selected (Multi TIM) Pack as separate PIX/TIM/PNG/BMP file
            if (!IsMultipackActive())
            {
                MessageBox.Show("Load a Multi-TIM PIX first (eg ITEMG.PIX).", "No multipack loaded",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using SaveFileDialog sfd = new SaveFileDialog
            {
                Filter =
                    "PlayStation TIM (*.tim)|*.tim|" +
                    "PIX file (*.pix)|*.pix|" +   // some tools expect .pix even if it's TIM inside
                    "PNG image (*.png)|*.png|" +
                    "Bitmap image (*.bmp)|*.bmp|" +
                    "All files (*.*)|*.*",
                FileName = $"tim_{_timIndex:D3}"
            };

            if (sfd.ShowDialog() != DialogResult.OK)
                return;

            string ext = Path.GetExtension(sfd.FileName).ToLowerInvariant();

            // Export raw TIM block (works for .tim and also "TIM disguised as .pix")
            if (ext == ".tim" || ext == ".pix")
            {
                File.WriteAllBytes(sfd.FileName, _timPack![_timIndex]);
                return;
            }

            // Export as PNG/BMP by decoding to bitmap
            //using var bmp = Tim.DecodeToBitmap(_timPack![_timIndex]);
            using var bmp = Tim.DecodeToBitmap(_timPack[_timIndex], _clutIndex);

            if (ext == ".png")
                bmp.Save(sfd.FileName, ImageFormat.Png);
            else if (ext == ".bmp")
                bmp.Save(sfd.FileName, ImageFormat.Bmp);
            else
                bmp.Save(sfd.FileName + ".png", ImageFormat.Png);
        }

        private void replaceSelectedMultipackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Replace Selected (Multi TIM) Pack as separate PIX/TIM/PNG/BMP file
            if (!IsMultipackActive())
            {
                MessageBox.Show("Load a Multi-TIM PIX first (eg ITEMG.PIX).", "No multipack loaded",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using OpenFileDialog ofd = new OpenFileDialog
            {
                Filter =
                    "PlayStation TIM (*.tim;*.pix)|*.tim;*.pix|" +
                    "All files (*.*)|*.*"
            };

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            byte[] repl = File.ReadAllBytes(ofd.FileName);

            // Basic validation: must start with TIM magic
            if (repl.Length < 8 || BitConverter.ToUInt32(repl, 0) != 0x00000010u)
            {
                MessageBox.Show("Selected file is not a valid TIM.", "Replace",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Replace the selected entry in memory
            _timPack![_timIndex] = repl;

            // Ask where to save the updated multipack
            using SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "PIX file (*.pix)|*.pix|All files (*.*)|*.*",
                FileName = _bgPath != null ? Path.GetFileName(_bgPath) : "ITEMG.PIX"
            };

            if (sfd.ShowDialog() != DialogResult.OK)
                return;

            // Rebuild the multipack by concatenating blocks (pad to 4 bytes)
            using var ms = new MemoryStream();
            foreach (var block in _timPack)
            {
                ms.Write(block, 0, block.Length);

                // Align to 4 bytes if needed
                while ((ms.Position & 3) != 0)
                    ms.WriteByte(0);
            }

            File.WriteAllBytes(sfd.FileName, ms.ToArray());

            // Reload current preview from updated pack
            RenderCurrentTimFromPack();

            MessageBox.Show("Replaced and saved multipack.", "Replace",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

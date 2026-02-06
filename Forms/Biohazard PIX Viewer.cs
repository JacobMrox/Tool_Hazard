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

namespace Tool_Hazard.Forms
{
    public partial class Biohazard_PIX_Viewer : Form
    {
        // Globals
        private string? _bgPath;
        private string? _bgLoadedExt; // ".pix" ".png" ".bmp" etc
        private Tool_Hazard.Imaging.ItemSheet? sheet;
        private string? _sheetPath;
        private int _iconIndex = 0;
        public Biohazard_PIX_Viewer()
        {
            InitializeComponent();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        //Background pix handling
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
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Background PIX / Images (*.pix;*.png;*.bmp)|*.pix;*.png;*.bmp|PIX files (*.pix)|*.pix|PNG files (*.png)|*.png|Bitmap files (*.bmp)|*.bmp|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            _bgPath = ofd.FileName;
            _bgLoadedExt = Path.GetExtension(ofd.FileName)?.ToLowerInvariant();

            try
            {
                if (_bgLoadedExt == ".pix")
                {
                    // This supports raw 320x240 16bpp PIX and TIM-disguised-as-PIX
                    var bmp = Tool_Hazard.Imaging.PixLoader.LoadAsBitmap(ofd.FileName);
                    SetPictureBoxImage(bmp);
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
        }

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
                        var raw = Tool_Hazard.Imaging.Psx1555.EncodeRaw320x240(bmp);
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

        // Item sheet pix handling
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

            // optional UI updates (if you have these controls)
            // labelIndex.Text = $"{_iconIndex + 1}/{sheet.IconCount}";
            // toolStripStatusLabel1.Text = $"Icon {_iconIndex}  ({_iconIndex + 1}/{sheet.IconCount})";
        }

        private void openToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "PIX files (*.pix)|*.pix|All files (*.*)|*.*"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                sheet = Tool_Hazard.Imaging.ItemSheet.Load(ofd.FileName);
                _sheetPath = ofd.FileName;
                _iconIndex = 0;
                RenderCurrentIcon();
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
                    if (img.Width != Tool_Hazard.Imaging.ItemSheet.IconW ||
                        img.Height != Tool_Hazard.Imaging.ItemSheet.IconH)
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
            if (sheet == null) return;
            // increment index
            _iconIndex++;
            // update a label with current index
            label1.Text = _iconIndex.ToString();
            if (_iconIndex >= sheet.IconCount)
                _iconIndex = 0; // wrap

            RenderCurrentIcon();
        }

        private void Prev_Click(object sender, EventArgs e)
        {
            if (sheet == null) return;

            _iconIndex--;
            // update a label with current index
            label1.Text = _iconIndex.ToString();
            if (_iconIndex < 0)
                _iconIndex = sheet.IconCount - 1; // wrap

            RenderCurrentIcon();
        }

    }
}
#nullable enable
using System.Drawing.Imaging;
using Tool_Hazard.Biohazard.MSG;

namespace Tool_Hazard.Biohazard.MSG
{
    /// <summary>
    /// Renders Classic RE (Bio1/2/3) MSG text using the game's font atlas
    /// (font.png) and the encoding dictionary (encoding.xml). Each byte value
    /// maps to a 16x16 glyph cell in font.png at the same index, so the byte
    /// value doubles as the glyph index.
    /// </summary>
    public sealed class MsgFontRenderer : IDisposable
    {
        private const int GlyphSize = 8;
        private const int PageWidth = 256;
        private const int PageHeight = 192;

        private readonly EncodingDictionary _dict;
        private Bitmap? _font;
        private int _cols = 16;
        private bool _useAlphaTransparency;

        public MsgFontRenderer(EncodingDictionary dictionary)
        {
            _dict = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        }

        public void LoadFont(string fontPngPath)
        {
            if (!File.Exists(fontPngPath))
                throw new FileNotFoundException("font.png not found", fontPngPath);

            _font?.Dispose();
            _font = new Bitmap(fontPngPath);

            // Detect transparency style (alpha vs black colorkey)
            _useAlphaTransparency = DetectAlphaUsage(_font);

            // Auto-detect columns based on 16x16 glyph cells
            _cols = Math.Max(1, _font.Width / GlyphSize);
        }

        /// <summary>
        /// Renders the given text onto a PageWidth x PageHeight transparent
        /// bitmap, using the game's font metrics (per-glyph widths from the
        /// encoding dictionary). Newlines advance to the next line.
        /// </summary>
        public Bitmap RenderPage(string text)
        {
            EnsureFontLoaded();

            var dst = new Bitmap(PageWidth, PageHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dst))
                g.Clear(Color.Transparent);

            DrawString(text ?? string.Empty, dst);
            return dst;
        }

        /// <summary>
        /// Renders the text into a bordered preview frame (320x240) suitable
        /// for display in a PictureBox, with black background and a red
        /// text-safe boundary box.
        /// </summary>
        public Bitmap RenderPreviewFrame(string text)
        {
            using var page = RenderPage(text);

            const int PreviewWidth = 320;
            const int PreviewHeight = 240;
            const int Left = 16 + 32;
            const int Right = PreviewWidth - Left;
            const int Top = 24;
            const int Bottom = PreviewHeight - 24;

            var frame = new Bitmap(PreviewWidth, PreviewHeight, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(frame);
            g.Clear(Color.Black);

            g.DrawImageUnscaled(page, 32, 24);

            using var pen = new Pen(Color.Red);
            g.DrawLine(pen, Left - 1, 0, Left - 1, PreviewHeight);
            g.DrawLine(pen, Right + 1, 0, Right + 1, PreviewHeight);
            g.DrawLine(pen, 0, Top - 1, PreviewWidth, Top - 1);
            g.DrawLine(pen, 0, Bottom + 1, PreviewWidth, Bottom + 1);

            return frame;
        }

        public void Dispose()
        {
            _font?.Dispose();
            _font = null;
        }

        private void DrawString(string text, Bitmap dst)
        {
            EnsureFontLoaded();
            if (dst.Width != PageWidth || dst.Height != PageHeight)
                throw new ArgumentException($"dst must be {PageWidth}x{PageHeight}.", nameof(dst));

            int px = 16; // BASE_X
            int py = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (ch == '\0')
                    return;

                if (ch == '\r')
                    continue; // skip carriage return (Windows line endings)

                if (ch == '\n')
                {
                    py += GlyphSize;
                    px = 16;
                    continue;
                }

                px = DrawChar(ch, dst, px, py);
            }
        }

        private int DrawChar(char ch, Bitmap dst, int px, int py)
        {
            if (ch == ' ')
            {
                // Space: look up its byte value for the advance width
                if (_dict.TextToByte.TryGetValue(" ", out var spaceByte))
                    return px + _dict.GetWidth(spaceByte);
                return px + _dict.DefaultWidth;
            }

            // The renderer dictionary describes the atlas layout. It is kept
            // separate from the MSG serialization dictionary because the
            // bundled font.png uses encoding_file.xml's glyph indexes.
            if (!_dict.TextToByte.TryGetValue(ch.ToString(), out var byteValue))
                return px + _dict.DefaultWidth;

            int cellIndex = byteValue;
            int width = _dict.GetWidth(byteValue);

            var font = _font!;

            int cx = (cellIndex % _cols) * GlyphSize;
            int cy = (cellIndex / _cols) * GlyphSize;

            // If out of bounds or cell is empty, don't crash; just advance.
            if (cx < 0 || cy < 0 || cx + GlyphSize > font.Width || cy + GlyphSize > font.Height)
                return px + width;

            for (int y = 0; y < GlyphSize; y++)
            {
                int dy = py + y;
                if ((uint)dy >= (uint)PageHeight) continue;

                for (int x = 0; x < GlyphSize; x++)
                {
                    int dx = px + x;
                    if ((uint)dx >= (uint)PageWidth) continue;

                    Color col = font.GetPixel(cx + x, cy + y);

                    if (_useAlphaTransparency)
                    {
                        if (col.A == 0) continue;
                    }
                    else
                    {
                        if (col.R == 0 && col.G == 0 && col.B == 0) continue;
                    }

                    dst.SetPixel(dx, dy, col);
                }
            }

            return px + width;
        }

        private static bool DetectAlphaUsage(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            int stepX = Math.Max(1, w / 64);
            int stepY = Math.Max(1, h / 64);

            for (int y = 0; y < h; y += stepY)
            {
                for (int x = 0; x < w; x += stepX)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.A < 255) return true;
                }
            }
            return false;
        }

        private void EnsureFontLoaded()
        {
            if (_font is null)
                throw new InvalidOperationException("Font not loaded. Call LoadFont(\"font.png\") first.");
        }
    }
}

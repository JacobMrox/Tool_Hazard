// Biohazard/FileEditor/FileFontRenderer.cs
// Ported and based on https://github.com/Gemini-Loboto3/RE1-Mod-SDK/tree/master/File%20Editor
// This is a port of the FILE page text rendering from the original C++ tool,
// which uses a 16x16 glyph atlas (font.png) and an encoding.xml mapping (FileEncoding).
// Credits to the original C++ tool for the rendering logic, including handling of tags and spacing.
// Biohazard/FileEditor/FileFontRenderer.cs
#nullable enable
using System.Drawing.Imaging;

namespace Tool_Hazard.Biohazard.FileEditor
{
    public sealed class FileFontRenderer : IDisposable
    {
        public const int PageWidth = 256;
        public const int PageHeight = 192;

        public const int PreviewWidth = 320;
        public const int PreviewHeight = 240;

        private const int BASE_X = 16;

        private const int LEFT = (16 + 32);
        private const int RIGHT = (320 - LEFT);
        private const int TOP = 24;
        private const int BOTTOM = (240 - 24);

        private Bitmap? _font;
        private readonly FileEncoding _encoding;
        private int _cols = 16;

        // If true: treat alpha==0 as transparent (normal PNG transparency)
        // If false: treat RGB==0,0,0 as transparent (old colorkey behavior)
        private bool _useAlphaTransparency;

        public FileFontRenderer(FileEncoding encoding)
        {
            _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        }

        public void LoadFont(string fontPngPath)
        {
            if (!File.Exists(fontPngPath))
                throw new FileNotFoundException("font.png not found", fontPngPath);

            _font?.Dispose();
            _font = new Bitmap(fontPngPath);

            // Auto-detect transparency style:
            // If we find any pixel with A < 255, assume proper alpha transparency is used.
            _useAlphaTransparency = DetectAlphaUsage(_font);

            // auto-detect columns (tile width is 16)
            _cols = Math.Max(1, _font.Width / 16);

        }

        public Bitmap RenderPage(string text)
        {
            EnsureFontLoaded();

            var dst = new Bitmap(PageWidth, PageHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dst))
                g.Clear(Color.Transparent);

            DrawString(text ?? string.Empty, dst);
            return dst;
        }

        public Bitmap RenderPreviewFrame(string text)
        {
            using var page = RenderPage(text);

            var frame = new Bitmap(PreviewWidth, PreviewHeight, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(frame);
            g.Clear(Color.Black);

            g.DrawImageUnscaled(page, 32, 24);

            using var pen = new Pen(Color.Red);
            g.DrawLine(pen, LEFT - 1, 0, LEFT - 1, PreviewHeight);
            g.DrawLine(pen, RIGHT + 1, 0, RIGHT + 1, PreviewHeight);
            g.DrawLine(pen, 0, TOP - 1, PreviewWidth, TOP - 1);
            g.DrawLine(pen, 0, BOTTOM + 1, PreviewWidth, BOTTOM + 1);

            return frame;
        }

        public void ExportSequence(string basePathNoExt, string[] pages)
        {
            if (pages is null) throw new ArgumentNullException(nameof(pages));

            for (int i = 0; i < pages.Length; i++)
            {
                using var img = RenderPage(pages[i] ?? string.Empty);
                var outPath = $"{basePathNoExt}{i:00}.png";
                img.Save(outPath, ImageFormat.Png);
            }
        }

        public void DrawString(string text, Bitmap dst)
        {
            EnsureFontLoaded();
            if (dst.Width != PageWidth || dst.Height != PageHeight)
                throw new ArgumentException($"dst must be {PageWidth}x{PageHeight}.", nameof(dst));

            int lines = 1;
            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n') lines++;

            int px = BASE_X;
            int py = (PageHeight - (lines * 16)) / 2;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (ch == '\0')
                    return;

                if (ch == '\n')
                {
                    py += 16;
                    px = BASE_X;
                    continue;
                }

                if (ch == '{')
                {
                    int close = text.IndexOf('}', i + 1);
                    if (close > i)
                    {
                        var tag = text.Substring(i + 1, close - (i + 1));
                        i = close;

                        if (tag.Equals("center", StringComparison.OrdinalIgnoreCase))
                            px = (PageWidth / 2) - (GetLineWidth(text, i + 1) / 2);
                        else if (tag.Equals("right", StringComparison.OrdinalIgnoreCase))
                            px = PageWidth - BASE_X - GetLineWidth(text, i + 1);
                        else if (tag.Equals("list", StringComparison.OrdinalIgnoreCase))
                            px = 30;

                        continue;
                    }
                }

                px = DrawChar(ch, dst, px, py);
            }
        }

        private int GetLineWidth(string text, int pos)
        {
            int w = 0;
            for (int i = pos; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\0' || ch == '{' || ch == '\n')
                    break;

                int encode = _encoding.FindEncode(ch);
                w += _encoding.GetWidthByEncode(encode);
            }
            return w;
        }

        /*
        private int DrawChar(char ch, Bitmap dst, int px, int py)
        {
            if (ch == ' ')
            {
                int eSpace = _encoding.FindEncode(' ');
                return px + _encoding.GetWidthByEncode(eSpace);
            }

            // IMPORTANT:
            // Your atlas is 16x16 glyphs. We use the low byte as the glyph index.
            int encode = _encoding.FindEncode(ch) & 0xFF;
            int cx = (encode % 16) * 16;
            int cy = (encode / 16) * 16;

            var font = _font!;

            for (int y = 0; y < 16; y++)
            {
                int dy = py + y;
                if ((uint)dy >= (uint)PageHeight) continue;

                for (int x = 0; x < 16; x++)
                {
                    int dx = px + x;
                    if ((uint)dx >= (uint)PageWidth) continue;

                    Color col = font.GetPixel(cx + x, cy + y);

                    // ✅ FIX: support alpha-transparent fonts AND old black-colorkey fonts
                    if (_useAlphaTransparency)
                    {
                        if (col.A == 0)
                            continue;
                    }
                    else
                    {
                        if (col.R == 0 && col.G == 0 && col.B == 0)
                            continue;
                    }

                    dst.SetPixel(dx, dy, col);
                }
            }

            return px + _encoding.GetWidthByEncode(encode);
        }
        */

        private int DrawChar(char ch, Bitmap dst, int px, int py)
        {
            if (ch == ' ')
            {
                int eSpace = _encoding.FindEncode(' ');
                return px + _encoding.GetWidthByEncode(eSpace);
            }

            // ✅ IMPORTANT: use FULL encode index (not & 0xFF)
            int encodeFull = _encoding.FindEncode(ch);

            // Some tables may use high ranges for control codes; ignore drawing those
            // (keep earlier behavior; safe)
            if ((encodeFull >> 8) >= 0xEE)
                return px;

            int glyphIndex = encodeFull & 0xFFFF;

            var font = _font!;

            // ✅ Auto atlas addressing based on detected columns
            int cx = (glyphIndex % _cols) * 16;
            int cy = (glyphIndex / _cols) * 16;

            // If out of bounds, don't crash; just advance
            if (cx < 0 || cy < 0 || cx + 16 > font.Width || cy + 16 > font.Height)
                return px + _encoding.GetWidthByEncode(encodeFull);

            for (int y = 0; y < 16; y++)
            {
                int dy = py + y;
                if ((uint)dy >= (uint)PageHeight) continue;

                for (int x = 0; x < 16; x++)
                {
                    int dx = px + x;
                    if ((uint)dx >= (uint)PageWidth) continue;

                    Color col = font.GetPixel(cx + x, cy + y);

                    if (_useAlphaTransparency)
                    {
                        if (col.A == 0)
                            continue;
                    }
                    else
                    {
                        if (col.R == 0 && col.G == 0 && col.B == 0)
                            continue;
                    }

                    dst.SetPixel(dx, dy, col);
                }
            }

            return px + _encoding.GetWidthByEncode(encodeFull);
        }

        private static bool DetectAlphaUsage(Bitmap bmp)
        {
            // quick sampling scan; avoids scanning the whole image
            int w = bmp.Width;
            int h = bmp.Height;

            // sample a grid
            int stepX = Math.Max(1, w / 64);
            int stepY = Math.Max(1, h / 64);

            for (int y = 0; y < h; y += stepY)
            {
                for (int x = 0; x < w; x += stepX)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.A < 255)
                        return true;
                }
            }
            return false;
        }

        private void EnsureFontLoaded()
        {
            if (_font is null)
                throw new InvalidOperationException("Font not loaded. Call LoadFont(\"font.png\") first.");
        }

        public void Dispose()
        {
            _font?.Dispose();
            _font = null;
        }
    }
}

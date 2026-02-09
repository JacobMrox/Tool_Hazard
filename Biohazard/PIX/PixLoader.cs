// RePixNet8.cs
// .NET 8.0 - PIX/TIM support based on Leo's (Leo2236) PIXViewer + ItemEditor Pascal logic.
// - Raw 320x240 16bpp (PS1 BGR555) "PIX backgrounds" (no header, exactly 153600 bytes)
// - TIM container detection (magic 0x10), including files named .PIX
// - Item sheet PIX: file size divisible by 1200, each icon is 40x30 8bpp indices
//   rendered using the ItemEditor embedded PALETTE resource (extracted from ItemEditor/Res.res).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Tool_Hazard.Biohazard.PIX
{
    public enum PixKind
    {
        Unknown = 0,
        RawBgr555_320x240,
        Tim,
        ItemSheet40x30_8bpp
    }

    public static class PixLoader
    {
        public static PixKind Detect(byte[] data)
        {
            if (data is null || data.Length < 4) return PixKind.Unknown;

            // TIM magic: 0x10 00 00 00
            if (BitConverter.ToUInt32(data, 0) == 0x00000010u)
                return PixKind.Tim;

            // PIXViewer: "support only 320x240 PIX" and reads raw words
            if (data.Length == 320 * 240 * 2)
                return PixKind.RawBgr555_320x240;

            // ItemEditor: size mod 1200 == 0 => icon sheet (40x30 bytes per icon)
            if (data.Length % 1200 == 0 && data.Length >= 1200)
                return PixKind.ItemSheet40x30_8bpp;

            return PixKind.Unknown;
        }

        public static PixKind Detect(string path) => Detect(File.ReadAllBytes(path));

        public static Bitmap LoadAsBitmap(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var kind = Detect(bytes);

            return kind switch
            {
                PixKind.RawBgr555_320x240 => Psx1555.DecodeRaw320x240(bytes),
                PixKind.Tim => Tim.DecodeToBitmap(bytes),
                PixKind.ItemSheet40x30_8bpp => ItemSheet.Load(bytes).RenderIcon(0),
                _ => throw new NotSupportedException("Unknown/unsupported PIX format.")
            };
        }
    }

    // --------------------------
    // PS1 BGR555 (1bit STP + 5/5/5)
    // --------------------------
    public static class Psx1555
    {
        // TIM / PS1 convention: bits 0-4 R, 5-9 G, 10-14 B, 15 = STP (semi-transparency flag)
        public static Color DecodeBgr555(ushort w)
        {
            int r5 = (w >> 0) & 0x1F;
            int g5 = (w >> 5) & 0x1F;
            int b5 = (w >> 10) & 0x1F;

            // Expand 5-bit to 8-bit (replicate high bits)
            int r8 = (r5 << 3) | (r5 >> 2);
            int g8 = (g5 << 3) | (g5 >> 2);
            int b8 = (b5 << 3) | (b5 >> 2);

            return Color.FromArgb(255, r8, g8, b8);
        }

        public static ushort EncodeBgr555(Color c, bool setStpBit = false)
        {
            int r5 = c.R >> 3;
            int g5 = c.G >> 3;
            int b5 = c.B >> 3;

            ushort w = (ushort)((r5 & 0x1F) | ((g5 & 0x1F) << 5) | ((b5 & 0x1F) << 10));
            if (setStpBit) w |= 0x8000;
            return w;
        }

        public static Bitmap DecodeRaw320x240(byte[] raw)
        {
            if (raw.Length != 320 * 240 * 2)
                throw new InvalidDataException("Raw background PIX must be exactly 153600 bytes (320*240*2).");

            var bmp = new Bitmap(320, 240, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* dst = (byte*)data.Scan0;
                    int stride = data.Stride;

                    int src = 0;
                    for (int y = 0; y < 240; y++)
                    {
                        uint* row = (uint*)(dst + y * stride);
                        for (int x = 0; x < 320; x++)
                        {
                            ushort w = (ushort)(raw[src] | (raw[src + 1] << 8));
                            src += 2;

                            var c = DecodeBgr555(w);
                            row[x] = (uint)(c.B | (c.G << 8) | (c.R << 16) | (0xFFu << 24));
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return bmp;
        }

        public static byte[] EncodeRaw320x240(Bitmap bmp, bool setStpBit = false)
        {
            if (bmp.Width != 320 || bmp.Height != 240)
                throw new ArgumentException("Bitmap must be 320x240 for raw background PIX.");

            // Ensure we can read consistently
            using var tmp = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(tmp))
                g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);

            var rect = new Rectangle(0, 0, tmp.Width, tmp.Height);
            var data = tmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                var outBytes = new byte[320 * 240 * 2];

                unsafe
                {
                    byte* src = (byte*)data.Scan0;
                    int stride = data.Stride;

                    int dst = 0;
                    for (int y = 0; y < 240; y++)
                    {
                        uint* row = (uint*)(src + y * stride);
                        for (int x = 0; x < 320; x++)
                        {
                            uint px = row[x];
                            byte b = (byte)(px & 0xFF);
                            byte g = (byte)((px >> 8) & 0xFF);
                            byte r = (byte)((px >> 16) & 0xFF);

                            ushort w = EncodeBgr555(Color.FromArgb(r, g, b), setStpBit);

                            outBytes[dst++] = (byte)(w & 0xFF);
                            outBytes[dst++] = (byte)((w >> 8) & 0xFF);
                        }
                    }
                }

                return outBytes;
            }
            finally
            {
                tmp.UnlockBits(data);
            }
        }
    }

    // --------------------------
    // PS1 TIM (supports 4bpp/8bpp/16bpp/24bpp decode; we also build 8bpp 40x30 TIM like ItemEditor)
    // --------------------------
    public static class Tim
    {
        public static Bitmap DecodeToBitmap(byte[] tim, int clutIndex = 0)
        {
            using var ms = new MemoryStream(tim, writable: false);
            using var br = new BinaryReader(ms);

            uint id = br.ReadUInt32();
            if (id != 0x00000010u) throw new InvalidDataException("Not a TIM (bad magic).");

            uint flags = br.ReadUInt32();
            int bppMode = (int)(flags & 0x7);
            bool hasClut = (flags & 0x8) != 0;

            ushort[]? selectedClut = null;

            if (hasClut)
            {
                uint clutBlockSize = br.ReadUInt32(); // includes this header (12 bytes)
                ushort clutX = br.ReadUInt16();
                ushort clutY = br.ReadUInt16();
                ushort clutW = br.ReadUInt16(); // colors per CLUT (16 for 4bpp, 256 for 8bpp)
                ushort clutH = br.ReadUInt16(); // number of CLUT sets (rows)

                int colorsTotal = clutW * clutH;
                var allCluts = new ushort[colorsTotal];

                for (int i = 0; i < colorsTotal; i++)
                    allCluts[i] = br.ReadUInt16();

                // clamp and select one CLUT row
                int clutCount = Math.Max(1, (int)clutH);
                if (clutIndex < 0) clutIndex = 0;
                if (clutIndex >= clutCount) clutIndex = 0;

                int rowSize = clutW;
                selectedClut = new ushort[rowSize];
                Buffer.BlockCopy(allCluts, clutIndex * rowSize * sizeof(ushort), selectedClut, 0, rowSize * sizeof(ushort));
            }

            // Image block
            uint imgBlockSize = br.ReadUInt32(); // includes this header (12 bytes)
            ushort vramX = br.ReadUInt16();
            ushort vramY = br.ReadUInt16();
            ushort wWords = br.ReadUInt16();
            ushort h = br.ReadUInt16();

            // Width in pixels depends on bpp
            int widthPx = bppMode switch
            {
                0 => wWords * 4, // 4bpp: 1 word = 4 pixels
                1 => wWords * 2, // 8bpp: 1 word = 2 pixels
                2 => wWords * 1, // 16bpp: 1 word = 1 pixel
                3 => (wWords * 1) / 1, // 24bpp handled by bytes in Decode24bpp
                _ => throw new NotSupportedException("Unsupported TIM bpp mode.")
            };

            byte[] imgData = br.ReadBytes((int)imgBlockSize - 12);

            return bppMode switch
            {
                0 => Decode4bpp(widthPx, h, imgData, selectedClut),
                1 => Decode8bpp(widthPx, h, imgData, selectedClut),
                2 => Decode16bpp(widthPx, h, imgData),
                3 => Decode24bpp(wWords, h, imgData),
                _ => throw new NotSupportedException()
            };
        }
        private static Bitmap Decode8bpp(int w, int h, byte[] data, ushort[]? clut)
        {
            if (clut is null || clut.Length < 256)
                throw new InvalidDataException("8bpp TIM requires CLUT (256 colors).");

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, w, h);
            var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* dst = (byte*)bd.Scan0;
                    int stride = bd.Stride;

                    int src = 0;
                    for (int y = 0; y < h; y++)
                    {
                        uint* row = (uint*)(dst + y * stride);
                        for (int x = 0; x < w; x++)
                        {
                            byte idx = data[src++];
                            var c = Psx1555.DecodeBgr555(clut[idx]);
                            row[x] = (uint)(c.B | (c.G << 8) | (c.R << 16) | (0xFFu << 24));
                        }
                    }
                }
            }
            finally { bmp.UnlockBits(bd); }

            return bmp;
        }

        private static Bitmap Decode4bpp(int w, int h, byte[] data, ushort[]? clut)
        {
            if (clut is null || clut.Length < 16)
                throw new InvalidDataException("4bpp TIM requires CLUT.");

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, w, h);
            var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* dst = (byte*)bd.Scan0;
                    int stride = bd.Stride;

                    int src = 0;
                    for (int y = 0; y < h; y++)
                    {
                        uint* row = (uint*)(dst + y * stride);
                        for (int x = 0; x < w; x += 2)
                        {
                            byte b = data[src++];
                            int lo = b & 0x0F;
                            int hi = (b >> 4) & 0x0F;

                            var c0 = Psx1555.DecodeBgr555(clut[lo]);
                            var c1 = Psx1555.DecodeBgr555(clut[hi]);

                            row[x] = (uint)(c0.B | (c0.G << 8) | (c0.R << 16) | (0xFFu << 24));
                            if (x + 1 < w)
                                row[x + 1] = (uint)(c1.B | (c1.G << 8) | (c1.R << 16) | (0xFFu << 24));
                        }
                    }
                }
            }
            finally { bmp.UnlockBits(bd); }

            return bmp;
        }

        private static Bitmap Decode16bpp(int w, int h, byte[] data)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, w, h);
            var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* dst = (byte*)bd.Scan0;
                    int stride = bd.Stride;

                    int src = 0;
                    for (int y = 0; y < h; y++)
                    {
                        uint* row = (uint*)(dst + y * stride);
                        for (int x = 0; x < w; x++)
                        {
                            ushort px = (ushort)(data[src] | (data[src + 1] << 8));
                            src += 2;
                            var c = Psx1555.DecodeBgr555(px);
                            row[x] = (uint)(c.B | (c.G << 8) | (c.R << 16) | (0xFFu << 24));
                        }
                    }
                }
            }
            finally { bmp.UnlockBits(bd); }

            return bmp;
        }

        private static Bitmap Decode24bpp(int wWords, int h, byte[] data)
        {
            // 24bpp TIM stores 3 bytes per pixel, but width in header is in 16-bit words.
            // Common interpretation: pixelsPerRow = (wWords * 2) / 3 * 2 ??? It’s messy across tools.
            // We'll decode conservatively: each row is wWords*2 bytes, pixels = rowBytes/3.
            int rowBytes = wWords * 2;
            int w = rowBytes / 3;

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, w, h);
            var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* dst = (byte*)bd.Scan0;
                    int stride = bd.Stride;

                    int src = 0;
                    for (int y = 0; y < h; y++)
                    {
                        uint* row = (uint*)(dst + y * stride);
                        for (int x = 0; x < w; x++)
                        {
                            byte r = data[src++];
                            byte g = data[src++];
                            byte b = data[src++];
                            row[x] = (uint)(b | (g << 8) | (r << 16) | (0xFFu << 24));
                        }
                        // skip padding if any
                        int consumed = w * 3;
                        int pad = rowBytes - consumed;
                        if (pad > 0) src += pad;
                    }
                }
            }
            finally { bmp.UnlockBits(bd); }

            return bmp;
        }

        // Build a TIM exactly like ItemEditor expects (8bpp, 40x30, CLUT 256*1)
        public static byte[] BuildItemTim40x30(byte[] palette512, byte[] image1200)
        {
            if (palette512.Length != 512) throw new ArgumentException("Palette must be 512 bytes (256*2).");
            if (image1200.Length != 1200) throw new ArgumentException("Image must be 1200 bytes (40*30).");

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // TIM header
            bw.Write(0x00000010u); // magic
            bw.Write(0x00000009u); // 8bpp + CLUT (matches Pascal Check_Tim)

            // CLUT block
            bw.Write(0x0000020Cu); // palette block size (0xC header + 0x200 data)
            bw.Write((ushort)0x0000); // PVramX (ignored by Pascal)
            bw.Write((ushort)0x0000); // PVramY (ignored by Pascal)
            bw.Write((ushort)0x0100); // PWidth = 256
            bw.Write((ushort)0x0001); // PHeight = 1
            bw.Write(palette512);     // 256 colors (BGR555)

            // Image block
            bw.Write(0x000004BCu); // image block size (0xC header + 0x4B0 data)
            bw.Write((ushort)0x0000); // VramX (ignored by Pascal)
            bw.Write((ushort)0x0000); // VramY (ignored by Pascal)
            bw.Write((ushort)0x0014); // Width in 16-bit words for 8bpp => 20 words => 40 pixels
            bw.Write((ushort)0x001E); // Height => 30
            bw.Write(image1200);

            return ms.ToArray();
        }

        // Quick check helper: does this TIM match ItemEditor requirements?
        public static bool IsItemTim40x30_8bpp(byte[] tim)
        {
            try
            {
                using var ms = new MemoryStream(tim, writable: false);
                using var br = new BinaryReader(ms);

                if (br.ReadUInt32() != 0x00000010u) return false;
                if (br.ReadUInt32() != 0x00000009u) return false;

                uint palSize = br.ReadUInt32();
                br.ReadUInt16(); br.ReadUInt16();
                ushort pW = br.ReadUInt16();
                ushort pH = br.ReadUInt16();
                if (palSize != 0x0000020Cu) return false;
                if (pW != 0x0100 || pH != 0x0001) return false;

                ms.Position = 0x214;
                uint imgSize = br.ReadUInt32();
                br.ReadUInt16(); br.ReadUInt16();
                ushort wWords = br.ReadUInt16();
                ushort h = br.ReadUInt16();
                if (wWords != 0x0014 || h != 0x001E) return false;
                if (imgSize != 0x000004BCu) return false;

                return true;
            }
            catch { return false; }
        }
    }

    // --------------------------
    // Item sheet (.PIX): N icons, each 40x30 8bpp indices.
    // Uses the same embedded palette as ItemEditor (extracted from ItemEditor/Res.res).
    // --------------------------
    public sealed class ItemSheet
    {
        public const int IconW = 40;
        public const int IconH = 30;
        public const int IconBytes = IconW * IconH; // 1200

        public Color[] GetPaletteColors()
        {
            // Build Color[256] from the internal 512-byte PS1 CLUT
            var colors = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                ushort w = (ushort)(_palette512[i * 2] | (_palette512[i * 2 + 1] << 8));
                colors[i] = Psx1555.DecodeBgr555(w);
            }
            return colors;
        }

        public void ReplaceIconFromBitmap(int index, Bitmap src, bool resizeIfNeeded = false)
        {
            if ((uint)index >= (uint)IconCount) throw new ArgumentOutOfRangeException(nameof(index));
            if (src == null) throw new ArgumentNullException(nameof(src));

            Bitmap bmp;

            if (src.Width != IconW || src.Height != IconH)
            {
                if (!resizeIfNeeded)
                    throw new ArgumentException($"Bitmap must be {IconW}x{IconH}.");

                bmp = new Bitmap(IconW, IconH, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                    g.DrawImage(src, 0, 0, IconW, IconH);
            }
            else
            {
                // normalize to 32bpp
                bmp = new Bitmap(IconW, IconH, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                    g.DrawImage(src, 0, 0, IconW, IconH);
            }

            try
            {
                var palette = GetPaletteColors();
                var indices = new byte[IconBytes];

                // Lock for speed
                var rect = new Rectangle(0, 0, IconW, IconH);
                var bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                try
                {
                    unsafe
                    {
                        byte* p = (byte*)bd.Scan0;
                        int stride = bd.Stride;

                        int dst = 0;
                        for (int y = 0; y < IconH; y++)
                        {
                            uint* row = (uint*)(p + y * stride);
                            for (int x = 0; x < IconW; x++)
                            {
                                uint px = row[x];
                                byte b = (byte)(px & 0xFF);
                                byte g = (byte)((px >> 8) & 0xFF);
                                byte r = (byte)((px >> 16) & 0xFF);

                                indices[dst++] = FindNearestPaletteIndex(palette, r, g, b);
                            }
                        }
                    }
                }
                finally
                {
                    bmp.UnlockBits(bd);
                }

                ReplaceIconIndices(index, indices);
            }
            finally
            {
                bmp.Dispose();
            }
        }

        private static byte FindNearestPaletteIndex(Color[] palette, byte r, byte g, byte b)
        {
            int best = 0;
            int bestDist = int.MaxValue;

            for (int i = 0; i < 256; i++)
            {
                var c = palette[i];
                int dr = c.R - r;
                int dg = c.G - g;
                int db = c.B - b;
                int dist = dr * dr + dg * dg + db * db;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                    if (dist == 0) break;
                }
            }

            return (byte)best;
        }

        // PALETTE extracted from ItemEditor/Res.res (resource name "PALETTE", type "DATA").
        // 512 bytes = 256 colors * 2 bytes (PS1 BGR555).
        private static readonly byte[] DefaultPalette512 = Convert.FromBase64String(
            "AICcc3tvGGP3XtZatVZzTlJKEELvPc45rTWMMSkl5xzGGKUUhBBjDHROMkbPOUwp0Tm3NXQtVSlnDM8Y" +
            "ZxznHMYYpRTGGKUUpRTnHMYY5z3nOec9z3nPOec9pRTnPMcY5zznOMY4xzjHOMY4xznHOMY4xznHOMY4" +
            "xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4" +
            "xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4" +
            "xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4" +
            "xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4" +
            "xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4" +
            "xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4" +
            "xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4" +
            "xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4" +
            "xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4xznHOMY4"
        );

        //Alternative to [DefaultPalette512] base64 string: read from embedded resource (extracted from Biohazard/PIX/Res.res)
        // call this once and cache it somewhere static if necessary
        //byte[] palette512 = BorlandResReader.ReadPalette512FromEmbeddedRes("Tool_Hazard.Biohazard.PIX.Res.res");

        private readonly byte[] _pix;
        private readonly byte[] _palette512; // 512 bytes (256*2)
        public int IconCount { get; }

        private ItemSheet(byte[] pix, byte[] palette512)
        {
            _pix = pix;
            _palette512 = palette512;
            IconCount = pix.Length / IconBytes;
        }

        public static ItemSheet Load(string pixPath, byte[]? palette512 = null)
            => Load(File.ReadAllBytes(pixPath), palette512);

        public static ItemSheet Load(byte[] pixBytes, byte[]? palette512 = null)
        {
            if (pixBytes.Length % IconBytes != 0)
                MessageBox.Show($"Failed to open file.\n\nItem sheet PIX must have size divisible by 1200 (40*30 bytes per icon", "Open error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //throw new InvalidDataException("Item sheet PIX must have size divisible by 1200 (40*30 bytes per icon).");//Replce with message box

            return new ItemSheet(pixBytes, palette512 ?? DefaultPalette512);
        }

        public Bitmap RenderIcon(int index)
        {
            if ((uint)index >= (uint)IconCount) throw new ArgumentOutOfRangeException(nameof(index));

            // Build ushort CLUT array (256 entries)
            ushort[] clut = new ushort[256];
            for (int i = 0; i < 256; i++)
                clut[i] = (ushort)(_palette512[i * 2] | (_palette512[i * 2 + 1] << 8));

            var bmp = new Bitmap(IconW, IconH, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, IconW, IconH);
            var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* dst = (byte*)bd.Scan0;
                    int stride = bd.Stride;

                    int src = index * IconBytes;
                    for (int y = 0; y < IconH; y++)
                    {
                        uint* row = (uint*)(dst + y * stride);
                        for (int x = 0; x < IconW; x++)
                        {
                            byte palIdx = _pix[src++];
                            var c = Psx1555.DecodeBgr555(clut[palIdx]);
                            row[x] = (uint)(c.B | (c.G << 8) | (c.R << 16) | (0xFFu << 24));
                        }
                    }
                }
            }
            finally { bmp.UnlockBits(bd); }

            return bmp;
        }

        public byte[] GetIconIndices(int index)
        {
            if ((uint)index >= (uint)IconCount) throw new ArgumentOutOfRangeException(nameof(index));
            byte[] outData = new byte[IconBytes];
            Buffer.BlockCopy(_pix, index * IconBytes, outData, 0, IconBytes);
            return outData;
        }

        public void ReplaceIconIndices(int index, byte[] indices1200)
        {
            if ((uint)index >= (uint)IconCount) throw new ArgumentOutOfRangeException(nameof(index));
            if (indices1200.Length != IconBytes) throw new ArgumentException("Icon indices must be exactly 1200 bytes.");
            Buffer.BlockCopy(indices1200, 0, _pix, index * IconBytes, IconBytes);
        }

        // Export icon as TIM (matches Pascal Make_Tim expectations)
        public void ExportIconAsTim(int index, string timPath)
        {
            var icon = GetIconIndices(index);
            var tim = Tim.BuildItemTim40x30(_palette512, icon);
            File.WriteAllBytes(timPath, tim);
        }

        // Replace icon from a TIM (matches Pascal ReplaceFromTIM: copy 0x4B0 bytes from offset 0x220)
        public void ReplaceIconFromTim(int index, string timPath)
        {
            byte[] tim = File.ReadAllBytes(timPath);
            if (!Tim.IsItemTim40x30_8bpp(tim))
                throw new InvalidDataException("TIM must be 8bpp + CLUT and 40x30 (ItemEditor format).");

            // Image data starts at 0x220 in that exact layout.
            const int imageDataOffset = 0x220;
            byte[] indices = new byte[IconBytes];
            Buffer.BlockCopy(tim, imageDataOffset, indices, 0, IconBytes);
            ReplaceIconIndices(index, indices);
        }

        public void Save(string pixPath) => File.WriteAllBytes(pixPath, _pix);
        public byte[] ToBytes() => (byte[])_pix.Clone();
    }
}

using System.Drawing.Imaging;
using Tool_Hazard.IntelOrca.Biohazard;

namespace Tool_Hazard.Biohazard.PIX
{
    public enum TimRenderMode
    {
        // Use one CLUT for the whole image (palette variants, 16bpp TIMs).
        SelectedClut = 0,

        // Classic RE texture atlas layout: every 128px page uses its own CLUT.
        PerPageClut = 1,

        // Stack every CLUT row vertically (palette sheet preview / export).
        AllCluts = 2
    }

    public sealed class TimImageInfo
    {
        public const int PageWidth = 128;

        public bool HasClut { get; init; }
        public int Bpp { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public int ClutCount { get; init; }
        public int ColoursPerClut { get; init; }
        public int SizeInBytes { get; init; }

        // A multi-CLUT TIM that is wider than one 128px page is the RE1-3 style
        // texture atlas (PLD/EMD textures): page N is drawn with CLUT N.
        public bool IsAtlas => HasClut && ClutCount > 1 && Width > PageWidth;

        public int PageCount => IsAtlas ? ClutCount : 1;

        public string BppText => Bpp switch
        {
            4 => "4bpp",
            8 => "8bpp",
            16 => "16bpp",
            24 => "24bpp",
            _ => $"{Bpp}bpp"
        };

        public string Describe()
        {
            var parts = new List<string> { BppText, $"{Width}×{Height}" };

            if (HasClut)
            {
                parts.Add(ClutCount > 1
                    ? $"{ClutCount} CLUTs × {ColoursPerClut} colours"
                    : $"{ColoursPerClut} colours");

                if (IsAtlas)
                    parts.Add("per-page CLUT atlas");
            }

            parts.Add($"{SizeInBytes:N0} bytes");
            return string.Join("  ·  ", parts);
        }
    }

    // Lightweight TIM reader used by the viewer. It keeps the raw CLUT rows and the
    // image block so the same data can be rendered with any CLUT / page mapping.
    public sealed class PsxTimImage
    {
        private readonly byte[] _imageData;
        private readonly ushort[] _clut;
        private readonly int _clutStride;
        private readonly int _rowBytes;

        public TimImageInfo Info { get; }

        public int ClutCount => Math.Max(1, Info.ClutCount);

        private PsxTimImage(TimImageInfo info, byte[] imageData, int rowBytes, ushort[] clut, int clutStride)
        {
            Info = info;
            _imageData = imageData;
            _rowBytes = rowBytes;
            _clut = clut;
            _clutStride = clutStride;
        }

        public static bool LooksLikeTim(byte[]? data)
        {
            if (data is null || data.Length < 8)
                return false;
            if (BitConverter.ToUInt32(data, 0) != 0x00000010u)
                return false;
            return (BitConverter.ToUInt32(data, 4) & 0x7) <= 3;
        }

        public static PsxTimImage Parse(byte[] tim)
        {
            if (tim is null || tim.Length < 8)
                throw new InvalidDataException("Not a TIM (too short).");

            int pos = 0;

            uint magic = BitConverter.ToUInt32(tim, pos);
            pos += 4;
            if (magic != 0x00000010u)
                throw new InvalidDataException("Not a TIM (bad magic).");

            uint flags = BitConverter.ToUInt32(tim, pos);
            pos += 4;

            int bppMode = (int)(flags & 0x7);
            bool hasClut = (flags & 0x8) != 0;

            int bpp = bppMode switch
            {
                0 => 4,
                1 => 8,
                2 => 16,
                3 => 24,
                _ => throw new NotSupportedException("Unsupported TIM bpp mode.")
            };

            ushort[] clut = Array.Empty<ushort>();
            int clutWidth = 0;
            int clutCount = 1;

            if (hasClut)
            {
                if (pos + 12 > tim.Length)
                    throw new InvalidDataException("Truncated TIM CLUT header.");

                uint clutBlockSize = BitConverter.ToUInt32(tim, pos);
                ushort clutW = BitConverter.ToUInt16(tim, pos + 8);
                ushort clutH = BitConverter.ToUInt16(tim, pos + 10);
                pos += 12;

                int clutBytes = (int)Math.Min((uint)Math.Max(0, tim.Length - pos), clutBlockSize >= 12 ? clutBlockSize - 12 : 0);
                int availableColours = clutBytes / 2;
                int declaredColours = Math.Max(0, clutW * clutH);
                int coloursToRead = Math.Min(availableColours, declaredColours);

                clut = new ushort[coloursToRead];
                for (int i = 0; i < coloursToRead; i++)
                    clut[i] = BitConverter.ToUInt16(tim, pos + (i * 2));

                clutWidth = Math.Max(0, (int)clutW);
                clutCount = clutWidth > 0 ? Math.Max(1, coloursToRead / clutWidth) : 1;
                pos += clutBytes;
            }

            if (pos + 12 > tim.Length)
                throw new InvalidDataException("Truncated TIM image header.");

            uint imageBlockSize = BitConverter.ToUInt32(tim, pos);
            ushort imageWidthWords = BitConverter.ToUInt16(tim, pos + 8);
            ushort imageHeight = BitConverter.ToUInt16(tim, pos + 10);
            pos += 12;

            int imageBytes = (int)Math.Min((uint)Math.Max(0, tim.Length - pos), imageBlockSize >= 12 ? imageBlockSize - 12 : 0);
            var imageData = new byte[imageBytes];
            Buffer.BlockCopy(tim, pos, imageData, 0, imageBytes);

            int rowBytes = imageWidthWords * 2;
            int width = bpp switch
            {
                4 => imageWidthWords * 4,
                8 => imageWidthWords * 2,
                16 => imageWidthWords,
                24 => rowBytes / 3,
                _ => 0
            };

            // Non-indexed TIMs are self contained; index 0 of a CLUT is transparent.
            int coloursPerClut = hasClut
                ? Math.Max(1, clutWidth)
                : 0;

            var info = new TimImageInfo
            {
                HasClut = hasClut,
                Bpp = bpp,
                Width = Math.Max(0, width),
                Height = imageHeight,
                ClutCount = hasClut ? clutCount : 1,
                ColoursPerClut = coloursPerClut,
                SizeInBytes = tim.Length
            };

            return new PsxTimImage(info, imageData, rowBytes, clut, Math.Max(1, coloursPerClut));
        }

        public Bitmap Render(TimRenderMode mode, int clutIndex)
        {
            if (Info.Width <= 0 || Info.Height <= 0)
                throw new InvalidDataException("TIM has an empty image block.");

            if (mode == TimRenderMode.AllCluts && Info.HasClut && Info.ClutCount > 1)
                return RenderAllCluts(clutIndex);

            bool perPage = mode == TimRenderMode.PerPageClut && Info.IsAtlas;
            int clampClut = Math.Clamp(clutIndex, 0, ClutCount - 1);

            var bmp = new Bitmap(Info.Width, Info.Height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* dst = (byte*)bd.Scan0;
                    int stride = bd.Stride;

                    for (int y = 0; y < Info.Height; y++)
                    {
                        uint* row = (uint*)(dst + (y * stride));

                        for (int x = 0; x < Info.Width; x++)
                        {
                            uint argb = Info.Bpp switch
                            {
                                4 => DecodeIndexed(x, y, 4, Math.Clamp(x / TimImageInfo.PageWidth, 0, ClutCount - 1), clampClut, perPage),
                                8 => DecodeIndexed(x, y, 8, Math.Clamp(x / TimImageInfo.PageWidth, 0, ClutCount - 1), clampClut, perPage),
                                16 => Decode16(x, y),
                                24 => Decode24(x, y),
                                _ => 0u
                            };

                            row[x] = argb;
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(bd);
            }

            return bmp;
        }

        private Bitmap RenderAllCluts(int highlightClut)
        {
            int rows = ClutCount;
            int height = Info.Height * rows;

            var bmp = new Bitmap(Info.Width, height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* dst = (byte*)bd.Scan0;
                    int stride = bd.Stride;

                    for (int clut = 0; clut < rows; clut++)
                    {
                        for (int y = 0; y < Info.Height; y++)
                        {
                            uint* row = (uint*)(dst + (((clut * Info.Height) + y) * stride));

                            for (int x = 0; x < Info.Width; x++)
                            {
                                row[x] = Info.Bpp switch
                                {
                                    4 => DecodeIndexed(x, y, 4, clut, clut, true),
                                    8 => DecodeIndexed(x, y, 8, clut, clut, true),
                                    16 => Decode16(x, y),
                                    24 => Decode24(x, y),
                                    _ => 0u
                                };
                            }
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(bd);
            }

            return bmp;
        }

        private uint DecodeIndexed(int x, int y, int bpp, int pageIndex, int selectedClut, bool perPage)
        {
            int rowStart = y * _rowBytes;
            if (rowStart < 0 || rowStart >= _imageData.Length)
                return 0;

            int index;

            if (bpp == 4)
            {
                int offset = rowStart + (x >> 1);
                if (offset >= _imageData.Length)
                    return 0;
                byte b = _imageData[offset];
                index = (x & 1) == 0 ? b & 0x0F : (b >> 4) & 0x0F;
            }
            else
            {
                int offset = rowStart + x;
                if (offset >= _imageData.Length)
                    return 0;
                index = _imageData[offset];
            }

            int clut = perPage ? pageIndex : selectedClut;
            return LookupClut(clut, index);
        }

        private uint LookupClut(int clutIndex, int index)
        {
            if (_clut.Length == 0)
            {
                // Missing CLUT: fall back to a neutral grey ramp so the shape is visible.
                int grey = index is >= 0 and <= 255 ? index : 0;
                return (uint)(grey | (grey << 8) | (grey << 16) | (0xFFu << 24));
            }

            int offset = (Math.Clamp(clutIndex, 0, Math.Max(0, _clut.Length / _clutStride - 1)) * _clutStride) + index;
            if (offset < 0 || offset >= _clut.Length)
                return 0;

            return ToArgb(_clut[offset]);
        }

        private unsafe uint Decode16(int x, int y)
        {
            int offset = (y * _rowBytes) + (x * 2);
            if (offset + 1 >= _imageData.Length)
                return 0;

            ushort c16 = (ushort)(_imageData[offset] | (_imageData[offset + 1] << 8));
            return ToArgb(c16);
        }

        private unsafe uint Decode24(int x, int y)
        {
            int offset = (y * _rowBytes) + (x * 3);
            if (offset + 2 >= _imageData.Length)
                return 0;

            byte r = _imageData[offset];
            byte g = _imageData[offset + 1];
            byte b = _imageData[offset + 2];
            return (uint)(b | (g << 8) | (r << 16) | (0xFFu << 24));
        }

        // PS1 convention (same as TimFile.Convert16to32): palette entry 0 is transparent.
        private static uint ToArgb(ushort c16)
        {
            if (c16 == 0)
                return 0;

            var c = Psx1555.DecodeBgr555(c16);
            return (uint)(c.B | (c.G << 8) | (c.R << 16) | (0xFFu << 24));
        }

        // ---------------------------------------------------------------
        // Import: rebuild a TIM from a replacement image.
        // Palette data is only touched when the new art needs colours the
        // existing CLUT cannot supply, so lossless round-trips stay intact.
        // ---------------------------------------------------------------

        public static byte[] BuildReplacement(byte[] referenceTim, Bitmap art, int clutIndex = 0)
        {
            if (art is null)
                throw new ArgumentNullException(nameof(art));

            var info = Parse(referenceTim).Info;

            if (art.Width != info.Width || art.Height != info.Height)
                throw new InvalidDataException(
                    $"Replacement is {art.Width}×{art.Height} but this TIM is {info.Width}×{info.Height}.\n\n" +
                    "The image must match the TIM size so the block can be swapped in-game.");

            if (info.Bpp is not (4 or 8 or 16))
                throw new InvalidDataException($"{info.BppText} TIMs cannot be rebuilt yet. Open a 4/8/16bpp TIM and try again.");

            var tim = new TimFile(referenceTim);
            var argb = ToArgbArray(art);

            if (info.Bpp == 16)
            {
                tim.ImportPixels(argb, 0);
                return tim.GetBytes();
            }

            int cluts = Math.Max(1, tim.ClutCount);
            int coloursPerClut = Math.Max(1, tim.ColoursPerClut);

            if (info.IsAtlas)
            {
                int pages = Math.Min(cluts, (info.Width + TimImageInfo.PageWidth - 1) / TimImageInfo.PageWidth);
                for (int page = 0; page < pages; page++)
                {
                    int x0 = page * TimImageInfo.PageWidth;
                    int w = Math.Min(TimImageInfo.PageWidth, info.Width - x0);
                    RebuildPalette(tim, page, argb, info.Width, x0, 0, w, info.Height, coloursPerClut);
                }

                tim.ImportPixels(argb, (x, y) => Math.Clamp(x / TimImageInfo.PageWidth, 0, cluts - 1));
            }
            else
            {
                int clut = Math.Clamp(clutIndex, 0, cluts - 1);
                RebuildPalette(tim, clut, argb, info.Width, 0, 0, info.Width, info.Height, coloursPerClut);
                tim.ImportPixels(argb, clut);
            }

            return tim.GetBytes();
        }

        private static void RebuildPalette(TimFile tim, int clutIndex, uint[] argb, int stride,
            int x0, int y0, int width, int height, int capacity)
        {
            var needed = new HashSet<ushort>();

            for (int y = y0; y < y0 + height; y++)
            {
                int row = y * stride;
                for (int x = x0; x < x0 + width; x++)
                {
                    ushort c16 = TimFile.Convert32to16(argb[row + x]);
                    if (c16 != 0)
                        needed.Add(c16);
                }
            }

            var existing = new HashSet<ushort>();
            var current = tim.GetPalette(Math.Clamp(clutIndex, 0, Math.Max(0, tim.ClutCount - 1)));
            foreach (var c in current)
            {
                if (c != 0)
                    existing.Add(c);
            }

            // Palette already covers the artwork: keep it byte-identical.
            if (needed.IsSubsetOf(existing))
                return;

            var colours = new ushort[capacity];
            int slot = 1; // index 0 stays transparent
            foreach (var c in needed)
            {
                if (slot >= capacity)
                    break;
                colours[slot++] = c;
            }

            tim.SetPalette(clutIndex, colours);
        }

        private static uint[] ToArgbArray(Bitmap bmp)
        {
            using var copy = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(copy))
                g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);

            var result = new uint[copy.Width * copy.Height];
            var rect = new Rectangle(0, 0, copy.Width, copy.Height);
            var data = copy.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    for (int y = 0; y < copy.Height; y++)
                    {
                        uint* row = (uint*)((byte*)data.Scan0 + (y * data.Stride));
                        for (int x = 0; x < copy.Width; x++)
                            result[(y * copy.Width) + x] = row[x];
                    }
                }
            }
            finally
            {
                copy.UnlockBits(data);
            }

            return result;
        }
    }

    public sealed class TimBlock
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public int Offset { get; set; }

        // Bytes found between this block and the next TIM header (padding or junk).
        // Keeping them means an untouched pack is written back byte for byte.
        public byte[] Gap { get; set; } = Array.Empty<byte>();
    }

    public static class TimPack
    {
        public static List<TimBlock> Parse(byte[] data)
        {
            var blocks = new List<TimBlock>();
            if (data is null || data.Length < 8)
                return blocks;

            int pos = 0;

            while (pos + 8 <= data.Length)
            {
                int start = FindNextTim(data, pos);
                if (start < 0)
                    break;

                if (!TryGetBlockEnd(data, start, out int end))
                {
                    pos = start + 4;
                    continue;
                }

                int next = FindNextTim(data, end);
                int gapEnd = next < 0 ? data.Length : next;

                var gap = new byte[Math.Max(0, gapEnd - end)];
                if (gap.Length > 0)
                    Buffer.BlockCopy(data, end, gap, 0, gap.Length);

                blocks.Add(new TimBlock
                {
                    Data = data[start..end],
                    Offset = start,
                    Gap = gap
                });

                pos = gapEnd;
            }

            return blocks;
        }

        public static byte[] Rebuild(IReadOnlyList<TimBlock> blocks)
        {
            using var ms = new MemoryStream();

            foreach (var block in blocks)
            {
                ms.Write(block.Data, 0, block.Data.Length);
                if (block.Gap.Length > 0)
                    ms.Write(block.Gap, 0, block.Gap.Length);

                while ((ms.Position & 3) != 0)
                    ms.WriteByte(0);
            }

            return ms.ToArray();
        }

        private static int FindNextTim(byte[] data, int from)
        {
            for (int i = Math.Max(0, from); i + 4 <= data.Length; i++)
            {
                if (data[i] == 0x10 && data[i + 1] == 0x00 && data[i + 2] == 0x00 && data[i + 3] == 0x00)
                    return i;
            }

            return -1;
        }

        private static bool TryGetBlockEnd(byte[] data, int start, out int endExclusive)
        {
            endExclusive = start;

            if (start + 8 > data.Length)
                return false;

            if (BitConverter.ToUInt32(data, start) != 0x00000010u)
                return false;

            uint flags = BitConverter.ToUInt32(data, start + 4);
            if ((flags & 0x7) > 3)
                return false;

            int pos = start + 8;

            if ((flags & 0x8) != 0)
            {
                if (pos + 4 > data.Length)
                    return false;

                uint clutSize = BitConverter.ToUInt32(data, pos);
                if (clutSize < 12 || pos + (long)clutSize > data.Length)
                    return false;

                pos += (int)clutSize;
            }

            if (pos + 4 > data.Length)
                return false;

            uint imageSize = BitConverter.ToUInt32(data, pos);
            if (imageSize < 12 || pos + (long)imageSize > data.Length)
                return false;

            pos += (int)imageSize;
            endExclusive = pos;
            return true;
        }
    }
}

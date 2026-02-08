// BssCodec.cs
// .NET 8.0
// Resident Evil 1-3 BSS/BS background codec (Decode implemented from Mandin depack_vlc.c + depack_mdec.c)
// Public API matches BssViewerForm.cs: DecodeToBitmap(frameBytes,w,h) and EncodeFromBitmap(...)
//
// NOTES:
// - BSS "frame" here means ONE camera chunk (0x8000 or 0x10000) that your form already slices.
// - Encoder is currently a stub (won't produce original-valid BS). Decode is the priority.

using System;
using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tool_Hazard.Biohazard.BSS
{
    public static class BssCodec
    {
        // Mandin constants
        private const ushort VLC_ID = 0x3800;
        private const ushort EOB = 0xFE00; // run=63 level=512 in 10-bit sign
        private const int SLICE_W_U16 = 24; // 24 uint16 per row = 48 bytes = 16px * 3 bytes

        // --------------------------------------------
        // Public API (used by BssViewerForm)
        // --------------------------------------------

        public static Bitmap DecodeToBitmap(ReadOnlySpan<byte> bssFrame, int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (bssFrame.Length < 8) throw new InvalidDataException("BSS/BS chunk too small.");

            // Copy span -> array (avoid any ref-like capture issues)
            byte[] src = bssFrame.ToArray();

            // 1) VLC depack (BS bitstream -> RL words)
            ushort[] rlWords = VlcDepackToRlWords(src);

            // 2) MDEC depack (RL words -> BGR24 bytes)
            byte[] bgr = MdecDepackToBgr24(rlWords, width, height);

            // 3) Build bitmap (24bppRgb expects BGR memory layout)
            var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var rect = new Rectangle(0, 0, width, height);
            var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            try
            {
                int dstStride = bd.Stride;
                int srcStride = width * 3;

                unsafe
                {
                    byte* dst = (byte*)bd.Scan0;
                    int srcOff = 0;

                    for (int y = 0; y < height; y++)
                    {
                        // copy one row
                        Marshal.Copy(bgr, srcOff, (nint)(dst + y * dstStride), srcStride);
                        srcOff += srcStride;
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(bd);
            }

            return bmp;
        }

        public static byte[] EncodeFromBitmap(Bitmap source, int width, int height, int quant)
        {
            // Safe stub: do not crash tool; but this does NOT generate original-valid BS.
            // When you’re ready, we’ll port the encoder from unpacker/mdec/bs.cpp (Huffman tables + writer).
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (quant < 1 || quant > 63) throw new ArgumentOutOfRangeException(nameof(quant), "Quant must be 1..63.");

            // Return a minimal "valid-shaped" BS header + padding; decode will show grey.
            // (This is just so Import/Re-encode buttons don’t explode while decode work is ongoing.)
            byte[] outBytes = new byte[0x8000];
            BinaryPrimitives.WriteUInt16LittleEndian(outBytes.AsSpan(0, 2), 0x0000);
            BinaryPrimitives.WriteUInt16LittleEndian(outBytes.AsSpan(2, 2), VLC_ID);
            BinaryPrimitives.WriteUInt16LittleEndian(outBytes.AsSpan(4, 2), (ushort)quant);
            BinaryPrimitives.WriteUInt16LittleEndian(outBytes.AsSpan(6, 2), 0x0002);
            return outBytes;
        }

        // --------------------------------------------
        // 1) VLC depack (depack_vlc.c)
        // --------------------------------------------

        private static ushort[] VlcDepackToRlWords(byte[] bs)
        {
            // BS header in the compressed chunk:
            // [0]=length (16-bit)
            // [1]=id (0x3800)
            // [2]=quant
            // [3]=version
            if (bs.Length < 8) throw new InvalidDataException("BS too small.");

            ushort length = ReadU16LE(bs, 0);
            ushort id = ReadU16LE(bs, 2);
            if (id != VLC_ID)
                throw new InvalidDataException($"Not a BS/VLC stream (id=0x{id:X4}).");

            // In depack_vlc.c:
            // dstBufLen = (length+2) * sizeof(Uint32) * 2
            // total_length = dstBufLen >> 1  (count of Uint16)
            int totalU16 = checked((length + 2) * 4);
            if (totalU16 < 16) totalU16 = 16;

            ushort[] dst = new ushort[totalU16];
            dst[0] = (ushort)Math.Min(0xFFFF, totalU16);
            dst[1] = VLC_ID;

            // Reader starts AFTER the 4 header words (8 bytes)
            var br = new BsBitReader(bs, byteOffset: 8);

            int dstOffset = 2;

            while (dstOffset < totalU16)
            {
                // q_scale (10 bits + sign bit)
                int qScale = br.GetBits(10);
                if (br.GetBit() != 0) qScale = -qScale;

                // dc (10 bits + sign bit)
                int dc = br.GetBits(10);
                if (br.GetBit() != 0) dc = -dc;

                dst[dstOffset++] = (ushort)(((qScale & 0x3F) << 10) | (dc & 0x03FF));

                // AC coefficients
                while (dstOffset < totalU16)
                {
                    uint code = GetVlcCode(ref br);

                    int run = (int)((code >> 10) & 0x3F);
                    int level = (int)(code & 0x03FF);
                    int bits = (int)(code >> 16);

                    // sign extend 10-bit
                    if ((level & 0x200) != 0) level -= 0x400;

                    // Special marker 0xFE00 used for ESC/EOB in Mandin tables
                    if (((ushort)code) == EOB)
                    {
                        // EOB: run==63 (in Mandin)
                        if (run == 63)
                        {
                            dst[dstOffset++] = EOB;
                            break;
                        }

                        // ESC: read explicit 16-bit run/level
                        // (Mandin uses ESC as 0xFE00 with run==0)
                        int esc = br.GetBits(16);
                        dst[dstOffset++] = (ushort)esc;
                        continue;
                    }

                    // normal RL
                    dst[dstOffset++] = (ushort)(((run & 0x3F) << 10) | (level & 0x03FF));

                    // End of block when run==63 in many streams; but Mandin ends via EOB code.
                }

                // Safety: if we ran out, stop
                if (!br.HasMore) break;
            }

            // Pad the rest with EOB to keep decoder safe
            for (int i = dstOffset; i < totalU16; i++)
                dst[i] = EOB;

            return dst;
        }

        private static uint GetVlcCode(ref BsBitReader br)
        {
            // This matches Mandin's table selection strategy
            int bits = br.ShowBits(16);

            // "next" table fast path
            if ((bits & 0xF000) >= 0x4000)
            {
                int idx = ((bits >> 12) - 4) * 2;
                if (idx < 0) idx = 0;
                if (idx >= VLCtabnext.Length) idx = VLCtabnext.Length - 2;
                uint code = VLCtabnext[idx];
                br.FlushBits((int)(code >> 16));
                return code;
            }

            // choose VLCtab by leading zeros
            int lz = CountLeadingZeros16(bits);

            uint c;
            if (lz <= 5)
            {
                int idx = ((bits >> 8) & 0x3F);
                idx &= 0x3B;
                idx >>= 1;
                idx *= 2;
                if (idx < 0) idx = 0;
                if (idx >= VLCtab0.Length) idx = VLCtab0.Length - 2;
                c = VLCtab0[idx];
            }
            else if (lz == 6)
            {
                int idx = (bits >> 6) & 0x0F;
                if (idx > 7) idx = 7;
                idx *= 2;
                c = VLCtab1[idx];
            }
            else if (lz == 7)
            {
                int idx = (bits >> 4) & 0x1F;
                idx &= 0x0F;
                idx *= 2;
                c = VLCtab2[idx];
            }
            else if (lz == 8)
            {
                int idx = (bits >> 3) & 0x1F;
                idx &= 0x0F;
                idx *= 2;
                c = VLCtab3[idx];
            }
            else if (lz == 9)
            {
                int idx = (bits >> 2) & 0x1F;
                idx &= 0x0F;
                idx *= 2;
                c = VLCtab4[idx];
            }
            else if (lz == 10)
            {
                int idx = (bits >> 1) & 0x1F;
                idx &= 0x0F;
                idx *= 2;
                c = VLCtab5[idx];
            }
            else
            {
                int idx = (bits >> 0) & 0x1F;
                idx &= 0x0F;
                idx *= 2;
                c = VLCtab6[idx];
            }

            br.FlushBits((int)(c >> 16));
            return c;
        }

        // --------------------------------------------
        // 2) MDEC depack (depack_mdec.c)
        // --------------------------------------------

        private static byte[] MdecDepackToBgr24(ushort[] rlWords, int width, int height)
        {
            if (rlWords.Length < 4) throw new InvalidDataException("RL buffer too small.");

            // rlWords[0] = total_length (u16 count)
            // rlWords[1] = VLC_ID
            if (rlWords[1] != VLC_ID)
                throw new InvalidDataException("RL stream missing VLC_ID.");

            int height2 = (height + 15) & ~15;
            int width2Words = (width * 3) >> 1; // output as u16 words (BGR bytes packed)

            // final output BGR bytes (row-major, top->bottom)
            byte[] outBgr = new byte[width * height * 3];

            // staging slice: height2 rows * 24 u16 words
            ushort[] sliceStage = new ushort[height2 * SLICE_W_U16];

            // setup MDEC context (iqtab init)
            var mdec = new MdecContext();
            InitIqTab(ref mdec);

            // reader over rlWords (start after length word, then VLC_ID word)
            var rr = new RlWordReader(rlWords);
            rr.Skip(1); // skip length
            ushort id = rr.Read();
            if (id != VLC_ID) throw new InvalidDataException("Bad VLC_ID in RL stream.");

            // decode slice-by-slice (each slice is 16px wide)
            for (int xWords = 0; xWords < width2Words; xWords += SLICE_W_U16)
            {
                // fill sliceStage with decoded RGB24 data in Mandin layout
                DecDctOutRgb24(ref mdec, ref rr, sliceStage, height2);

                // copy sliceStage into outBgr with vertical flip (like Mandin)
                CopySliceToOutBgr(outBgr, width, height, sliceStage, xWords);
            }

            return outBgr;
        }

        private static void CopySliceToOutBgr(byte[] outBgr, int width, int height, ushort[] sliceStage, int xWords)
        {
            // sliceStage row: SLICE_W_U16 * 2 bytes = 48 bytes = 16 pixels
            // destination row bytes = width*3
            int dstRowBytes = width * 3;
            int dstXBytes = xWords * 2;
            int sliceRowBytes = SLICE_W_U16 * 2;

            Span<byte> sliceBytes = MemoryMarshal.AsBytes(sliceStage.AsSpan());

            for (int y = 0; y < height; y++)
            {
                int srcY = (height - 1) - y; // vertical flip
                int srcOff = srcY * sliceRowBytes;

                int dstOff = y * dstRowBytes + dstXBytes;

                int remaining = dstRowBytes - dstXBytes;
                int toCopy = sliceRowBytes;
                if (toCopy > remaining) toCopy = remaining;
                if (toCopy <= 0) continue;

                sliceBytes.Slice(srcOff, toCopy).CopyTo(outBgr.AsSpan(dstOff, toCopy));
            }
        }

        private static void DecDctOutRgb24(ref MdecContext ctxt, ref RlWordReader rr, ushort[] sliceStage, int height2)
        {
            // This matches dec_dct_out() + yuv2rgb24() for one 16px slice.
            // For each macroblock row (16px tall), decode 6 blocks and render to sliceStage.

            Span<byte> dstBytes = MemoryMarshal.AsBytes(sliceStage.AsSpan());

            int strideU16 = SLICE_W_U16; // 24 u16
            int strideBytes = strideU16 * 2;

            // reusable blocks
            int[] blocks = new int[64 * 6];

            for (int y = 0; y < height2; y += 16)
            {
                // decode one macroblock (slice is exactly 16px wide, so x is always 0 once)
                Array.Clear(blocks, 0, blocks.Length);

                // block order in Mandin: U, V, Y0, Y1, Y2, Y3
                // (your earlier screenshots strongly indicate this order)
                DecDctIn(ref ctxt, ref rr, blocks, 0 * 64); // U
                DecDctIn(ref ctxt, ref rr, blocks, 1 * 64); // V
                DecDctIn(ref ctxt, ref rr, blocks, 2 * 64); // Y0
                DecDctIn(ref ctxt, ref rr, blocks, 3 * 64); // Y1
                DecDctIn(ref ctxt, ref rr, blocks, 4 * 64); // Y2
                DecDctIn(ref ctxt, ref rr, blocks, 5 * 64); // Y3

                // IDCT each block (in-place)
                for (int i = 0; i < 6; i++)
                    Idct8x8InPlace(blocks, i * 64);

                // render to RGB24 into dstBytes at row y
                int dstBaseBytes = y * strideBytes;
                YuvToRgb24Macroblock(blocks, dstBytes, dstBaseBytes, strideBytes);
            }
        }

        private static void DecDctIn(ref MdecContext ctxt, ref RlWordReader rr, int[] blk6, int blkBase)
        {
            // matches dec_dct_in() logic (dequant + zigzag fill)
            ushort w0 = rr.Read();
            int qScale = (w0 >> 10) & 0x3F;
            int dc = Sign10(w0 & 0x03FF);

            // dc scaling like Mandin path: blk[0] = iqtab[0] * dc
            blk6[blkBase + 0] = ctxt.IqTab[0] * dc;

            int k = 0;
            while (true)
            {
                ushort w = rr.Read();
                if (w == EOB) break;

                int run = (w >> 10) & 0x3F;
                int level = Sign10(w & 0x03FF);

                k += run + 1;
                if (k >= 64) break;

                int zz = ZigZag[k];

                // blk[zz] = (iqtab[zz] * qScale * level) >> 3
                int v = (ctxt.IqTab[zz] * qScale * level) >> 3;
                blk6[blkBase + zz] = v;
            }
        }

        private static void YuvToRgb24Macroblock(int[] blk, Span<byte> dst, int dstBase, int rowStrideBytes)
        {
            // blk layout: [U 64][V 64][Y0 64][Y1 64][Y2 64][Y3 64]
            int uBase = 0;
            int vBase = 64;
            int yBase = 128;

            for (int yy = 0; yy < 16; yy++)
            {
                int dstRow = dstBase + yy * rowStrideBytes;

                for (int xx = 0; xx < 16; xx++)
                {
                    int cy = yy >> 1;
                    int cx = xx >> 1;
                    int ci = cy * 8 + cx;

                    int cb = blk[uBase + ci];
                    int cr = blk[vBase + ci];

                    int y = GetY(blk, yBase, xx, yy);

                    // conversion constants (fixed-point-ish like Mandin)
                    int r = y + ((1436 * cr) >> 10);
                    int g = y - ((352 * cb + 731 * cr) >> 10);
                    int b = y + ((1815 * cb) >> 10);

                    int o = dstRow + xx * 3;
                    dst[o + 0] = (byte)Clamp8(b + 128);
                    dst[o + 1] = (byte)Clamp8(g + 128);
                    dst[o + 2] = (byte)Clamp8(r + 128);
                }
            }
        }

        private static int GetY(int[] blk, int yBase, int x, int y)
        {
            // map 16x16 to Y0..Y3 blocks (8x8)
            int bx = x & 7;
            int by = y & 7;
            int idx = by * 8 + bx;

            int block;
            if (y < 8)
                block = (x < 8) ? 0 : 1;
            else
                block = (x < 8) ? 2 : 3;

            return blk[yBase + block * 64 + idx];
        }

        // --------------------------------------------
        // IQ table init (from depack_mdec.c idea)
        // --------------------------------------------

        private static void InitIqTab(ref MdecContext ctxt)
        {
            // iqtab[i] = bs_iqtab[i] * aanscales[i] >> 12
            // We build aanscales via AAN outer product (standard fast IDCT scaling)
            for (int i = 0; i < 64; i++)
            {
                int v = BsIqTab[i] * AanScales[i];
                ctxt.IqTab[i] = v >> 12;
                if (ctxt.IqTab[i] == 0) ctxt.IqTab[i] = 1;
            }
        }

        // --------------------------------------------
        // IDCT (simple, stable)
        // --------------------------------------------

        private static void Idct8x8InPlace(int[] data, int baseIndex)
        {
            // Simple double IDCT (quality > speed). Good enough to validate correctness of bitstream.
            double[] tmp = new double[64];

            // rows
            for (int y = 0; y < 8; y++)
            {
                int row = baseIndex + y * 8;
                for (int x = 0; x < 8; x++)
                {
                    double sum = 0.0;
                    for (int u = 0; u < 8; u++)
                    {
                        double cu = (u == 0) ? 1.0 / Math.Sqrt(2.0) : 1.0;
                        sum += cu * data[row + u] * Math.Cos(((2 * x + 1) * u * Math.PI) / 16.0);
                    }
                    tmp[y * 8 + x] = sum / 2.0;
                }
            }

            // cols
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    double sum = 0.0;
                    for (int v = 0; v < 8; v++)
                    {
                        double cv = (v == 0) ? 1.0 / Math.Sqrt(2.0) : 1.0;
                        sum += cv * tmp[v * 8 + x] * Math.Cos(((2 * y + 1) * v * Math.PI) / 16.0);
                    }
                    data[baseIndex + y * 8 + x] = (int)Math.Round(sum / 2.0);
                }
            }
        }

        // --------------------------------------------
        // Helpers / structs
        // --------------------------------------------

        private struct MdecContext
        {
            public int[] IqTab; // 64
            public MdecContext()
            {
                IqTab = new int[64];
            }
        }

        private struct RlWordReader
        {
            private readonly ushort[] _w;
            private int _p;

            public RlWordReader(ushort[] words)
            {
                _w = words;
                _p = 0;
            }

            public void Skip(int count)
            {
                _p += count;
                if (_p < 0) _p = 0;
                if (_p > _w.Length) _p = _w.Length;
            }

            public ushort Read()
            {
                if (_p >= _w.Length) return EOB;
                return _w[_p++];
            }
        }

        private struct BsBitReader
        {
            private readonly byte[] _src;
            private int _bytePos;
            private ushort _cur;
            private int _mask; // 0x8000..1

            public BsBitReader(byte[] src, int byteOffset)
            {
                _src = src;
                _bytePos = byteOffset;
                _cur = 0;
                _mask = 0;
                LoadWord();
            }

            public bool HasMore => _bytePos < _src.Length || _mask != 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetBit()
            {
                int r = ((_cur & _mask) != 0) ? 1 : 0;
                _mask >>= 1;
                if (_mask == 0) LoadWord();
                return r;
            }

            public int GetBits(int n)
            {
                int v = 0;
                for (int i = 0; i < n; i++)
                    v = (v << 1) | GetBit();
                return v;
            }

            public int ShowBits(int n)
            {
                // non-destructive peek (slow but safe)
                int saveBytePos = _bytePos;
                ushort saveCur = _cur;
                int saveMask = _mask;

                int v = GetBits(n);

                _bytePos = saveBytePos;
                _cur = saveCur;
                _mask = saveMask;

                return v;
            }

            public void FlushBits(int n)
            {
                for (int i = 0; i < n; i++)
                    GetBit();
            }

            private void LoadWord()
            {
                if (_bytePos + 2 <= _src.Length)
                {
                    _cur = BinaryPrimitives.ReadUInt16LittleEndian(_src.AsSpan(_bytePos, 2));
                    _bytePos += 2;
                    _mask = 0x8000;
                }
                else
                {
                    _cur = 0;
                    _mask = 0;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ReadU16LE(byte[] b, int off)
        {
            if (off + 2 > b.Length) return 0;
            return BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(off, 2));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Sign10(int v)
        {
            v &= 0x3FF;
            if ((v & 0x200) != 0) v -= 0x400;
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Clamp8(int v) => v < 0 ? 0 : (v > 255 ? 255 : v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountLeadingZeros16(int x)
        {
            x &= 0xFFFF;
            int n = 0;
            for (int i = 15; i >= 0; i--)
            {
                if (((x >> i) & 1) != 0) break;
                n++;
            }
            return n;
        }

        // ZigZag like Mandin (ZAG in depack_mdec.c)
        private static readonly int[] ZigZag = new int[64]
        {
            0,  1,  8, 16,  9,  2,  3, 10,
            17, 24, 32, 25, 18, 11,  4,  5,
            12, 19, 26, 33, 40, 48, 41, 34,
            27, 20, 13,  6,  7, 14, 21, 28,
            35, 42, 49, 56, 57, 50, 43, 36,
            29, 22, 15, 23, 30, 37, 44, 51,
            58, 59, 52, 45, 38, 31, 39, 46,
            53, 60, 61, 54, 47, 55, 62, 63
        };

        // BS quant table (from depack_mdec.c bs_iqtab)
        private static readonly int[] BsIqTab = new int[64]
        {
            2,16,19,22,26,27,29,34,
            16,16,22,24,27,29,34,37,
            19,22,26,27,29,34,34,38,
            22,22,26,27,29,34,37,40,
            22,26,27,29,32,35,40,48,
            26,27,29,32,35,40,48,58,
            26,27,29,34,38,46,56,69,
            27,29,35,38,46,56,69,83
        };

        // AAN scales outer-product (standard libjpeg fast-idct scaling)
        private static readonly int[] AanScales = BuildAanScales();

        private static int[] BuildAanScales()
        {
            // base factors
            int[] f = new int[8] { 16384, 22725, 21407, 19266, 16384, 12873, 8867, 4520 };
            int[] a = new int[64];
            int k = 0;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    a[k++] = (f[y] * f[x] + 8192) >> 14;
            return a;
        }

        // --------------------------------------------
        // VLC tables expanded from depack_vlc.c macros
        // (These are required for correct depack)
        // --------------------------------------------

        private static readonly uint[] VLCtabnext = new uint[]
        {
            0x00050002u, 0x000503FEu, 0x00050801u, 0x00050BFFu, 0x00040401u, 0x00040401u,
            0x000407FFu, 0x000407FFu, 0x0002FE00u, 0x0002FE00u, 0x0002FE00u, 0x0002FE00u,
            0x0002FE00u, 0x0002FE00u, 0x0002FE00u, 0x0002FE00u, 0x00030001u, 0x00030001u,
            0x00030001u, 0x00030001u, 0x000303FFu, 0x000303FFu, 0x000303FFu, 0x000303FFu,
        };

        private static readonly uint[] VLCtab0 = new uint[]
        {
            // 120 entries (60*2) expanded from Mandin table macros
            0x0006FC00u, 0x0006FC00u, 0x0006FC00u, 0x0006FC00u, 0x0007FC00u, 0x0007FC00u,
            0x0007FC00u, 0x0007FC00u, 0x00070002u, 0x0007FFFEu, 0x00070009u, 0x0007FFF7u,
            0x00070000u, 0x0007FFFCu, 0x00070008u, 0x0007FFF8u, 0x00060007u, 0x0006FFF9u,
            0x00060006u, 0x0006FFFAu, 0x00060001u, 0x0006FFFFu, 0x00060005u, 0x0006FFFBu,
            0x00060003u, 0x0006FFFDu, 0x00060004u, 0x0006FFFCu, 0x0005000Fu, 0x0005FFF1u,
            0x00050010u, 0x0005FFF0u, 0x0005000Eu, 0x0005FFF2u, 0x00050011u, 0x0005FFEFu,
            0x0005000Du, 0x0005FFF3u, 0x00050012u, 0x0005FFEEu, 0x0005000Cu, 0x0005FFF4u,
            0x00050013u, 0x0005FFEDu, 0x0005000Bu, 0x0005FFF5u, 0x00050014u, 0x0005FFEcu,
            0x0004001Bu, 0x0004FFE5u, 0x0004001Au, 0x0004FFE6u, 0x00040019u, 0x0004FFE7u,
            0x00040018u, 0x0004FFE8u, 0x00040017u, 0x0004FFE9u, 0x00040016u, 0x0004FFEAu,
            0x00040015u, 0x0004FFEbu, 0x00040014u, 0x0004FFEcu, 0x00040013u, 0x0004FFEDu,
            0x00040012u, 0x0004FFEEu, 0x00040011u, 0x0004FFEFu, 0x00040010u, 0x0004FFF0u,
            0x00030022u, 0x0003FFDEu, 0x00030021u, 0x0003FFDFu, 0x00030020u, 0x0003FFE0u,
            0x0003001Fu, 0x0003FFE1u, 0x0003001Eu, 0x0003FFE2u, 0x0003001Du, 0x0003FFE3u,
            0x0003001Cu, 0x0003FFE4u, 0x0003001Bu, 0x0003FFE5u, 0x0003001Au, 0x0003FFE6u,
            0x00030019u, 0x0003FFE7u, 0x00030018u, 0x0003FFE8u, 0x00030017u, 0x0003FFE9u,
            0x00030016u, 0x0003FFEAu, 0x00030015u, 0x0003FFEbu, 0x00030014u, 0x0003FFEcu,
            0x00030013u, 0x0003FFEDu, 0x00030012u, 0x0003FFEEu, 0x00030011u, 0x0003FFEFu,
            0x00030010u, 0x0003FFF0u, 0x0003000Fu, 0x0003FFF1u, 0x0003000Eu, 0x0003FFF2u,
        };

        private static readonly uint[] VLCtab1 = new uint[]
        {
            0x000A0010u, 0x000AFFF0u, 0x000A0001u, 0x000AFFFFu, 0x000A0002u, 0x000AFFFEu, 0x000A0003u, 0x000AFFFDu,
            0x000A0004u, 0x000AFFFCu, 0x000A0005u, 0x000AFFFBu, 0x000A0006u, 0x000AFFFAu, 0x000A0007u, 0x000AFFF9u,
        };

        private static readonly uint[] VLCtab2 = new uint[]
        {
            0x000C000Bu, 0x000CFFF5u, 0x000C0008u, 0x000CFFF8u, 0x000C0004u, 0x000CFFFCu, 0x000C000Au, 0x000CFFF6u,
            0x000C0009u, 0x000CFFF7u, 0x000C0007u, 0x000CFFF9u, 0x000C0006u, 0x000CFFFAu, 0x000C0005u, 0x000CFFFBu,
        };

        private static readonly uint[] VLCtab3 = new uint[]
        {
            0x000D000Au, 0x000DFFF6u, 0x000D0009u, 0x000DFFF7u, 0x000D0005u, 0x000DFFFBu, 0x000D0003u, 0x000DFFFDu,
            0x000D0008u, 0x000DFFF8u, 0x000D0007u, 0x000DFFF9u, 0x000D0006u, 0x000DFFFAu, 0x000D0004u, 0x000DFFFCu,
        };

        private static readonly uint[] VLCtab4 = new uint[]
        {
            0x000E001Fu, 0x000EFFE1u, 0x000E001Eu, 0x000EFFE2u, 0x000E001Du, 0x000EFFE3u, 0x000E001Cu, 0x000EFFE4u,
            0x000E001Bu, 0x000EFFE5u, 0x000E001Au, 0x000EFFE6u, 0x000E0019u, 0x000EFFE7u, 0x000E0018u, 0x000EFFE8u,
        };

        private static readonly uint[] VLCtab5 = new uint[]
        {
            0x000F0028u, 0x000FFFD8u, 0x000F0027u, 0x000FFFD9u, 0x000F0026u, 0x000FFFDAu, 0x000F0025u, 0x000FFFDBu,
            0x000F0024u, 0x000FFFDCu, 0x000F0023u, 0x000FFFDDu, 0x000F0022u, 0x000FFFDEu, 0x000F0021u, 0x000FFFDFu,
        };

        private static readonly uint[] VLCtab6 = new uint[]
        {
            0x00100012u, 0x0010FFEEu, 0x00100011u, 0x0010FFEFu, 0x00100010u, 0x0010FFF0u, 0x0010000Fu, 0x0010FFF1u,
            0x0010000Eu, 0x0010FFF2u, 0x0010000Du, 0x0010FFF3u, 0x0010000Cu, 0x0010FFF4u, 0x0010000Bu, 0x0010FFF5u,
        };
    }
}
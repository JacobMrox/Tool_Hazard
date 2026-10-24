// BssCodec.cs
// .NET 8.0
// Resident Evil 1-3 BSS/BS background codec (Decode implemented from Mandin depack_vlc.c + depack_mdec.c)
// Public API matches BssViewerForm.cs: DecodeToBitmap(frameBytes,w,h) and EncodeFromBitmap(...)
//
// NOTES:
// - BSS "frame" here means ONE camera chunk (0x8000 or 0x10000) that your form already slices.
// - Encoder is currently a stub (won't produce original-valid BS). Decode is the priority.

using System.Buffers.Binary;
using System.Drawing.Imaging;
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
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (quant < 1 || quant > 63) throw new ArgumentOutOfRangeException(nameof(quant), "Quant must be 1..63.");
            throw new NotSupportedException(
                "BSS encoding is not implemented yet. The previous placeholder produced invalid game data, so import and re-encode are disabled until a real encoder is available.");
        }

        // --------------------------------------------
        // 1) VLC depack (depack_vlc.c)
        // --------------------------------------------

        private static ushort[] VlcDepackToRlWords(byte[] bs)
        {
            if (bs.Length < 8) throw new InvalidDataException("BS too small.");

            ushort length = ReadU16LE(bs, 0);
            ushort id = ReadU16LE(bs, 2);
            ushort quant = ReadU16LE(bs, 4);
            ushort version = ReadU16LE(bs, 6);
            if (id != VLC_ID)
                throw new InvalidDataException($"Not a BS/VLC stream (id=0x{id:X4}).");
            if (version is not 2 and not 3)
                throw new InvalidDataException($"Unsupported PlayStation BS version {version}.");

            int totalU16 = checked((length + 2) * 4);
            if (totalU16 < 2)
                throw new InvalidDataException("Invalid BS output length.");

            ushort[] dst = new ushort[totalU16];
            dst[0] = length;
            dst[1] = VLC_ID;
            var br = new BsBitReader(bs, byteOffset: 8);
            int dstOffset = 2;
            int block = 0;
            int[] lastDc = new int[3];
            int qCode = quant << 10;

            while (dstOffset < totalU16)
            {
                uint code = version == 2
                    ? (uint)(br.ShowBits(10) | (10 << 16))
                    : DecodeVersion3Dc(ref br, block, lastDc);
                code = (code & 0xFFFF03FFu) | (uint)qCode;

                while (true)
                {
                    if (dstOffset >= totalU16)
                        return dst;

                    dst[dstOffset++] = (ushort)code;
                    br.FlushBits((int)(code >> 16));

                    code = DecodeAcCode(ref br);
                    if (code == EndStreamCode)
                    {
                        while (dstOffset < totalU16)
                            dst[dstOffset++] = EOB;
                        return dst;
                    }
                    if (code == EobCode)
                        break;

                    if (code == EscapeCode)
                    {
                        br.FlushBits(6);
                        code = (uint)(br.ShowBits(16) | (16 << 16));
                    }
                }

                if (dstOffset < totalU16)
                    dst[dstOffset++] = EOB;
                br.FlushBits(2);
                block = (block + 1) % 6;
            }

            return dst;
        }

        private static uint DecodeVersion3Dc(ref BsBitReader br, int block, int[] lastDc)
        {
            int prefix = br.ShowBits(6);
            uint code;
            int channel = block < 2 ? block : 2;

            if (block >= 2)
            {
                if (prefix < DcYTable.Length)
                    code = DcYTable[prefix];
                else
                    code = DecodeLongDc(ref br, 3, yChannel: true);
            }
            else
            {
                if (prefix < DcUvTable.Length)
                    code = DcUvTable[prefix];
                else
                    code = DecodeLongDc(ref br, 4, yChannel: false);
            }

            int delta = Sign10((int)code) * 4;
            int dc = lastDc[channel] + delta;
            lastDc[channel] = dc;
            return (code & 0xFFFF0000u) | (uint)(dc & 0x3FF);
        }

        private static uint DecodeLongDc(ref BsBitReader br, int startBits, bool yChannel)
        {
            int bit = startBits;
            while ((br.ShowBits(bit) & 1) != 0 && bit < 16)
                bit++;

            if (yChannel)
                bit++;

            int totalBits = yChannel ? bit * 2 - 1 : bit * 2;
            int value = br.ShowBits(totalBits) & ((1 << bit) - 1);
            if ((value & (1 << (bit - 1))) == 0)
                value -= (1 << bit) - 1;
            return (uint)((totalBits << 16) | (value & 0x3FF));
        }

        private static uint DecodeAcCode(ref BsBitReader br)
        {
            int code = br.ShowBits(17);
            if (code >= 1 << 15) return VlcNext[(code >> 12) - 8];
            if (code >= 1 << 11)
            {
                int index = (code >> 8) - 8;
                if ((uint)index >= (uint)Vlc0.Length)
                    throw new InvalidDataException($"Invalid VLC table-0 index {index} (bits=0x{code:X5}).");
                return Vlc0[index];
            }
            if (code >= 1 << 10) return Vlc1[(code >> 6) - 16];
            if (code >= 1 << 9) return Vlc2[(code >> 4) - 32];
            if (code >= 1 << 8) return Vlc3[(code >> 3) - 32];
            if (code >= 1 << 7) return Vlc4[(code >> 2) - 32];
            if (code >= 1 << 6) return Vlc5[(code >> 1) - 32];
            if (code >= 1 << 5) return Vlc6[code - 32];
            return EndStreamCode;
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
                int srcY = y;
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
                    // The PlayStation decoder's 24-bit output is RGB byte order.
                    // Format24bppRgb stores scanlines as BGR, so reverse R/B here.
                    dst[o + 0] = (byte)Clamp8(r + 128);
                    dst[o + 1] = (byte)Clamp8(g + 128);
                    dst[o + 2] = (byte)Clamp8(b + 128);
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
            // The BS quantizer is paired with the AAN integer IDCT below.
            // Folding its scale factors into this table is part of the format.
            for (int i = 0; i < 64; i++)
                ctxt.IqTab[i] = (BsIqTab[i] * AanScales[i]) >> 12;
        }

        // --------------------------------------------
        // AAN fast integer IDCT used by the original BS/MDEC decoder.
        // --------------------------------------------

        private static void Idct8x8InPlace(int[] data, int baseIndex)
        {
            // Pass 1: columns.
            for (int col = 0; col < 8; col++)
            {
                int p = baseIndex + col;
                if ((data[p + 8] | data[p + 16] | data[p + 24] | data[p + 32] |
                     data[p + 40] | data[p + 48] | data[p + 56]) == 0)
                {
                    int dc = data[p];
                    for (int row = 0; row < 8; row++) data[p + row * 8] = dc;
                    continue;
                }

                IdctPass(data, p, 8, false);
            }

            // Pass 2: rows and final scale-down (PASS1_BITS + 3 = 5).
            for (int row = 0; row < 8; row++)
            {
                int p = baseIndex + row * 8;
                if ((data[p + 1] | data[p + 2] | data[p + 3] | data[p + 4] |
                     data[p + 5] | data[p + 6] | data[p + 7]) == 0)
                {
                    int dc = data[p] >> 5;
                    for (int col = 0; col < 8; col++) data[p + col] = dc;
                    continue;
                }

                IdctPass(data, p, 1, true);
            }
        }

        private static void IdctPass(int[] data, int p, int step, bool finalPass)
        {
            int z10 = data[p] + data[p + 4 * step];
            int z11 = data[p] - data[p + 4 * step];
            int z13 = data[p + 2 * step] + data[p + 6 * step];
            int z12 = MultiplyIdct(data[p + 2 * step] - data[p + 6 * step], 362) - z13;

            int tmp0 = z10 + z13;
            int tmp3 = z10 - z13;
            int tmp1 = z11 + z12;
            int tmp2 = z11 - z12;

            z13 = data[p + 3 * step] + data[p + 5 * step];
            z10 = data[p + 3 * step] - data[p + 5 * step];
            z11 = data[p + step] + data[p + 7 * step];
            z12 = data[p + step] - data[p + 7 * step];

            int z5 = MultiplyIdct(z12 - z10, 473);
            int tmp7 = z11 + z13;
            int tmp6 = MultiplyIdct(z10, 669) + z5 - tmp7;
            int tmp5 = MultiplyIdct(z11 - z13, 362) - tmp6;
            int tmp4 = MultiplyIdct(z12, 277) - z5 + tmp5;

            int shift = finalPass ? 5 : 0;
            data[p] = (tmp0 + tmp7) >> shift;
            data[p + 7 * step] = (tmp0 - tmp7) >> shift;
            data[p + step] = (tmp1 + tmp6) >> shift;
            data[p + 6 * step] = (tmp1 - tmp6) >> shift;
            data[p + 2 * step] = (tmp2 + tmp5) >> shift;
            data[p + 5 * step] = (tmp2 - tmp5) >> shift;
            data[p + 4 * step] = (tmp3 + tmp4) >> shift;
            data[p + 3 * step] = (tmp3 - tmp4) >> shift;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int MultiplyIdct(int value, int constant) => (value * constant) >> 8;

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

        private static readonly int[] AanScales = new int[64]
        {
            16384,22725,21407,19266,16384,12873, 8867,4520,
            22725,31521,29692,26722,22725,17855,12299,6270,
            21407,29692,27969,25172,21407,16819,11585,5906,
            19266,26722,25172,22654,19266,15137,10426,5315,
            16384,22725,21407,19266,16384,12873, 8867,4520,
            12873,17855,16819,15137,12873,10114, 6967,3552,
             8867,12299,11585,10426, 8867, 6967, 4799,2446,
             4520, 6270, 5906, 5315, 4520, 3552, 2446,1247
        };

        private static uint Code(int run, int level, int bits) =>
            (uint)((run << 10) | (level & 0x3FF) | (bits << 16));

        private static readonly uint EscapeCode = Code(63, 0, 6);
        private static readonly uint EobCode = Code(63, 512, 2);
        private const uint EndStreamCode = uint.MaxValue;
        private static readonly uint[] VlcNext = BuildVlcNext();
        private static readonly uint[] Vlc0 = BuildVlc0();
        private static readonly uint[] Vlc1 = Pairs((16,1),(5,2),(0,7),(2,3),(1,4),(15,1),(14,1),(4,2), 10);
        private static readonly uint[] Vlc2 = Pairs((0,11),(8,2),(4,3),(0,10),(2,4),(7,2),(21,1),(20,1),(0,9),(19,1),(18,1),(1,5),(3,3),(0,8),(6,2),(17,1), 12);
        private static readonly uint[] Vlc3 = Pairs((10,2),(9,2),(5,3),(3,4),(2,5),(1,7),(1,6),(0,15),(0,14),(0,13),(0,12),(26,1),(25,1),(24,1),(23,1),(22,1), 13);
        private static readonly uint[] Vlc4 = Pairs((0,31),(0,30),(0,29),(0,28),(0,27),(0,26),(0,25),(0,24),(0,23),(0,22),(0,21),(0,20),(0,19),(0,18),(0,17),(0,16), 14);
        private static readonly uint[] Vlc5 = Pairs((0,40),(0,39),(0,38),(0,37),(0,36),(0,35),(0,34),(0,33),(0,32),(1,14),(1,13),(1,12),(1,11),(1,10),(1,9),(1,8), 15);
        private static readonly uint[] Vlc6 = Pairs((1,18),(1,17),(1,16),(1,15),(6,3),(16,2),(15,2),(14,2),(13,2),(12,2),(11,2),(31,1),(30,1),(29,1),(28,1),(27,1), 16);
        private static readonly uint[] DcYTable = BuildDcYTable();
        private static readonly uint[] DcUvTable = BuildDcUvTable();

        private static uint[] Pairs((int run, int level) a,(int,int) b,(int,int) c,(int,int) d,(int,int) e,(int,int) f,(int,int) g,(int,int) h, int bits) => Pairs(new[] { a,b,c,d,e,f,g,h }, bits);
        private static uint[] Pairs((int run, int level) a,(int,int) b,(int,int) c,(int,int) d,(int,int) e,(int,int) f,(int,int) g,(int,int) h,(int,int) i,(int,int) j,(int,int) k,(int,int) l,(int,int) m,(int,int) n,(int,int) o,(int,int) p, int bits) => Pairs(new[] { a,b,c,d,e,f,g,h,i,j,k,l,m,n,o,p }, bits);
        private static uint[] Pairs(IEnumerable<(int run, int level)> values, int bits) =>
            values.SelectMany(v => new[] { Code(v.run, v.level, bits + 1), Code(v.run, -v.level, bits + 1) }).ToArray();

        private static uint[] BuildVlcNext()
        {
            var t = new List<uint>();
            AddPair(t, 0, 2, 4); AddPair(t, 2, 1, 4);
            AddTwice(t, 1, 1, 3); AddTwice(t, 1, -1, 3);
            AddRepeat(t, 63, 512, 2, 8);
            AddTwice(t, 0, 1, 2); AddTwice(t, 0, 1, 2);
            AddTwice(t, 0, -1, 2); AddTwice(t, 0, -1, 2);
            return t.ToArray();
        }

        private static uint[] BuildVlc0()
        {
            var t = new List<uint>();
            AddRepeat(t,63,0,6,8); AddTwice(t,2,2,7); AddTwice(t,2,-2,7); AddTwice(t,9,1,7); AddTwice(t,9,-1,7);
            AddTwice(t,0,4,7); AddTwice(t,0,-4,7); AddTwice(t,8,1,7); AddTwice(t,8,-1,7);
            foreach (var v in new[] {(7,1),(7,1),(7,-1),(7,-1),(6,1),(6,1),(6,-1),(6,-1),
                                      (1,2),(1,2),(1,-2),(1,-2),(5,1),(5,1),(5,-1),(5,-1)})
                AddTwice(t,v.Item1,v.Item2,6);
            foreach (var v in new[] {(13,1),(0,6),(12,1),(11,1),(3,2),(1,3),(0,5),(10,1)}) AddPair(t,v.Item1,v.Item2,8);
            foreach (var v in new[] {(0,3),(0,-3),(4,1),(4,-1),(3,1),(3,-1)}) AddRepeat(t,v.Item1,v.Item2,6,8);
            return t.ToArray();
        }

        private static uint[] BuildDcYTable()
        {
            var t = new List<uint>(48);
            AddRepeat(t,0,-1,3,8); AddRepeat(t,0,1,3,8);
            AddRepeat(t,0,-3,4,4); AddRepeat(t,0,-2,4,4); AddRepeat(t,0,2,4,4); AddRepeat(t,0,3,4,4);
            AddRepeat(t,0,0,3,8);
            foreach (int v in new[] {-7,-6,-5,-4,4,5,6,7}) t.Add(Code(0,v,6));
            return t.ToArray();
        }

        private static uint[] BuildDcUvTable()
        {
            var t = new List<uint>(56);
            AddRepeat(t,0,0,2,16); AddRepeat(t,0,-1,3,8); AddRepeat(t,0,1,3,8);
            AddRepeat(t,0,-3,4,4); AddRepeat(t,0,-2,4,4); AddRepeat(t,0,2,4,4); AddRepeat(t,0,3,4,4);
            foreach (int v in new[] {-7,-6,-5,-4,4,5,6,7}) t.Add(Code(0,v,6));
            return t.ToArray();
        }

        private static void AddPair(List<uint> t, int run, int level, int bits) { t.Add(Code(run,level,bits+1)); t.Add(Code(run,-level,bits+1)); }
        private static void AddTwice(List<uint> t, int run, int level, int bits) { t.Add(Code(run,level,bits+1)); t.Add(Code(run,level,bits+1)); }
        private static void AddRepeat(List<uint> t, int run, int level, int bits, int count) { for (int i=0;i<count;i++) t.Add(Code(run,level,bits)); }
    }
}

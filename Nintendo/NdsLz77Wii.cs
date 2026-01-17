using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tool_Hazard.Nintendo
{
    /// <summary>
    /// Equivalent to QuickBMS "comtype lz77wii" for common Nintendo LZ77 formats (0x10, 0x11).
    /// </summary>
    public static class NdsLz77Wii
    {
        public static bool TryDecompress(ReadOnlySpan<byte> src, out byte[] output)
        {
            output = Array.Empty<byte>();

            if (src.Length < 4)
                return false;

            byte type = src[0];
            if (type != 0x10 && type != 0x11)
                return false;

            try
            {
                output = type == 0x10 ? DecompressLz10(src) : DecompressLz11(src);
                return output.Length > 0; // if 0, treat as "not a real LZ stream" for our purposes
            }
            catch
            {
                output = Array.Empty<byte>();
                return false;
            }
        }

        public static byte[] Decompress(ReadOnlySpan<byte> src)
        {
            if (!TryDecompress(src, out var outBytes))
                throw new InvalidDataException("Input is not a valid Nintendo LZ77 stream (0x10/0x11).");
            return outBytes;
        }

        private static (int outSize, int start) ReadNintendoSize(ReadOnlySpan<byte> src)
        {
            // [0]=type, [1..3]=24-bit little endian size
            int size24 = src[1] | (src[2] << 8) | (src[3] << 16);
            if (size24 != 0)
                return (size24, 4);

            // Extended size variant: if size24 == 0, next 4 bytes are 32-bit LE size, stream starts at 8
            if (src.Length < 8)
                return (0, 4);

            int size32 = BinaryPrimitives.ReadInt32LittleEndian(src.Slice(4, 4));
            return (size32, 8);
        }

        private static byte[] DecompressLz10(ReadOnlySpan<byte> src)
        {
            var (outSize, sp0) = ReadNintendoSize(src);
            if (outSize <= 0)
                return Array.Empty<byte>();

            byte[] dst = new byte[outSize];
            int sp = sp0;
            int dp = 0;

            while (dp < outSize)
            {
                if (sp >= src.Length) throw new InvalidDataException("Unexpected end of LZ10 input.");
                byte flags = src[sp++];

                for (int bit = 7; bit >= 0 && dp < outSize; bit--)
                {
                    bool isCompressed = ((flags >> bit) & 1) != 0;

                    if (!isCompressed)
                    {
                        if (sp >= src.Length) throw new InvalidDataException("Unexpected end of LZ10 input (literal).");
                        dst[dp++] = src[sp++];
                    }
                    else
                    {
                        if (sp + 1 >= src.Length) throw new InvalidDataException("Unexpected end of LZ10 input (token).");

                        byte b1 = src[sp++];
                        byte b2 = src[sp++];

                        int length = (b1 >> 4) + 3;
                        int disp = (((b1 & 0x0F) << 8) | b2) + 1;

                        int copyFrom = dp - disp;
                        if (copyFrom < 0) throw new InvalidDataException("LZ10 invalid back-reference.");

                        for (int i = 0; i < length && dp < outSize; i++)
                            dst[dp++] = dst[copyFrom++];
                    }
                }
            }

            return dst;
        }

        private static byte[] DecompressLz11(ReadOnlySpan<byte> src)
        {
            var (outSize, sp0) = ReadNintendoSize(src);
            if (outSize <= 0)
                return Array.Empty<byte>();

            byte[] dst = new byte[outSize];
            int sp = sp0;
            int dp = 0;

            while (dp < outSize)
            {
                if (sp >= src.Length) throw new InvalidDataException("Unexpected end of LZ11 input.");
                byte flags = src[sp++];

                for (int bit = 7; bit >= 0 && dp < outSize; bit--)
                {
                    bool isCompressed = ((flags >> bit) & 1) != 0;

                    if (!isCompressed)
                    {
                        if (sp >= src.Length) throw new InvalidDataException("Unexpected end of LZ11 input (literal).");
                        dst[dp++] = src[sp++];
                        continue;
                    }

                    if (sp >= src.Length) throw new InvalidDataException("Unexpected end of LZ11 input (token).");
                    byte b1 = src[sp++];

                    int length;
                    int disp;

                    int top = b1 >> 4;

                    if (top == 0)
                    {
                        // 3-byte token
                        // length = ((b1 & 0xF) << 4) | (b2 >> 4) + 0x11
                        // disp   = ((b2 & 0xF) << 8) | b3 + 1
                        if (sp + 1 >= src.Length) throw new InvalidDataException("Unexpected end of LZ11 input (3-byte token).");

                        byte b2 = src[sp++];
                        byte b3 = src[sp++];

                        length = (((b1 & 0x0F) << 4) | (b2 >> 4)) + 0x11;
                        disp = (((b2 & 0x0F) << 8) | b3) + 1;
                    }
                    else if (top == 1)
                    {
                        // 4-byte token
                        // length = ((b1 & 0xF) << 12) | (b2 << 4) | (b3 >> 4) + 0x111
                        // disp   = ((b3 & 0xF) << 8) | b4 + 1
                        if (sp + 2 >= src.Length) throw new InvalidDataException("Unexpected end of LZ11 input (4-byte token).");

                        byte b2 = src[sp++];
                        byte b3 = src[sp++];
                        byte b4 = src[sp++];

                        length = (((b1 & 0x0F) << 12) | (b2 << 4) | (b3 >> 4)) + 0x111;
                        disp = (((b3 & 0x0F) << 8) | b4) + 1;
                    }
                    else
                    {
                        // 2-byte token
                        // length = top + 1
                        // disp   = ((b1 & 0xF) << 8) | b2 + 1
                        if (sp >= src.Length) throw new InvalidDataException("Unexpected end of LZ11 input (2-byte token).");

                        byte b2 = src[sp++];
                        length = top + 1;
                        disp = (((b1 & 0x0F) << 8) | b2) + 1;
                    }

                    int copyFrom = dp - disp;
                    if (copyFrom < 0) throw new InvalidDataException("LZ11 invalid back-reference.");

                    for (int i = 0; i < length && dp < outSize; i++)
                        dst[dp++] = dst[copyFrom++];
                }
            }

            return dst;
        }
    }
    /// <summary>
    /// Data model for an extracted entry (matches the BMS name i/x/z.).
    /// </summary>
    public sealed class DeadlySilenceEntry
    {
        public required string Name { get; init; }           // e.g. "0/3/12."
        public required long DataOffset { get; init; }       // absolute offset in file
        public required int StoredSize { get; init; }        // after stripping 0x80000000 if compressed
        public required bool IsCompressed { get; init; }     // SIZE high bit set
        public int? DecompressedSizeHint { get; init; }      // optional (some containers store it)
    }

    /// <summary>
    /// Translation of your QuickBMS script into stream-based C#.
    /// It discovers entries and can extract them (raw or LZ77Wii decompressed).
    /// </summary>
    public static class ResidentEvilDeadlySilenceExtractor
    {
        public static List<DeadlySilenceEntry> Scan(Stream input, bool leaveOpen = true)
        {
            if (!input.CanSeek)
                throw new ArgumentException("Input stream must be seekable.", nameof(input));

            var entries = new List<DeadlySilenceEntry>();

            using var br = new BinaryReader(input, Encoding.ASCII, leaveOpen: leaveOpen);

            // get FILES long; math FILES / 8; goto 0
            input.Position = 0;
            uint filesRaw = ReadU32LE(br);
            uint files = filesRaw / 8;

            // for i = 0 < FILES: get OFFSET long, get SIZE long
            input.Position = 0;
            for (uint i = 0; i < files; i++)
            {
                uint offset = ReadU32LE(br);
                uint size = ReadU32LE(br);

                long tmpOff = input.Position;         // savepos TMP_OFF
                input.Position = offset;              // goto OFFSET

                long entryOff = input.Position;       // savepos ENTRY_OFF
                uint headerSize = ReadU32LE(br);
                uint entriesCount = ReadU32LE(br);

                for (uint x = 0; x < entriesCount; x++)
                {
                    string entryType = ReadFixedString(br, 4);
                    uint entrySize = ReadU32LE(br);

                    // if ENTRY_TYPE u== "TADB"
                    if (string.Equals(entryType, "TADB", StringComparison.OrdinalIgnoreCase))
                    {
                        long baseOff = entryOff + entrySize; // BASE_OFF = ENTRY_OFF + ENTRY_SIZE
                        long num = ((long)entrySize - headerSize) / 8;

                        for (long z = 0; z < num; z++)
                        {
                            uint subOff = ReadU32LE(br);
                            uint subSize = ReadU32LE(br);

                            long absOff = baseOff + subOff;
                            bool compressed = (subSize & 0x80000000u) != 0;
                            int storedSize = (int)(subSize & 0x7fffffffu);

                            entries.Add(new DeadlySilenceEntry
                            {
                                Name = $"{i}/{x}/{z}.",
                                DataOffset = absOff,
                                StoredSize = storedSize,
                                IsCompressed = compressed,
                                DecompressedSizeHint = null
                            });
                        }
                    }
                }

                input.Position = tmpOff; // goto TMP_OFF
            }

            return entries;
        }

        /// <summary>
        /// Extract a single entry. If compressed, applies NdsLz77Wii decompression.
        /// </summary>
        public static byte[] ExtractEntry(Stream input, DeadlySilenceEntry entry)
        {
            if (!input.CanSeek)
                throw new ArgumentException("Input stream must be seekable.", nameof(input));

            input.Position = entry.DataOffset;
            byte[] data = ReadExactly(input, entry.StoredSize);

            if (!entry.IsCompressed)
                return data;

            // Equivalent of QuickBMS "clog" with comtype lz77wii.
            return NdsLz77Wii.Decompress(data);
        }

        /// <summary>
        /// Convenience: extract all entries using a callback that provides the destination stream.
        /// </summary>
        public static void ExtractAll(
            Stream input,
            IEnumerable<DeadlySilenceEntry> entries,
            Func<DeadlySilenceEntry, Stream> openOutputStream,
            bool closeOutputStreams = true)
        {
            foreach (var e in entries)
            {
                var bytes = ExtractEntry(input, e);
                Stream outStream = openOutputStream(e);
                try
                {
                    outStream.Write(bytes, 0, bytes.Length);
                }
                finally
                {
                    if (closeOutputStreams)
                        outStream.Dispose();
                }
            }
        }

        // -------- helpers --------

        private static uint ReadU32LE(BinaryReader br)
        {
            Span<byte> b = stackalloc byte[4];
            int read = br.Read(b);
            if (read != 4) throw new EndOfStreamException();
            return BinaryPrimitives.ReadUInt32LittleEndian(b);
        }

        private static string ReadFixedString(BinaryReader br, int len)
        {
            byte[] b = br.ReadBytes(len);
            if (b.Length != len) throw new EndOfStreamException();
            return Encoding.ASCII.GetString(b);
        }

        private static byte[] ReadExactly(Stream s, int len)
        {
            byte[] buf = new byte[len];
            int total = 0;
            while (total < len)
            {
                int r = s.Read(buf, total, len - total);
                if (r <= 0) throw new EndOfStreamException();
                total += r;
            }
            return buf;
        }
    }
}

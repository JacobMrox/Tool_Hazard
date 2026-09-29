using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Tool_Hazard.Biohazard.RE4
{
    /// <summary>
    /// Resident Evil 4 UHD LFS compressor/decompressor.
    ///
    /// The format and XMem calls are ported from RE4LFS by emoose,
    /// together with the LFS Binary Template and QuickBMS script.
    ///
    /// Requires xcompress64.dll to be available beside the application
    /// or somewhere in the normal DLL search path.
    /// </summary>
    public static class LFS
    {
        public const int ChunkSize = 0x10000;

        private const uint MagicLittleEndian = 0x584C4452; // "XLDR"
        private const uint MagicBigEndian = 0x52444C58;    // byte-swapped "XLDR"
        // XMem codec type used by the original RE4LFS implementation.
        private const int XMemCodecType = 1;

        private static class Native
        {
            [DllImport("xcompress64.dll",
                CallingConvention = CallingConvention.Winapi,
                EntryPoint = "XMemCreateCompressionContext")]
            internal static extern int XMemCreateCompressionContext(
                int codecType,
                IntPtr codecParams,
                uint flags,
                out IntPtr context);

            [DllImport("xcompress64.dll",
                CallingConvention = CallingConvention.Winapi,
                EntryPoint = "XMemDestroyCompressionContext")]
            internal static extern void XMemDestroyCompressionContext(IntPtr context);

            [DllImport("xcompress64.dll",
                CallingConvention = CallingConvention.Winapi,
                EntryPoint = "XMemCompress")]
            internal static extern int XMemCompress(
                IntPtr context,
                IntPtr destination,
                ref UIntPtr destinationSize,
                IntPtr source,
                UIntPtr sourceSize);

            [DllImport("xcompress64.dll",
                CallingConvention = CallingConvention.Winapi,
                EntryPoint = "XMemCreateDecompressionContext")]
            internal static extern int XMemCreateDecompressionContext(
                int codecType,
                IntPtr codecParams,
                uint flags,
                out IntPtr context);

            [DllImport("xcompress64.dll",
                CallingConvention = CallingConvention.Winapi,
                EntryPoint = "XMemDestroyDecompressionContext")]
            internal static extern void XMemDestroyDecompressionContext(IntPtr context);

            [DllImport("xcompress64.dll",
                CallingConvention = CallingConvention.Winapi,
                EntryPoint = "XMemDecompress")]
            internal static extern int XMemDecompress(
                IntPtr context,
                IntPtr destination,
                ref UIntPtr destinationSize,
                IntPtr source,
                UIntPtr sourceSize);
        }

        private const uint Magic2Value = 0xFEEEBAAAu;

        private struct Header
        {
            public uint Magic1;
            public uint Magic2;
            public uint SizeDecompressed;
            public uint SizeCompressed;
            public uint NumChunks;
        }

        private struct Chunk
        {
            public ushort SizeCompressed;
            public ushort SizeDecompressed;
            public uint Offset;
        }

        /// <summary>
        /// Decompresses an RE4 UHD .lfs file.
        /// </summary>
        public static void Decompress(string inputPath, string outputPath)
        {
            if (inputPath == null) throw new ArgumentNullException(nameof(inputPath));
            if (outputPath == null) throw new ArgumentNullException(nameof(outputPath));

            byte[] data = File.ReadAllBytes(inputPath);
            byte[] result = Decompress(data);

            File.WriteAllBytes(outputPath, result);
        }

        /// <summary>
        /// Decompresses an RE4 UHD LFS byte array.
        /// </summary>
        public static byte[] Decompress(byte[] lfsData)
        {
            if (lfsData == null) throw new ArgumentNullException(nameof(lfsData));
            if (lfsData.Length < 20)
                throw new InvalidDataException("The file is too small to contain an LFS header.");

            bool bigEndian = ReadUInt32LE(lfsData, 0) == MagicBigEndian;

            Header header = ReadHeader(lfsData, bigEndian);

            if (header.Magic1 != MagicLittleEndian)
                throw new InvalidDataException(
                    $"Invalid LFS magic 0x{header.Magic1:X8}. Expected 0x{MagicLittleEndian:X8}.");

            if (header.NumChunks == 0)
                return Array.Empty<byte>();

            long tableEnd = 20L + (long)header.NumChunks * 8L;
            if (tableEnd > lfsData.Length)
                throw new InvalidDataException("The LFS chunk table extends beyond the end of the file.");

            using (XMemDecompressionContext context = new XMemDecompressionContext())
            {
                int expectedTotal = checked((int)header.SizeDecompressed);
                using (MemoryStream output = new MemoryStream(expectedTotal))
                {
                    for (uint i = 0; i < header.NumChunks; i++)
                    {
                        Chunk chunk = ReadChunk(
                            lfsData,
                            checked(20 + (int)i * 8),
                            bigEndian);

                        bool compressed = (chunk.Offset & 1u) != 0;

                        // Offset is relative to the byte immediately after the
                        // 20-byte LFS header. The low bit is the compression flag.
                        long actualOffset = 20L + (chunk.Offset & ~1u);

                        int compressedSize = chunk.SizeCompressed == 0
                            ? ChunkSize
                            : chunk.SizeCompressed;

                        int decompressedSize = chunk.SizeDecompressed == 0
                            ? ChunkSize
                            : chunk.SizeDecompressed;

                        if (actualOffset < 0 ||
                            actualOffset > lfsData.Length ||
                            compressedSize < 0 ||
                            actualOffset + compressedSize > lfsData.Length)
                        {
                            throw new InvalidDataException(
                                $"LFS chunk {i} points outside the file.");
                        }

                        if (!compressed)
                        {
                            // Uncompressed chunks are copied directly.
                            output.Write(lfsData, checked((int)actualOffset), compressedSize);
                            continue;
                        }

                        byte[] compressedBuffer = new byte[compressedSize];
                        Buffer.BlockCopy(
                            lfsData,
                            checked((int)actualOffset),
                            compressedBuffer,
                            0,
                            compressedSize);

                        byte[] decompressed = context.Decompress(
                            compressedBuffer,
                            decompressedSize);

                        if (decompressed.Length != decompressedSize)
                        {
                            throw new InvalidDataException(
                                $"LFS chunk {i} decompressed to 0x{decompressed.Length:X} bytes; " +
                                $"expected 0x{decompressedSize:X}.");
                        }

                        output.Write(decompressed, 0, decompressed.Length);
                    }

                    byte[] finalData = output.ToArray();

                    if (finalData.Length != header.SizeDecompressed)
                    {
                        throw new InvalidDataException(
                            $"LFS decompression produced 0x{finalData.Length:X} bytes; " +
                            $"header specifies 0x{header.SizeDecompressed:X}.");
                    }

                    return finalData;
                }
            }
        }

        /// <summary>
        /// Compresses a normal file into an RE4 UHD PC LFS archive.
        /// </summary>
        public static void Compress(string inputPath, string outputPath, bool bigEndian = false)
        {
            if (inputPath == null) throw new ArgumentNullException(nameof(inputPath));
            if (outputPath == null) throw new ArgumentNullException(nameof(outputPath));

            byte[] data = File.ReadAllBytes(inputPath);
            byte[] result = Compress(data, bigEndian);

            File.WriteAllBytes(outputPath, result);
        }

        /// <summary>
        /// Compresses data into an RE4 UHD LFS archive.
        /// Set bigEndian=true to produce the byte-swapped/X360-style header/chunk
        /// representation implemented by the original RE4LFS tool.
        /// </summary>
        public static byte[] Compress(byte[] input, bool bigEndian = false)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            // The original tool uses uint32 fields for file size/chunk count.
            if ((ulong)input.Length > uint.MaxValue)
                throw new ArgumentException("LFS files cannot exceed 0xFFFFFFFF bytes.", nameof(input));

            uint chunkCount = (uint)((input.Length + ChunkSize - 1L) / ChunkSize);

            // A zero-length source has no chunks in the original calculation.
            if (input.Length == 0)
                chunkCount = 0;

            int headerSize = checked(20 + (int)chunkCount * 8);

            Header header = new Header
            {
                Magic1 = MagicLittleEndian,
                Magic2 = Magic2Value,
                SizeDecompressed = (uint)input.Length,
                SizeCompressed = 0,
                NumChunks = chunkCount
            };

            Chunk[] chunks = new Chunk[chunkCount];
            byte[][] compressedChunks = new byte[chunkCount][];

            using (XMemCompressionContext context = new XMemCompressionContext())
            {
                long dataOffset = headerSize;
                long remaining = input.Length;

                for (uint i = 0; i < chunkCount; i++)
                {
                    int chunkSize = (int)Math.Min(ChunkSize, remaining);
                    int sourceOffset = checked((int)(input.Length - remaining));

                    byte[] source = new byte[chunkSize];
                    Buffer.BlockCopy(input, sourceOffset, source, 0, chunkSize);

                    byte[] compressed = context.Compress(source);
                    compressedChunks[i] = compressed;

                    if (compressed.Length > ChunkSize)
                    {
                        throw new InvalidDataException(
                            $"XMem compressed chunk {i} is larger than 0x10000 bytes.");
                    }

                    // SizeCompressed is a ushort, except that 0 means 0x10000.
                    if (chunkSize != ChunkSize && compressed.Length > ushort.MaxValue)
                    {
                        throw new InvalidDataException(
                            $"XMem compressed final/partial chunk {i} is too large " +
                            $"for the LFS SizeCompressed field.");
                    }

                    // Match RE4LFS alignment:
                    long alignedOffset = AlignLfsDataOffset(dataOffset);

                    ushort compressedSize = compressed.Length == ChunkSize
                        ? (ushort)0
                        : checked((ushort)compressed.Length);

                    ushort decompressedSize = chunkSize == ChunkSize
                        ? (ushort)0
                        : checked((ushort)chunkSize);

                    uint relativeOffset = checked((uint)(alignedOffset - 20));
                    relativeOffset |= 1u;

                    chunks[i] = new Chunk
                    {
                        SizeCompressed = compressedSize,
                        SizeDecompressed = decompressedSize,
                        Offset = relativeOffset
                    };

                    header.SizeCompressed = checked(
                        header.SizeCompressed + (uint)compressed.Length);

                    dataOffset = checked(alignedOffset + compressed.Length);
                    remaining -= chunkSize;

                    if (bigEndian)
                    {
                        long nextAlignedOffset = AlignLfsDataOffset(dataOffset);
                        header.SizeCompressed = checked(
                            header.SizeCompressed +
                            (uint)(nextAlignedOffset - dataOffset));
                    }
                }

                // The original little-endian PC writer naturally ends at the end
                // of the final chunk. Big-endian/X360 output is padded to alignment.
                long finalFileSize = bigEndian
                    ? AlignLfsDataOffset(dataOffset)
                    : dataOffset;

                if (finalFileSize > int.MaxValue)
                    throw new InvalidDataException("Generated LFS file is too large.");

                byte[] output = new byte[(int)finalFileSize];

                for (uint i = 0; i < chunkCount; i++)
                {
                    long actualOffset = 20L + (chunks[i].Offset & ~1u);

                    Buffer.BlockCopy(
                        compressedChunks[i],
                        0,
                        output,
                        checked((int)actualOffset),
                        compressedChunks[i].Length);
                }

                WriteHeader(output, 0, header, bigEndian);

                for (int i = 0; i < chunks.Length; i++)
                {
                    WriteChunk(
                        output,
                        20 + i * 8,
                        chunks[i],
                        bigEndian);
                }

                return output;
            }
        }

        /// <summary>
        /// Returns true if the supplied data starts with an LFS signature.
        /// </summary>
        public static bool IsLfs(byte[] data)
        {
            if (data == null || data.Length < 4)
                return false;

            uint magic = ReadUInt32LE(data, 0);
            return magic == MagicLittleEndian || magic == MagicBigEndian;
        }

        private static long AlignLfsDataOffset(long offset)
        {
            // Same expression as the original C++:
            // ((((data_offset - 4) + 0xF) / 0x10) * 0x10) + 4
            return (((offset - 4 + 0xF) / 0x10) * 0x10) + 4;
        }

        private static Header ReadHeader(byte[] data, bool bigEndian)
        {
            return new Header
            {
                Magic1 = ReadUInt32(data, 0, bigEndian),
                Magic2 = ReadUInt32(data, 4, bigEndian),
                SizeDecompressed = ReadUInt32(data, 8, bigEndian),
                SizeCompressed = ReadUInt32(data, 12, bigEndian),
                NumChunks = ReadUInt32(data, 16, bigEndian)
            };
        }

        private static Chunk ReadChunk(byte[] data, int offset, bool bigEndian)
        {
            return new Chunk
            {
                SizeCompressed = ReadUInt16(data, offset, bigEndian),
                SizeDecompressed = ReadUInt16(data, offset + 2, bigEndian),
                Offset = ReadUInt32(data, offset + 4, bigEndian)
            };
        }

        private static void WriteHeader(
            byte[] data,
            int offset,
            Header header,
            bool bigEndian)
        {
            WriteUInt32(data, offset, header.Magic1, bigEndian);
            WriteUInt32(data, offset + 4, header.Magic2, bigEndian);
            WriteUInt32(data, offset + 8, header.SizeDecompressed, bigEndian);
            WriteUInt32(data, offset + 12, header.SizeCompressed, bigEndian);
            WriteUInt32(data, offset + 16, header.NumChunks, bigEndian);
        }

        private static void WriteChunk(
            byte[] data,
            int offset,
            Chunk chunk,
            bool bigEndian)
        {
            WriteUInt16(data, offset, chunk.SizeCompressed, bigEndian);
            WriteUInt16(data, offset + 2, chunk.SizeDecompressed, bigEndian);
            WriteUInt32(data, offset + 4, chunk.Offset, bigEndian);
        }

        private static uint ReadUInt32LE(byte[] data, int offset)
        {
            return (uint)(
                data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24));
        }

        private static ushort ReadUInt16(
            byte[] data,
            int offset,
            bool bigEndian)
        {
            if (bigEndian)
            {
                return (ushort)(
                    (data[offset] << 8) |
                    data[offset + 1]);
            }

            return (ushort)(
                data[offset] |
                (data[offset + 1] << 8));
        }

        private static uint ReadUInt32(
            byte[] data,
            int offset,
            bool bigEndian)
        {
            if (bigEndian)
            {
                return
                    ((uint)data[offset] << 24) |
                    ((uint)data[offset + 1] << 16) |
                    ((uint)data[offset + 2] << 8) |
                    data[offset + 3];
            }

            return ReadUInt32LE(data, offset);
        }

        private static void WriteUInt16(
            byte[] data,
            int offset,
            ushort value,
            bool bigEndian)
        {
            if (bigEndian)
            {
                data[offset] = (byte)(value >> 8);
                data[offset + 1] = (byte)value;
            }
            else
            {
                data[offset] = (byte)value;
                data[offset + 1] = (byte)(value >> 8);
            }
        }

        private static void WriteUInt32(
            byte[] data,
            int offset,
            uint value,
            bool bigEndian)
        {
            if (bigEndian)
            {
                data[offset] = (byte)(value >> 24);
                data[offset + 1] = (byte)(value >> 16);
                data[offset + 2] = (byte)(value >> 8);
                data[offset + 3] = (byte)value;
            }
            else
            {
                data[offset] = (byte)value;
                data[offset + 1] = (byte)(value >> 8);
                data[offset + 2] = (byte)(value >> 16);
                data[offset + 3] = (byte)(value >> 24);
            }
        }

        private sealed class XMemCompressionContext : IDisposable
        {
            private IntPtr _context;
            private bool _disposed;

            public XMemCompressionContext()
            {
                int result = Native.XMemCreateCompressionContext(
                    XMemCodecType,
                    IntPtr.Zero,
                    0,
                    out _context);

                CheckXMemResult(result, "XMemCreateCompressionContext");
            }

            public byte[] Compress(byte[] source)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(XMemCompressionContext));

                if (source == null)
                    throw new ArgumentNullException(nameof(source));

                byte[] destination = new byte[ChunkSize];

                GCHandle sourceHandle = GCHandle.Alloc(
                    source,
                    GCHandleType.Pinned);

                GCHandle destinationHandle = GCHandle.Alloc(
                    destination,
                    GCHandleType.Pinned);

                try
                {
                    UIntPtr destinationSize = (UIntPtr)destination.Length;

                    int result = Native.XMemCompress(
                        _context,
                        destinationHandle.AddrOfPinnedObject(),
                        ref destinationSize,
                        sourceHandle.AddrOfPinnedObject(),
                        (UIntPtr)source.Length);

                    CheckXMemResult(result, "XMemCompress");

                    ulong size = destinationSize.ToUInt64();

                    if (size > (ulong)destination.Length)
                        throw new InvalidDataException(
                            "XMemCompress returned an invalid destination size.");

                    byte[] compressed = new byte[(int)size];
                    Buffer.BlockCopy(destination, 0, compressed, 0, compressed.Length);
                    return compressed;
                }
                finally
                {
                    destinationHandle.Free();
                    sourceHandle.Free();
                }
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                if (_context != IntPtr.Zero)
                {
                    Native.XMemDestroyCompressionContext(_context);
                    _context = IntPtr.Zero;
                }

                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        private sealed class XMemDecompressionContext : IDisposable
        {
            private IntPtr _context;
            private bool _disposed;

            public XMemDecompressionContext()
            {
                int result = Native.XMemCreateDecompressionContext(
                    XMemCodecType,
                    IntPtr.Zero,
                    0,
                    out _context);

                CheckXMemResult(result, "XMemCreateDecompressionContext");
            }

            public byte[] Decompress(
                byte[] source,
                int expectedSize)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(XMemDecompressionContext));

                if (source == null)
                    throw new ArgumentNullException(nameof(source));

                if (expectedSize < 0 || expectedSize > ChunkSize)
                    throw new InvalidDataException(
                        $"Invalid LFS decompressed chunk size 0x{expectedSize:X}.");

                byte[] destination = new byte[expectedSize];

                GCHandle sourceHandle = GCHandle.Alloc(
                    source,
                    GCHandleType.Pinned);

                GCHandle destinationHandle = GCHandle.Alloc(
                    destination,
                    GCHandleType.Pinned);

                try
                {
                    UIntPtr destinationSize = (UIntPtr)destination.Length;

                    int result = Native.XMemDecompress(
                        _context,
                        destinationHandle.AddrOfPinnedObject(),
                        ref destinationSize,
                        sourceHandle.AddrOfPinnedObject(),
                        (UIntPtr)source.Length);

                    CheckXMemResult(result, "XMemDecompress");

                    ulong size = destinationSize.ToUInt64();

                    if (size > (ulong)destination.Length)
                        throw new InvalidDataException(
                            "XMemDecompress returned an invalid destination size.");

                    if (size != (ulong)expectedSize)
                        throw new InvalidDataException(
                            $"XMemDecompress produced 0x{size:X} bytes; " +
                            $"expected 0x{expectedSize:X}.");

                    return destination;
                }
                finally
                {
                    destinationHandle.Free();
                    sourceHandle.Free();
                }
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                if (_context != IntPtr.Zero)
                {
                    Native.XMemDestroyDecompressionContext(_context);
                    _context = IntPtr.Zero;
                }

                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        private static void CheckXMemResult(int result, string function)
        {
            // XMem returns a status code. The original C++ checks for failure
            // after calling the function; using HRESULT-style failure detection
            // here avoids treating a normal non-negative result as an error.
            if (result < 0)
                throw new InvalidOperationException(
                    $"{function} failed with XMem error 0x{unchecked((uint)result):X8}.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tool_Hazard.Biohazard.RE4
{
    public class UdasSection
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    public static class UdasHandler
    {
        public static readonly string[] SectionNames = new string[]
        {
            "TPL", "EFF", "TEX", "VIB", "BIN", "MDT", "LIT", "CAM", "SAT", "UWF"
        };

        /// <summary>
        /// Reads a .udas file, automatically detecting Big-Endian vs Little-Endian header offsets.
        /// </summary>
        public static List<UdasSection> Read(string udasFilePath, out bool isBigEndian)
        {
            var sections = new List<UdasSection>();
            byte[] rawData = File.ReadAllBytes(udasFilePath);
            long fileSize = rawData.Length;

            if (fileSize < 40)
                throw new InvalidDataException("File is too small to be a valid UDAS archive.");

            // Read the 10 standard 32-bit header offsets
            uint[] rawOffsets = new uint[10];
            for (int i = 0; i < 10; i++)
            {
                rawOffsets[i] = BitConverter.ToUInt32(rawData, i * 4);
            }

            // Auto-detect Endianness: If first offset exceeds fileSize, swap bytes
            isBigEndian = false;
            uint firstOffset = rawOffsets.FirstOrDefault(off => off != 0);

            if (firstOffset > fileSize)
            {
                uint swapped = SwapEndianness(firstOffset);
                if (swapped <= fileSize)
                {
                    isBigEndian = true;
                }
            }

            // Convert offsets to native endianness
            uint[] offsets = new uint[10];
            for (int i = 0; i < 10; i++)
            {
                offsets[i] = isBigEndian ? SwapEndianness(rawOffsets[i]) : rawOffsets[i];
            }

            // Gather all unique, valid boundaries strictly <= fileSize
            var sortedBoundaries = offsets
                .Where(off => off > 0 && off <= fileSize)
                .Concat(new uint[] { (uint)fileSize })
                .Distinct()
                .OrderBy(off => off)
                .ToList();

            for (int i = 0; i < 10; i++)
            {
                uint start = offsets[i];

                // Empty or invalid offset pointer
                if (start == 0 || start >= fileSize)
                {
                    sections.Add(new UdasSection
                    {
                        Index = i,
                        Name = SectionNames[i],
                        Data = Array.Empty<byte>()
                    });
                    continue;
                }

                // Locate the next distinct boundary strictly GREATER than start
                uint end = sortedBoundaries.FirstOrDefault(b => b > start);
                if (end == 0) end = (uint)fileSize;

                int size = (int)(end - start);
                if (size <= 0 || start + size > fileSize)
                {
                    sections.Add(new UdasSection
                    {
                        Index = i,
                        Name = SectionNames[i],
                        Data = Array.Empty<byte>()
                    });
                    continue;
                }

                byte[] buffer = new byte[size];
                Buffer.BlockCopy(rawData, (int)start, buffer, 0, size);

                sections.Add(new UdasSection
                {
                    Index = i,
                    Name = SectionNames[i],
                    Data = buffer
                });
            }

            return sections;
        }

        public static List<UdasSection> Read(string udasFilePath)
        {
            return Read(udasFilePath, out _);
        }

        /// <summary>
        /// Repacks section data into a valid .udas file with 16-byte alignment and target endianness.
        /// </summary>
        public static void Save(List<UdasSection> sections, string outputPath, bool isBigEndian = false, int alignment = 16)
        {
            using (var stream = File.Create(outputPath))
            using (var writer = new BinaryWriter(stream))
            {
                uint[] headerOffsets = new uint[10];
                byte[][] sectionBuffers = new byte[10][];

                foreach (var sec in sections)
                {
                    if (sec.Index >= 0 && sec.Index < 10)
                    {
                        sectionBuffers[sec.Index] = sec.Data;
                    }
                }

                // Reserve 40 bytes for header table
                writer.Write(new byte[40]);

                for (int i = 0; i < 10; i++)
                {
                    if (sectionBuffers[i] == null || sectionBuffers[i].Length == 0)
                    {
                        headerOffsets[i] = 0;
                        continue;
                    }

                    // Apply byte alignment padding
                    long currentPos = stream.Position;
                    long remainder = currentPos % alignment;
                    if (remainder != 0)
                    {
                        int padding = (int)(alignment - remainder);
                        writer.Write(new byte[padding]);
                    }

                    headerOffsets[i] = (uint)stream.Position;
                    writer.Write(sectionBuffers[i]);
                }

                // Overwrite header table with recalculated offsets
                stream.Position = 0;
                for (int i = 0; i < 10; i++)
                {
                    uint val = isBigEndian ? SwapEndianness(headerOffsets[i]) : headerOffsets[i];
                    writer.Write(val);
                }
            }
        }

        private static uint SwapEndianness(uint value)
        {
            return ((value & 0x000000FF) << 24) |
                   ((value & 0x0000FF00) << 8) |
                   ((value & 0x00FF0000) >> 8) |
                   ((value & 0xFF000000) >> 24);
        }
    }
}
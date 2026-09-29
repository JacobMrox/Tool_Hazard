using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tool_Hazard.Biohazard.RE4
{
    public enum ArchiveEndianness
    {
        Auto,
        BigEndian,
        LittleEndian
    }

    public class HeaderInfo
    {
        public bool IsBigEndian { get; set; }
        public bool HasCountHeader { get; set; } // true = .dat style (count at 0x00), false = .udas style (direct offsets at 0x00)
        public List<uint> Offsets { get; set; } = new List<uint>();
    }

    /// <summary>
    /// Handles RE4 archive containers (.dat, .udas, .map, .das, .drs, .decmp)
    /// across GameCube, Wii, PS2, PS3, Xbox 360, PC 2007, and PC 2014 UHD releases.
    /// </summary>
    public static class Re4BigEndianArchive
    {
        private const string INDEX_FILE_NAME = "repack_index.txt";

        /// <summary>
        /// Extracts RE4 archives with automatic container type & endianness detection.
        /// </summary>
        public static int Extract(
            string archivePath,
            string outputDir,
            Action<string, int>? progressCallback = null,
            ArchiveEndianness preferredEndianness = ArchiveEndianness.Auto)
        {
            if (!File.Exists(archivePath))
                throw new FileNotFoundException("Archive file was not found.", archivePath);

            Directory.CreateDirectory(outputDir);

            using (FileStream fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                if (fs.Length < 8)
                    throw new InvalidDataException("Archive file is too small to contain a valid RE4 header.");

                HeaderInfo header = ParseHeader(fs, br, archivePath, preferredEndianness);
                uint fileCount = (uint)header.Offsets.Count;

                if (fileCount == 0)
                {
                    throw new InvalidDataException("No valid sub-files could be indexed from this archive.");
                }

                // Check for trailing end-of-file offset marker
                uint endOffsetCandidate = (uint)fs.Length;

                // Detect header alignment based on first file offset
                uint firstOffset = header.Offsets[0];
                uint minHeaderSize = header.HasCountHeader ? (4 + (fileCount * 4)) : (fileCount * 4);
                uint alignment = 16;
                if (firstOffset % 32 == 0 && firstOffset >= minHeaderSize) alignment = 32;
                if (firstOffset % 2048 == 0 && firstOffset >= minHeaderSize) alignment = 2048;

                List<string> indexLines = new List<string>
                {
                    $"# RE4 Archive Manifest Index",
                    $"FileCount={fileCount}",
                    $"Alignment={alignment}",
                    $"Endianness={(header.IsBigEndian ? "BigEndian" : "LittleEndian")}",
                    $"HasCountHeader={header.HasCountHeader}"
                };

                for (int i = 0; i < fileCount; i++)
                {
                    uint startPos = header.Offsets[i];
                    uint endPos = (i < fileCount - 1) ? header.Offsets[i + 1] : endOffsetCandidate;

                    if (endPos < startPos || endPos > fs.Length)
                    {
                        endPos = (uint)fs.Length;
                    }

                    int length = (int)(endPos - startPos);
                    fs.Position = startPos;
                    byte[] fileBuffer = br.ReadBytes(length);

                    string ext = DetectFileType(fileBuffer);
                    string fileName = $"{i:D4}{ext}";
                    string fullOutputPath = Path.Combine(outputDir, fileName);

                    File.WriteAllBytes(fullOutputPath, fileBuffer);
                    indexLines.Add(fileName);

                    int percentage = (int)(((float)(i + 1) / fileCount) * 100);
                    progressCallback?.Invoke($"Extracted: {fileName} ({i + 1}/{fileCount})", percentage);
                }

                File.WriteAllLines(Path.Combine(outputDir, INDEX_FILE_NAME), indexLines);
                return (int)fileCount;
            }
        }

        /// <summary>
        /// Repacks directory contents back into an RE4 archive structure.
        /// </summary>
        public static int Repack(
            string inputFolder,
            string outputArchivePath,
            Action<string, int>? progressCallback = null,
            ArchiveEndianness overrideEndianness = ArchiveEndianness.Auto)
        {
            if (!Directory.Exists(inputFolder))
                throw new DirectoryNotFoundException("Input folder not found.");

            string indexPath = Path.Combine(inputFolder, INDEX_FILE_NAME);
            List<string> filesToPack = new List<string>();
            uint alignment = 16;
            bool isBigEndian = true;
            bool hasCountHeader = true;

            if (overrideEndianness == ArchiveEndianness.BigEndian) isBigEndian = true;
            else if (overrideEndianness == ArchiveEndianness.LittleEndian) isBigEndian = false;

            if (File.Exists(indexPath))
            {
                foreach (string rawLine in File.ReadAllLines(indexPath))
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                    if (line.StartsWith("Alignment=", StringComparison.OrdinalIgnoreCase))
                    {
                        uint.TryParse(line.Split('=')[1], out alignment);
                    }
                    else if (line.StartsWith("Endianness=", StringComparison.OrdinalIgnoreCase) && overrideEndianness == ArchiveEndianness.Auto)
                    {
                        string endianStr = line.Split('=')[1].Trim();
                        isBigEndian = !endianStr.Equals("LittleEndian", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (line.StartsWith("HasCountHeader=", StringComparison.OrdinalIgnoreCase))
                    {
                        bool.TryParse(line.Split('=')[1], out hasCountHeader);
                    }
                    else if (!line.Contains("="))
                    {
                        string file = Path.Combine(inputFolder, line);
                        if (File.Exists(file)) filesToPack.Add(file);
                    }
                }
            }

            // Fallback: search folder if no valid index manifest is present
            if (filesToPack.Count == 0)
            {
                filesToPack = Directory.GetFiles(inputFolder)
                                       .Where(f => !f.EndsWith(INDEX_FILE_NAME, StringComparison.OrdinalIgnoreCase))
                                       .OrderBy(f => f)
                                       .ToList();
            }

            if (filesToPack.Count == 0)
                throw new InvalidOperationException("No files available to repack.");

            uint fileCount = (uint)filesToPack.Count;
            List<uint> writtenOffsets = new List<uint>();

            using (FileStream fs = new FileStream(outputArchivePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                // Write dummy header reserved space
                if (hasCountHeader)
                {
                    WriteUInt32(bw, fileCount, isBigEndian);
                }

                for (int i = 0; i < fileCount; i++)
                {
                    WriteUInt32(bw, 0, isBigEndian);
                }

                PadStream(bw, alignment);

                for (int i = 0; i < filesToPack.Count; i++)
                {
                    string filePath = filesToPack[i];
                    byte[] data = File.ReadAllBytes(filePath);

                    writtenOffsets.Add((uint)fs.Position);
                    bw.Write(data);

                    if (i < filesToPack.Count - 1)
                    {
                        PadStream(bw, alignment);
                    }

                    int percentage = (int)(((float)(i + 1) / fileCount) * 100);
                    progressCallback?.Invoke($"Repacked: {Path.GetFileName(filePath)} ({i + 1}/{fileCount})", percentage);
                }

                // Go back and write real header offsets
                fs.Position = 0;
                if (hasCountHeader)
                {
                    WriteUInt32(bw, fileCount, isBigEndian);
                }

                foreach (uint off in writtenOffsets)
                {
                    WriteUInt32(bw, off, isBigEndian);
                }
            }

            return (int)fileCount;
        }

        #region Header Analysis & Endianness Parsing

        private static HeaderInfo ParseHeader(FileStream fs, BinaryReader br, string archivePath, ArchiveEndianness explicitPreference)
        {
            fs.Position = 0;
            uint val0 = br.ReadUInt32();
            uint val1 = br.ReadUInt32();

            uint val0_LE = val0;
            uint val0_BE = ReverseBytes(val0);
            uint val1_LE = val1;
            uint val1_BE = ReverseBytes(val1);

            List<HeaderInfo> candidates = new List<HeaderInfo>();

            // 1. Test Format A: Has Count Header (Big Endian)
            if (val0_BE > 0 && val0_BE <= 0xFFFF && val1_BE >= (4 + val0_BE * 4) && val1_BE <= fs.Length)
            {
                candidates.Add(ReadCountHeader(fs, br, val0_BE, isBigEndian: true));
            }

            // 2. Test Format A: Has Count Header (Little Endian)
            if (val0_LE > 0 && val0_LE <= 0xFFFF && val1_LE >= (4 + val0_LE * 4) && val1_LE <= fs.Length)
            {
                candidates.Add(ReadCountHeader(fs, br, val0_LE, isBigEndian: false));
            }

            // 3. Test Format B: Direct Offset Table (.udas style, Big Endian)
            if (val0_BE >= 8 && val0_BE % 4 == 0 && val0_BE < fs.Length && val0_BE <= 4096)
            {
                HeaderInfo? info = TryReadDirectOffsetHeader(fs, br, val0_BE, isBigEndian: true);
                if (info != null) candidates.Add(info);
            }

            // 4. Test Format B: Direct Offset Table (.udas style, Little Endian)
            if (val0_LE >= 8 && val0_LE % 4 == 0 && val0_LE < fs.Length && val0_LE <= 4096)
            {
                HeaderInfo? info = TryReadDirectOffsetHeader(fs, br, val0_LE, isBigEndian: false);
                if (info != null) candidates.Add(info);
            }

            // Apply explicit preference if specified
            if (explicitPreference == ArchiveEndianness.BigEndian)
            {
                HeaderInfo? match = candidates.FirstOrDefault(c => c.IsBigEndian);
                if (match != null) return match;
            }
            else if (explicitPreference == ArchiveEndianness.LittleEndian)
            {
                HeaderInfo? match = candidates.FirstOrDefault(c => !c.IsBigEndian);
                if (match != null) return match;
            }

            if (candidates.Count > 0)
            {
                // Prefer exact match by file extension hint if ambiguous (.udas defaults to Format B)
                string ext = Path.GetExtension(archivePath).ToLowerInvariant();
                if (ext == ".udas")
                {
                    HeaderInfo? udasMatch = candidates.FirstOrDefault(c => !c.HasCountHeader);
                    if (udasMatch != null) return udasMatch;
                }

                return candidates[0];
            }

            throw new InvalidDataException("Unable to determine RE4 archive structure or endianness.");
        }

        private static HeaderInfo ReadCountHeader(FileStream fs, BinaryReader br, uint count, bool isBigEndian)
        {
            HeaderInfo info = new HeaderInfo
            {
                IsBigEndian = isBigEndian,
                HasCountHeader = true
            };

            fs.Position = 4;
            for (int i = 0; i < count; i++)
            {
                info.Offsets.Add(ReadUInt32(br, isBigEndian));
            }

            return info;
        }

        private static HeaderInfo? TryReadDirectOffsetHeader(FileStream fs, BinaryReader br, uint firstOffset, bool isBigEndian)
        {
            HeaderInfo info = new HeaderInfo
            {
                IsBigEndian = isBigEndian,
                HasCountHeader = false
            };

            fs.Position = 0;
            info.Offsets.Add(firstOffset);

            while (fs.Position < firstOffset && fs.Position + 4 <= fs.Length)
            {
                uint nextOffset = ReadUInt32(br, isBigEndian);
                if (nextOffset == 0 || nextOffset > fs.Length)
                {
                    break; // Alignment/padding boundary reached
                }

                // Sanity check offset monotonicity
                if (nextOffset < info.Offsets.Last())
                {
                    return null; // Invalid offset sequence
                }

                info.Offsets.Add(nextOffset);
            }

            return info.Offsets.Count > 0 ? info : null;
        }

        #endregion

        #region Helpers

        private static string DetectFileType(byte[] data)
        {
            if (data.Length >= 4)
            {
                // YZ2 Compression Magic ("YZ2\0" or "YZ2")
                if (data[0] == 0x59 && data[1] == 0x5A && data[2] == 0x32) return ".yz2";

                // TPL Texture Header Magic
                if ((data[0] == 0x00 && data[1] == 0x20 && data[2] == 0xAF && data[3] == 0x30) ||
                    (data[0] == 0x20 && data[1] == 0x00 && data[2] == 0x00 && data[3] == 0x00) ||
                    (data[0] == 0x00 && data[1] == 0x20 && data[2] == 0x00 && data[3] == 0x00)) return ".tpl";

                // DAS Audio Header Magic
                if (data[0] == (byte)'D' && data[1] == (byte)'A' && data[2] == (byte)'S') return ".das";

                // ESL Sound Layout
                if (data[0] == (byte)'E' && data[1] == (byte)'S' && data[2] == (byte)'L') return ".esl";

                // Sub-container data formats
                if (data[0] == (byte)'S' && data[1] == (byte)'A' && data[2] == (byte)'T') return ".sat";
                if (data[0] == (byte)'L' && data[1] == (byte)'I' && data[2] == (byte)'T') return ".lit";
                if (data[0] == (byte)'M' && data[1] == (byte)'D' && data[2] == (byte)'T') return ".mdt";
                if (data[0] == (byte)'C' && data[1] == (byte)'A' && data[2] == (byte)'M') return ".cam";
                if (data[0] == (byte)'E' && data[1] == (byte)'F' && data[2] == (byte)'F') return ".eff";
                if (data[0] == (byte)'U' && data[1] == (byte)'W' && data[2] == (byte)'F') return ".uwf";

                // DirectDraw Surface (DDS)
                if (data[0] == (byte)'D' && data[1] == (byte)'D' && data[2] == (byte)'S' && data[3] == 0x20) return ".dds";
            }
            return ".bin";
        }

        private static uint ReadUInt32(BinaryReader br, bool isBigEndian)
        {
            uint val = br.ReadUInt32();
            return isBigEndian ? ReverseBytes(val) : val;
        }

        private static void WriteUInt32(BinaryWriter bw, uint val, bool isBigEndian)
        {
            uint output = isBigEndian ? ReverseBytes(val) : val;
            bw.Write(output);
        }

        private static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FF) << 24) |
                   ((value & 0x0000FF00) << 8) |
                   ((value & 0x00FF0000) >> 8) |
                   ((value & 0xFF000000) >> 24);
        }

        private static void PadStream(BinaryWriter bw, uint alignment)
        {
            long currentPos = bw.BaseStream.Position;
            long remainder = currentPos % alignment;
            if (remainder != 0)
            {
                int paddingBytes = (int)(alignment - remainder);
                bw.Write(new byte[paddingBytes]);
            }
        }

        #endregion
    }
}
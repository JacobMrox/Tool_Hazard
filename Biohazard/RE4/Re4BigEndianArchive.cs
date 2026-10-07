using System;
using System.Collections.Generic;
using System.Globalization;
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
        public bool HasCountHeader { get; set; }
        public List<uint> Offsets { get; set; } = new List<uint>();
    }

    /// <summary>
    /// RE4 DAT/UDAS/MAP/DAS/DRS/DECMP archive reader/writer.
    ///
    /// This implementation follows the archive layout used by the original
    /// RE4_DASYZ2_TOOL source:
    ///
    /// UDAS:
    ///   0x00..0x1F = UDAS top/header
    ///   0x20       = first 0x20-byte record
    ///   0x40       = second 0x20-byte record
    ///
    /// Record:
    ///   +0x00 = type
    ///   +0x04 = data length
    ///   +0x08 = unused
    ///   +0x0C = data offset
    ///
    /// DAT:
    ///   +0x00 = file count
    ///   +0x04 = header/version fields
    ///   +tableOffset = offset table
    ///   +tableOffset + count*4 = 4-byte extension/name table
    ///
    /// Both big-endian and little-endian variants are supported.
    /// </summary>
    public static class Re4BigEndianArchive
    {
        private const string INDEX_FILE_NAME = "repack_index.txt";
        private const uint UDAS_RECORD_START = 0x20;
        private const uint UDAS_RECORD_SIZE = 0x20;
        private const uint DEFAULT_ALIGNMENT = 0x20;

        private sealed class UdasEntry
        {
            public uint Type;
            public uint Length;
            public uint Offset;
        }

        private sealed class DatEntry
        {
            public uint Offset;
            public string Extension = string.Empty;
        }

        /// <summary>
        /// Extracts a RE4 archive.
        ///
        /// .udas/.das are parsed as UDAS containers.
        /// .dat/.map/.decmp are parsed as DAT containers.
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

            string extension = Path.GetExtension(archivePath);

            if (extension.Equals(".udas", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".das", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractUdas(
                    archivePath,
                    outputDir,
                    progressCallback,
                    preferredEndianness);
            }

            return ExtractDatLike(
                archivePath,
                outputDir,
                progressCallback,
                preferredEndianness);
        }

        /// <summary>
        /// Repackages a directory created by this extractor.
        ///
        /// The archive type is taken from ArchiveType= in repack_index.txt.
        /// If no index exists, DAT is assumed.
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

            string archiveType = "DAT";
            bool isBigEndian = true;
            bool hasE3Header = false;
            uint soundFlag = 4;
            uint topLength = 0x400;
            string? topFile = null;
            string? middleFile = null;
            string? endFile = null;

            List<string> indexedFiles = new List<string>();

            if (File.Exists(indexPath))
            {
                foreach (string raw in File.ReadAllLines(indexPath))
                {
                    string line = raw.Trim();

                    if (line.Length == 0 || line.StartsWith("#"))
                        continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;

                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();

                    if (key.Equals("ArchiveType", StringComparison.OrdinalIgnoreCase))
                    {
                        archiveType = value;
                    }
                    else if (key.Equals("Endianness", StringComparison.OrdinalIgnoreCase))
                    {
                        isBigEndian = !value.Equals(
                            "LittleEndian",
                            StringComparison.OrdinalIgnoreCase);
                    }
                    else if (key.Equals("HasE3Header", StringComparison.OrdinalIgnoreCase))
                    {
                        bool.TryParse(value, out hasE3Header);
                    }
                    else if (key.Equals("SoundFlag", StringComparison.OrdinalIgnoreCase))
                    {
                        uint.TryParse(
                            value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out soundFlag);
                    }
                    else if (key.Equals("TopLength", StringComparison.OrdinalIgnoreCase))
                    {
                        TryParseUInt(value, out topLength);
                    }
                    else if (key.Equals("TopFile", StringComparison.OrdinalIgnoreCase))
                    {
                        topFile = value;
                    }
                    else if (key.Equals("MiddleFile", StringComparison.OrdinalIgnoreCase))
                    {
                        middleFile = value;
                    }
                    else if (key.Equals("EndFile", StringComparison.OrdinalIgnoreCase))
                    {
                        endFile = value;
                    }
                    else if (key.StartsWith("File", StringComparison.OrdinalIgnoreCase))
                    {
                        indexedFiles.Add(value);
                    }
                }
            }
            else
            {
                // Compatibility with the previous C# port's index format.
                List<string> oldFiles = new List<string>();

                if (File.Exists(indexPath))
                {
                    oldFiles = File.ReadAllLines(indexPath)
                        .Select(x => x.Trim())
                        .Where(x => x.Length > 0 && !x.StartsWith("#"))
                        .Where(x => !x.Contains("="))
                        .ToList();
                }

                indexedFiles.AddRange(oldFiles);
            }

            if (overrideEndianness == ArchiveEndianness.BigEndian)
                isBigEndian = true;
            else if (overrideEndianness == ArchiveEndianness.LittleEndian)
                isBigEndian = false;

            string outputExtension =
                archiveType.Equals("UDAS", StringComparison.OrdinalIgnoreCase)
                    ? ".udas"
                    : archiveType.Equals("DAS", StringComparison.OrdinalIgnoreCase)
                        ? ".das"
                        : archiveType.Equals("MAP", StringComparison.OrdinalIgnoreCase)
                            ? ".map"
                            : archiveType.Equals("DECMP", StringComparison.OrdinalIgnoreCase)
                                ? ".decmp"
                                : ".dat";

            if (string.IsNullOrWhiteSpace(Path.GetExtension(outputArchivePath)))
                outputArchivePath += outputExtension;

            if (archiveType.Equals("UDAS", StringComparison.OrdinalIgnoreCase) ||
                archiveType.Equals("DAS", StringComparison.OrdinalIgnoreCase))
            {
                return RepackUdas(
                    inputFolder,
                    outputArchivePath,
                    indexedFiles,
                    topFile,
                    middleFile,
                    endFile,
                    topLength,
                    soundFlag,
                    isBigEndian,
                    hasE3Header,
                    progressCallback);
            }

            return RepackDat(
                inputFolder,
                outputArchivePath,
                indexedFiles,
                isBigEndian,
                hasE3Header,
                progressCallback);
        }

        // =====================================================================
        // UDAS EXTRACTION
        // =====================================================================

        private static int ExtractUdas(
            string archivePath,
            string outputDir,
            Action<string, int>? progressCallback,
            ArchiveEndianness preferredEndianness)
        {
            byte[] data = File.ReadAllBytes(archivePath);

            if (data.Length < 0x60)
                throw new InvalidDataException(
                    "UDAS file is too small to contain a valid header.");

            bool isBigEndian =
                preferredEndianness == ArchiveEndianness.BigEndian
                    ? true
                    : preferredEndianness == ArchiveEndianness.LittleEndian
                        ? false
                        : DetectUdasEndianness(data);

            List<UdasEntry> entries = ReadUdasEntries(data, isBigEndian);

            if (entries.Count == 0)
                throw new InvalidDataException(
                    "No valid UDAS entries were found.");

            string baseName = Path.GetFileNameWithoutExtension(archivePath);
            string folder = Path.Combine(outputDir, baseName);
            Directory.CreateDirectory(folder);

            List<string> index = new List<string>
            {
                "# RE4 UDAS Archive Manifest",
                "ArchiveType=UDAS",
                "Endianness=" + (isBigEndian ? "BigEndian" : "LittleEndian"),
                "TopLength=" + FormatHex(entries[0].Offset)
            };

            int extracted = 0;

            // -------------------------------------------------------------
            // UDAS TOP
            // -------------------------------------------------------------
            uint topLength = entries[0].Offset;

            if (topLength == 0 || topLength > data.Length)
                throw new InvalidDataException(
                    $"Invalid UDAS TOP length: 0x{topLength:X}.");

            string topName = baseName + "_TOP.HEX";
            File.WriteAllBytes(
                Path.Combine(folder, topName),
                Slice(data, 0, checked((int)topLength)));

            index.Add("TopFile=" + topName);
            extracted++;
            progressCallback?.Invoke(
                $"Extracted: {topName}",
                10);

            // -------------------------------------------------------------
            // Determine whether the first record is the DAT.
            // The original extractor uses type == 0 for DAT.
            // -------------------------------------------------------------
            bool readDat = false;
            bool readSnd = false;
            int datCount = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                UdasEntry entry = entries[i];

                if (entry.Type == 0 && !readDat)
                {
                    if ((ulong)entry.Offset + entry.Length > (ulong)data.Length)
                        throw new InvalidDataException(
                            $"UDAS DAT exceeds EOF. Offset=0x{entry.Offset:X}, Length=0x{entry.Length:X}.");

                    int resultCount = ExtractDatPayload(
                        data,
                        entry.Offset,
                        entry.Length,
                        folder,
                        baseName,
                        isBigEndian,
                        archivePath,
                        progressCallback,
                        index,
                        out bool isCompressed,
                        out string? compressedFile,
                        out bool e3);

                    datCount = resultCount;
                    index.Add("DatOffset=" + FormatHex(entry.Offset));
                    index.Add("DatLength=" + FormatHex(entry.Length));
                    index.Add("HasE3Header=" + e3.ToString());

                    if (isCompressed && compressedFile != null)
                        index.Add("YZ2File=" + compressedFile);

                    readDat = true;
                }
                else if (entry.Type != 0 &&
                         entry.Type != 0xFFFFFFFF &&
                         !readSnd)
                {
                    // -----------------------------------------------------
                    // SND/END
                    // -----------------------------------------------------
                    if (entry.Offset > data.Length)
                        throw new InvalidDataException(
                            $"Invalid UDAS END offset: 0x{entry.Offset:X}.");

                    int length = data.Length - checked((int)entry.Offset);

                    string endExtension =
                        length > 0 && entry.Type == 4
                            ? ".SND"
                            : ".EMPTY";

                    string endName = baseName + "_END" + endExtension;

                    File.WriteAllBytes(
                        Path.Combine(folder, endName),
                        Slice(data, checked((int)entry.Offset), length));

                    index.Add("SoundFlag=" + entry.Type.ToString(CultureInfo.InvariantCulture));
                    index.Add("EndFile=" + endName);

                    extracted++;

                    progressCallback?.Invoke(
                        $"Extracted: {endName}",
                        95);

                    // -----------------------------------------------------
                    // Middle bytes between previous DAT end and SND start.
                    // -----------------------------------------------------
                    if (i > 0)
                    {
                        UdasEntry previous = entries[i - 1];

                        ulong middleStart =
                            (ulong)previous.Offset + previous.Length;

                        long middleLength =
                            (long)entry.Offset - (long)middleStart;

                        if (middleLength > 0 &&
                            middleStart < (ulong)data.Length)
                        {
                            int middleStartInt = checked((int)middleStart);
                            int middleLengthInt = checked((int)Math.Min(
                                middleLength,
                                data.Length - middleStartInt));

                            string middleName = baseName + "_MIDDLE.HEX";

                            File.WriteAllBytes(
                                Path.Combine(folder, middleName),
                                Slice(
                                    data,
                                    middleStartInt,
                                    middleLengthInt));

                            index.Add("MiddleFile=" + middleName);
                            extracted++;

                            progressCallback?.Invoke(
                                $"Extracted: {middleName}",
                                85);
                        }
                    }

                    readSnd = true;
                }
            }

            index.Insert(2, "FileCount=" + datCount.ToString(CultureInfo.InvariantCulture));
            File.WriteAllLines(
                Path.Combine(folder, INDEX_FILE_NAME),
                index);

            progressCallback?.Invoke(
                $"Finished UDAS: {datCount} DAT files",
                100);

            return extracted;
        }

        private static List<UdasEntry> ReadUdasEntries(
            byte[] data,
            bool isBigEndian)
        {
            List<UdasEntry> result = new List<UdasEntry>();

            // The original format currently defines two useful records,
            // but accepting further records makes the reader safer.
            for (uint record = UDAS_RECORD_START;
                 record + 0x10 <= data.Length;
                 record += UDAS_RECORD_SIZE)
            {
                uint type = ReadUInt32(
                    data,
                    checked((int)record),
                    isBigEndian);

                uint length = ReadUInt32(
                    data,
                    checked((int)record + 4),
                    isBigEndian);

                uint offset = ReadUInt32(
                    data,
                    checked((int)record + 12),
                    isBigEndian);

                if (type == 0xFFFFFFFF)
                    break;

                // Stop when a completely empty record is encountered.
                if (type == 0 && length == 0 && offset == 0)
                    break;

                // A valid record must have an offset inside the file.
                if (offset >= data.Length)
                {
                    // Do not immediately fail on an unused later record.
                    if (result.Count > 0)
                        break;

                    throw new InvalidDataException(
                        $"Invalid first UDAS record: type=0x{type:X8}, offset=0x{offset:X8}.");
                }

                result.Add(new UdasEntry
                {
                    Type = type,
                    Length = length,
                    Offset = offset
                });

                // Original UDAS parser examines the first two records.
                if (result.Count >= 2)
                    break;
            }

            return result;
        }

        private static bool DetectUdasEndianness(byte[] data)
        {
            if (data.Length < 0x50)
                throw new InvalidDataException(
                    "UDAS file is too small to determine endianness.");

            // First try the little-endian format used by RE4 2007/UHD-style
            // UDAS files, including the supplied pl00.udas.
            uint leType = ReadUInt32(data, 0x20, false);
            uint leLength = ReadUInt32(data, 0x24, false);
            uint leOffset = ReadUInt32(data, 0x2C, false);

            uint leSecondType = ReadUInt32(data, 0x40, false);
            uint leSecondOffset = ReadUInt32(data, 0x4C, false);

            bool leValid =
                leType == 0 &&
                leOffset > 0 &&
                leOffset <= data.Length &&
                leLength > 0 &&
                (ulong)leOffset + leLength <= (ulong)data.Length &&
                IsPlausibleUdasSecondType(leSecondType) &&
                (leSecondOffset == 0 || leSecondOffset >= leOffset);

            if (leValid)
                return false;

            // Big-endian GameCube/Wii-style UDAS.
            uint beType = ReadUInt32(data, 0x20, true);
            uint beLength = ReadUInt32(data, 0x24, true);
            uint beOffset = ReadUInt32(data, 0x2C, true);

            uint beSecondType = ReadUInt32(data, 0x40, true);
            uint beSecondOffset = ReadUInt32(data, 0x4C, true);

            bool beValid =
                beType == 0 &&
                beOffset > 0 &&
                beOffset <= data.Length &&
                beLength > 0 &&
                (ulong)beOffset + beLength <= (ulong)data.Length &&
                IsPlausibleUdasSecondType(beSecondType) &&
                (beSecondOffset == 0 || beSecondOffset >= beOffset);

            if (beValid)
                return true;

            throw new InvalidDataException(
                "Unable to determine UDAS archive endianness.");
        }

        private static bool IsPlausibleUdasSecondType(uint type)
        {
            return type == 0 ||
                   type == 4 ||
                   type == 0xFFFFFFFE ||
                   type == 0xFFFFFFFF;
        }

        // =====================================================================
        // DAT EXTRACTION
        // =====================================================================

        private static int ExtractDatLike(
            string archivePath,
            string outputDir,
            Action<string, int>? progressCallback,
            ArchiveEndianness preferredEndianness)
        {
            byte[] data = File.ReadAllBytes(archivePath);

            if (data.Length < 0x20)
                throw new InvalidDataException(
                    "DAT archive is too small to contain a valid header.");

            bool isBigEndian;

            if (preferredEndianness == ArchiveEndianness.BigEndian)
                isBigEndian = true;
            else if (preferredEndianness == ArchiveEndianness.LittleEndian)
                isBigEndian = false;
            else
                isBigEndian = DetectDatEndianness(data);

            string baseName = Path.GetFileNameWithoutExtension(archivePath);
            string folder = Path.Combine(outputDir, baseName);
            Directory.CreateDirectory(folder);

            List<string> index = new List<string>
            {
                "# RE4 DAT Archive Manifest",
                "ArchiveType=DAT",
                "Endianness=" + (isBigEndian ? "BigEndian" : "LittleEndian")
            };

            bool compressed;
            string? compressedFile;
            bool e3;

            int count = ExtractDatPayload(
                data,
                0,
                checked((uint)data.Length),
                folder,
                baseName,
                isBigEndian,
                archivePath,
                progressCallback,
                index,
                out compressed,
                out compressedFile,
                out e3);

            index.Insert(
                2,
                "FileCount=" + count.ToString(CultureInfo.InvariantCulture));

            index.Add("HasE3Header=" + e3.ToString());

            if (compressedFile != null)
                index.Add("YZ2File=" + compressedFile);

            File.WriteAllLines(
                Path.Combine(folder, INDEX_FILE_NAME),
                index);

            progressCallback?.Invoke(
                $"Finished DAT: {count} files",
                100);

            return count;
        }

        private static bool DetectDatEndianness(byte[] data)
        {
            // DAT files normally begin with a small file count.
            // Try both byte orders and validate the resulting table.
            uint leCount = ReadUInt32(data, 0, false);
            uint beCount = ReadUInt32(data, 0, true);

            bool leValid = IsPlausibleDatHeader(data, leCount, false);
            bool beValid = IsPlausibleDatHeader(data, beCount, true);

            if (leValid && !beValid)
                return false;

            if (beValid && !leValid)
                return true;

            if (leValid && beValid)
            {
                // Prefer the endian whose first offset points into the
                // payload after the complete offset/extension tables.
                uint leFirst = ReadUInt32(data, 0x10, false);
                uint beFirst = ReadUInt32(data, 0x10, true);

                bool leFirstValid = leFirst < data.Length && leFirst >= 0x10;
                bool beFirstValid = beFirst < data.Length && beFirst >= 0x10;

                if (leFirstValid && !beFirstValid)
                    return false;

                if (beFirstValid && !leFirstValid)
                    return true;

                // RE4 PC/UHD DATs encountered by this tool are commonly
                // little-endian. Keep the existing class default for an
                // otherwise ambiguous header.
                return false;
            }

            throw new InvalidDataException(
                "Unable to determine DAT archive endianness.");
        }

        private static bool IsPlausibleDatHeader(
            byte[] data,
            uint count,
            bool isBigEndian)
        {
            if (count == 0 || count > 0x10000)
                return false;

            // Standard DAT: count + 3 header words, then offset table and
            // four-byte extension table. E3: count followed immediately by
            // the offset table. Test both forms.
            ulong normalTableEnd = 0x10UL + (ulong)count * 8UL;
            ulong e3TableEnd = 0x04UL + (ulong)count * 8UL;

            if (normalTableEnd > (ulong)data.Length &&
                e3TableEnd > (ulong)data.Length)
                return false;

            bool normal = false;
            bool e3 = false;

            if (normalTableEnd <= (ulong)data.Length)
            {
                uint first = ReadUInt32(data, 0x10, isBigEndian);
                normal = first >= normalTableEnd && first <= data.Length;
            }

            if (e3TableEnd <= (ulong)data.Length)
            {
                uint first = ReadUInt32(data, 0x04, isBigEndian);
                e3 = first >= e3TableEnd && first <= data.Length;
            }

            return normal || e3;
        }

        private static int ExtractDatPayload(
            byte[] source,
            uint offsetStart,
            uint fullLength,
            string outputFolder,
            string baseName,
            bool isBigEndian,
            string archivePath,
            Action<string, int>? progressCallback,
            List<string> index,
            out bool isCompressed,
            out string? compressedFile,
            out bool isE3Version)
        {
            isCompressed = false;
            compressedFile = null;
            isE3Version = false;

            int start = checked((int)offsetStart);
            int length = checked((int)fullLength);

            if ((ulong)offsetStart + fullLength > (ulong)source.Length)
                throw new InvalidDataException(
                    "DAT payload exceeds the containing archive.");

            // -------------------------------------------------------------
            // Original CheckYZ2 logic:
            // the first uint is normally a DAT file count.
            // A value >= 0x10000 is treated as possible YZ2.
            //
            // We do not silently decode YZ2 here because the existing
            // C# class has no YZ2 implementation. Instead the compressed
            // payload is preserved as a .YZ2 file.
            // -------------------------------------------------------------
            uint amount = ReadUInt32(
                source,
                start,
                isBigEndian);

            if (amount >= 0x10000)
            {
                string yz2Name = baseName + ".YZ2";

                File.WriteAllBytes(
                    Path.Combine(outputFolder, yz2Name),
                    Slice(source, start, length));

                index.Add("HAS_YZ2=true");
                index.Add("YZ2File=" + yz2Name);

                isCompressed = true;
                compressedFile = yz2Name;

                progressCallback?.Invoke(
                    $"Extracted compressed YZ2: {yz2Name}",
                    100);

                return 0;
            }

            if (amount == 0)
                throw new InvalidDataException(
                    "DAT contains zero entries.");

            // Original Dat.cpp:
            // final format normally has the table at 0x10.
            // E3 format uses 0x04.
            uint tableOffset = 0x10;

            if (start + 16 > source.Length)
                throw new InvalidDataException("DAT header is truncated.");

            uint u2 = ReadUInt32(source, start + 4, isBigEndian);
            uint u3 = ReadUInt32(source, start + 8, isBigEndian);
            uint u4 = ReadUInt32(source, start + 12, isBigEndian);

            if (u2 != 0 || u3 != 0 || u4 != 0)
            {
                tableOffset = 0x04;
                isE3Version = true;
                index.Add("HasE3Header=true");
            }
            else
            {
                index.Add("HasE3Header=false");
            }

            ulong tableBytes = (ulong)amount * 8UL;

            if ((ulong)tableOffset + tableBytes > fullLength)
                throw new InvalidDataException(
                    $"DAT table exceeds payload. Count={amount}, Length=0x{fullLength:X}.");

            List<DatEntry> entries = new List<DatEntry>(
                checked((int)amount));

            int offsetTable = checked(start + (int)tableOffset);
            int extensionTable = checked(
                offsetTable + checked((int)(amount * 4)));

            for (int i = 0; i < amount; i++)
            {
                int p = checked(offsetTable + i * 4);

                uint relativeOffset = ReadUInt32(
                    source,
                    p,
                    isBigEndian);

                int ep = checked(extensionTable + i * 4);

                string extension = ReadExtension(
                    source,
                    ep);

                entries.Add(new DatEntry
                {
                    Offset = relativeOffset,
                    Extension = extension
                });
            }

            uint endDatOffset = fullLength;

            // For ordinary DAT/MAP, last entry ends at fullLength.
            // Validate monotonicity and bounds before extracting.
            for (int i = 0; i < entries.Count; i++)
            {
                uint relativeStart = entries[i].Offset;

                if (relativeStart > endDatOffset)
                    throw new InvalidDataException(
                        $"Invalid DAT offset at entry {i}: 0x{relativeStart:X}.");

                uint relativeEnd =
                    i + 1 < entries.Count
                        ? entries[i + 1].Offset
                        : endDatOffset;

                if (relativeEnd < relativeStart ||
                    relativeEnd > fullLength)
                {
                    throw new InvalidDataException(
                        $"Invalid DAT offset sequence at entry {i}: " +
                        $"0x{relativeStart:X} -> 0x{relativeEnd:X}.");
                }

                int absoluteStart =
                    checked(start + (int)relativeStart);

                int subLength =
                    checked((int)(relativeEnd - relativeStart));

                byte[] fileData =
                    Slice(source, absoluteStart, subLength);

                string extension = entries[i].Extension;
                string fileName =
                    baseName + "_" +
                    i.ToString("D3", CultureInfo.InvariantCulture);

                if (extension.Length > 0)
                    fileName += "." + extension;

                string fullName =
                    Path.Combine(outputFolder, fileName);

                File.WriteAllBytes(fullName, fileData);

                index.Add(
                    "File" +
                    i.ToString("D3", CultureInfo.InvariantCulture) +
                    "=" +
                    fileName);

                int percentage =
                    15 +
                    (int)(((long)(i + 1) * 75L) /
                          Math.Max(1, entries.Count));

                progressCallback?.Invoke(
                    $"Extracted: {fileName} ({i + 1}/{entries.Count})",
                    Math.Min(90, percentage));
            }

            return entries.Count;
        }

        private static string ReadExtension(
            byte[] data,
            int offset)
        {
            if (offset < 0 || offset + 4 > data.Length)
                return "BIN";

            StringBuilder sb = new StringBuilder(4);

            for (int i = 0; i < 4; i++)
            {
                byte b = data[offset + i];

                if ((b >= (byte)'A' && b <= (byte)'Z') ||
                    (b >= (byte)'a' && b <= (byte)'z') ||
                    (b >= (byte)'0' && b <= (byte)'9'))
                {
                    sb.Append((char)b);
                }
            }

            return sb.Length == 0
                ? "BIN"
                : sb.ToString().ToUpperInvariant();
        }

        // =====================================================================
        // DAT REPACK
        // =====================================================================

        private static int RepackDat(
            string inputFolder,
            string outputArchivePath,
            List<string> indexedFiles,
            bool isBigEndian,
            bool hasE3Header,
            Action<string, int>? progressCallback)
        {
            List<string> files = ResolveIndexedFiles(
                inputFolder,
                indexedFiles);

            if (files.Count == 0)
                throw new InvalidOperationException(
                    "No DAT files available to repack.");

            int count = files.Count;

            uint tableOffset = hasE3Header ? 4u : 0x10u;

            uint headerLength =
                checked(tableOffset + (uint)(count * 8));

            uint firstDataOffset =
                Align(headerLength, DEFAULT_ALIGNMENT);

            List<byte[]> fileData = new List<byte[]>(count);
            List<uint> offsets = new List<uint>(count);

            uint position = firstDataOffset;

            for (int i = 0; i < files.Count; i++)
            {
                byte[] bytes = File.ReadAllBytes(files[i]);

                offsets.Add(position);
                fileData.Add(bytes);

                position = checked(
                    position + Align((uint)bytes.Length, DEFAULT_ALIGNMENT));
            }

            using FileStream fs = new FileStream(
                outputArchivePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);

            using BinaryWriter bw = new BinaryWriter(fs);

            // Header
            WriteUInt32(bw, (uint)count, isBigEndian);

            if (!hasE3Header)
            {
                WriteUInt32(bw, 0, isBigEndian);
                WriteUInt32(bw, 0, isBigEndian);
                WriteUInt32(bw, 0, isBigEndian);
            }

            // Offsets
            foreach (uint offset in offsets)
                WriteUInt32(bw, offset, isBigEndian);

            // Four-character extensions
            foreach (string file in files)
            {
                string ext =
                    Path.GetExtension(file)
                        .TrimStart('.')
                        .ToUpperInvariant();

                byte[] extBytes = new byte[4];
                byte[] source = Encoding.ASCII.GetBytes(ext);

                Array.Copy(
                    source,
                    0,
                    extBytes,
                    0,
                    Math.Min(4, source.Length));

                bw.Write(extBytes);
            }

            PadStream(bw, DEFAULT_ALIGNMENT);

            for (int i = 0; i < fileData.Count; i++)
            {
                bw.Write(fileData[i]);

                if (i < fileData.Count - 1)
                    PadStream(bw, DEFAULT_ALIGNMENT);

                progressCallback?.Invoke(
                    $"Repacked: {Path.GetFileName(files[i])} ({i + 1}/{count})",
                    10 + (int)(((long)(i + 1) * 90L) / count));
            }

            return count;
        }

        // =====================================================================
        // UDAS REPACK
        // =====================================================================

        private static int RepackUdas(
            string inputFolder,
            string outputArchivePath,
            List<string> indexedFiles,
            string? topFile,
            string? middleFile,
            string? endFile,
            uint topLength,
            uint soundFlag,
            bool isBigEndian,
            bool hasE3Header,
            Action<string, int>? progressCallback)
        {
            List<string> datFiles = ResolveIndexedFiles(
                inputFolder,
                indexedFiles);

            // Remove top/middle/end files if they were accidentally included
            // as ordinary File entries.
            datFiles = datFiles
                .Where(f =>
                    !Path.GetFileName(f).Equals(topFile, StringComparison.OrdinalIgnoreCase) &&
                    !Path.GetFileName(f).Equals(middleFile, StringComparison.OrdinalIgnoreCase) &&
                    !Path.GetFileName(f).Equals(endFile, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (datFiles.Count == 0)
                throw new InvalidOperationException(
                    "No DAT files available for UDAS repack.");

            byte[] top;

            if (!string.IsNullOrWhiteSpace(topFile) &&
                File.Exists(Path.Combine(inputFolder, topFile)))
            {
                top = File.ReadAllBytes(
                    Path.Combine(inputFolder, topFile));

                if (top.Length < 0x80)
                    top = MakeNewUdasTop(
                        isBigEndian,
                        true,
                        !string.IsNullOrWhiteSpace(endFile),
                        soundFlag);
            }
            else
            {
                top = MakeNewUdasTop(
                    isBigEndian,
                    true,
                    !string.IsNullOrWhiteSpace(endFile),
                    soundFlag);
            }

            topLength = (uint)top.Length;

            // Build DAT in memory first.
            byte[] dat;

            using (MemoryStream ms = new MemoryStream())
            {
                RepackDatStream(
                    ms,
                    datFiles,
                    isBigEndian,
                    hasE3Header,
                    progressCallback,
                    5,
                    65);

                dat = ms.ToArray();
            }

            byte[] middle = Array.Empty<byte>();

            if (!string.IsNullOrWhiteSpace(middleFile))
            {
                string path = Path.Combine(inputFolder, middleFile);

                if (File.Exists(path))
                    middle = File.ReadAllBytes(path);
            }

            byte[] end = Array.Empty<byte>();

            if (!string.IsNullOrWhiteSpace(endFile))
            {
                string path = Path.Combine(inputFolder, endFile);

                if (File.Exists(path))
                    end = File.ReadAllBytes(path);
            }

            uint datOffset = topLength;

            uint middleOffset =
                checked(datOffset + (uint)dat.Length);

            uint endOffset =
                checked(middleOffset + (uint)middle.Length);

            // Update UDAS record 0.
            WriteUInt32At(
                top,
                0x20,
                0,
                isBigEndian);

            WriteUInt32At(
                top,
                0x24,
                (uint)dat.Length,
                isBigEndian);

            WriteUInt32At(
                top,
                0x2C,
                datOffset,
                isBigEndian);

            if (end.Length > 0)
            {
                WriteUInt32At(
                    top,
                    0x40,
                    soundFlag,
                    isBigEndian);

                WriteUInt32At(
                    top,
                    0x44,
                    (uint)end.Length,
                    isBigEndian);

                WriteUInt32At(
                    top,
                    0x4C,
                    endOffset,
                    isBigEndian);

                if (top.Length >= 0x64)
                {
                    WriteUInt32At(
                        top,
                        0x60,
                        0xFFFFFFFF,
                        isBigEndian);
                }
            }
            else if (top.Length >= 0x44)
            {
                WriteUInt32At(
                    top,
                    0x40,
                    0xFFFFFFFF,
                    isBigEndian);
            }

            using FileStream fs = new FileStream(
                outputArchivePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);

            fs.Write(top, 0, top.Length);
            fs.Write(dat, 0, dat.Length);

            if (middle.Length > 0)
                fs.Write(middle, 0, middle.Length);

            if (end.Length > 0)
                fs.Write(end, 0, end.Length);

            progressCallback?.Invoke(
                "Repacked UDAS",
                100);

            return datFiles.Count;
        }

        private static void RepackDatStream(
            Stream output,
            List<string> files,
            bool isBigEndian,
            bool hasE3Header,
            Action<string, int>? progressCallback,
            int progressStart,
            int progressEnd)
        {
            int count = files.Count;

            uint tableOffset = hasE3Header ? 4u : 0x10u;
            uint headerLength =
                checked(tableOffset + (uint)(count * 8));

            uint firstDataOffset =
                Align(headerLength, DEFAULT_ALIGNMENT);

            List<uint> offsets = new List<uint>(count);
            List<byte[]> contents = new List<byte[]>(count);

            uint current = firstDataOffset;

            foreach (string file in files)
            {
                byte[] bytes = File.ReadAllBytes(file);

                offsets.Add(current);
                contents.Add(bytes);

                current = checked(
                    current + Align(
                        (uint)bytes.Length,
                        DEFAULT_ALIGNMENT));
            }

            using BinaryWriter bw =
                new BinaryWriter(
                    output,
                    Encoding.Default,
                    true);

            WriteUInt32(bw, (uint)count, isBigEndian);

            if (!hasE3Header)
            {
                WriteUInt32(bw, 0, isBigEndian);
                WriteUInt32(bw, 0, isBigEndian);
                WriteUInt32(bw, 0, isBigEndian);
            }

            foreach (uint offset in offsets)
                WriteUInt32(bw, offset, isBigEndian);

            foreach (string file in files)
            {
                string ext =
                    Path.GetExtension(file)
                        .TrimStart('.')
                        .ToUpperInvariant();

                byte[] four = new byte[4];
                byte[] extBytes = Encoding.ASCII.GetBytes(ext);

                Array.Copy(
                    extBytes,
                    four,
                    Math.Min(4, extBytes.Length));

                bw.Write(four);
            }

            PadStream(bw, DEFAULT_ALIGNMENT);

            for (int i = 0; i < contents.Count; i++)
            {
                bw.Write(contents[i]);

                if (i + 1 < contents.Count)
                    PadStream(bw, DEFAULT_ALIGNMENT);

                int pct =
                    progressStart +
                    (int)(((long)(i + 1) *
                          (progressEnd - progressStart)) /
                          contents.Count);

                progressCallback?.Invoke(
                    $"Packed: {Path.GetFileName(files[i])}",
                    pct);
            }
        }

        private static byte[] MakeNewUdasTop(
            bool isBigEndian,
            bool hasDat,
            bool hasEnd,
            uint soundFlag)
        {
            byte[] top = new byte[0x400];

            // Standard non-DRS RE4 UDAS signature/header.
            for (int i = 0; i < 8; i++)
            {
                int p = i * 4;
                top[p + 0] = 0xCA;
                top[p + 1] = 0xB6;
                top[p + 2] = 0xBE;
                top[p + 3] = 0x20;
            }

            WriteUInt32At(
                top,
                0x2C,
                0x400,
                isBigEndian);

            if (hasDat && hasEnd)
            {
                WriteUInt32At(
                    top,
                    0x40,
                    soundFlag,
                    isBigEndian);

                WriteUInt32At(
                    top,
                    0x60,
                    0xFFFFFFFF,
                    isBigEndian);
            }
            else if (hasDat)
            {
                WriteUInt32At(
                    top,
                    0x40,
                    0xFFFFFFFF,
                    isBigEndian);
            }
            else if (hasEnd)
            {
                WriteUInt32At(
                    top,
                    0x20,
                    soundFlag,
                    isBigEndian);

                WriteUInt32At(
                    top,
                    0x40,
                    0xFFFFFFFF,
                    isBigEndian);
            }
            else
            {
                WriteUInt32At(
                    top,
                    0x20,
                    0xFFFFFFFF,
                    isBigEndian);
            }

            return top;
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private static List<string> ResolveIndexedFiles(
            string inputFolder,
            List<string> indexedFiles)
        {
            List<string> result = new List<string>();

            foreach (string item in indexedFiles)
            {
                if (string.IsNullOrWhiteSpace(item))
                    continue;

                string normalized = item
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);

                string path =
                    Path.IsPathRooted(normalized)
                        ? normalized
                        : Path.Combine(inputFolder, normalized);

                if (File.Exists(path))
                    result.Add(path);
            }

            if (result.Count > 0)
                return result;

            return Directory
                .GetFiles(inputFolder)
                .Where(f =>
                    !Path.GetFileName(f).Equals(
                        INDEX_FILE_NAME,
                        StringComparison.OrdinalIgnoreCase))
                .Where(f =>
                    !Path.GetFileName(f).EndsWith(
                        "_TOP.HEX",
                        StringComparison.OrdinalIgnoreCase))
                .Where(f =>
                    !Path.GetFileName(f).EndsWith(
                        "_MIDDLE.HEX",
                        StringComparison.OrdinalIgnoreCase))
                .Where(f =>
                    !Path.GetFileName(f).EndsWith(
                        "_END.SND",
                        StringComparison.OrdinalIgnoreCase))
                .Where(f =>
                    !Path.GetFileName(f).EndsWith(
                        "_END.EMPTY",
                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static uint ReadUInt32(
            byte[] data,
            int offset,
            bool isBigEndian)
        {
            if (offset < 0 || offset + 4 > data.Length)
                throw new EndOfStreamException();

            if (isBigEndian)
            {
                return ((uint)data[offset] << 24) |
                       ((uint)data[offset + 1] << 16) |
                       ((uint)data[offset + 2] << 8) |
                       data[offset + 3];
            }

            return data[offset] |
                   ((uint)data[offset + 1] << 8) |
                   ((uint)data[offset + 2] << 16) |
                   ((uint)data[offset + 3] << 24);
        }

        private static void WriteUInt32(
            BinaryWriter bw,
            uint value,
            bool isBigEndian)
        {
            if (isBigEndian)
            {
                bw.Write((byte)(value >> 24));
                bw.Write((byte)(value >> 16));
                bw.Write((byte)(value >> 8));
                bw.Write((byte)value);
            }
            else
            {
                bw.Write((byte)value);
                bw.Write((byte)(value >> 8));
                bw.Write((byte)(value >> 16));
                bw.Write((byte)(value >> 24));
            }
        }

        private static void WriteUInt32At(
            byte[] data,
            int offset,
            uint value,
            bool isBigEndian)
        {
            if (offset < 0 || offset + 4 > data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (isBigEndian)
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

        private static byte[] Slice(
            byte[] data,
            int offset,
            int length)
        {
            if (offset < 0 ||
                length < 0 ||
                offset > data.Length ||
                length > data.Length - offset)
            {
                throw new InvalidDataException(
                    $"Invalid data slice: offset=0x{offset:X}, length=0x{length:X}.");
            }

            byte[] result = new byte[length];

            Buffer.BlockCopy(
                data,
                offset,
                result,
                0,
                length);

            return result;
        }

        private static uint Align(
            uint value,
            uint alignment)
        {
            if (alignment == 0)
                return value;

            uint remainder = value % alignment;

            return remainder == 0
                ? value
                : checked(value + alignment - remainder);
        }

        private static void PadStream(
            BinaryWriter bw,
            uint alignment)
        {
            long position = bw.BaseStream.Position;
            long remainder = position % alignment;

            if (remainder == 0)
                return;

            int count = checked((int)(alignment - remainder));

            bw.Write(new byte[count]);
        }

        private static bool TryParseUInt(
            string value,
            out uint result)
        {
            value = value.Trim();

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return uint.TryParse(
                    value.Substring(2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out result);
            }

            return uint.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result);
        }

        private static string FormatHex(uint value)
        {
            return "0x" + value.ToString(
                "X",
                CultureInfo.InvariantCulture);
        }
    }
}

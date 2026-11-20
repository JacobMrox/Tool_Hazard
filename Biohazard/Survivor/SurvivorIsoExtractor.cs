using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Tool_Hazard.Biohazard.Survivor;

/// <summary>
/// Extracts the game data from the Resident Evil Survivor / Biohazard Gun
/// Survivor (2000) PC disc image.
///
/// The installer on that disc is an InstallShield v6 set: DATA1.HDR holds the
/// file directory and DATA1.CAB / DATA2.CAB hold the compressed payload. Both
/// are "ISc(" (InstallShield Cabinet) archives, which are unrelated to
/// Microsoft MSCF cabinets. Every file is stored as a sequence of raw-DEFLATE
/// chunks preceded by a 2-byte little-endian chunk size.
///
/// The disc image itself is ISO9660 with 2352-byte raw sectors (12-byte sync,
/// 4-byte MSF/mode header, 2048 bytes of user data). Cooked 2048-byte ISOs are
/// detected as well. Both layers are read directly from the image, so the
/// ~500 MB DATA2.CAB is never staged to disk in full.
///
/// Format references (MIT licensed):
///   https://github.com/twogood/unshield
///   https://github.com/sp00nznet/pcrecomp/blob/main/tools/assets/isextract.py
/// </summary>
public static class SurvivorIsoExtractor
{
    /// <summary>
    /// Extracts all files listed in DATA1.HDR from the given disc image.
    /// </summary>
    /// <param name="isoPath">Path to the .BIN/.ISO image of the game disc.</param>
    /// <param name="outputDir">Directory to write the extracted files into.</param>
    /// <param name="progressCallback">Optional (message, percent) progress callback.</param>
    /// <returns>The number of file entries processed (including skipped invalid ones).</returns>
    public static int Extract(
        string isoPath,
        string outputDir,
        Action<string, int>? progressCallback = null)
    {
        if (!File.Exists(isoPath))
            throw new FileNotFoundException("Disc image was not found.", isoPath);

        Directory.CreateDirectory(outputDir);

        using var iso = new Iso9660Image(isoPath);

        progressCallback?.Invoke("Reading InstallShield directory...", 0);
        if (!iso.TryFindFile("DATA1.HDR", out var headerEntry) || headerEntry is null)
            throw new InvalidDataException("DATA1.HDR was not found on the disc image. This does not look like the Biohazard Gun Survivor PC disc.");

        byte[] header;
        using (var stream = iso.OpenFile(headerEntry))
        {
            header = new byte[stream.Length];
            stream.ReadExactly(header);
        }

        // The InstallShield set may span several cabinets. Discover them on the
        // disc and read each volume's file-index range from its own header.
        var volumes = new List<CabinetVolume>();
        foreach (var entry in iso.RootFiles
                     .Where(f => f.Name.EndsWith(".CAB", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(f => VolumeNumber(f.Name)))
        {
            using var stream = iso.OpenFile(entry);
            var volumeHeader = new byte[64];
            if (stream.Read(volumeHeader, 0, volumeHeader.Length) < 36)
                continue;
            if (ReadUInt32(volumeHeader, 0) != InstallShieldCabinet.Signature)
                continue;

            volumes.Add(new CabinetVolume(
                VolumeNumber(entry.Name),
                ReadUInt32(volumeHeader, 28),
                ReadUInt32(volumeHeader, 32),
                () => iso.OpenFile(entry)));
        }

        if (volumes.Count == 0)
            throw new InvalidDataException("No InstallShield .CAB volumes were found on the disc image.");

        var cabinet = new InstallShieldCabinet(header, volumes);
        cabinet.Extract(iso, outputDir, progressCallback);
        return cabinet.Files.Count;
    }

    private static int VolumeNumber(string fileName)
    {
        // "DATA2.CAB" -> 2. Fall back to 0 for cabinets without a numbered name.
        int dot = fileName.LastIndexOf('.');
        if (dot <= 0)
            return 0;

        int start = dot;
        while (start > 0 && char.IsDigit(fileName[start - 1]))
            start--;

        return int.TryParse(fileName.AsSpan(start, dot - start), out int number) ? number : 0;
    }

    internal static uint ReadUInt32(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));

    internal static ushort ReadUInt16(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));

    internal static ulong ReadUInt64(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset, 8));
}

/// <summary>
/// Entry in the ISO9660 directory tree (or in an InstallShield directory).
/// </summary>
internal sealed class IsoEntry
{
    public IsoEntry(string name, uint extent, uint size, bool isDirectory)
    {
        Name = name;
        Extent = extent;
        Size = size;
        IsDirectory = isDirectory;
    }

    public string Name { get; }
    public uint Extent { get; }
    public uint Size { get; }
    public bool IsDirectory { get; }
    public string FullPath { get; set; } = string.Empty;
}

/// <summary>
/// Minimal read-only ISO9660 reader. Handles both cooked (2048-byte) images
/// and raw CD images with 2352-byte sectors.
/// </summary>
internal sealed class Iso9660Image : IDisposable
{
    private const int LogicalSectorSize = 2048;
    private const int RawSectorSize = 2352;
    private const int RawSectorHeaderSize = 16;

    private readonly FileStream _stream;
    private readonly int _sectorSize;
    private readonly int _sectorHeaderSize;
    private readonly Dictionary<string, IsoEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public Iso9660Image(string path)
    {
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            (_sectorSize, _sectorHeaderSize) = DetectSectorLayout();
            var pvd = ReadSectors(16, 1);
            if (pvd[0] != 1 || Encoding.ASCII.GetString(pvd, 1, 5) != "CD001")
                throw new InvalidDataException("No primary volume descriptor found at ISO9660 sector 16.");

            var root = ParseRecord(pvd, 156);
            if (root is null || !root.IsDirectory)
                throw new InvalidDataException("The ISO9660 root directory record is invalid.");

            Walk(root.Extent, root.Size, string.Empty);
        }
        catch
        {
            _stream.Dispose();
            throw;
        }
    }

    public IEnumerable<IsoEntry> RootFiles => _entries.Values
        .Where(e => !e.IsDirectory && !e.FullPath.Contains('/', StringComparison.Ordinal));

    public bool TryFindFile(string path, out IsoEntry? entry) =>
        _entries.TryGetValue(Normalize(path), out entry);

    public Stream OpenFile(IsoEntry entry)
    {
        if (entry.IsDirectory)
            throw new IOException($"Cannot open a directory as a file: {entry.FullPath}");
        return new SectorStream(_stream, _sectorSize, _sectorHeaderSize, entry.Extent, entry.Size);
    }

    public Stream OpenFile(string path)
    {
        if (!TryFindFile(path, out var entry) || entry is null)
            throw new FileNotFoundException("File was not found in the disc image.", path);
        return OpenFile(entry);
    }

    private (int SectorSize, int HeaderSize) DetectSectorLayout()
    {
        if (HasVolumeDescriptor(16L * LogicalSectorSize + 1))
            return (LogicalSectorSize, 0);
        if (HasVolumeDescriptor(16L * RawSectorSize + RawSectorHeaderSize + 1))
            return (RawSectorSize, RawSectorHeaderSize);
        throw new InvalidDataException("Unrecognised disc image: expected an ISO9660 volume descriptor with 2048- or 2352-byte sectors.");
    }

    private bool HasVolumeDescriptor(long offset)
    {
        if (offset + 6 > _stream.Length)
            return false;

        _stream.Position = offset;
        Span<byte> magic = stackalloc byte[5];
        _stream.ReadExactly(magic);
        return Encoding.ASCII.GetString(magic) == "CD001";
    }

    private byte[] ReadSectors(uint extent, int count)
    {
        var result = new byte[count * LogicalSectorSize];
        for (int i = 0; i < count; i++)
        {
            _stream.Position = (long)(extent + i) * _sectorSize + _sectorHeaderSize;
            _stream.ReadExactly(result, i * LogicalSectorSize, LogicalSectorSize);
        }
        return result;
    }

    private static IsoEntry? ParseRecord(byte[] data, int offset)
    {
        if (offset < 0 || offset >= data.Length)
            return null;

        int length = data[offset];
        if (length < 34 || offset + length > data.Length)
            return null;

        uint extent = SurvivorIsoExtractor.ReadUInt32(data, offset + 2);
        uint size = SurvivorIsoExtractor.ReadUInt32(data, offset + 10);
        bool isDirectory = (data[offset + 25] & 0x02) != 0;
        int nameLength = Math.Min(data[offset + 32], length - 33);

        // ECMA-119 uses the single identifier bytes 0 and 1 for "." and "..".
        // Without this mapping Walk() would recurse into the current directory.
        string name;
        if (nameLength == 1 && data[offset + 33] == 0)
            name = ".";
        else if (nameLength == 1 && data[offset + 33] == 1)
            name = "..";
        else
            name = nameLength > 0
                ? Encoding.ASCII.GetString(data, offset + 33, nameLength)
                : string.Empty;

        if (name.EndsWith(";1", StringComparison.Ordinal))
            name = name[..^2];

        return new IsoEntry(name, extent, size, isDirectory);
    }

    private void Walk(uint extent, uint size, string parent)
    {
        // Guard against a malformed image claiming a multi-gigabyte directory.
        if (size > 64u * 1024 * 1024)
            throw new InvalidDataException("An ISO9660 directory record has an implausible size.");

        int sectorCount = checked((int)((size + LogicalSectorSize - 1) / LogicalSectorSize));
        var data = ReadSectors(extent, sectorCount);

        int position = 0;
        while (position < size)
        {
            int length = data[position];
            if (length == 0)
            {
                // Remaining bytes up to the next sector boundary are padding.
                position = ((position / LogicalSectorSize) + 1) * LogicalSectorSize;
                continue;
            }
            if (position + length > size)
                break;

            var entry = ParseRecord(data, position);
            position += length;
            if (entry is null || entry.Name is "." or "..")
                continue;

            string fullPath = parent.Length == 0 ? entry.Name : parent + "/" + entry.Name;
            entry.FullPath = fullPath;
            _entries[fullPath] = entry;
            if (entry.IsDirectory)
                Walk(entry.Extent, entry.Size, fullPath);
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    public void Dispose() => _stream.Dispose();
}

/// <summary>
/// Presents the 2048-byte user-data area of consecutive ISO sectors as a
/// contiguous seekable stream.
/// </summary>
internal sealed class SectorStream : Stream
{
    private readonly FileStream _source;
    private readonly int _sectorSize;
    private readonly int _sectorHeaderSize;
    private readonly uint _startSector;
    private readonly long _length;
    private long _position;

    public SectorStream(FileStream source, int sectorSize, int sectorHeaderSize, uint startSector, uint length)
    {
        _source = source;
        _sectorSize = sectorSize;
        _sectorHeaderSize = sectorHeaderSize;
        _startSector = startSector;
        _length = length;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => _position = Math.Clamp(value, 0, _length);
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty || _position >= _length)
            return 0;

        int wanted = (int)Math.Min(buffer.Length, _length - _position);
        int total = 0;
        while (total < wanted)
        {
            int withinSector = (int)(_position % 2048);
            long rawOffset = (long)(_startSector + (_position / 2048)) * _sectorSize
                             + _sectorHeaderSize + withinSector;
            _source.Position = rawOffset;

            int chunk = Math.Min(wanted - total, 2048 - withinSector);
            int read = _source.Read(buffer.Slice(total, chunk));
            if (read <= 0)
                break;

            total += read;
            _position += read;
        }
        return total;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        return _position;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

internal sealed class CabinetVolume
{
    public CabinetVolume(int index, uint firstFile, uint lastFile, Func<Stream> open)
    {
        Index = index;
        FirstFile = firstFile;
        LastFile = lastFile;
        Open = open;
    }

    public int Index { get; }
    public uint FirstFile { get; }
    public uint LastFile { get; }
    public Func<Stream> Open { get; }
}

internal sealed class InstallShieldFile
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Directory { get; set; } = string.Empty;
    public ushort Flags { get; set; }
    public ulong ExpandedSize { get; set; }
    public ulong CompressedSize { get; set; }
    public ulong DataOffset { get; set; }
    public ushort Volume { get; set; }

    public bool IsCompressed => (Flags & 0x04) != 0;
    public bool IsObfuscated => (Flags & 0x02) != 0;
    public bool IsInvalid => (Flags & 0x08) != 0;
}

/// <summary>
/// Reader for the InstallShield v5/v6 "ISc(" cabinet directory stored in
/// DATA1.HDR. File payloads are read from the CAB volumes on demand.
/// </summary>
internal sealed class InstallShieldCabinet
{
    internal const uint Signature = 0x28635349; // "ISc("
    private const int CommonHeaderSize = 20;

    private readonly byte[] _header;
    private readonly int _majorVersion;
    private readonly uint _descriptorOffset;
    private readonly uint _fileTableBase;
    private readonly List<CabinetVolume> _volumes;

    public InstallShieldCabinet(byte[] header, List<CabinetVolume> volumes)
    {
        _header = header;
        _volumes = volumes;

        if (header.Length < CommonHeaderSize || SurvivorIsoExtractor.ReadUInt32(header, 0) != Signature)
            throw new InvalidDataException("DATA1.HDR is not an InstallShield \"ISc(\" cabinet header.");

        uint version = SurvivorIsoExtractor.ReadUInt32(header, 4);
        _majorVersion = (int)((version >> 12) & 0xF);
        if (_majorVersion < 5)
            throw new InvalidDataException($"Unsupported InstallShield major version {_majorVersion}.");

        _descriptorOffset = SurvivorIsoExtractor.ReadUInt32(header, 12);
        _fileTableBase = _descriptorOffset + SurvivorIsoExtractor.ReadUInt32(header, (int)_descriptorOffset + 0x0C);

        uint directoryCount = SurvivorIsoExtractor.ReadUInt32(header, (int)_descriptorOffset + 0x1C);
        uint fileCount = SurvivorIsoExtractor.ReadUInt32(header, (int)_descriptorOffset + 0x28);
        if (directoryCount > 100_000 || fileCount > 1_000_000)
            throw new InvalidDataException("The InstallShield directory counts are implausibly large.");

        var directories = new List<string>(checked((int)directoryCount));
        for (uint i = 0; i < directoryCount; i++)
            directories.Add(ReadString(FileTableEntry((int)i)));

        uint v6Base = _majorVersion > 5
            ? _fileTableBase + SurvivorIsoExtractor.ReadUInt32(header, (int)_descriptorOffset + 0x2C)
            : 0;

        Files = new List<InstallShieldFile>(checked((int)fileCount));
        for (uint i = 0; i < fileCount; i++)
        {
            int descriptorOffset = _majorVersion <= 5
                ? (int)(_fileTableBase + FileTableEntry((int)(directoryCount + i)))
                : (int)(v6Base + i * 0x57);

            var file = new InstallShieldFile { Index = (int)i };
            if (_majorVersion <= 5)
            {
                uint nameOffset = SurvivorIsoExtractor.ReadUInt32(_header, descriptorOffset + 0x00);
                file.Directory = ReadDirectory(directories, SurvivorIsoExtractor.ReadUInt16(_header, descriptorOffset + 0x04));
                file.Flags = SurvivorIsoExtractor.ReadUInt16(_header, descriptorOffset + 0x08);
                file.ExpandedSize = SurvivorIsoExtractor.ReadUInt32(_header, descriptorOffset + 0x0A);
                file.CompressedSize = SurvivorIsoExtractor.ReadUInt32(_header, descriptorOffset + 0x0E);
                file.DataOffset = SurvivorIsoExtractor.ReadUInt32(_header, descriptorOffset + 0x26);
                file.Name = file.IsInvalid ? string.Empty : ReadString(nameOffset);
            }
            else
            {
                file.Flags = SurvivorIsoExtractor.ReadUInt16(_header, descriptorOffset + 0x00);
                file.ExpandedSize = SurvivorIsoExtractor.ReadUInt64(_header, descriptorOffset + 0x02);
                file.CompressedSize = SurvivorIsoExtractor.ReadUInt64(_header, descriptorOffset + 0x0A);
                file.DataOffset = SurvivorIsoExtractor.ReadUInt64(_header, descriptorOffset + 0x12);
                uint nameOffset = SurvivorIsoExtractor.ReadUInt32(_header, descriptorOffset + 0x3A);
                ushort directoryIndex = SurvivorIsoExtractor.ReadUInt16(_header, descriptorOffset + 0x3E);
                file.Volume = SurvivorIsoExtractor.ReadUInt16(_header, descriptorOffset + 0x55);
                file.Directory = ReadDirectory(directories, directoryIndex);
                file.Name = file.IsInvalid ? string.Empty : ReadString(nameOffset);
            }
            Files.Add(file);
        }
    }

    public List<InstallShieldFile> Files { get; }

    public void Extract(
        Iso9660Image iso,
        string outputDir,
        Action<string, int>? progress)
    {
        int total = Files.Count;
        int done = 0;
        var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Files)
        {
            done++;
            progress?.Invoke($"Extracting {file.Name} ({done}/{total})", total == 0 ? 100 : done * 100 / total);

            if (file.IsInvalid || string.IsNullOrWhiteSpace(file.Name))
                continue;

            string relativePath = BuildRelativePath(file.Directory, file.Name);
            if (relativePath.Length == 0)
                continue;

            string outputPath = MakeUniquePath(Path.Combine(outputDir, relativePath), usedPaths);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            if (file.IsCompressed)
            {
                var volume = SelectVolume(file);
                using var source = volume.Open();
                source.Position = (long)file.DataOffset;
                using var destination = File.Create(outputPath);
                Decompress(source, destination, file.ExpandedSize, file.IsObfuscated);
            }
            else
            {
                string isoPath = (file.Directory + "/" + file.Name).Replace('\\', '/').TrimStart('/');
                if (!iso.TryFindFile(isoPath, out var looseEntry) || looseEntry is null)
                {
                    // Some loose files are listed relative to the disc root only.
                    isoPath = file.Name;
                    iso.TryFindFile(isoPath, out looseEntry);
                }
                if (looseEntry is not null && !looseEntry.IsDirectory)
                {
                    using var source = iso.OpenFile(looseEntry);
                    using var destination = File.Create(outputPath);
                    source.CopyTo(destination);
                }
            }
        }
    }

    private uint FileTableEntry(int index) =>
        SurvivorIsoExtractor.ReadUInt32(_header, checked((int)(_fileTableBase + index * 4)));

    private string ReadString(uint relativeOffset)
    {
        long absolute = _fileTableBase + relativeOffset;
        if (absolute < 0 || absolute >= _header.Length)
            return string.Empty;

        int start = (int)absolute;
        int end = Array.IndexOf(_header, (byte)0, start);
        if (end < 0)
            end = _header.Length;
        return Encoding.Latin1.GetString(_header, start, end - start);
    }

    private static string ReadDirectory(List<string> directories, ushort index) =>
        index < directories.Count ? directories[index] : string.Empty;

    private CabinetVolume SelectVolume(InstallShieldFile file)
    {
        if (file.Volume > 0)
        {
            var match = _volumes.FirstOrDefault(v => v.Index == file.Volume);
            if (match is not null)
                return match;
        }

        // Fall back to the index ranges declared by each volume header.
        var selected = _volumes[0];
        foreach (var volume in _volumes)
        {
            if (volume.FirstFile <= (uint)file.Index)
                selected = volume;
        }
        return selected;
    }

    private static void Decompress(Stream source, Stream destination, ulong expandedSize, bool obfuscated)
    {
        ulong written = 0;
        int seed = 0;
        var sizeBuffer = new byte[2];
        var copyBuffer = new byte[81920];

        while (written < expandedSize)
        {
            ReadExact(source, sizeBuffer, 2, ref seed, obfuscated);
            int chunkSize = BinaryPrimitives.ReadUInt16LittleEndian(sizeBuffer);
            if (chunkSize == 0)
                break;

            var chunk = new byte[chunkSize];
            ReadExact(source, chunk, chunkSize, ref seed, obfuscated);

            using var chunkStream = new MemoryStream(chunk, writable: false);
            using var deflate = new DeflateStream(chunkStream, CompressionMode.Decompress);
            int read;
            while ((read = deflate.Read(copyBuffer, 0, copyBuffer.Length)) > 0)
            {
                destination.Write(copyBuffer, 0, read);
                written += (ulong)read;
            }
        }
    }

    private static void ReadExact(Stream stream, byte[] buffer, int count, ref int seed, bool obfuscated)
    {
        int total = 0;
        while (total < count)
        {
            int read = stream.Read(buffer, total, count - total);
            if (read <= 0)
                throw new EndOfStreamException("Unexpected end of InstallShield cabinet data.");
            total += read;
        }

        if (!obfuscated)
            return;

        for (int i = 0; i < count; i++)
        {
            int value = buffer[i] ^ 0xD5;
            value = ((value >> 2) | (value << 6)) & 0xFF;
            value = (value - (seed % 0x47)) & 0xFF;
            buffer[i] = (byte)value;
            seed++;
        }
    }

    private static string BuildRelativePath(string directory, string name)
    {
        var parts = new List<string>();
        foreach (var raw in (directory + "/" + name).Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string part = raw.Trim();
            if (part.Length == 0 || part is "." or "..")
                continue;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                part = part.Replace(invalid, '_');
            parts.Add(part);
        }
        return Path.Combine(parts.ToArray());
    }

    private static string MakeUniquePath(string path, HashSet<string> usedPaths)
    {
        if (usedPaths.Add(path))
            return path;

        string directory = Path.GetDirectoryName(path)!;
        string stem = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        for (int i = 2; ; i++)
        {
            string candidate = Path.Combine(directory, $"{stem} ({i}){extension}");
            if (usedPaths.Add(candidate))
                return candidate;
        }
    }
}

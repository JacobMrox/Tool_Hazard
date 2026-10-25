using System.Text;

namespace Tool_Hazard.Biohazard.Audio;

public sealed class XwbEntry
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required int Channels { get; init; }
    public required int SampleRate { get; init; }
    public required int BitsPerSample { get; init; }
    public required int DurationSamples { get; init; }
    public required int DataLength { get; init; }
    internal Func<byte[]> WaveFactory { get; init; } = null!;
    internal uint MiniFormat { get; init; }
    internal uint EntryFlags { get; init; }
    public byte[] GetWaveData() => WaveFactory();
    public TimeSpan Duration => SampleRate > 0
        ? TimeSpan.FromSeconds(DurationSamples / (double)SampleRate)
        : TimeSpan.Zero;
}

public sealed class XwbBank
{
    public required string Name { get; init; }
    public required IReadOnlyList<XwbEntry> Entries { get; init; }
    internal string SourcePath { get; init; } = string.Empty;
    internal bool IsCompact { get; init; }
    internal int MetadataOffset { get; init; }
    internal int MetadataSize { get; init; }
    internal int WaveOffset { get; init; }
}

/// <summary>
/// Reads little-endian XACT WaveBank files. The structure and packed mini-wave
/// format follow Microsoft's MIT-licensed DirectXTK WaveBankReader.
/// </summary>
public static class XwbService
{
    private const uint Signature = 0x444E4257; // WBND
    private const uint CompactFlag = 0x00020000;
    private const int HeaderSize = 52;
    private const int BankDataSize = 96;

    public static XwbBank Load(string path)
    {
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (source.Length < HeaderSize)
            throw new InvalidDataException("XWB file is too small.");
        byte[] header = new byte[HeaderSize];
        source.ReadExactly(header);
        if (ReadU32(header, 0) != Signature)
            throw new InvalidDataException("Not a little-endian XACT WaveBank (missing WBND signature).");

        var segments = new (uint Offset, uint Length)[5];
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = (ReadU32(header, 12 + i * 8), ReadU32(header, 16 + i * 8));
            if (i == 4 && segments[i].Offset <= source.Length &&
                (ulong)segments[i].Offset + segments[i].Length > (ulong)source.Length)
            {
                // Some shipped streaming banks declare the sector-aligned
                // wave-data allocation while omitting the final pad bytes.
                segments[i].Length = checked((uint)(source.Length - segments[i].Offset));
            }
            ValidateRange(source.Length, segments[i].Offset, segments[i].Length, $"segment {i}");
        }

        ulong prefixEnd = 0;
        for (int i = 0; i < 4; i++)
            prefixEnd = Math.Max(prefixEnd, (ulong)segments[i].Offset + segments[i].Length);
        if (prefixEnd > int.MaxValue)
            throw new InvalidDataException("XWB metadata is too large.");
        byte[] data = new byte[(int)prefixEnd];
        source.Position = 0;
        source.ReadExactly(data);

        var bankSegment = segments[0];
        if (bankSegment.Length < BankDataSize)
            throw new InvalidDataException("XWB bank-data segment is truncated.");

        int bank = checked((int)bankSegment.Offset);
        uint flags = ReadU32(data, bank);
        int count = checked((int)ReadU32(data, bank + 4));
        string bankName = ReadCString(data, bank + 8, 64);
        int metadataSize = checked((int)ReadU32(data, bank + 72));
        int nameSize = checked((int)ReadU32(data, bank + 76));
        int alignment = checked((int)ReadU32(data, bank + 80));
        uint compactFormat = ReadU32(data, bank + 84);

        if (count < 0 || count > 1_000_000)
            throw new InvalidDataException("XWB entry count is unreasonable.");
        if (alignment <= 0)
            alignment = 1;

        var metadata = segments[1];
        var names = segments[3];
        var waves = segments[4];
        bool compact = (flags & CompactFlag) != 0;
        int expectedMetadata = compact ? 4 : 24;
        if (metadataSize < expectedMetadata)
            throw new InvalidDataException("Unsupported XWB metadata element size.");
        if ((ulong)metadataSize * (uint)count > metadata.Length)
            throw new InvalidDataException("XWB metadata table is truncated.");

        var entries = new List<XwbEntry>(count);
        for (int i = 0; i < count; i++)
        {
            uint mini;
            uint entryFlags = 0;
            uint relativeOffset;
            uint length;
            int durationSamples;

            if (compact)
            {
                uint packed = ReadU32(data, checked((int)metadata.Offset + i * metadataSize));
                relativeOffset = (packed & 0x1FFFFF) * (uint)alignment;
                uint deviation = packed >> 21;
                uint nextOffset = i + 1 < count
                    ? (ReadU32(data, checked((int)metadata.Offset + (i + 1) * metadataSize)) & 0x1FFFFF) * (uint)alignment
                    : waves.Length;
                if (nextOffset < relativeOffset + deviation)
                    throw new InvalidDataException($"Invalid compact XWB entry {i}.");
                length = nextOffset - relativeOffset - deviation;
                mini = compactFormat;
                durationSamples = CalculatePcmDuration(length, mini);
            }
            else
            {
                int entry = checked((int)metadata.Offset + i * metadataSize);
                uint flagsAndDuration = ReadU32(data, entry);
                entryFlags = flagsAndDuration & 0xF;
                durationSamples = checked((int)(flagsAndDuration >> 4));
                mini = ReadU32(data, entry + 4);
                relativeOffset = ReadU32(data, entry + 8);
                length = ReadU32(data, entry + 12);
            }

            int tag = (int)(mini & 3);
            int channels = (int)((mini >> 2) & 7);
            int sampleRate = (int)((mini >> 5) & 0x3FFFF);
            int blockAlign = (int)((mini >> 23) & 0xFF);
            int bits = ((mini >> 31) & 1) != 0 ? 16 : 8;
            if (tag != 0)
                throw new NotSupportedException($"XWB entry {i} uses codec tag {tag}; this player currently supports PCM WaveBanks.");
            if (channels <= 0 || sampleRate <= 0 || blockAlign <= 0)
                throw new InvalidDataException($"XWB entry {i} has an invalid PCM format.");
            if ((ulong)relativeOffset + length > waves.Length)
            {
                // A few shipped streaming banks omit their final sector padding
                // and consequently leave the last entry a handful of bytes
                // beyond physical EOF. Keep the available PCM data, but never
                // relax range checks for an interior entry or invalid offset.
                if (i == count - 1 && relativeOffset < waves.Length)
                    length = waves.Length - relativeOffset;
                else
                    throw new InvalidDataException($"XWB entry {i} exceeds the wave-data segment.");
            }

            int absolute = checked((int)(waves.Offset + relativeOffset));
            if (durationSamples <= 0)
                durationSamples = CalculatePcmDuration(length, mini);
            string name = nameSize > 0 && (ulong)(i + 1) * (uint)nameSize <= names.Length
                ? ReadCString(data, checked((int)names.Offset + i * nameSize), nameSize)
                : $"{i:D4}";

            entries.Add(new XwbEntry
            {
                Index = i,
                Name = string.IsNullOrWhiteSpace(name) ? $"{i:D4}" : name,
                Channels = channels,
                SampleRate = sampleRate,
                BitsPerSample = bits,
                DurationSamples = durationSamples,
                DataLength = checked((int)length),
                WaveFactory = () => BuildPcmWaveFromFile(path, absolute, checked((int)length), channels, sampleRate, bits, blockAlign)
                ,MiniFormat = mini,
                EntryFlags = entryFlags
            });
        }

        return new XwbBank
        {
            Name = string.IsNullOrWhiteSpace(bankName) ? Path.GetFileNameWithoutExtension(path) : bankName,
            Entries = entries,
            SourcePath = path,
            IsCompact = compact,
            MetadataOffset = checked((int)metadata.Offset),
            MetadataSize = metadataSize,
            WaveOffset = checked((int)waves.Offset)
        };
    }

    public static void Save(XwbBank bank, string outputPath, IReadOnlyDictionary<int, byte[]> replacements)
    {
        if (bank.IsCompact && replacements.Count > 0)
            throw new NotSupportedException("Replacing entries in compact XWB banks is not supported yet.");

        string fullOutput = Path.GetFullPath(outputPath);
        string fullSource = Path.GetFullPath(bank.SourcePath);
        bool replaceOriginal = string.Equals(fullOutput, fullSource, StringComparison.OrdinalIgnoreCase);
        string writePath = replaceOriginal
            ? fullOutput + ".toolhazard.tmp"
            : fullOutput;

        try
        {
            using var source = new FileStream(fullSource, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var output = new FileStream(writePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            CopyBytes(source, output, bank.WaveOffset);

            uint relativeOffset = 0;
            foreach (XwbEntry entry in bank.Entries.OrderBy(x => x.Index))
            {
                byte[] pcm;
                uint mini = entry.MiniFormat;
                int duration;
                if (replacements.TryGetValue(entry.Index, out byte[]? wave))
                    (pcm, mini, duration) = ReadPcmWave(wave);
                else
                {
                    byte[] originalWave = entry.GetWaveData();
                    pcm = originalWave.AsSpan(44).ToArray();
                    duration = entry.DurationSamples;
                }

                output.Position = (long)bank.WaveOffset + relativeOffset;
                output.Write(pcm);

                long metadata = bank.MetadataOffset + (long)entry.Index * bank.MetadataSize;
                output.Position = metadata;
                WriteU32(output, checked((uint)duration << 4) | entry.EntryFlags);
                WriteU32(output, mini);
                WriteU32(output, relativeOffset);
                WriteU32(output, checked((uint)pcm.Length));
                relativeOffset = checked(relativeOffset + (uint)pcm.Length);
            }

            output.Position = 48;
            WriteU32(output, relativeOffset);
            output.SetLength((long)bank.WaveOffset + relativeOffset);
            output.Flush(true);
        }
        catch
        {
            if (File.Exists(writePath))
                File.Delete(writePath);
            throw;
        }

        if (replaceOriginal)
            File.Move(writePath, fullOutput, true);
    }

    private static (byte[] Pcm, uint Mini, int Duration) ReadPcmWave(byte[] wave)
    {
        using var stream = new MemoryStream(wave, false);
        using var reader = new BinaryReader(stream, Encoding.ASCII, true);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
            throw new InvalidDataException("Replacement is not a RIFF WAV file.");
        reader.ReadUInt32();
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
            throw new InvalidDataException("Replacement is not a WAVE file.");

        ushort format = 0, channels = 0, bits = 0, blockAlign = 0;
        uint sampleRate = 0;
        byte[]? pcm = null;
        while (stream.Position + 8 <= stream.Length)
        {
            string id = Encoding.ASCII.GetString(reader.ReadBytes(4));
            uint size = reader.ReadUInt32();
            long end = checked(stream.Position + size);
            if (end > stream.Length) throw new InvalidDataException("Replacement WAV contains a truncated chunk.");
            if (id == "fmt ")
            {
                if (size < 16) throw new InvalidDataException("Replacement WAV format chunk is too small.");
                format = reader.ReadUInt16(); channels = reader.ReadUInt16(); sampleRate = reader.ReadUInt32();
                reader.ReadUInt32(); blockAlign = reader.ReadUInt16(); bits = reader.ReadUInt16();
            }
            else if (id == "data") pcm = reader.ReadBytes(checked((int)size));
            stream.Position = Math.Min(stream.Length, end + (size & 1));
        }
        if (format != 1 || channels is < 1 or > 7 || sampleRate is 0 or > 0x3FFFF ||
            bits is not (8 or 16) || blockAlign == 0 || blockAlign > byte.MaxValue || pcm == null)
            throw new InvalidDataException("XWB replacement must be 8-bit or 16-bit PCM WAV with 1-7 channels and a supported sample rate.");

        uint mini = ((uint)(bits == 16 ? 1 : 0) << 31) | ((uint)blockAlign << 23) |
                    (sampleRate << 5) | ((uint)channels << 2);
        return (pcm, mini, pcm.Length / blockAlign);
    }

    private static void CopyBytes(Stream source, Stream output, int count)
    {
        byte[] buffer = new byte[81920];
        int remaining = count;
        while (remaining > 0)
        {
            int read = source.Read(buffer, 0, Math.Min(buffer.Length, remaining));
            if (read == 0) throw new EndOfStreamException();
            output.Write(buffer, 0, read);
            remaining -= read;
        }
    }

    private static void WriteU32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BitConverter.TryWriteBytes(bytes, value);
        stream.Write(bytes);
    }

    private static int CalculatePcmDuration(uint length, uint mini)
    {
        int channels = (int)((mini >> 2) & 7);
        int bits = ((mini >> 31) & 1) != 0 ? 16 : 8;
        int bytesPerSample = channels * bits / 8;
        return bytesPerSample > 0 ? checked((int)(length / bytesPerSample)) : 0;
    }

    private static byte[] BuildPcmWaveFromFile(string path, int offset, int length, int channels, int sampleRate, int bits, int blockAlign)
    {
        byte[] pcm = new byte[length];
        using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            input.Position = offset;
            input.ReadExactly(pcm);
        }
        using var output = new MemoryStream(44 + pcm.Length);
        using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcm.Length);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);
        writer.Write((ushort)blockAlign);
        writer.Write((ushort)bits);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(pcm.Length);
        writer.Write(pcm);
        return output.ToArray();
    }

    private static void ValidateRange(long fileLength, uint offset, uint length, string label)
    {
        if ((ulong)offset + length > (ulong)fileLength)
            throw new InvalidDataException($"XWB {label} exceeds the file.");
    }

    private static uint ReadU32(byte[] data, int offset)
    {
        if (offset < 0 || offset + 4 > data.Length)
            throw new InvalidDataException("Unexpected end of XWB file.");
        return BitConverter.ToUInt32(data, offset);
    }

    private static string ReadCString(byte[] data, int offset, int length)
    {
        int safeLength = Math.Min(length, data.Length - offset);
        if (offset < 0 || safeLength < 0)
            return string.Empty;
        int end = Array.IndexOf(data, (byte)0, offset, safeLength);
        if (end < 0) end = offset + safeLength;
        return Encoding.ASCII.GetString(data, offset, end - offset);
    }
}

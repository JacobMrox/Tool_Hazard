namespace Tool_Hazard.Biohazard.RE4;

public enum TplVariant
{
    UhdReferenceTable,
    NintendoEmbedded
}

public sealed class TplEntryInfo
{
    public int Index { get; set; }
    public ushort Width { get; set; }
    public ushort Height { get; set; }
    public uint PixelFormat { get; set; }
    public uint PackId { get; set; }
    public uint TextureId { get; set; }
    public bool HasPalette { get; set; }
    internal int ImageHeaderOffset { get; set; }
    internal int ReferenceOffset { get; set; }
}

public sealed class TplDocument
{
    public required string SourcePath { get; init; }
    public required TplVariant Variant { get; init; }
    public required byte[] OriginalData { get; init; }
    public required List<TplEntryInfo> Entries { get; init; }
}

/// <summary>
/// Parses RE4 UHD reference TPLs and Nintendo GC/Wii embedded TPL headers.
/// UHD files can be edited and saved without rewriting unrelated bytes.
/// </summary>
public static class TplExplorerService
{
    private const uint UhdMagic = 0x12345678;
    private const uint UhdAlternateMagic = 0x78563412;
    private const uint NintendoMagic = 0x0020AF30;

    public static TplDocument Load(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        if (data.Length < 12)
            throw new InvalidDataException("TPL file is too small.");

        uint littleEndianMagic = ReadLe32(data, 0);
        if (littleEndianMagic == UhdMagic || littleEndianMagic == UhdAlternateMagic)
            return LoadUhd(path, data);
        if (ReadBe32(data, 0) == NintendoMagic)
            return LoadNintendo(path, data);
        throw new InvalidDataException("Unknown TPL signature. Expected RE4 UHD 78 56 34 12 / 12 34 56 78 or Nintendo 00 20 AF 30.");
    }

    public static void SaveUhd(TplDocument document, string outputPath)
    {
        if (document.Variant != TplVariant.UhdReferenceTable)
            throw new NotSupportedException("Nintendo embedded TPL writing is not enabled yet.");

        byte[] data = (byte[])document.OriginalData.Clone();
        foreach (TplEntryInfo entry in document.Entries)
        {
            WriteLe16(data, entry.ImageHeaderOffset, entry.Height);
            WriteLe16(data, entry.ImageHeaderOffset + 2, entry.Width);
            WriteLe32(data, entry.ImageHeaderOffset + 4, entry.PixelFormat);
            WriteLe32(data, entry.ReferenceOffset, entry.PackId);
            WriteLe32(data, entry.ReferenceOffset + 4, entry.TextureId);
        }
        File.WriteAllBytes(outputPath, data);
    }

    private static TplDocument LoadUhd(string path, byte[] data)
    {
        uint count = ReadLe32(data, 4);
        uint tableOffset = ReadLe32(data, 8);
        if (count > 0x10000 || (ulong)tableOffset + count * 8UL > (ulong)data.Length)
            throw new InvalidDataException("Invalid UHD TPL entry table.");

        var entries = new List<TplEntryInfo>(checked((int)count));
        for (int i = 0; i < count; i++)
        {
            int table = checked((int)tableOffset + i * 8);
            int image = checked((int)ReadLe32(data, table));
            int palette = checked((int)ReadLe32(data, table + 4));
            if (image < 0 || image + 36 > data.Length)
                throw new InvalidDataException($"UHD TPL image header {i} is outside the file.");
            int reference = checked((int)ReadLe32(data, image + 8));
            if (reference < 0 || reference + 8 > data.Length)
                throw new InvalidDataException($"UHD TPL reference {i} is outside the file.");

            entries.Add(new TplEntryInfo
            {
                Index = i,
                Height = ReadLe16(data, image),
                Width = ReadLe16(data, image + 2),
                PixelFormat = ReadLe32(data, image + 4),
                PackId = ReadLe32(data, reference),
                TextureId = ReadLe32(data, reference + 4),
                HasPalette = palette != 0,
                ImageHeaderOffset = image,
                ReferenceOffset = reference
            });
        }

        return new TplDocument { SourcePath = path, Variant = TplVariant.UhdReferenceTable, OriginalData = data, Entries = entries };
    }

    private static TplDocument LoadNintendo(string path, byte[] data)
    {
        uint count = ReadBe32(data, 4);
        uint tableOffset = ReadBe32(data, 8);
        if (count > 0x10000 || (ulong)tableOffset + count * 8UL > (ulong)data.Length)
            throw new InvalidDataException("Invalid Nintendo TPL entry table.");

        var entries = new List<TplEntryInfo>(checked((int)count));
        for (int i = 0; i < count; i++)
        {
            int table = checked((int)tableOffset + i * 8);
            int image = checked((int)ReadBe32(data, table));
            int palette = checked((int)ReadBe32(data, table + 4));
            if (image < 0 || image + 12 > data.Length)
                throw new InvalidDataException($"Nintendo TPL image header {i} is outside the file.");
            uint imageData = ReadBe32(data, image + 8);
            if (imageData >= data.Length)
                throw new InvalidDataException($"Nintendo TPL image data {i} is outside the file.");

            entries.Add(new TplEntryInfo
            {
                Index = i,
                Height = ReadBe16(data, image),
                Width = ReadBe16(data, image + 2),
                PixelFormat = ReadBe32(data, image + 4),
                HasPalette = palette != 0,
                ImageHeaderOffset = image
            });
        }

        return new TplDocument { SourcePath = path, Variant = TplVariant.NintendoEmbedded, OriginalData = data, Entries = entries };
    }

    private static ushort ReadLe16(byte[] data, int offset) => BitConverter.ToUInt16(data, offset);
    private static uint ReadLe32(byte[] data, int offset) => BitConverter.ToUInt32(data, offset);
    private static ushort ReadBe16(byte[] data, int offset) => (ushort)((data[offset] << 8) | data[offset + 1]);
    private static uint ReadBe32(byte[] data, int offset) => ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
    private static void WriteLe16(byte[] data, int offset, ushort value) => BitConverter.GetBytes(value).CopyTo(data, offset);
    private static void WriteLe32(byte[] data, int offset, uint value) => BitConverter.GetBytes(value).CopyTo(data, offset);
}

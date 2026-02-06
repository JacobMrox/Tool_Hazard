using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

public static class BorlandResReader
{
    public static byte[] ReadPalette512FromEmbeddedRes(string embeddedResName, string resourceEntryName = "PALETTE")
    {
        using Stream? s = Assembly.GetExecutingAssembly().GetManifestResourceStream(embeddedResName);
        if (s == null)
            throw new FileNotFoundException($"Embedded resource not found: {embeddedResName}");

        using var br = new BinaryReader(s);

        while (br.BaseStream.Position + 8 <= br.BaseStream.Length)
        {
            uint dataSize = br.ReadUInt32();
            uint headerSize = br.ReadUInt32();

            if (dataSize == 0 && headerSize == 0)
                break;

            long headerStart = br.BaseStream.Position - 8;

            var type = ReadNameOrId(br);
            var name = ReadNameOrId(br);

            Align4(br);

            // skip fixed header tail (16 bytes)
            br.ReadUInt32(); // DataVersion
            br.ReadUInt16(); // MemoryFlags
            br.ReadUInt16(); // LanguageId
            br.ReadUInt32(); // Version
            br.ReadUInt32(); // Characteristics

            // jump to end of header
            br.BaseStream.Position = headerStart + headerSize;

            byte[] data = br.ReadBytes((int)dataSize);
            Align4(br);

            if (type.kind == "str" && type.str == "DATA" &&
                name.kind == "str" && string.Equals(name.str, resourceEntryName, StringComparison.OrdinalIgnoreCase))
            {
                if (data.Length != 512)
                    throw new InvalidDataException($"PALETTE entry was {data.Length} bytes; expected 512.");

                return data;
            }
        }

        throw new InvalidDataException($"DATA/{resourceEntryName} not found in .res.");
    }

    private static (string kind, string? str, ushort id) ReadNameOrId(BinaryReader br)
    {
        ushort first = br.ReadUInt16();
        if (first == 0xFFFF)
        {
            ushort id = br.ReadUInt16();
            return ("id", null, id);
        }

        // it's a UTF-16LE null-terminated string; first is first character
        using var ms = new MemoryStream();
        ms.Write(BitConverter.GetBytes(first));

        while (true)
        {
            ushort c = br.ReadUInt16();
            ms.Write(BitConverter.GetBytes(c));
            if (c == 0) break;
        }

        string s = Encoding.Unicode.GetString(ms.ToArray()).TrimEnd('\0');
        return ("str", s, 0);
    }

    private static void Align4(BinaryReader br)
    {
        long pos = br.BaseStream.Position;
        long pad = (4 - (pos & 3)) & 3;
        if (pad != 0) br.BaseStream.Position += pad;
    }
}

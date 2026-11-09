// RE4 TPL reference tables — adapted from JADERLINK_MODEL_VIEWER (MIT)
// ALL_VIEWER/SHARED_UHD_BIN_TPL/UhdTplDecoder.cs + UhdTPL.cs
// Extends the existing Tool Hazard TplExplorerService (RE4 game table) with
// big-endian + PS4/Switch wide-offset variants so every BIN platform can link
// textures through TPL PackID/TextureID pairs.
using System;
using System.Collections.Generic;
using System.IO;

namespace Tool_Hazard.Biohazard.RE4
{
    public enum Re4TplKind { UhdLittleEndian, UhdBigEndian, Ps4SwitchWide, NintendoEmbedded }

    public sealed class Re4TplEntry
    {
        public int Index;
        public ushort Width;
        public ushort Height;
        public uint PixelFormatType;
        public uint PackId;
        public uint TextureId;
        public bool HasPalette;
        public ushort ColorsCount;
        public uint PaletteFormatType;
        public uint WrapS = 1, WrapT = 1, MinFilter = 1, MagFilter = 1;
        public float LodBias;
        public byte EnableLod, MinLod, MaxLod, IsCompressed;
        internal int ImageHeaderOffset;
        internal int ReferenceOffset;
        internal int PaletteOffset;
        public string TextureKey => $"{PackId:X8}/{TextureId:D4}";
    }

    public sealed class Re4TplDocument
    {
        public string SourcePath = "";
        public Re4TplKind Kind;
        public Re4Endian Endian;
        public bool IsPs4Switch;
        public byte[] RawBytes = Array.Empty<byte>();
        public List<Re4TplEntry> Entries = new();
        public string Description = "";
    }

    public static class Re4TplModel
    {
        public static Re4TplDocument Load(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            return Decode(data, path);
        }

        public static Re4TplDocument Decode(byte[] data, string? name = null)
        {
            if (data.Length < 12) throw new InvalidDataException("TPL file is too small.");
            // Nintendo embedded has BE magic 00 20 AF 30; UHD magics 78 56 34 12 / 12 34 56 78.
            if (Be32(data, 0) == 0x0020AF30u)
            {
                // Defer to the existing table explorer (header-only view, data embedded).
                // Here we surface the same header fields plus Pack/Texture as 0 (embedded pixels).
                uint count = Be32(data, 4); uint tableOff = Be32(data, 8);
                if (count > 0x10000 || (ulong)tableOff + count * 8UL > (ulong)data.Length) throw new InvalidDataException("Invalid Nintendo TPL entry table.");
                var doc = new Re4TplDocument { SourcePath = name ?? "", Kind = Re4TplKind.NintendoEmbedded, Endian = Re4Endian.Big, RawBytes = data, Description = "RE4 GC/Wii embedded TPL" };
                for (int i = 0; i < count; i++)
                {
                    int table = (int)tableOff + i * 8;
                    int image = (int)Be32(data, table); int palette = (int)Be32(data, table + 4);
                    if (image < 0 || image + 12 > data.Length) throw new InvalidDataException($"Nintendo TPL image header {i} is outside the file.");
                    doc.Entries.Add(new Re4TplEntry { Index = i, Height = Be16(data, image), Width = Be16(data, image + 2), PixelFormatType = Be32(data, image + 4), HasPalette = palette != 0, ImageHeaderOffset = image, PaletteOffset = palette });
                }
                return doc;
            }
            uint leMagic = Le32(data, 0);
            if (leMagic != 0x78563412u && leMagic != 0x12345678u)
                throw new InvalidDataException("Unknown TPL signature. Expected UHD 78 56 34 12 / 12 34 56 78 or Nintendo 00 20 AF 30.");
            // PS4/Switch wide entries use 64-bit offsets. Detect by trying wide parse:
            // the wide table is 16 bytes per entry; if 8-byte parse goes out of range, retry wide.
            bool wide = IsWideTable(data, leMagic);
            var doc2 = new Re4TplDocument { SourcePath = name ?? "", Kind = wide ? Re4TplKind.Ps4SwitchWide : Re4TplKind.UhdLittleEndian, Endian = Re4Endian.Little, IsPs4Switch = wide, RawBytes = data, Description = wide ? "RE4 PS4/Switch TPL (wide)" : "RE4 UHD TPL" };
            // UHD TPL is normally LE; X360/PS3 twins are BE — try LE first, fall back to BE.
            try { ParseUhd(data, doc2, Re4Endian.Little, wide); return doc2; }
            catch (InvalidDataException)
            {
                if (wide) throw;
                var beDoc = new Re4TplDocument { SourcePath = name ?? "", Kind = Re4TplKind.UhdBigEndian, Endian = Re4Endian.Big, RawBytes = data, Description = "RE4 X360/PS3 TPL (BE)" };
                ParseUhd(data, beDoc, Re4Endian.Big, false);
                return beDoc;
            }
        }

        private static bool IsWideTable(byte[] data, uint magic)
        {
            // Wide tables store (uint64 image, uint64 palette). Validate first entry fits and magic sanity.
            try
            {
                uint count = Le32(data, 4); uint tableOff = Le32(data, 8);
                if (count == 0 || count > 0x10000) return false;
                if ((ulong)tableOff + count * 16UL <= (ulong)data.Length)
                {
                    uint imgLo = Le32(data, (int)tableOff);
                    uint palLo = Le32(data, (int)tableOff + 8);
                    if (imgLo > 0 && imgLo < data.Length && (palLo == 0 || palLo < data.Length)) return true;
                }
            }
            catch { }
            return false;
        }

        private static void ParseUhd(byte[] data, Re4TplDocument doc, Re4Endian e, bool wide)
        {
            uint count = e == Re4Endian.Little ? Le32(data, 4) : Be32(data, 4);
            uint tableOff = e == Re4Endian.Little ? Le32(data, 8) : Be32(data, 8);
            int stride = wide ? 16 : 8;
            if (count > 0x10000 || (ulong)tableOff + count * (ulong)stride > (ulong)data.Length)
                throw new InvalidDataException("Invalid UHD TPL entry table.");
            doc.Entries.Clear();
            for (int i = 0; i < count; i++)
            {
                int table = (int)tableOff + i * stride;
                int image = (int)(wide ? Le32(data, table) : (e == Re4Endian.Little ? Le32(data, table) : Be32(data, table)));
                int palette = (int)(wide ? Le32(data, table + 8) : (e == Re4Endian.Little ? Le32(data, table + 4) : Be32(data, table + 4)));
                if (image < 0 || image + 36 > data.Length) throw new InvalidDataException($"UHD TPL image header {i} is outside the file.");
                var ent = new Re4TplEntry { Index = i, ImageHeaderOffset = image, PaletteOffset = palette };
                if (palette != 0)
                {
                    ent.HasPalette = true;
                    ent.ColorsCount = e == Re4Endian.Little ? Le16(data, palette) : Be16(data, palette);
                    ent.PaletteFormatType = e == Re4Endian.Little ? Le32(data, palette + 4) : Be32(data, palette + 4);
                }
                int c = image;
                ent.Height = e == Re4Endian.Little ? Le16(data, c) : Be16(data, c); c += 2;
                ent.Width = e == Re4Endian.Little ? Le16(data, c) : Be16(data, c); c += 2;
                ent.PixelFormatType = e == Re4Endian.Little ? Le32(data, c) : Be32(data, c); c += 4;
                int refOff = (int)(wide ? Le32(data, c) : (e == Re4Endian.Little ? Le32(data, c) : Be32(data, c)));
                if (wide) c += 4; // high word of 64-bit offset
                ent.WrapS = e == Re4Endian.Little ? Le32(data, c + 4) : Be32(data, c + 4);
                // Wrap/filter fields may exceed header if wide offsets present; guard.
                if (c + 36 <= data.Length)
                {
                    ent.WrapS = Rd(data, c + 4, e); ent.WrapT = Rd(data, c + 8, e); ent.MinFilter = Rd(data, c + 12, e); ent.MagFilter = Rd(data, c + 16, e);
                    ent.LodBias = RdF(data, c + 20, e);
                    ent.EnableLod = data[c + 24]; ent.MinLod = data[c + 25]; ent.MaxLod = data[c + 26]; ent.IsCompressed = data[c + 27];
                }
                if (refOff < 0 || refOff + 8 > data.Length) throw new InvalidDataException($"UHD TPL reference {i} is outside the file.");
                ent.ReferenceOffset = refOff;
                ent.PackId = e == Re4Endian.Little ? Le32(data, refOff) : Be32(data, refOff);
                ent.TextureId = e == Re4Endian.Little ? Le32(data, refOff + 4) : Be32(data, refOff + 4);
                doc.Entries.Add(ent);
            }
        }

        public static void Save(Re4TplDocument doc, string outputPath)
        {
            if (doc.Kind == Re4TplKind.NintendoEmbedded) throw new NotSupportedException("Nintendo embedded TPL export is not supported; UHD-family TPL only.");
            byte[] data = (byte[])doc.RawBytes.Clone();
            bool wide = doc.IsPs4Switch;
            foreach (var ent in doc.Entries)
            {
                int h = ent.ImageHeaderOffset;
                if (doc.Endian == Re4Endian.Little)
                {
                    BitConverter.GetBytes(ent.Height).CopyTo(data, h);
                    BitConverter.GetBytes(ent.Width).CopyTo(data, h + 2);
                    BitConverter.GetBytes(ent.PixelFormatType).CopyTo(data, h + 4);
                }
                else
                {
                    WriteBe16(data, h, ent.Height); WriteBe16(data, h + 2, ent.Width); WriteBe32(data, h + 4, ent.PixelFormatType);
                }
                int r = ent.ReferenceOffset;
                if (doc.Endian == Re4Endian.Little)
                {
                    BitConverter.GetBytes(ent.PackId).CopyTo(data, r);
                    BitConverter.GetBytes(ent.TextureId).CopyTo(data, r + 4);
                }
                else
                {
                    WriteBe32(data, r, ent.PackId); WriteBe32(data, r + 4, ent.TextureId);
                }
            }
            _ = wide;
            File.WriteAllBytes(outputPath, data);
        }

        private static ushort Le16(byte[] d, int o) => BitConverter.ToUInt16(d, o);
        private static uint Le32(byte[] d, int o) => BitConverter.ToUInt32(d, o);
        private static ushort Be16(byte[] d, int o) => (ushort)((d[o] << 8) | d[o + 1]);
        private static uint Be32(byte[] d, int o) => ((uint)d[o] << 24) | ((uint)d[o + 1] << 16) | ((uint)d[o + 2] << 8) | d[o + 3];
        private static uint Rd(byte[] d, int o, Re4Endian e) => e == Re4Endian.Little ? Le32(d, o) : Be32(d, o);
        private static float RdF(byte[] d, int o, Re4Endian e)
        {
            uint u = Rd(d, o, e); return BitConverter.ToSingle(BitConverter.GetBytes(u), 0);
        }
        private static void WriteBe16(byte[] d, int o, ushort v) { d[o] = (byte)(v >> 8); d[o + 1] = (byte)v; }
        private static void WriteBe32(byte[] d, int o, uint v) { d[o] = (byte)(v >> 24); d[o + 1] = (byte)(v >> 16); d[o + 2] = (byte)(v >> 8); d[o + 3] = (byte)v; }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.Extensions;

namespace IntelOrca.Biohazard
{
    public readonly struct Tim2
    {
        public ReadOnlyMemory<byte> Data { get; }

        public Tim2(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }

        public int Magic => Data.GetSafeSpan<int>(0, 1)[0];
        public byte Revision => Data.Span[4];
        public byte Format => Data.Span[5];
        public short PictureCount => Data.GetSafeSpan<short>(6, 1)[0];
        public uint Reserved1 => Data.GetSafeSpan<uint>(8, 1)[0];
        public uint Reserved2 => Data.GetSafeSpan<uint>(12, 1)[0];

        private int GetPictureOffset(int index)
        {
            var headerEnd = Format > 0 ? 0x80 : 0x10;
            return headerEnd;
        }

        public Picture GetPicture(int index)
        {
            var offset = GetPictureOffset(index);
            var header = MemoryMarshal.Cast<byte, PictureHeader>(Data.Span[offset..])[0];
            var totalSize = header.TotalSize;
            return new Picture(Data.Slice(offset, totalSize));
        }

        public PictureHeader GetHeader(int index)
        {
            var offset = GetPictureOffset(index);
            var header = MemoryMarshal.Cast<byte, PictureHeader>(Data.Span[offset..])[0];
            return header;
        }

        public ReadOnlyMemory<byte> GetPixelData(int index)
        {
            var offset = GetPictureOffset(index);
            var header = GetHeader(index);
            var pixelOffset = offset + header.HeaderSize;
            var pixelLength = header.ImageSize;
            return Data.Slice(pixelOffset, pixelLength);
        }

        public ReadOnlyMemory<byte> GetPaletteData(int index)
        {
            var offset = GetPictureOffset(index);
            var header = GetHeader(index);
            var paletteOffset = offset + header.HeaderSize + header.ImageSize;
            var paletteLength = header.ClutSize;
            return Data.Slice(paletteOffset, paletteLength);
        }

        public Picture Picture0 => GetPicture(0);

        public Builder ToBuilder()
        {
            var builder = new Builder();
            builder.Reserved1 = Reserved1;
            builder.Reserved2 = Reserved2;
            for (var i = 0; i < PictureCount; i++)
            {
                builder.Pictures.Add(GetPicture(i));
            }
            return builder;
        }

        public class Builder
        {
            public uint Reserved1 { get; set; }
            public uint Reserved2 { get; set; }
            public List<Picture> Pictures { get; } = new List<Picture>();

            public Tim2 ToTim2()
            {
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                bw.Write(0x324D4954);
                bw.Write((byte)4);
                bw.Write((byte)1);
                bw.Write((ushort)Pictures.Count);
                bw.Write(Reserved1);
                bw.Write(Reserved2);

                bw.Seek(0x80, SeekOrigin.Begin);
                foreach (var picture in Pictures)
                {
                    bw.Write(picture.Data);
                }
                return new Tim2(ms.ToArray());
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PictureHeader
        {
            public int TotalSize;
            public int ClutSize;
            public int ImageSize;
            public short HeaderSize;
            public short ColoursPerClut;
            public byte Format;
            public byte MipmapCount;
            public byte ClutColourType;
            public byte ImageColourType;
            public short Width;
            public short Height;
            public long GsTex0;
            public long GsTex1;
            public int GsFlags;
            public int GsClut;
        }

        public readonly struct Picture
        {
            public ReadOnlyMemory<byte> Data { get; }

            private readonly PictureHeader _header;
            private readonly ReadOnlyMemory<byte> _paletteData;
            private readonly ReadOnlyMemory<byte> _pixelData;
            private readonly ReadOnlyMemory<byte> _fixedPalette;

            public Picture(ReadOnlyMemory<byte> data)
            {
                Data = data;

                _header = MemoryMarshal.Cast<byte, PictureHeader>(Data.Span)[0];
                _paletteData = Data.Slice(_header.HeaderSize + _header.ImageSize, _header.ClutSize);
                _pixelData = Data.Slice(_header.HeaderSize, _header.ImageSize);
                _fixedPalette = (_header.ClutColourType & 0x80) == 0 && _header.ImageColourType == 5 ?
                    FixPalette(_paletteData.Span) :
                    _paletteData;
            }

            public PictureHeader Header => _header;
            public int Width => _header.Width;
            public int Height => _header.Height;
            public ReadOnlyMemory<byte> PixelData => _pixelData;
            public int Depth => _header.ImageColourType == 5 ? 8 : 4;

            public int GetColour(int paletteIndex)
            {
                var palette = MemoryMarshal.Cast<byte, int>(_fixedPalette.Span);
                return palette[paletteIndex];
            }

            public byte GetPixelRaw(int x, int y)
            {
                ref readonly var header = ref _header;
                var pixelData = _pixelData.Span;
                if (header.ImageColourType == 5)
                {
                    return pixelData[y * header.Width + x];
                }
                else
                {
                    var byteIndex = y * (header.Width / 2) + (x / 2);
                    var bb = pixelData[byteIndex];
                    if ((x & 1) == 0)
                        return (byte)(bb & 0x0F);
                    else
                        return (byte)(bb >> 4);
                }
            }

            public int GetPixel(int x, int y)
            {
                var raw = GetPixelRaw(x, y);
                return Rgba2Argb(GetColour(raw));
            }

            private static ReadOnlyMemory<byte> FixPalette(ReadOnlySpan<byte> paletteData)
            {
                var colours = MemoryMarshal.Cast<byte, int>(paletteData);
                var resultData = new byte[colours.Length * 4];
                var result = MemoryMarshal.Cast<byte, int>(resultData);
                var parts = colours.Length / 32;
                var stripes = 2;
                var colors = 8;
                var blocks = 2;
                var startIndex = 0;
                var i = 0;
                for (var part = 0; part < parts; part++)
                {
                    for (var block = 0; block < blocks; block++)
                    {
                        for (var stripe = 0; stripe < stripes; stripe++)
                        {
                            for (var color = 0; color < colors; color++)
                            {
                                result[i++] = colours[startIndex + part * colors * stripes * blocks + block * colors + stripe * stripes * colors + color];
                            }
                        }
                    }
                }
                return resultData;
            }

            private static int Rgba2Argb(int value)
            {
                int r = (value & 0xFF);
                int g = (value >> 8) & 0xFF;
                int b = (value >> 16) & 0xFF;
                int a = (value >> 24) & 0xFF;
                return (a << 24) | (r << 16) | (g << 8) | b;
            }

            private static int Argb2Rgba(int value)
            {
                int a = (value >> 24) & 0xFF;
                int r = (value >> 16) & 0xFF;
                int g = (value >> 8) & 0xFF;
                int b = (value & 0xFF);
                return (a << 24) | (b << 16) | (g << 8) | r;
            }

            public int[] GetArgb()
            {
                var result = new int[Width * Height];
                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x++)
                    {
                        result[y * Width + x] = GetPixel(x, y);
                    }
                }
                return result;
            }

            public Bmp ToBmp() => Bmp.FromArgb(Width, Height, GetArgb());

            public Builder ToBuilder() => new Builder(_header, _paletteData.ToArray(), _pixelData.ToArray());

            public class Builder
            {
                public PictureHeader Header { get; }
                public int Width => Header.Width;
                public int Height => Header.Height;
                public byte[] Palette { get; }
                public byte[] PixelData { get; }

                public Builder(PictureHeader header, byte[] palette, byte[] pixelData)
                {
                    Header = header;
                    Palette = palette;
                    PixelData = pixelData;
                }

                public void Import(uint[] argb)
                {
                    var paletteSize = 0;
                    var colours = new Dictionary<int, byte>();
                    var unusedColours = new HashSet<int>();
                    var pixelData = new byte[argb.Length];
                    for (var i = 0; i < argb.Length; i++)
                    {
                        var colour = Argb2Rgba((int)argb[i]);
                        if (!colours.TryGetValue(colour, out var paletteIndex))
                        {
                            if (paletteSize >= 256)
                            {
                                // Ran out of colours
                                paletteIndex = 0;
                                unusedColours.Add(colour);
                            }
                            else
                            {
                                paletteIndex = (byte)paletteSize;
                                paletteSize++;
                                colours.Add(colour, paletteIndex);
                            }
                        }
                        pixelData[i] = paletteIndex;
                    }

                    var palette = new int[256];
                    foreach (var kvp in colours)
                    {
                        palette[kvp.Value] = kvp.Key;
                    }
                    var paletteData = MemoryMarshal.Cast<int, byte>(palette);
                    var paletteData2 = FixPalette(paletteData).ToArray();

                    paletteData2.CopyTo(new Span<byte>(Palette));
                    pixelData.CopyTo(PixelData, 0);
                }

                public Picture ToPicture()
                {
                    var ms = new MemoryStream();
                    var bw = new BinaryWriter(ms);
                    bw.Write(Header);
                    bw.Seek(Header.HeaderSize, SeekOrigin.Begin);
                    bw.Write(PixelData);
                    bw.Write(Palette);
                    return new Picture(ms.ToArray());
                }
            }
        }
    }
}

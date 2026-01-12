using System;
using System.IO;
using IntelOrca.Biohazard.Extensions;

namespace IntelOrca.Biohazard
{
    public readonly struct Bmp
    {
        public ReadOnlyMemory<byte> Data { get; }

        public Bmp(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }

        public static Bmp FromArgb(int width, int height, ReadOnlySpan<int> argb)
        {
            var builder = new Builder(width, height, argb);
            return builder.ToBmp();
        }

        public int Width => Data.GetSafeSpan<int>(18, 1)[0];
        public int Height => Data.GetSafeSpan<int>(22, 1)[0];
        public int Stride => (((Width * 3) + 3) / 4) * 4;
        public int Padding => Stride - (Width * 3);

        public int[] GetArgb()
        {
            var result = new int[Width * Height];
            var padding = Padding;
            var pixelData = Data.Span.Slice(14 + 40);
            var i = 0;
            for (var y = Height - 1; y >= 0; y--)
            {
                for (var x = 0; x < Width; x++)
                {
                    var pixel = 0xFF << 24;
                    pixel |= pixelData[i++];
                    pixel |= pixelData[i++] << 8;
                    pixel |= pixelData[i++] << 16;
                    result[y * Width + x] = pixel;
                    i += padding;
                }
            }
            return result;
        }

        public Builder ToBuilder()
        {
            return new Builder(Width, Height, GetArgb());
        }

        public class Builder
        {
            public int Width { get; }
            public int Height { get; }
            public int[] Argb { get; }

            public Builder(int width, int height, ReadOnlySpan<int> argb)
            {
                Width = width;
                Height = height;
                Argb = argb.ToArray();
            }

            private int GetPixel(int x, int y)
            {
                return Argb[y * Width + x];
            }

            public Bmp ToBmp()
            {
                var stride = (((Width * 3) + 3) / 4) * 4;
                var padding = stride - (Width * 3);
                var rawDataLength = stride * Height;

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                // BMP header
                bw.Write((byte)0x42);
                bw.Write((byte)0x4D);
                bw.Write(0);
                bw.Write((ushort)0);
                bw.Write((ushort)0);
                bw.Write((uint)54);

                // DIB header
                bw.Write((uint)40);
                bw.Write((uint)Width);
                bw.Write((uint)Height);
                bw.Write((ushort)1);
                bw.Write((ushort)24);
                bw.Write((uint)0);
                bw.Write((uint)rawDataLength);
                bw.Write((uint)0x0B13); // DPI X
                bw.Write((uint)0x0B13); // DPI Y
                bw.Write((uint)0);
                bw.Write((uint)0);

                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x++)
                    {
                        var p = GetPixel(x, Height - y - 1);
                        bw.Write((byte)((p >> 0) & 0xFF));
                        bw.Write((byte)((p >> 8) & 0xFF));
                        bw.Write((byte)((p >> 16) & 0xFF));
                    }
                    for (var i = 0; i < padding; i++)
                    {
                        bw.Write(0);
                    }
                }

                ms.Position = 2;
                bw.Write((uint)ms.Length);

                return new Bmp(ms.ToArray());
            }
        }
    }
}

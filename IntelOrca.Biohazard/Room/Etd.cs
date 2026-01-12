using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.Extensions;

namespace IntelOrca.Biohazard.Room
{
    /// <summary>
    /// 4-bit texture data for RE 3 EFFs.
    /// </summary>
    public readonly struct Etd
    {
        public ReadOnlyMemory<byte> Data { get; }

        public Etd(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }

        public int ChunkCount => Data.GetSafeSpan<int>(0, 1)[0];

        public ReadOnlySpan<int> ChunkOffsets => Data.GetSafeSpan<int>(4, ChunkCount);

        public ReadOnlySpan<byte> GetChunk(int i)
        {
            var offsets = ChunkOffsets;
            var start = offsets[i];
            var end = i == offsets.Length - 1 ?
                Data.Length :
                offsets[i + 1];
            return Data.Slice(start, end - start).Span;
        }

        public int PaletteCount
        {
            get
            {
                if (ChunkCount == 0)
                    return 0;

                var paletteData = GetChunk(0);
                return MemoryMarshal.Cast<byte, ushort>(paletteData.Slice(6, 2))[0];
            }
        }

        public ReadOnlySpan<Palette> Palettes
        {
            get
            {
                if (ChunkCount == 0)
                    return ReadOnlySpan<Palette>.Empty;

                var paletteData = GetChunk(0);
                return MemoryMarshal.Cast<byte, Palette>(paletteData.Slice(8));
            }
        }

        public int PageCount => ChunkCount == 0 ? 0 : ChunkCount - 1;

        public ReadOnlySpan<byte> GetPage(int i) => GetChunk(1 + i).Slice(8);

        public Builder ToBuilder()
        {
            var builder = new Builder();
            builder.Palettes.AddRange(Palettes.ToArray());
            for (var i = 0; i < PageCount; i++)
            {
                builder.Pages.Add(GetPage(i).ToArray());
            }
            return builder;
        }

        public unsafe struct Palette
        {
            private fixed byte data[0x20];

            public ReadOnlySpan<byte> Data
            {
                get
                {
                    fixed (byte* b = data)
                    {
                        return new ReadOnlySpan<byte>(b, 0x20);
                    }
                }
            }
        }

        public class Builder
        {
            public List<Palette> Palettes { get; } = new List<Palette>();
            public List<byte[]> Pages { get; } = new List<byte[]>();

            public void AppendData(ReadOnlySpan<byte> paletteData, ReadOnlySpan<byte> pixelData)
            {
                var headers = MemoryMarshal.Cast<byte, Palette>(paletteData);
                Palettes.AddRange(headers.ToArray());
                if (Pages.Count == 0)
                {
                    Pages.Add(pixelData.ToArray());
                }
                else
                {
                    var lastPage = Pages[Pages.Count - 1];
                    Pages[Pages.Count - 1] = lastPage.Concat(pixelData.ToArray()).ToArray();
                }
            }

            public Etd ToEtd()
            {
                if (Palettes.Count == 0 && Pages.Count == 0)
                    return new Etd(ReadOnlyMemory<byte>.Empty);

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                var numChunks = 1 + Pages.Count;

                bw.Write(numChunks);
                ms.Position += numChunks * 4;

                var offsets = new List<int>();
                offsets.Add((int)ms.Position);
                bw.Write(0x01E00120);
                bw.Write((ushort)0x10);
                bw.Write((ushort)Palettes.Count);
                foreach (var palette in Palettes)
                {
                    bw.Write(palette);
                }

                var pageId = 0x03C0;
                foreach (var page in Pages)
                {
                    offsets.Add((int)ms.Position);
                    bw.Write(pageId);
                    bw.Write((ushort)0x40);
                    bw.Write((ushort)(page.Length / 128));
                    bw.Write(page);
                    pageId -= 64;
                }

                ms.Position = 4;
                foreach (var offset in offsets)
                {
                    bw.Write(offset);
                }

                return new Etd(ms.ToArray());
            }
        }
    }
}

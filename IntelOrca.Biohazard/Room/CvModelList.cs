using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.Extensions;

namespace IntelOrca.Biohazard.Room
{
    public readonly struct CvModelList
    {
        public int BaseOffset { get; }
        public ReadOnlyMemory<byte> Data { get; }

        public CvModelList(int baseOffset, ReadOnlyMemory<byte> data)
        {
            BaseOffset = baseOffset;
            Data = data;
        }

        public int PageCount
        {
            get
            {
                var offsets = MemoryMarshal.Cast<byte, int>(Data.Span);
                for (var i = 0; i < offsets.Length; i++)
                {
                    if (offsets[i] != 0)
                    {
                        var firstOffset = offsets[i];
                        return (firstOffset - BaseOffset) / 4;
                    }
                }
                return 0;
            }
        }

        public ReadOnlySpan<int> Offsets => Data.GetSafeSpan<int>(0, PageCount);

        public ReadOnlySpan<CvModelListPage> Pages
        {
            get
            {
                var offsets = Offsets;
                var realOffsets = new List<int>();
                for (var i = 0; i < offsets.Length; i++)
                {
                    if (offsets[i] != 0)
                    {
                        realOffsets.Add(offsets[i]);
                    }
                }
                realOffsets.Add(BaseOffset + Data.Span.Length);

                var pages = new CvModelListPage[PageCount];
                for (var i = 0; i < pages.Length; i++)
                {
                    var offset = Offsets[i];
                    if (offset != 0)
                    {
                        var endOffset = realOffsets.First(x => x > offset);
                        var length = endOffset - offset;
                        var data = Data.Slice(offset - BaseOffset, length);
                        pages[i] = new CvModelListPage(data);
                    }
                }
                return pages;
            }
        }

        public CvModelList WithNewBaseOffset(int baseOffset)
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            var originalOffsets = Offsets;
            for (var i = 0; i < PageCount; i++)
            {
                var offset = originalOffsets[i];
                if (offset != 0)
                    offset = baseOffset + (originalOffsets[i] - BaseOffset);
                bw.Write(offset);
            }

            bw.Write(Data[(PageCount * 4)..]);
            return new CvModelList(baseOffset, ms.ToArray());
        }

        public Builder ToBuilder()
        {
            var result = new Builder();
            result.BaseOffset = BaseOffset;
            result.Pages.AddRange(Pages.ToArray());
            return result;
        }

        public class Builder
        {
            public int BaseOffset { get; set; }
            public List<CvModelListPage> Pages { get; } = new List<CvModelListPage>();

            public CvModelList ToCvModelList()
            {
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                while (Pages.Count != 0 && Pages[^1].Data.Length == 0)
                {
                    Pages.RemoveAt(Pages.Count - 1);
                }
                while ((Pages.Count % 16) != 0)
                {
                    Pages.Add(new CvModelListPage());
                }

                var offset = BaseOffset + (Pages.Count * 4);
                foreach (var page in Pages)
                {
                    if (page.Data.Length == 0)
                    {
                        bw.Write(0);
                    }
                    else
                    {
                        bw.Write(offset);
                        offset += page.Data.Length;
                    }
                }

                foreach (var page in Pages)
                {
                    bw.Write(page.Data);
                }

                return new CvModelList(BaseOffset, ms.ToArray());
            }
        }
    }

    public readonly struct CvModelListPage
    {
        public ReadOnlyMemory<byte> Data { get; }

        public CvModelListPage(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }

        public int Count
        {
            get
            {
                if (Data.Length == 0)
                    return 0;

                var first4 = MemoryMarshal.Read<uint>(Data.Span);
                if ((first4 & 0xFFFFFF) == CvModel.MDL || first4 == CvModel.SKIN || first4 == CvModel.MASK)
                {
                    return 1;
                }

                var count = 0;
                var offset = 0;
                int len;
                while ((len = MemoryMarshal.Read<int>(Data.Span[offset..])) > 0)
                {
                    offset += len + 4;
                    count++;
                }

                return count;
            }
        }

        public ReadOnlyMemory<CvModel> Models
        {
            get
            {
                var result = new CvModel[Count];
                for (var i = 0; i < result.Length; i++)
                {
                    result[i] = GetModel(i);
                }
                return result;
            }
        }

        public CvModel GetModel(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            var first4 = MemoryMarshal.Read<uint>(Data.Span);
            if ((first4 & 0xFFFFFF) == CvModel.MDL || first4 == CvModel.SKIN || first4 == CvModel.MASK)
            {
                return new CvModel(Data);
            }

            var count = 0;
            var offset = 0;
            int len;
            while ((len = MemoryMarshal.Read<int>(Data.Span[offset..])) != 0)
            {
                offset += 4;
                if (index == count)
                {
                    return new CvModel(Data.Slice(offset, len));
                }
                offset += len;
                count++;
            }

            throw new InvalidDataException();
        }

        public Builder ToBuilder()
        {
            var result = new Builder();
            var count = Count;
            for (var i = 0; i < count; i++)
            {
                result.Models.Add(GetModel(i));
            }
            return result;
        }

        public class Builder
        {
            public List<CvModel> Models { get; } = new List<CvModel>();

            public CvModelListPage ToCvModelListPage()
            {
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                if (Models.Count == 0)
                {
                }
                else if (Models.Count == 1)
                {
                    bw.Write(Models[0].Data);
                }
                else
                {
                    for (var i = 0; i < Models.Count; i++)
                    {
                        bw.Write(Models[i].Data.Length);
                        bw.Write(Models[i].Data);
                    }
                    bw.Write(-1);
                    while ((ms.Length % 64) != 0)
                    {
                        bw.Write((byte)0);
                    }
                }

                return new CvModelListPage(ms.ToArray());
            }
        }
    }

    public readonly struct CvModel
    {
        public const uint MDL = 0x004C444D;
        public const uint SKIN = 0x4E494B53;
        public const uint MASK = 0x4B53414D;

        public ReadOnlyMemory<byte> Data { get; }

        public CvModel(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }

        public CvModelKind Kind
        {
            get
            {
                var k = MemoryMarshal.Read<uint>(Data.Span);
                if ((k & 0xFFFFFF) == MDL)
                    return CvModelKind.Mdl;
                if (k == SKIN)
                    return CvModelKind.Skin;
                if (k == MASK)
                    return CvModelKind.Mask;
                return CvModelKind.Unknown;
            }
        }
    }

    public enum CvModelKind
    {
        Unknown,
        Mdl,
        Skin,
        Mask,
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.Room
{
    public readonly struct ScdEventList
    {
        public ReadOnlyMemory<byte> Data { get; }

        public ScdEventList(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }

        public int Count
        {
            get
            {
                var offsets = MemoryMarshal.Cast<byte, int>(Data.Span);
                var count = 0;
                for (var i = 0; i < offsets.Length; i++)
                {
                    if (offsets[i] == 0)
                    {
                        break;
                    }
                    count++;
                }
                return count;
            }
        }

        public ScdEvent this[int index]
        {
            get
            {
                var count = Count;
                if (index < 0 || index >= count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                var offset = GetOffset(index);
                var length = index < count - 1 ? GetOffset(index + 1) - offset : Data.Length - offset;
                return new ScdEvent(Data.Slice(offset, length));
            }
        }

        public int GetOffset(int index)
        {
            var offsets = MemoryMarshal.Cast<byte, int>(Data.Span);
            return offsets[index];
        }

        public Builder ToBuilder()
        {
            var builder = new Builder();
            for (var i = 0; i < Count; i++)
                builder.Events.Add(this[i]);
            return builder;
        }

        public class Builder
        {
            public List<ScdEvent> Events { get; } = new List<ScdEvent>();

            public ScdEventList ToEventList()
            {
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                for (var i = 0; i < Events.Count; i++)
                {
                    bw.Write(0);
                }
                bw.Write(0);

                var offsets = new int[Events.Count];
                for (var i = 0; i < Events.Count; i++)
                {
                    offsets[i] = (int)ms.Position;
                    bw.Write(Events[i].Data);
                }

                ms.Position = 0;
                for (var i = 0; i < Events.Count; i++)
                {
                    bw.Write(offsets[i]);
                }

                var bytes = ms.ToArray();
                return new ScdEventList(bytes);
            }
        }
    }

    public readonly struct ScdEvent
    {
        public ReadOnlyMemory<byte> Data { get; }

        public ScdEvent(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }
    }
}

using System;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.Room
{
    public readonly struct EmbeddedItemIcons
    {
        public ReadOnlyMemory<byte> Data { get; }

        public EmbeddedItemIcons(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }

        public int Count => Data.Length / EmbeddedItemIcon.Size;
        public ReadOnlySpan<EmbeddedItemIcon> Icons => MemoryMarshal.Cast<byte, EmbeddedItemIcon>(Data.Span);
    }

    public readonly struct EmbeddedItemIcon
    {
        public const int Size = 1200;

        public ReadOnlyMemory<byte> Data { get; }

        public EmbeddedItemIcon(ReadOnlyMemory<byte> data)
        {
            Data = data;
            if (data.Length != Size)
                throw new ArgumentException();
        }
    }
}

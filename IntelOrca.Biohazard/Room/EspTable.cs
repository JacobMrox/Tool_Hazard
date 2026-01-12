using System;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.Extensions;

namespace IntelOrca.Biohazard.Room
{
    public readonly struct EspTable
    {
        public BioVersion Version { get; }
        public ReadOnlyMemory<byte> Data { get; }

        public EspTable(BioVersion version, ReadOnlyMemory<byte> data)
        {
            Version = version;
            Data = data;
        }

        public ReadOnlySpan<byte> Ids => Data.Span.Slice(0, MaxEsps);
        public ReadOnlySpan<int> Offsets => MemoryMarshal.Cast<byte, int>(Data.Span.TruncateStartBy(-MaxEsps * 4));

        public int Count
        {
            get
            {
                var count = 0;
                if (Data.Length < 8)
                    return 0;

                var span = Ids;
                for (var i = 0; i < span.Length; i++)
                {
                    if (span[i] != 0xFF)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public Eff GetEff(int index)
        {
            var offset0 = Offsets[MaxEsps - 1 - index];
            var offset1 = index == Count - 1 ?
                Data.Length - (MaxEsps * 4) :
                Offsets[MaxEsps - 1 - index - 1];
            var data = Data.Slice(offset0, offset1 - offset0);
            return new Eff(data);
        }

        private int MaxEsps => Version == BioVersion.Biohazard2 ? 8 : 16;

        public bool ContainsId(int id)
        {
            var count = Count;
            for (var i = 0; i < count; i++)
            {
                if (Ids[i] == id)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

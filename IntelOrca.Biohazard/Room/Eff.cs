using System;

namespace IntelOrca.Biohazard.Room
{
    public readonly struct Eff
    {
        public ReadOnlyMemory<byte> Data { get; }

        public Eff(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }
    }
}

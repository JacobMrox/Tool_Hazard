using System;

namespace IntelOrca.Biohazard.Room
{
    public readonly struct ScdProcedure
    {
        public BioVersion Version { get; }
        public ReadOnlyMemory<byte> Data { get; }

        public ScdProcedure(BioVersion version, ReadOnlyMemory<byte> data)
        {
            Version = version;
            Data = data;
        }
    }
}

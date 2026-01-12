using System;

namespace IntelOrca.Biohazard.Room
{
    public static class Rdt
    {
        public static IRdt FromData(BioVersion version, ReadOnlyMemory<byte> data)
        {
            return version switch
            {
                BioVersion.Biohazard1 => new Rdt1(data),
                BioVersion.Biohazard2 => new Rdt2(version, data),
                BioVersion.Biohazard3 => new Rdt2(version, data),
                BioVersion.BiohazardCv => new RdtCv(data),
                _ => throw new NotSupportedException()
            };
        }

        public static IRdt FromFile(BioVersion version, string path)
        {
            return version switch
            {
                BioVersion.Biohazard1 => new Rdt1(path),
                BioVersion.Biohazard2 => new Rdt2(version, path),
                BioVersion.Biohazard3 => new Rdt2(version, path),
                BioVersion.BiohazardCv => new RdtCv(path),
                _ => throw new NotSupportedException()
            };
        }
    }
}

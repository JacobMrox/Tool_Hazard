using Tool_Hazard.IntelOrca.Biohazard;

namespace Tool_Hazard.IntelOrca.Biohazard.Room
{
    public interface IRdt
    {
        BioVersion Version { get; }
        ReadOnlyMemory<byte> Data { get; }
        IRdtBuilder ToBuilder();
    }

    public interface IRdtBuilder
    {
        IRdt ToRdt();
    }
}

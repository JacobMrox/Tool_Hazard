using Tool_Hazard.IntelOrca.Biohazard;

namespace Tool_Hazard.IntelOrca.Biohazard.Model
{
    public interface IModelMesh
    {
        BioVersion Version { get; }
        int NumParts { get; }
        ReadOnlyMemory<byte> Data { get; }
        IModelMeshBuilder ToBuilder();
    }
}

using System;

namespace IntelOrca.Biohazard.Model
{
    public interface IModelMesh
    {
        BioVersion Version { get; }
        int NumParts { get; }
        ReadOnlyMemory<byte> Data { get; }
        IModelMeshBuilder ToBuilder();
    }
}

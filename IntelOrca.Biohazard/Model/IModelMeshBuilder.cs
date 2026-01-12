namespace IntelOrca.Biohazard.Model
{
    public interface IModelMeshBuilder
    {
        int Count { get; }
        IModelMeshBuilderPart this[int partIndex] { get; set; }
        IModelMeshBuilder Clear();
        IModelMeshBuilder Add();
        IModelMeshBuilder RemoveAt(int partIndex);
        IModelMesh ToMesh();
    }

    public interface IModelMeshBuilderPart
    {
    }
}

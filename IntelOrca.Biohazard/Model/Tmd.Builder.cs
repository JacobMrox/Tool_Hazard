using System.Collections.Generic;
using System.IO;

namespace IntelOrca.Biohazard.Model
{
    public sealed partial class Tmd : IModelMesh
    {
        public class Builder : IModelMeshBuilder
        {
            public List<Part> Parts { get; } = new List<Part>();

            public int Count => Parts.Count;

            public IModelMeshBuilderPart this[int partIndex]
            {
                get => Parts[partIndex];
                set => Parts[partIndex] = (Part)value;
            }

            public IModelMeshBuilder Clear()
            {
                Parts.Clear();
                return this;
            }

            public IModelMeshBuilder Add()
            {
                Parts.Add(new Part());
                return this;
            }

            public IModelMeshBuilder RemoveAt(int partIndex)
            {
                Parts.RemoveAt(partIndex);
                return this;
            }

            IModelMesh IModelMeshBuilder.ToMesh() => ToMesh();

            public unsafe Tmd ToMesh()
            {
                var objects = new List<ObjectDescriptor>();
                var positions = new List<Vector>();
                var normals = new List<Vector>();
                var triangles = new List<Triangle>();
                foreach (var part in Parts)
                {
                    // Take a note of current index of each array
                    var firstPositionIndex = positions.Count;
                    var firstNormalIndex = normals.Count;
                    var firstTriangleIndex = triangles.Count;

                    // Add positions and normal placeholders
                    positions.AddRange(part.Positions);
                    normals.AddRange(part.Normals);
                    triangles.AddRange(part.Triangles);

                    // Add object (offsets are just an index at the moment)
                    objects.Add(new ObjectDescriptor()
                    {
                        vtx_offset = (ushort)firstPositionIndex,
                        vtx_count = (ushort)(positions.Count - firstPositionIndex),
                        nor_offset = (ushort)firstNormalIndex,
                        nor_count = (ushort)(normals.Count - firstNormalIndex),
                        pri_offset = (ushort)firstTriangleIndex,
                        pri_count = (ushort)(triangles.Count - firstTriangleIndex),
                        unknown = 0
                    });
                }

                var triangleOffset = Parts.Count * sizeof(ObjectDescriptor);
                var vertexOffset = triangleOffset + triangles.Count * sizeof(Triangle);
                var normalOffset = vertexOffset + positions.Count * sizeof(Vector);

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                bw.Write(0);
                bw.Write(0);
                bw.Write(Parts.Count);
                for (int i = 0; i < Parts.Count; i++)
                {
                    var tmdObject = objects[i];
                    tmdObject.vtx_offset = (ushort)(vertexOffset + tmdObject.vtx_offset * sizeof(Vector));
                    tmdObject.nor_offset = (ushort)(normalOffset + tmdObject.nor_offset * sizeof(Vector));
                    tmdObject.pri_offset = (ushort)(triangleOffset + tmdObject.pri_offset * sizeof(Triangle));
                    bw.Write(tmdObject);
                }

                foreach (var t in triangles)
                    bw.Write(t);
                foreach (var p in positions)
                    bw.Write(p);
                foreach (var n in normals)
                    bw.Write(n);

                ms.Position = 0;
                bw.Write((uint)ms.Length);

                return new Tmd(ms.ToArray());
            }

            public class Part : IModelMeshBuilderPart
            {
                public List<Vector> Positions { get; } = new List<Vector>();
                public List<Vector> Normals { get; } = new List<Vector>();
                public List<Triangle> Triangles { get; } = new List<Triangle>();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IntelOrca.Biohazard.Model
{
    public sealed partial class Md2 : IModelMesh
    {
        public sealed class Builder : IModelMeshBuilder
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

            public unsafe Md2 ToMesh()
            {
                var objects = new List<ObjectDescriptor>();
                var positions = new List<Vector>();
                var normals = new List<Vector>();
                var primitives = new List<object>();
                foreach (var part in Parts)
                {
                    // Take a note of current index of each array
                    var firstPositionIndex = positions.Count;
                    var firstNormalIndex = normals.Count;
                    var firstTriangleIndex = primitives.Count;
                    var firstQuadIndex = primitives.Count + part.Triangles.Count;

                    // Add positions and normal placeholders
                    positions.AddRange(part.Positions);
                    normals.AddRange(part.Normals);
                    primitives.AddRange(part.Triangles.Cast<object>());
                    primitives.AddRange(part.Quads.Cast<object>());

                    // Add object (offsets are just an index at the moment)
                    objects.Add(new ObjectDescriptor()
                    {
                        vtx_offset = (ushort)firstPositionIndex,
                        nor_offset = (ushort)firstNormalIndex,
                        vtx_count = (ushort)(positions.Count - firstPositionIndex),
                        tri_offset = (ushort)firstTriangleIndex,
                        quad_offset = (ushort)firstQuadIndex,
                        tri_count = (ushort)part.Triangles.Count,
                        quad_count = (ushort)part.Quads.Count
                    });
                }

                // Serialise the data
                if (positions.Count != normals.Count)
                    throw new Exception("Expected same number of normals as positions.");

                var primitiveOffset = Parts.Count * sizeof(ObjectDescriptor);
                var vertexOffset = primitiveOffset + primitives.Sum(x => x is Triangle ? sizeof(Triangle) : sizeof(Quad));
                var normalOffset = vertexOffset + positions.Count * sizeof(Vector);

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                bw.Write(0);
                if (Parts.Count > 0)
                {
                    bw.Write(Parts.Count);
                    for (int i = 0; i < Parts.Count; i++)
                    {
                        var md2Object = objects[i];
                        md2Object.vtx_offset = (ushort)(vertexOffset + md2Object.vtx_offset * sizeof(Vector));
                        md2Object.nor_offset = (ushort)(normalOffset + md2Object.nor_offset * sizeof(Vector));
                        md2Object.tri_offset = (ushort)primitiveOffset;
                        primitiveOffset += md2Object.tri_count * sizeof(Triangle);
                        md2Object.quad_offset = (ushort)primitiveOffset;
                        primitiveOffset += md2Object.quad_count * sizeof(Quad);
                        bw.Write(md2Object);
                    }
                    foreach (var t in primitives)
                    {
                        if (t is Triangle triangle)
                            bw.Write(triangle);
                        else if (t is Quad quad)
                            bw.Write(quad);
                    }
                    foreach (var p in positions)
                        bw.Write(p);
                    foreach (var n in normals)
                        bw.Write(n);

                    ms.Position = 0;
                    bw.Write((uint)ms.Length);
                }
                return new Md2(ms.ToArray());
            }

            public class Part : IModelMeshBuilderPart
            {
                public List<Vector> Positions { get; } = new List<Vector>();
                public List<Vector> Normals { get; } = new List<Vector>();
                public List<Triangle> Triangles { get; } = new List<Triangle>();
                public List<Quad> Quads { get; } = new List<Quad>();
            }
        }
    }
}

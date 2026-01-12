using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.Extensions;

namespace IntelOrca.Biohazard.Model
{
    public sealed partial class Tmd : IModelMesh
    {
        public ReadOnlyMemory<byte> Data { get; }

        public Tmd(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }

        public BioVersion Version => BioVersion.Biohazard1;
        public int NumParts => NumObjects;
        public int Length => GetSpan<int>(0, 1)[0];
        public int NumObjects => GetSpan<int>(8, 1)[0];
        public ReadOnlySpan<ObjectDescriptor> Objects => GetSpan<ObjectDescriptor>(12, NumObjects);
        public ReadOnlySpan<Vector> GetPositionData(in ObjectDescriptor obj) => GetSpan<Vector>(12 + obj.vtx_offset, obj.vtx_count);
        public ReadOnlySpan<Vector> GetNormalData(in ObjectDescriptor obj) => GetSpan<Vector>(12 + obj.nor_offset, obj.nor_count);
        public ReadOnlySpan<Triangle> GetTriangles(in ObjectDescriptor obj) => GetSpan<Triangle>(12 + obj.pri_offset, obj.pri_count);

        private unsafe ReadOnlySpan<T> GetSpan<T>(int offset, int count) where T : struct => Data.GetSafeSpan<T>(offset, count);

        IModelMeshBuilder IModelMesh.ToBuilder() => ToBuilder();

        public Builder ToBuilder()
        {
            var builder = new Builder();
            for (var i = 0; i < NumParts; i++)
            {
                var obj = Objects[i];
                var part = new Builder.Part();
                part.Positions.AddRange(GetPositionData(obj).ToArray());
                part.Normals.AddRange(GetNormalData(obj).ToArray());
                part.Triangles.AddRange(GetTriangles(obj).ToArray());
                builder.Parts.Add(part);
            }
            return builder;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ObjectDescriptor
        {
            public int vtx_offset;
            public int vtx_count;
            public int nor_offset;
            public int nor_count;
            public int pri_offset;
            public int pri_count;
            public int unknown;
        }

        [DebuggerDisplay("{x}, {y}, {z}")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Vector
        {
            public short x;
            public short y;
            public short z;
            public short zero;

            public Vector(short x, short y, short z)
                : this()
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }

            public Md2.Vector ToMd2() => new Md2.Vector(x, y, z);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Triangle
        {
            public uint unknown;
            public byte tu0;
            public byte tv0;
            public ushort clutId;
            public byte tu1;
            public byte tv1;
            public ushort page;
            public byte tu2;
            public byte tv2;
            public ushort dummy;
            public ushort n0;
            public ushort v0;
            public ushort n1;
            public ushort v1;
            public ushort n2;
            public ushort v2;
        }
    }
}

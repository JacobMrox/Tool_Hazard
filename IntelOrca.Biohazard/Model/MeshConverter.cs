using System;
using System.Collections.Generic;

namespace IntelOrca.Biohazard.Model
{
    public class MeshConverter
    {
        private static readonly int[] g_partNoRemap = new[]
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14
        };

        private static readonly int[] g_partRemap2to1 = new[]
        {
            0, 2, 6, 7, 8, 3, 4, 5, 1, 12, 13, 14, 9, 10, 11
        };

        private static readonly int[] g_partRemap3to2 = new[]
        {
            0, 8, 9, 10, 11, 12, 13, 14, 1, 2, 3, 4, 5, 6, 7
        };

        private static readonly int[] g_partRemap1to2 = g_partRemap2to1;
        private static readonly int[] g_partRemap2to3 = g_partRemap3to2;
        private static readonly int[] g_partRemap3to1 = Remap(g_partRemap3to2, g_partRemap2to1);
        private static readonly int[] g_partRemap1to3 = Remap(g_partRemap1to2, g_partRemap2to3);

        private static int[] Remap(int[] input, int[] map)
        {
            var dst = new int[input.Length];
            for (var i = 0; i < input.Length; i++)
            {
                dst[i] = map[input[i]];
            }
            return dst;
        }

        public int[] GetPartRemap(BioVersion from, BioVersion target)
        {
            if (from == target)
                return g_partNoRemap;

            if (from == BioVersion.Biohazard1)
            {
                if (target == BioVersion.Biohazard2)
                {
                    return g_partRemap1to2;
                }
                else if (target == BioVersion.Biohazard3)
                {
                    return g_partRemap1to3;
                }
            }
            else if (from == BioVersion.Biohazard2)
            {
                if (target == BioVersion.Biohazard1)
                {
                    return g_partRemap2to1;
                }
                else if (target == BioVersion.Biohazard3)
                {
                    return g_partRemap3to2;
                }
            }
            else if (from == BioVersion.Biohazard3)
            {
                if (target == BioVersion.Biohazard1)
                {
                    return g_partRemap3to1;
                }
                else if (target == BioVersion.Biohazard2)
                {
                    return g_partRemap3to2;
                }
            }
            throw new NotSupportedException();
        }

        public IModelMesh ConvertMesh(IModelMesh mesh, BioVersion version, bool remap = true)
        {
            if (mesh.Version == version)
                return mesh;

            var remapArray = remap ? GetPartRemap(mesh.Version, version) : new int[0];
            switch (version)
            {
                case BioVersion.Biohazard1:
                {
                    var visitor = new TmdConverter(remapArray);
                    visitor.Accept(mesh);
                    return visitor.ToTmd();
                }
                case BioVersion.Biohazard2:
                {
                    var visitor = new Md1Converter(remapArray);
                    visitor.Accept(mesh);
                    return visitor.ToTmd();
                }
                case BioVersion.Biohazard3:
                {
                    var visitor = new Md2Converter(remapArray);
                    visitor.Accept(mesh);
                    return visitor.ToTmd();
                }
                default:
                    throw new NotSupportedException();
            }
        }

        public Emr ConvertEmr(Emr targetEmr, Emr sourceEmr)
        {
            var remap = GetPartRemap(sourceEmr.Version, targetEmr.Version);
            var emrBuilder = targetEmr.ToBuilder();
            for (var i = 0; i < remap.Length; i++)
            {
                var srcPartIndex = i;
                var dstPartIndex = remap[i];
                var src = sourceEmr.GetRelativePosition(srcPartIndex);
                emrBuilder.RelativePositions[dstPartIndex] = src;
            }
            return emrBuilder.ToEmr();
        }

        private class TmdConverter : MeshVisitor
        {
            private readonly Tmd.Builder _builder = new Tmd.Builder();
            private readonly int[] _partRemap;

            private Tmd.Builder.Part? _part;
            private Tmd.Triangle _triangle;
            private int _pointIndex;

            public Tmd ToTmd() => _builder.ToMesh();

            public TmdConverter(int[] partRemap)
            {
                _partRemap = partRemap;
                Trianglulate = true;
            }

            public override bool VisitPart(int index)
            {
                _part = new Tmd.Builder.Part();
                return true;
            }

            public override void VisitPosition(Vector value)
            {
                _part!.Positions.Add(new Tmd.Vector(value.x, value.y, value.z));
            }

            public override void VisitNormal(Vector value)
            {
                _part!.Normals.Add(new Tmd.Vector(value.x, value.y, value.z));
            }

            public override void VisitPrimitive(int numPoints, byte page)
            {
                _triangle = new Tmd.Triangle();
                _triangle.unknown = 0x34000609;
                _triangle.clutId = (ushort)(0x7800 | page * 0x40);
                _triangle.page = (byte)(0x80 | page & 0x0F);
                _pointIndex = 0;
            }

            public override void VisitPrimitivePoint(ushort v, ushort n, byte tu, byte tv)
            {
                switch (_pointIndex)
                {
                    case 0:
                        _triangle.v0 = v;
                        _triangle.n0 = n;
                        _triangle.tu0 = tu;
                        _triangle.tv0 = tv;
                        break;
                    case 1:
                        _triangle.v1 = v;
                        _triangle.n1 = n;
                        _triangle.tu1 = tu;
                        _triangle.tv1 = tv;
                        break;
                    case 2:
                        _triangle.v2 = v;
                        _triangle.n2 = n;
                        _triangle.tu2 = tu;
                        _triangle.tv2 = tv;
                        break;
                }
                _pointIndex++;
            }

            public override void LeavePrimitive()
            {
                _part!.Triangles.Add(_triangle);
            }

            public override void LeavePart(int index)
            {
                var targetIndex = index < _partRemap.Length ? _partRemap[index] : index;
                while (_builder.Parts.Count <= targetIndex)
                {
                    _builder.Parts.Add(new Tmd.Builder.Part());
                }
                _builder.Parts[targetIndex] = _part!;
            }
        }

        private class Md1Converter : MeshVisitor
        {
            private readonly Md1.Builder _builder = new Md1.Builder();
            private readonly int[] _partRemap;

            private Md1.Builder.Part? _part;
            private Md1.Triangle _triangle;
            private Md1.TriangleTexture _triangleTexture;
            private Md1.Quad _quad;
            private Md1.QuadTexture _quadTexture;
            private int _pointCount;
            private int _pointIndex;

            public Md1 ToTmd() => _builder.ToMesh();

            public Md1Converter(int[] partRemap)
            {
                _partRemap = partRemap;
            }

            public override bool VisitPart(int index)
            {
                _part = new Md1.Builder.Part();
                return true;
            }

            public override void VisitPosition(Vector value)
            {
                _part!.Positions.Add(new Md1.Vector(value.x, value.y, value.z));
            }

            public override void VisitNormal(Vector value)
            {
                _part!.Normals.Add(new Md1.Vector(value.x, value.y, value.z));
            }

            public override void VisitPrimitive(int numPoints, byte page)
            {
                _pointCount = numPoints;
                if (numPoints == 3)
                {
                    _triangle = new Md1.Triangle();
                    _triangleTexture = new Md1.TriangleTexture();
                    _triangleTexture.clutId = (ushort)(0x7800 | page * 0x40);
                    _triangleTexture.page = (byte)(0x80 | page & 0x0F);
                }
                else if (numPoints == 4)
                {
                    _quad = new Md1.Quad();
                    _quadTexture = new Md1.QuadTexture();
                    _quadTexture.clutId = (ushort)(0x7800 | page * 0x40);
                    _quadTexture.page = (byte)(0x80 | page & 0x0F);
                }
                else
                {
                    throw new NotSupportedException();
                }
                _pointIndex = 0;
            }

            public override void VisitPrimitivePoint(ushort v, ushort n, byte tu, byte tv)
            {
                if (_pointCount == 3)
                {
                    switch (_pointIndex)
                    {
                        case 0:
                            _triangle.v0 = v;
                            _triangle.n0 = n;
                            _triangleTexture.u0 = tu;
                            _triangleTexture.v0 = tv;
                            break;
                        case 1:
                            _triangle.v1 = v;
                            _triangle.n1 = n;
                            _triangleTexture.u1 = tu;
                            _triangleTexture.v1 = tv;
                            break;
                        case 2:
                            _triangle.v2 = v;
                            _triangle.n2 = n;
                            _triangleTexture.u2 = tu;
                            _triangleTexture.v2 = tv;
                            break;
                    }
                }
                else if (_pointCount == 4)
                {
                    switch (_pointIndex)
                    {
                        case 0:
                            _quad.v0 = v;
                            _quad.n0 = n;
                            _quadTexture.u0 = tu;
                            _quadTexture.v0 = tv;
                            break;
                        case 1:
                            _quad.v1 = v;
                            _quad.n1 = n;
                            _quadTexture.u1 = tu;
                            _quadTexture.v1 = tv;
                            break;
                        case 2:
                            _quad.v2 = v;
                            _quad.n2 = n;
                            _quadTexture.u2 = tu;
                            _quadTexture.v2 = tv;
                            break;
                        case 3:
                            _quad.v3 = v;
                            _quad.n3 = n;
                            _quadTexture.u3 = tu;
                            _quadTexture.v3 = tv;
                            break;
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }
                _pointIndex++;
            }

            public override void LeavePrimitive()
            {
                if (_pointCount == 3)
                {
                    _part!.Triangles.Add(_triangle);
                    _part!.TriangleTextures.Add(_triangleTexture);
                }
                else if (_pointCount == 4)
                {
                    _part!.Quads.Add(_quad);
                    _part!.QuadTextures.Add(_quadTexture);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }

            public override void LeavePart(int index)
            {
                var targetIndex = index < _partRemap.Length ? _partRemap[index] : index;
                while (_builder.Parts.Count <= targetIndex)
                {
                    _builder.Parts.Add(new Md1.Builder.Part());
                }
                _builder.Parts[targetIndex] = _part!;
            }
        }

        private class Md2Converter : MeshVisitor
        {
            private readonly Md2.Builder _builder = new Md2.Builder();
            private readonly int[] _partRemap;

            private Md2.Builder.Part? _part;
            private Md2.Triangle _triangle;
            private Md2.Quad _quad;
            private int _pointCount;
            private int _pointIndex;

            private readonly List<Md2.Vector> _positions = new List<Md2.Vector>();
            private readonly List<Md2.Vector> _normals = new List<Md2.Vector>();

            public Md2 ToTmd() => _builder.ToMesh();

            public Md2Converter(int[] partRemap)
            {
                _partRemap = partRemap;
            }

            private byte GetAddPositionNormalPair(ushort v, ushort n)
            {
                var vValue = _positions[v];
                var nValue = _normals[n];

                var part = _part!;
                for (var i = 0; i < part.Positions.Count; i++)
                {
                    if (part.Positions[i].Equals(vValue) && part.Normals[i].Equals(nValue))
                    {
                        return (byte)i;
                    }
                }

                part.Positions.Add(vValue);
                part.Normals.Add(nValue);
                return (byte)(part.Positions.Count - 1);
            }

            public override bool VisitPart(int index)
            {
                _part = new Md2.Builder.Part();
                _positions.Clear();
                _normals.Clear();
                return true;
            }

            public override void VisitPosition(Vector value)
            {
                _positions.Add(new Md2.Vector(value.x, value.y, value.z));
            }

            public override void VisitNormal(Vector value)
            {
                _normals.Add(new Md2.Vector(value.x, value.y, value.z));
            }

            public override void VisitPrimitive(int numPoints, byte page)
            {
                _pointCount = numPoints;
                if (numPoints == 3)
                {
                    _triangle = new Md2.Triangle();
                    _triangle.dummy0 = (byte)(page * 64);
                    _triangle.visible = 120;
                    _triangle.page = (byte)(0x80 | page & 0x0F);
                }
                else if (numPoints == 4)
                {
                    _quad = new Md2.Quad();
                    _quad.dummy2 = (byte)(page * 64);
                    _quad.visible = 120;
                    _quad.page = (byte)(0x80 | page & 0x0F);
                }
                else
                {
                    throw new NotSupportedException();
                }
                _pointIndex = 0;
            }

            public override void VisitPrimitivePoint(ushort v, ushort n, byte tu, byte tv)
            {
                if (_pointCount == 3)
                {
                    switch (_pointIndex)
                    {
                        case 0:
                            _triangle.v0 = GetAddPositionNormalPair(v, n);
                            _triangle.tu0 = tu;
                            _triangle.tv0 = tv;
                            break;
                        case 1:
                            _triangle.v1 = GetAddPositionNormalPair(v, n);
                            _triangle.tu1 = tu;
                            _triangle.tv1 = tv;
                            break;
                        case 2:
                            _triangle.v2 = GetAddPositionNormalPair(v, n);
                            _triangle.tu2 = tu;
                            _triangle.tv2 = tv;
                            break;
                    }
                }
                else if (_pointCount == 4)
                {
                    switch (_pointIndex)
                    {
                        case 0:
                            _quad.v0 = GetAddPositionNormalPair(v, n);
                            _quad.tu0 = tu;
                            _quad.tv0 = tv;
                            break;
                        case 1:
                            _quad.v1 = GetAddPositionNormalPair(v, n);
                            _quad.tu1 = tu;
                            _quad.tv1 = tv;
                            break;
                        case 2:
                            _quad.v2 = GetAddPositionNormalPair(v, n);
                            _quad.tu2 = tu;
                            _quad.tv2 = tv;
                            break;
                        case 3:
                            _quad.v3 = GetAddPositionNormalPair(v, n);
                            _quad.tu3 = tu;
                            _quad.tv3 = tv;
                            break;
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }
                _pointIndex++;
            }

            public override void LeavePrimitive()
            {
                if (_pointCount == 3)
                {
                    _part!.Triangles.Add(_triangle);
                }
                else if (_pointCount == 4)
                {
                    _part!.Quads.Add(_quad);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }

            public override void LeavePart(int index)
            {
                var targetIndex = index < _partRemap.Length ? _partRemap[index] : index;
                while (_builder.Parts.Count <= targetIndex)
                {
                    _builder.Parts.Add(new Md2.Builder.Part());
                }
                _builder.Parts[targetIndex] = _part!;
            }
        }
    }
}

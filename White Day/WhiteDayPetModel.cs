#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Tool_Hazard.White_Day
{
    public sealed class WhiteDayPetBone
    {
        public string Name = "";
        public byte Parent;
        public Matrix4x4 Matrix = Matrix4x4.Identity;
    }

    public sealed class WhiteDayPetVertex
    {
        public Vector4 Position;
        public List<(byte Weight, byte BoneId)> BoneWeights = new();
    }

    public sealed class WhiteDayPetPolygonIndex
    {
        public uint Index;
        public Vector3 Normal;
        public Vector2 Uv;
    }

    public sealed class WhiteDayPetPolygon
    {
        public WhiteDayPetPolygonIndex[] Indices = Array.Empty<WhiteDayPetPolygonIndex>();
    }

    public sealed class WhiteDayPetMesh
    {
        public string Name = "";
        public List<WhiteDayPetVertex> Vertices = new();
        public List<WhiteDayPetPolygon> Polygons = new();
        public byte[] TexMap = Array.Empty<byte>();
    }

    public sealed class WhiteDayPetModel
    {
        public List<string> Textures = new();
        public List<WhiteDayPetBone> Bones = new();
        public List<WhiteDayPetMesh> Meshes = new();
        public string Description = "";

        public static WhiteDayPetModel Load(string path)
        {
            var model = new WhiteDayPetModel { Description = Path.GetFileName(path) };
            byte[] data = File.ReadAllBytes(path);
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            while (ms.Position < ms.Length - 8)
            {
                string id = Encoding.ASCII.GetString(reader.ReadBytes(4));
                uint size = reader.ReadUInt32();
                if (size > ms.Length - ms.Position) break;

                byte[] blockData = reader.ReadBytes((int)size);
                using var blockMs = new MemoryStream(blockData);
                using var br = new BinaryReader(blockMs);

                switch (id)
                {
                    case "VERS":
                        // Version block, ignored.
                        break;

                    case "TEXT":
                    {
                        int count = br.ReadInt32();
                        for (int i = 0; i < count; i++)
                        {
                            byte[] nameBytes = br.ReadBytes(44);
                            int nullIdx = Array.IndexOf(nameBytes, (byte)0);
                            string name = nullIdx >= 0
                                ? Encoding.GetEncoding("EUC-KR").GetString(nameBytes, 0, nullIdx)
                                : Encoding.GetEncoding("EUC-KR").GetString(nameBytes);
                            model.Textures.Add(name);
                        }
                        break;
                    }

                    case "BONE":
                    {
                        int count = br.ReadByte();
                        for (int i = 0; i < count; i++)
                        {
                            if (blockMs.Position >= blockMs.Length) break;
                            var bone = new WhiteDayPetBone();
                            bone.Name = ReadCString(br);
                            if (blockMs.Position >= blockMs.Length) break;
                            bone.Parent = br.ReadByte();
                            if (blockMs.Position + 48 > blockMs.Length) break;
                            float[] m = new float[12];
                            for (int j = 0; j < 12; j++) m[j] = br.ReadSingle();
                            // PET stores a 3x4 matrix in column-major order (DirectX style).
                            // Convert to row-major Matrix4x4:
                            //   [m0 m3 m6 m9]
                            //   [m1 m4 m7 m10]
                            //   [m2 m5 m8 m11]
                            //   [0  0  0  1]
                            bone.Matrix = new Matrix4x4(
                                m[0], m[1], m[2], 0,
                                m[3], m[4], m[5], 0,
                                m[6], m[7], m[8], 0,
                                m[9], m[10], m[11], 1);
                            model.Bones.Add(bone);
                        }
                        break;
                    }

                    case "MESH":
                    {
                        int count = br.ReadByte();
                        // meshid + 4 unknown uint32s
                        if (blockMs.Length - blockMs.Position >= 20)
                        {
                            uint meshid = br.ReadUInt32();
                            br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32();
                        }
                        for (int i = 0; i < count; i++)
                        {
                            var mesh = new WhiteDayPetMesh { Name = $"Mesh {i}" };
                            int numVertices = br.ReadInt32();
                            if (numVertices < 0 || numVertices > 1000000) break;
                            for (int v = 0; v < numVertices; v++)
                            {
                                if (blockMs.Position + 16 > blockMs.Length) break;
                                var vertex = new WhiteDayPetVertex();
                                vertex.Position = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                                // Bone weights: pairs of (weight, boneId) until weight sum reaches 0xFF.
                                int wsum = 0;
                                while (wsum < 0xFF && blockMs.Position + 2 <= blockMs.Length)
                                {
                                    byte weight = br.ReadByte();
                                    byte boneId = br.ReadByte();
                                    wsum += weight;
                                    vertex.BoneWeights.Add((weight, boneId));
                                    if (wsum >= 0xFF) break;
                                }
                                // File leaves space for at least 2 weights; pad if needed.
                                while (vertex.BoneWeights.Count < 2 && blockMs.Position + 2 <= blockMs.Length)
                                {
                                    br.ReadByte(); br.ReadByte();
                                    vertex.BoneWeights.Add((0, 0));
                                }
                                mesh.Vertices.Add(vertex);
                            }

                            if (blockMs.Position + 4 > blockMs.Length) break;
                            int numPolygons = br.ReadInt32();
                            if (numPolygons < 0 || numPolygons > 1000000) break;
                            for (int p = 0; p < numPolygons; p++)
                            {
                                if (blockMs.Position + 72 > blockMs.Length) break;
                                var polygon = new WhiteDayPetPolygon();
                                var indices = new WhiteDayPetPolygonIndex[3];
                                for (int c = 0; c < 3; c++)
                                {
                                    var idx = new WhiteDayPetPolygonIndex
                                    {
                                        Index = br.ReadUInt32(),
                                        Normal = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                                        Uv = new Vector2(br.ReadSingle(), br.ReadSingle()),
                                    };
                                    indices[c] = idx;
                                }
                                polygon.Indices = indices;
                                mesh.Polygons.Add(polygon);
                            }

                            // Texture map: one byte per polygon.
                            if (blockMs.Position + numPolygons > blockMs.Length) break;
                            mesh.TexMap = br.ReadBytes(numPolygons);
                            model.Meshes.Add(mesh);
                        }
                        break;
                    }

                    case "FANM":
                    case "FRAM":
                    case "MOTI":
                    case "COLL":
                    case "ANIM":
                        // Animation/collision blocks are skipped for viewing purposes.
                        break;

                    default:
                        // Unknown block: skip.
                        break;
                }
            }

            return model;
        }

        private static string ReadCString(BinaryReader br)
        {
            var sb = new StringBuilder();
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                byte b = br.ReadByte();
                if (b == 0) break;
                sb.Append((char)b);
            }
            return sb.ToString();
        }
    }
}

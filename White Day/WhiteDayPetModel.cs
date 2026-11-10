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
        // Diagnostic: numVertices of first mesh (if any)
        public int FirstMeshNumVertices;

        // PET vertex positions are stored in each influencing bone's local frame,
        // and the BONE block stores parent-relative transforms (3ds-max Biped
        // hierarchy). The on-disk 4x3 matrix is Max/Direct3D row-vector style
        // (rows X,Y,Z, translation in the last row) stored column-major; Load()
        // turns it into its transpose, so Transpose() here restores the disk
        // layout. System.Numerics is row-vector, so the world pose for a bone is
        // local * parent chained up through the parents (parent 255 = root).
        public List<Matrix4x4> ComputeWorldBoneMatrices()
        {
            var world = new Matrix4x4[Bones.Count];
            for (int i = 0; i < Bones.Count; i++)
            {
                Matrix4x4 local = Matrix4x4.Transpose(Bones[i].Matrix);
                int parent = Bones[i].Parent;
                world[i] = parent < i ? Matrix4x4.Multiply(local, world[parent]) : local;
            }
            return world.ToList();
        }

        // Skin a local-space vertex by ALL of its bone weights (weights sum to 0xFF).
        public Vector3 SkinVertex(Vector3 local, List<(byte Weight, byte BoneId)> boneWeights, List<Matrix4x4> worldBones)
        {
            float total = 0f;
            Vector3 acc = Vector3.Zero;
            foreach (var (weight, boneId) in boneWeights)
            {
                if (weight == 0 || boneId >= worldBones.Count) continue;
                acc += Vector3.Transform(local, worldBones[boneId]) * (weight / 255f);
                total += weight / 255f;
            }
            return total > 0f ? acc / total : local;
        }

        // Skin a direction (e.g. a vertex normal) the same way, without translation
        // (W=0 drops the translation row from the transform).
        public Vector3 SkinDirection(Vector3 local, List<(byte Weight, byte BoneId)> boneWeights, List<Matrix4x4> worldBones)
        {
            float total = 0f;
            Vector3 acc = Vector3.Zero;
            foreach (var (weight, boneId) in boneWeights)
            {
                if (weight == 0 || boneId >= worldBones.Count) continue;
                Vector4 dir = new(local, 0f);
                Vector4 t = Vector4.Transform(dir, worldBones[boneId]);
                acc += new Vector3(t.X, t.Y, t.Z) * (weight / 255f);
                total += weight / 255f;
            }
            return total > 0f ? acc / total : local;
        }

        public static WhiteDayPetModel Load(string path)
        {
            var model = new WhiteDayPetModel { Description = Path.GetFileName(path) };
            byte[] data = File.ReadAllBytes(path);
            // ".mpet" files (Pangya "version 1" per the petImp reference plugin) carry a
            // 21-byte MESH header and a 4-byte extra field per vertex; ".pet" files do not.
            bool mpetV1 = Path.GetExtension(path).Equals(".mpet", StringComparison.OrdinalIgnoreCase);
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            while (ms.Position < ms.Length - 8)
            {
                string id = Encoding.ASCII.GetString(reader.ReadBytes(4));
                uint size = reader.ReadUInt32();
                if (size > ms.Length - ms.Position) break;

                byte[] blockData = reader.ReadBytes((int)size);
                using var blockMs = new MemoryStream(blockData);
                var br = new BinaryReader(blockMs);

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
                            // PET stores a 3x4 matrix in column-major order (DirectX style):
                            //   col0 = m[0..2], col1 = m[3..5], col2 = m[6..8], col3(translation) = m[9..11]
                            // Construct row-major Matrix4x4:
                            //   [m0 m3 m6 m9]
                            //   [m1 m4 m7 m10]
                            //   [m2 m5 m8 m11]
                            //   [0  0  0  1]
                            bone.Matrix = new Matrix4x4(
                                m[0], m[3], m[6], m[9],
                                m[1], m[4], m[7], m[10],
                                m[2], m[5], m[8], m[11],
                                0, 0, 0, 1);
                            model.Bones.Add(bone);
                        }
                        break;
                    }

                    case "MESH":
                    {
                        // MESH layout (cross-checked against the petImp 3ds-max plugin and
                        // all 1005 White Day game files; every file uses exactly one MESH
                        // block ordered TEXT>BONE>ANIM>MESH>FANM>FRAM>MOTI>COLL):
                        //   [.mpet "version 1" files only: 21-byte header, skipped]
                        //   int32 numVertices
                        //   per vertex: float x,y,z (12 bytes), then
                        //     [.mpet files only: 4 extra bytes, skipped]
                        //     byte weight, byte boneId
                        //       - weight == 0xFF: single weight; exactly 2 trailing bytes
                        //         (an empty second slot) always follow
                        //       - weight < 0xFF: further (weight, boneId) pairs follow with
                        //         NO padding until the weights sum to 0xFF
                        //   int32 numPolygons
                        //   per polygon: 3 corners x { u16 vertexIndex, u16 pad,
                        //     float nx,ny,nz, float u,v } = 72 bytes
                        //   per polygon: 1 texture-map byte (index into TEXT)
                        if (mpetV1)
                        {
                            if (blockMs.Position + 21 > blockMs.Length) break;
                            br.ReadBytes(21);
                        }
                        if (blockMs.Position + 4 > blockMs.Length) break;
                        int numVertices = br.ReadInt32();
                        model.FirstMeshNumVertices = numVertices;
                        if (numVertices <= 0 || numVertices > 1000000) break;

                        var mesh = new WhiteDayPetMesh { Name = $"Mesh {model.Meshes.Count}" };

                        for (int v = 0; v < numVertices; v++)
                        {
                            int minBytes = mpetV1 ? 18 : 14; // 12 pos + (v1: 4 extra) + 2 weight bytes
                            if (blockMs.Position + minBytes > blockMs.Length) break;
                            var vertex = new WhiteDayPetVertex();
                            vertex.Position = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), 1f);
                            if (mpetV1) br.ReadBytes(4); // version-1 extra field after the position
                            byte weight = br.ReadByte();
                            byte boneId = br.ReadByte();
                            vertex.BoneWeights.Add((weight, boneId));
                            if (weight == 0xFF)
                            {
                                // Single weight: two trailing bytes always follow.
                                if (blockMs.Position + 2 > blockMs.Length) break;
                                br.ReadBytes(2);
                            }
                            else
                            {
                                // Multi-weight: pairs until the weights sum to 0xFF, no padding.
                                int wsum = weight;
                                while (wsum < 0xFF && blockMs.Position + 2 <= blockMs.Length)
                                {
                                    byte w2 = br.ReadByte();
                                    byte b2 = br.ReadByte();
                                    wsum += w2;
                                    vertex.BoneWeights.Add((w2, b2));
                                }
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
                                    indices[c] = new WhiteDayPetPolygonIndex
                                    {
                                        Index = br.ReadUInt32(),
                                        Normal = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                                        Uv = new Vector2(br.ReadSingle(), br.ReadSingle()),
                                    };
                                }
                                polygon.Indices = indices;
                                mesh.Polygons.Add(polygon);
                            }

                            // Texture map: one byte per polygon.
                            if (blockMs.Position + numPolygons > blockMs.Length) break;
                            mesh.TexMap = br.ReadBytes(numPolygons);

                            model.Meshes.Add(mesh);
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

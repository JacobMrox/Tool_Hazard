// RE4 (2007 PC) PMD + scenario SMD(-text) + binary scenario container support.
// Adapted from JADERLINK_MODEL_VIEWER (MIT):
//   ALL_VIEWER/PMD_LIB/{PMD.cs,PmdDecoder.cs}
//   ALL_VIEWER/RE4_2007_MODEL_VIEWER/src/{LoadPmdModel.cs,LoadScenarioSMD.cs,LoadTGA.cs}
//   ALL_VIEWER/SHARED_SCENARIO_SMD/{SmdExtract.cs,Extract_BIN_Inside_SMD.cs,Extract_TPL_Inside_SMD.cs,CounterBinTpl.cs}
//   ALL_VIEWER/SHARED_UHD_SCENARIO_SMD/{Extract_TPL_Inside_SMD_UHD.cs,Extract_TPL_Content_UHD.cs}
//   ALL_VIEWER/SMX/{SMX.cs,SMXextract.cs} (header parse only)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tool_Hazard.Biohazard.RE4
{
    public sealed class Re4PmdMaterial
    {
        public float[] Data = new float[17];
        public int Enable;
        public string TextureName = "";
    }

    public sealed class Re4PmdMesh
    {
        public int TextureIndex;
        public ushort[] Order = Array.Empty<ushort>();
        public float[] VertexData = Array.Empty<float>(); // 16 per vertex: x y z w0 w1 i0 i1 nx ny nz tu tv r g b a
        public int VertexCount => VertexData.Length / 16;
    }

    public sealed class Re4PmdNode
    {
        public int SkeletonIndex;
        public int NodeId;
        public List<Re4PmdMesh> Meshes = new();
        public List<(int BoneId, float[] Unknown13, float X, float Y, float Z, float W)> Bones = new();
    }

    public sealed class Re4PmdModel
    {
        public string SourcePath = "";
        public byte[] RawBytes = Array.Empty<byte>();
        public string[] SkeletonBoneNames = Array.Empty<string>();
        public string[] NodeGroupNames = Array.Empty<string>();
        public float[][] SkeletonBoneData = Array.Empty<float[]>();
        public int[] SkeletonBoneParents = Array.Empty<int>();
        public List<Re4PmdNode> Nodes = new();
        public List<Re4PmdMaterial> Materials = new();
        public List<Re4MeshPart> Parts = new(); // unified view parts (material index = texture index)

        public static Re4PmdModel Load(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            var m = Decode(data, path);
            return m;
        }

        public static Re4PmdModel Decode(byte[] data, string? name = null)
        {
            var m = new Re4PmdModel { SourcePath = name ?? "", RawBytes = data };
            var r = new Cursor(data);
            r.Read(58); // header text
            uint boneNamesLen = r.U32();
            m.SkeletonBoneNames = new string[boneNamesLen];
            for (uint i = 0; i < boneNamesLen; i++)
            {
                int len = (int)r.U32();
                string s = Encoding.ASCII.GetString(r.Read(len));
                uint id = r.U32();
                if (id < m.SkeletonBoneNames.Length) m.SkeletonBoneNames[id] = s;
            }
            uint nodeNamesLen = r.U32();
            m.NodeGroupNames = new string[nodeNamesLen];
            for (uint i = 0; i < nodeNamesLen; i++)
            {
                int len = (int)r.U32();
                string s = Encoding.ASCII.GetString(r.Read(len));
                uint id = r.U32();
                if (id < m.NodeGroupNames.Length) m.NodeGroupNames[id] = s;
            }
            r.U32(); // zeros
            uint skelLen = r.U32();
            m.SkeletonBoneData = new float[skelLen][];
            m.SkeletonBoneParents = new int[skelLen];
            for (uint i = 0; i < skelLen; i++)
            {
                int parent = (int)r.U32();
                m.SkeletonBoneParents[i] = parent;
                m.SkeletonBoneData[i] = new float[26];
                for (int k = 0; k < 26; k++) m.SkeletonBoneData[i][k] = r.F32();
            }
            uint nodeCount = r.U32();
            for (uint n = 0; n < nodeCount; n++)
            {
                var node = new Re4PmdNode { NodeId = (int)n };
                node.SkeletonIndex = (int)r.U32();
                r.Read(32);
                uint meshCount = r.U32();
                var texIdx = new int[meshCount];
                for (uint k = 0; k < meshCount; k++) texIdx[k] = (int)r.U32();
                r.U32(); r.U32();
                for (uint k = 0; k < meshCount; k++)
                    node.Meshes.Add(new Re4PmdMesh { TextureIndex = texIdx[k] });
                for (uint k = 0; k < meshCount; k++)
                {
                    uint marker = r.U32();
                    if (marker != 64) continue;
                    uint indexAmount = r.U32();
                    var order = new ushort[indexAmount];
                    for (uint j = 0; j < indexAmount; j++) order[j] = r.U16();
                    uint vAmount = r.U32();
                    var verts = new float[vAmount * 16];
                    for (uint j = 0; j < vAmount; j++)
                    {
                        byte[] row = r.Read(64);
                        for (int f = 0; f < 16; f++) verts[j * 16 + f] = BitConverter.ToSingle(row, f * 4);
                    }
                    node.Meshes[(int)k].Order = order;
                    node.Meshes[(int)k].VertexData = verts;
                }
                uint boneCount = r.U32();
                for (uint b = 0; b < boneCount; b++)
                {
                    int boneId = (int)r.U32();
                    byte[] unk = r.Read(52);
                    var u13 = new float[13];
                    for (int f = 0; f < 13; f++) u13[f] = BitConverter.ToSingle(unk, f * 4);
                    byte[] bone = r.Read(16);
                    node.Bones.Add((boneId, u13, BitConverter.ToSingle(bone, 0), BitConverter.ToSingle(bone, 4), BitConverter.ToSingle(bone, 8), BitConverter.ToSingle(bone, 12)));
                }
                m.Nodes.Add(node);
            }
            uint texCount = r.U32();
            for (uint i = 0; i < texCount; i++)
            {
                byte[] row = r.Read(72);
                var mat = new Re4PmdMaterial();
                for (int f = 0; f < 17; f++) mat.Data[f] = BitConverter.ToSingle(row, f * 4);
                mat.Enable = BitConverter.ToInt32(row, 68);
                int nameLen = (int)r.U32();
                mat.TextureName = Encoding.ASCII.GetString(r.Read(nameLen));
                m.Materials.Add(mat);
            }
            // Build unified parts.
            bool isScenarioPmd = false;
            if (!string.IsNullOrEmpty(name))
            {
                string fn = System.IO.Path.GetFileName(name).ToUpperInvariant();
                isScenarioPmd = System.Text.RegularExpressions.Regex.IsMatch(fn, "^(R)([0-9|A-F]{1,3})(_)([0-9]{1,3})(.PMD).*$");
            }
            float sx = 1, sy = 1, sz = 1;
            if (isScenarioPmd && m.SkeletonBoneData.Length > 0) { sx = m.SkeletonBoneData[0][0]; sy = m.SkeletonBoneData[0][1]; sz = m.SkeletonBoneData[0][2]; }
            foreach (var node in m.Nodes)
                foreach (var mesh in node.Meshes)
                {
                    if (mesh.Order.Length == 0 || mesh.VertexCount == 0) continue;
                    var verts = new float[mesh.VertexCount * 3];
                    var nrms = new float[mesh.VertexCount * 3];
                    var uvs = new float[mesh.VertexCount * 2];
                    var cols = new float[mesh.VertexCount * 4];
                    for (int i = 0; i < mesh.VertexCount; i++)
                    {
                        int o = i * 16;
                        verts[i * 3 + 0] = mesh.VertexData[o + 0] * sx;
                        verts[i * 3 + 1] = mesh.VertexData[o + 1] * sy;
                        verts[i * 3 + 2] = mesh.VertexData[o + 2] * sz;
                        nrms[i * 3 + 0] = mesh.VertexData[o + 7];
                        nrms[i * 3 + 1] = mesh.VertexData[o + 8];
                        nrms[i * 3 + 2] = mesh.VertexData[o + 9];
                        uvs[i * 2 + 0] = mesh.VertexData[o + 10];
                        uvs[i * 2 + 1] = mesh.VertexData[o + 11];
                        cols[i * 4 + 0] = mesh.VertexData[o + 12];
                        cols[i * 4 + 1] = mesh.VertexData[o + 13];
                        cols[i * 4 + 2] = mesh.VertexData[o + 14];
                        cols[i * 4 + 3] = mesh.VertexData[o + 15];
                    }
                    var part = new Re4MeshPart { MaterialIndex = mesh.TextureIndex, Name = $"Material_{mesh.TextureIndex}", Indices = mesh.Order.Select(x => (uint)x).ToArray() };
                    part.Vertices = verts; part.UniqueNormals = nrms; part.UniqueUvs = uvs; part.UniqueColors = cols;
                    part.Positions = verts; part.Normals = nrms; part.Uvs = uvs; part.Colors = cols;
                    m.Parts.Add(part);
                }
            if (m.Parts.Count == 0) throw new InvalidDataException("PMD contains no mesh data.");
            return m;
        }

        public void Save(string path) => File.WriteAllBytes(path, RawBytes);

        public void ExportObj(string path)
        {
            using var w = new StreamWriter(path, false, new UTF8Encoding(false));
            w.WriteLine("# RE4 2007 PMD export (Tool Hazard)");
            int baseIndex = 1;
            foreach (var part in Parts)
            {
                w.WriteLine($"o {part.Name}");
                for (int i = 0; i < part.VertexCount; i++) w.WriteLine(FormattableString.Invariant($"v {part.Vertices[i * 3]:R} {part.Vertices[i * 3 + 1]:R} {part.Vertices[i * 3 + 2]:R}"));
                for (int i = 0; i < part.VertexCount; i++) w.WriteLine(FormattableString.Invariant($"vn {part.UniqueNormals[i * 3]:R} {part.UniqueNormals[i * 3 + 1]:R} {part.UniqueNormals[i * 3 + 2]:R}"));
                for (int i = 0; i < part.VertexCount; i++) w.WriteLine(FormattableString.Invariant($"vt {part.UniqueUvs[i * 2]:R} {part.UniqueUvs[i * 2 + 1]:R}"));
                for (int t = 0; t < part.Indices.Length; t += 3)
                {
                    uint a = part.Indices[t], b = part.Indices[t + 1], c = part.Indices[t + 2];
                    w.WriteLine($"f {baseIndex + (int)a}/{baseIndex + (int)a}/{baseIndex + (int)a} {baseIndex + (int)b}/{baseIndex + (int)b}/{baseIndex + (int)b} {baseIndex + (int)c}/{baseIndex + (int)c}/{baseIndex + (int)c}");
                }
                baseIndex += part.VertexCount;
            }
        }

        private sealed class Cursor
        {
            private readonly byte[] _d; private int _p;
            public Cursor(byte[] d) { _d = d; }
            public byte[] Read(int n) { if (_p + n > _d.Length) throw new InvalidDataException("PMD truncated."); var r = new byte[n]; Array.Copy(_d, _p, r, 0, n); _p += n; return r; }
            public uint U32() { var b = Read(4); return BitConverter.ToUInt32(b, 0); }
            public ushort U16() { var b = Read(2); return BitConverter.ToUInt16(b, 0); }
            public float F32() { var b = Read(4); return BitConverter.ToSingle(b, 0); }
        }
    }

    // Binary scenario SMD container (UHD/GCWII/X360 families share SmdExtract layout).
    public sealed class Re4ScenarioLine
    {
        public float PositionX, PositionY, PositionZ;
        public float AngleX, AngleY, AngleZ;
        public float ScaleX, ScaleY, ScaleZ;
        public byte BinFileId, TplFileId, FixedFf, SmxId;
        public uint ObjectStatus;
        public int Index;
    }

    public sealed class Re4ScenarioSmd
    {
        public string SourcePath = "";
        public byte[] RawBytes = Array.Empty<byte>();
        public ushort Magic;
        public Re4Endian Endian;
        public uint OffsetBinArr;
        public uint OffsetTplArr;
        public uint[] ExtraParameters = Array.Empty<uint>();
        public List<Re4ScenarioLine> Lines = new();
        public Dictionary<int, byte[]> BinBlobs = new(); // BinID -> raw bytes (re-decoded on demand)
        public Dictionary<int, byte[]> TplBlobs = new();
        public Dictionary<int, Re4BinModel> Bins = new();

        public static bool IsScenarioSmd(byte[] data)
        {
            if (data.Length < 12) return false;
            ushort le = BitConverter.ToUInt16(data, 0);
            return le == 0x0040 || le == 0x0140 || le == 0x0031 || le == 0x0020 || le == 0x0010;
        }

        public static Re4ScenarioSmd Load(string path, Re4Endian? endianHint = null)
        {
            byte[] data = File.ReadAllBytes(path);
            return Decode(data, path, endianHint);
        }

        public static Re4ScenarioSmd Decode(byte[] data, string? name = null, Re4Endian? endianHint = null)
        {
            // Magic read LE in upstream; container body follows body endianness (X360 BE).
            ushort magic = BitConverter.ToUInt16(data, 0);
            if (!(magic == 0x0040 || magic == 0x0140 || magic == 0x0031 || magic == 0x0020 || magic == 0x0010))
                throw new InvalidDataException("Invalid scenario SMD file.");
            var smd = new Re4ScenarioSmd { SourcePath = name ?? "", RawBytes = data, Magic = magic };
            // Endian: hint wins; else try LE then BE by validating line count vs file size.
            var candidates = endianHint.HasValue ? new[] { endianHint.Value } : new[] { Re4Endian.Little, Re4Endian.Big };
            foreach (var e in candidates)
            {
                try { Parse(data, smd, e); smd.Endian = e; return smd; }
                catch (InvalidDataException) { }
            }
            throw new InvalidDataException("Scenario SMD header is invalid (tried LE and BE).");
        }

        private static void Parse(byte[] data, Re4ScenarioSmd smd, Re4Endian e)
        {
            int p = 0;
            ushort count = Rd16(data, p, e); p += 2;
            uint offBin = Rd32(data, p, e); p += 4;
            uint offTpl = Rd32(data, p, e); p += 4;
            Rd32(data, p, e); p += 4;
            smd.OffsetBinArr = offBin; smd.OffsetTplArr = offTpl;
            if (smd.Magic == 0x0140)
            {
                uint extra = Rd32(data, p, e); p += 4;
                if (extra > 0x10000) throw new InvalidDataException("Invalid SMD extra-parameter count.");
                smd.ExtraParameters = new uint[extra];
                for (uint i = 0; i < extra; i++) { smd.ExtraParameters[i] = Rd32(data, p, e); p += 4; }
            }
            if (count > 0x10000) throw new InvalidDataException("Invalid SMD line count.");
            smd.Lines.Clear();
            for (int i = 0; i < count; i++)
            {
                var line = new Re4ScenarioLine
                {
                    Index = i,
                    PositionX = RdF(data, p, e), PositionY = RdF(data, p + 4, e), PositionZ = RdF(data, p + 8, e),
                    AngleX = RdF(data, p + 12, e), AngleY = RdF(data, p + 16, e), AngleZ = RdF(data, p + 20, e),
                    ScaleX = RdF(data, p + 24, e), ScaleY = RdF(data, p + 28, e), ScaleZ = RdF(data, p + 32, e),
                };
                line.BinFileId = data[p + 36]; line.TplFileId = data[p + 37]; line.FixedFf = data[p + 38]; line.SmxId = data[p + 39];
                p += 72;
                if (p > data.Length) throw new InvalidDataException("Scenario SMD lines are truncated.");
                smd.Lines.Add(line);
            }
            // Embedded BIN/TPL offset tables: counts are max(BinFileId/TplFileId)+1 over non-shared lines.
            int bins = 0, tpls = 0;
            foreach (var l in smd.Lines) { if (l.BinFileId + 1 > bins) bins = l.BinFileId + 1; if (l.TplFileId + 1 > tpls) tpls = l.TplFileId + 1; }
            ExtractBlobs(data, smd, e, offBin, bins, isBin: true);
            ExtractBlobs(data, smd, e, offTpl, tpls, isBin: false);
            // Decode embedded BINs (UHD-family) for viewing.
            foreach (var kv in smd.BinBlobs.ToList())
            {
                try { smd.Bins[kv.Key] = Re4BinModel.Decode(kv.Value, 0, $"ScenarioBIN_{kv.Key}"); }
                catch { /* keep blob for manual extraction */ }
            }
        }

        private static void ExtractBlobs(byte[] data, Re4ScenarioSmd smd, Re4Endian e, uint tableOff, int count, bool isBin)
        {
            if (tableOff == 0 || count == 0 || tableOff >= data.Length) return;
            int table = (int)tableOff;
            if (table + count * 4 > data.Length) throw new InvalidDataException("Scenario SMD offset table is truncated.");
            for (int i = 0; i < count; i++)
            {
                uint rel = Rd32(data, table + i * 4, e);
                if (rel == 0) continue;
                long start = (long)tableOff + rel;
                if (start < 0 || start >= data.Length) continue;
                // Blob extent: next nonzero offset or EOF (same approach as upstream decoders
                // which return endOffset from nested decoders). Capture to next table entry.
                long end = data.Length;
                for (int j = i + 1; j < count; j++)
                {
                    uint rel2 = Rd32(data, table + j * 4, e);
                    if (rel2 != 0) { end = (long)tableOff + rel2; break; }
                }
                int len = (int)(end - start);
                if (len < 0) continue;
                var blob = new byte[len]; Array.Copy(data, start, blob, 0, len);
                (isBin ? smd.BinBlobs : smd.TplBlobs)[i] = blob;
            }
        }

        // Scenario title export: one OBJ per embedded BIN with line placements baked.
        public void ExportPlacedObj(string path)
        {
            using var w = new StreamWriter(path, false, new UTF8Encoding(false));
            w.WriteLine("# RE4 scenario SMD (Tool Hazard) — embedded BINs with line placements baked");
            foreach (var line in Lines)
            {
                if (!Bins.TryGetValue(line.BinFileId, out var bin)) continue;
                WritePlaced(w, bin, line);
            }
        }

        private static void WritePlaced(StreamWriter w, Re4BinModel bin, Re4ScenarioLine line)
        {
            double lx = line.AngleX, ly = line.AngleY, lz = line.AngleZ;
            // Legacy layout: position/100, angle degrees -> radians, non-uniform scale.
            // Apply local translate -> rotate Z/Y/X -> scale (same order as TheRender prefixes).
            int baseIndex = 1;
            foreach (var part in bin.Parts.OrderBy(p => p.MaterialIndex))
            {
                w.WriteLine($"o BIN_{line.BinFileId}_Material_{part.MaterialIndex}");
                for (int i = 0; i < part.VertexCount; i++)
                {
                    double x = part.Vertices[i * 3], y = part.Vertices[i * 3 + 1], z = part.Vertices[i * 3 + 2];
                    double vx = x * line.ScaleX, vy = y * line.ScaleY, vz = z * line.ScaleZ;
                    // ZYX euler (degrees -> radians)
                    double ax = lx * Math.PI / 180, ay = ly * Math.PI / 180, az = lz * Math.PI / 180;
                    double cx = Math.Cos(ax), sx2 = Math.Sin(ax), cy = Math.Cos(ay), sy2 = Math.Sin(ay), cz = Math.Cos(az), sz = Math.Sin(az);
                    double x1 = cx * vx + sx2 * vy, y1 = -sx2 * vx + cx * vy, z1 = vz;
                    double x2 = cy * x1 - sy2 * z1, z2 = sy2 * x1 + cy * z1, y2 = y1;
                    double x3 = cz * x2 - sz * y2, y3 = sz * x2 + cz * y2, z3 = z2;
                    x3 += line.PositionX / 100.0; y3 += line.PositionY / 100.0; z3 += line.PositionZ / 100.0;
                    w.WriteLine(FormattableString.Invariant($"v {x3:R} {y3:R} {z3:R}"));
                }
                for (int i = 0; i < part.VertexCount; i++) w.WriteLine(FormattableString.Invariant($"vn {part.UniqueNormals[i * 3]:R} {part.UniqueNormals[i * 3 + 1]:R} {part.UniqueNormals[i * 3 + 2]:R}"));
                for (int i = 0; i < part.VertexCount; i++) w.WriteLine(FormattableString.Invariant($"vt {part.UniqueUvs[i * 2]:R} {part.UniqueUvs[i * 2 + 1]:R}"));
                for (int t = 0; t < part.Indices.Length; t += 3)
                {
                    uint a = part.Indices[t], b = part.Indices[t + 1], c = part.Indices[t + 2];
                    w.WriteLine($"f {baseIndex + (int)a}/{baseIndex + (int)a}/{baseIndex + (int)a} {baseIndex + (int)b}/{baseIndex + (int)b}/{baseIndex + (int)b} {baseIndex + (int)c}/{baseIndex + (int)c}/{baseIndex + (int)c}");
                }
                baseIndex += part.VertexCount;
            }
        }

        public void Save(string path) => File.WriteAllBytes(path, RawBytes);

        private static ushort Rd16(byte[] d, int o, Re4Endian e) => e == Re4Endian.Little ? BitConverter.ToUInt16(d, o) : (ushort)((d[o] << 8) | d[o + 1]);
        private static uint Rd32(byte[] d, int o, Re4Endian e) => e == Re4Endian.Little ? BitConverter.ToUInt32(d, o) : ((uint)d[o] << 24) | ((uint)d[o + 1] << 16) | ((uint)d[o + 2] << 8) | d[o + 3];
        private static float RdF(byte[] d, int o, Re4Endian e) => BitConverter.ToSingle(BitConverter.GetBytes(Rd32(d, o, e)), 0);
    }

    // SMX: 144-byte records, magic 0x10, count at +1. Header parse for scenario tint/movement metadata.
    public sealed class Re4SmxEntry
    {
        public byte UseSmxId, Mode, OpacityHierarchy, FaceCulling;
        public byte[] Raw144 = new byte[144];
        public float TexU, TexV;
        public byte[] ColorRgb = new byte[3];
    }

    public static class Re4SmxModel
    {
        public static List<Re4SmxEntry> Load(string path)
        {
            byte[] d = File.ReadAllBytes(path);
            if (d.Length < 16 || d[0] != 0x10) throw new InvalidDataException("Invalid SMX file (magic must be 0x10).");
            int amount = d[1];
            int need = amount * 144 + 16;
            if (d.Length < need) throw new InvalidDataException("SMX file is smaller than its entry count.");
            var list = new List<Re4SmxEntry>();
            for (int i = 0; i < amount; i++)
            {
                var e = new Re4SmxEntry();
                Array.Copy(d, 16 + i * 144, e.Raw144, 0, 144);
                e.UseSmxId = d[16 + i * 144]; e.Mode = d[16 + i * 144 + 1];
                e.OpacityHierarchy = d[16 + i * 144 + 2]; e.FaceCulling = d[16 + i * 144 + 3];
                e.ColorRgb = new[] { d[16 + i * 144 + 12], d[16 + i * 144 + 13], d[16 + i * 144 + 14] };
                list.Add(e);
            }
            return list;
        }
        public static void Save(string path, byte[] raw) => File.WriteAllBytes(path, raw);
    }
}

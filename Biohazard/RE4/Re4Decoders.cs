// GC/Wii + PS2 BIN decoders adapted from JADERLINK_MODEL_VIEWER (MIT).
// Upstream: ALL_VIEWER/SHARED_GCWII_BIN/GcWiiBinDecoder.cs, ALL_VIEWER/PS2_BIN/BINdecoder.cs,
//           ALL_VIEWER/RE4_GCWII_MODEL_VIEWER/src/LoadGcWiiBinModel.cs,
//           ALL_VIEWER/RE4_PS2_MODEL_VIEWER/src/LoadPs2BinModel.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tool_Hazard.Biohazard.RE4
{
    internal static class Re4GcwiiDecoder
    {
        public static Re4BinModel Decode(byte[] data, int start)
        {
            var e = Re4Endian.Big;
            var m = new Re4BinModel { Kind = Re4BinKind.Gcwii, Endian = e, RawBytes = data, StartOffset = start, Description = "RE4 GC/Wii BIN" };
            int p = start;
            uint boneOff = Rd32(data, ref p, e);
            if (!(boneOff == 0x40 || boneOff == 0x60)) throw new InvalidDataException($"Invalid GC/Wii BIN header 0x{boneOff:X}.");
            Rd32(data, ref p, e); Rd32(data, ref p, e);
            uint colorOff = Rd32(data, ref p, e);
            uint uvOff = Rd32(data, ref p, e);
            uint wmOff = Rd32(data, ref p, e);
            byte wmCount = data[p++]; byte boneCount = data[p++]; ushort matCount = Rd16(data, ref p, e);
            uint matOff = Rd32(data, ref p, e);
            uint flags = Rd32(data, ref p, e); uint texCount = Rd32(data, ref p, e);
            byte vScale = data[p++]; p++; ushort wm2Count = Rd16(data, ref p, e);
            Rd32(data, ref p, e); uint posOff = Rd32(data, ref p, e); uint nrmOff = Rd32(data, ref p, e);
            Rd16(data, ref p, e); Rd16(data, ref p, e); Rd32(data, ref p, e);
            uint bonePairOff = 0;
            if (boneOff >= 0x50) { bonePairOff = Rd32(data, ref p, e); Rd32(data, ref p, e); Rd32(data, ref p, e); Rd32(data, ref p, e); }
            m.BinFlags = flags; m.VertexScale = vScale; m.TexCount = texCount;
            bool modern = (flags & 0x80000000u) != 0;
            bool altNormals = (flags & 0x20000000u) != 0;
            m.HasVertexColors = (flags & 0x40000000u) != 0;
            bool bonepairTag = (flags & 0x00000100u) != 0;

            // Pass 1: collect triangles as tuples + true counts.
            var perMaterial = new List<List<(int V, int N, int U, int C)>>();
            var matRaw = new int[matCount];
            int iV = 0, iN = 0, iUv = 0, iC = 0;
            int mp = start + (int)matOff;
            for (int i = 0; i < matCount; i++)
            {
                matRaw[i] = mp;
                var mat = new Re4Material(); Array.Copy(data, mp, mat.Raw24, 0, 24);
                mat.MaterialFlag = data[mp + 11]; mat.DiffuseMap = data[mp + 12]; mat.DiffuseTextureIndex = mat.DiffuseMap;
                m.Materials.Add(mat);
                mp += 24;
                uint bufSize = Rd32At(data, mp, e); mp += 8; int blockStart = mp;
                var corners = new List<(int V, int N, int U, int C)>();
                while (mp < blockStart + (int)bufSize)
                {
                    byte ft = data[mp++]; if (!(ft == 0x98 || ft == 0x90 || ft == 0x80 || ft == 0xA0)) break;
                    ushort fc = Rd16At(data, mp, e); mp += 2;
                    var arr = new (ushort V, ushort N, ushort C, ushort U)[fc];
                    for (int k = 0; k < fc; k++)
                    {
                        ushort v = Rd16At(data, mp, e); ushort n = Rd16At(data, mp + 2, e); mp += 4;
                        ushort c = 0; if (modern) { c = Rd16At(data, mp, e); mp += 2; }
                        ushort u = Rd16At(data, mp, e); mp += 2;
                        arr[k] = (v, n, c, u);
                        iV = Math.Max(iV, v + 1); iN = Math.Max(iN, n + 1); iUv = Math.Max(iUv, u + 1); iC = Math.Max(iC, c + 1);
                    }
                    void AddCorner((ushort V, ushort N, ushort C, ushort U) x) => corners.Add((x.V, x.N, x.U, x.C));
                    void AddTri((ushort V, ushort N, ushort C, ushort U) a, (ushort V, ushort N, ushort C, ushort U) b, (ushort V, ushort N, ushort C, ushort U) c)
                    {
                        if (a.V == b.V || a.V == c.V || b.V == c.V) return;
                        AddCorner(a); AddCorner(b); AddCorner(c);
                    }
                    if (ft == 0x98 && fc >= 3)
                    {
                        int bk = -1; AddTri(arr[0], arr[1], arr[2]); bk *= -1;
                        for (int k = 1; k < fc - 2; k++) { bk *= -1; if (bk == 1) AddTri(arr[k], arr[k + 1], arr[k + 2]); else AddTri(arr[k + 2], arr[k + 1], arr[k]); }
                    }
                    else if (ft == 0x90 && fc >= 3)
                    {
                        for (int k = 0; k < fc / 3; k++) AddTri(arr[k * 3], arr[k * 3 + 1], arr[k * 3 + 2]);
                    }
                    else if (ft == 0x80 && fc >= 4)
                    {
                        for (int k = 0; k < fc / 4; k++) { AddTri(arr[k * 4], arr[k * 4 + 1], arr[k * 4 + 2]); AddTri(arr[k * 4], arr[k * 4 + 2], arr[k * 4 + 3]); }
                    }
                    else if (ft == 0xA0)
                    {
                        for (int k = 2; k < fc; k++) AddTri(arr[0], arr[k - 1], arr[k]);
                    }
                }
                mp = blockStart + (int)bufSize;
                perMaterial.Add(corners);
            }
            // Pass 2: read arrays.
            float scale = 100f * (float)Math.Pow(2, vScale);
            var posArr = new (float X, float Y, float Z)[iV];
            int po = start + (int)posOff;
            for (int i = 0; i < iV; i++) posArr[i] = (Rd16(data, po + i * 8, e) / scale, Rd16(data, po + i * 8 + 2, e) / scale, Rd16(data, po + i * 8 + 4, e) / scale);
            int uo = start + (int)uvOff;
            var uvArr = new (float U, float V)[iUv];
            if (modern) { for (int i = 0; i < iUv; i++) uvArr[i] = (data[uo + i * 2] / 255f, data[uo + i * 2 + 1] / 255f); }
            else { for (int i = 0; i < iUv; i++) uvArr[i] = (Rd16(data, uo + i * 4, e) / 32767f, Rd16(data, uo + i * 4 + 2, e) / 32767f); }
            var nrmArr = new (float X, float Y, float Z)[iN];
            int no = start + (int)nrmOff;
            if (altNormals) { for (int i = 0; i < iN; i++) nrmArr[i] = ((sbyte)data[no + i * 4], (sbyte)data[no + i * 4 + 1], (sbyte)data[no + i * 4 + 2]); }
            else { for (int i = 0; i < iN; i++) nrmArr[i] = (Rd16(data, no + i * 8, e), Rd16(data, no + i * 8 + 2, e), Rd16(data, no + i * 8 + 4, e)); }
            var colArr = new (byte A, byte R, byte G, byte B)[Math.Max(0, iC)];
            if (colorOff != 0 && m.HasVertexColors)
            {
                int co = start + (int)colorOff;
                for (int i = 0; i < colArr.Length; i++) { byte b = data[co + i * 4], g = data[co + i * 4 + 1], r = data[co + i * 4 + 2], a = data[co + i * 4 + 3]; colArr[i] = (a, r, g, b); }
            }
            // All* arrays are the deduped unique vertices across materials in order.
            var allPos = new List<float>(); var allNrm = new List<float>(); var allUv = new List<float>(); var allCol = new List<float>();
            var globalMap = new Dictionary<(int V, int N, int U, int C), uint>();
            uint Gmk((int V, int N, int U, int C) x)
            {
                if (globalMap.TryGetValue(x, out uint id)) return id;
                id = (uint)(allPos.Count / 3); globalMap[x] = id;
                var p = posArr[x.V]; allPos.Add(p.X); allPos.Add(p.Y); allPos.Add(p.Z);
                var n = nrmArr[x.N];
                float len = (float)Math.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
                if (len == 0) len = 1;
                allNrm.Add(n.X / len); allNrm.Add(n.Y / len); allNrm.Add(n.Z / len);
                var u = uvArr[x.U]; allUv.Add(u.U); allUv.Add(u.V);
                if (x.C < colArr.Length && m.HasVertexColors) { var c = colArr[x.C]; allCol.Add(c.R / 255f); allCol.Add(c.G / 255f); allCol.Add(c.B / 255f); allCol.Add(c.A / 255f); }
                else { allCol.Add(1); allCol.Add(1); allCol.Add(1); allCol.Add(1); }
                return id;
            }
            for (int i = 0; i < matCount; i++)
            {
                var corners = perMaterial[i];
                var map = new Dictionary<(int V, int N, int U, int C), uint>();
                var idx = new List<uint>();
                uint mk((int V, int N, int U, int C) x)
                {
                    if (map.TryGetValue(x, out uint id)) return id;
                    id = Gmk(x); map[x] = id; return id;
                }
                foreach (var corner in corners) idx.Add(mk(corner));
                var part = new Re4MeshPart { MaterialIndex = i, Name = $"Material_{i}", Indices = idx.ToArray() };
                m.Parts.Add(part);
            }
            m.AllPositions = allPos.ToArray(); m.AllNormals = allNrm.ToArray(); m.AllUvs = allUv.ToArray(); m.AllColors = allCol.ToArray();
            // Per-part slices: unique vertices of each part are consecutive bands
            // because Gmk assigns ids in first-seen order across materials.
            int cur = 0;
            foreach (var part in m.Parts)
            {
                int vc = part.Indices.Length == 0 ? 0 : (int)part.Indices.Max() - cur + 1;
                if (vc < 0) vc = 0;
                float[] vp = new float[vc * 3], vn = new float[vc * 3], vt = new float[vc * 2], vc4 = new float[vc * 4];
                Array.Copy(m.AllPositions, cur * 3, vp, 0, Math.Min(vp.Length, m.AllPositions.Length - cur * 3));
                Array.Copy(m.AllNormals, cur * 3, vn, 0, Math.Min(vn.Length, m.AllNormals.Length - cur * 3));
                Array.Copy(m.AllUvs, cur * 2, vt, 0, Math.Min(vt.Length, m.AllUvs.Length - cur * 2));
                Array.Copy(m.AllColors, cur * 4, vc4, 0, Math.Min(vc4.Length, m.AllColors.Length - cur * 4));
                part.Vertices = vp; part.UniqueNormals = vn; part.UniqueUvs = vt; part.UniqueColors = vc4;
                part.Positions = vp; part.Normals = vn; part.Uvs = vt; part.Colors = vc4;
                for (int k = 0; k < part.Indices.Length; k++) part.Indices[k] = (uint)((int)part.Indices[k] - cur);
                cur += vc;
            }
            if (boneCount != 0)
            {
                int bo = start + (int)boneOff;
                for (int i = 0; i < boneCount; i++)
                {
                    int o = bo + i * 16;
                    m.Bones.Add(new Re4Bone { BoneId = data[o], BoneParent = data[o + 1], X = Re4BinModel.ReadF32Direct(data, o + 4, e), Y = Re4BinModel.ReadF32Direct(data, o + 8, e), Z = Re4BinModel.ReadF32Direct(data, o + 12, e) });
                }
            }
            int wmTotal = wm2Count > 255 ? wm2Count : wmCount;
            if (wmOff != 0 && wmTotal > 0)
            {
                int wo = start + (int)wmOff;
                for (int i = 0; i < wmTotal; i++)
                {
                    int o = wo + i * 8;
                    m.WeightMaps.Add(new Re4WeightMap { B1 = data[o], B2 = data[o + 1], B3 = data[o + 2], Count = data[o + 3], W1 = data[o + 4], W2 = data[o + 5], W3 = data[o + 6], Unused = data[o + 7] });
                }
            }
            if (bonePairOff != 0 && bonepairTag)
            {
                int bpo = start + (int)bonePairOff;
                uint count = Rd32At(data, bpo, e);
                for (uint i = 0; i < count && i < 0x10000; i++)
                {
                    int o = bpo + 4 + (int)i * 8;
                    m.BonePairs.Add((Rd16At(data, o, e), Rd16At(data, o + 2, e), Rd16At(data, o + 4, e), Rd16At(data, o + 6, e)));
                }
            }
            m.MaterialRawOffsets = matRaw;
            m.PosOffset = start + (int)posOff; m.NrmOffset = start + (int)nrmOff; m.UvOffset = start + (int)uvOff;
            m.EndOffset = p;
            return m;
        }
        private static uint Rd32At(byte[] d, int o, Re4Endian e) => Re4BinModel.ReadU32(d, o, e);
        private static ushort Rd16At(byte[] d, int o, Re4Endian e) => Re4BinModel.ReadU16(d, o, e);
        private static short Rd16(byte[] d, int o, Re4Endian e) => Re4BinModel.ReadI16(d, o, e);
        private static uint Rd32(byte[] d, ref int p, Re4Endian e) { uint v = Rd32At(d, p, e); p += 4; return v; }
        private static ushort Rd16(byte[] d, ref int p, Re4Endian e) { ushort v = Rd16At(d, p, e); p += 2; return v; }
    }

    internal static class Re4Ps2Decoder
    {
        public static Re4BinModel Decode(byte[] data, int start)
        {
            // Faithful port of PS2_BIN/BINdecoder.cs + PopulateTreatedModel strip logic.
            var m = new Re4BinModel { Kind = Re4BinKind.Ps2Vif, Endian = Re4Endian.Little, RawBytes = data, StartOffset = start, Description = "RE4 PS2 BIN (VIF, read-only)" };
            int p = start;
            ushort magic = LeU16(data, p); p += 2;
            ushort texCount = LeU16(data, p); p += 2;
            uint bonesPoint = LeU32(data, p); p += 4;
            byte vScale = data[p++]; byte bonesCount = data[p++];
            ushort matCount = LeU16(data, p); p += 2;
            uint matOff = LeU32(data, p); p += 4;
            uint pad1 = LeU32(data, p); p += 4;
            LeU32(data, p); p += 4;
            LeU32(data, p); p += 4; uint bpPoint = LeU32(data, p); p += 4;
            LeU32(data, p); p += 4; LeU32(data, p); p += 4; LeU32(data, p); p += 4; LeU32(data, p); p += 4;
            p += 12; p += 4 + 12 + 4;
            if (magic != 0x0030) throw new InvalidDataException($"Invalid PS2 BIN magic 0x{magic:X4}.");
            // Bonepairs (skipped for geometry).
            // Bones raw 16 bytes each.
            int bo = start + (int)bonesPoint;
            for (int i = 0; i < bonesCount; i++)
            {
                int o = bo + i * 16;
                m.Bones.Add(new Re4Bone { BoneId = data[o], BoneParent = data[o + 1], X = BitConverter.ToSingle(data, o + 4), Y = BitConverter.ToSingle(data, o + 8), Z = BitConverter.ToSingle(data, o + 12) });
            }
            // Materials: 16 bytes each, node pointer at +12.
            var nodePtrs = new uint[matCount];
            int mo = start + (int)matOff;
            for (int i = 0; i < matCount; i++)
            {
                int o = mo + i * 16;
                var mat = new Re4Material();
                Array.Copy(data, o, mat.Raw24, 0, 16);
                // PS2 materialLine diffuse_map at byte 2 (MaterialPart layout differs); keep generic mapping.
                mat.DiffuseMap = data[o + 2]; mat.DiffuseTextureIndex = data[o + 2];
                mat.MaterialFlag = data[o + 3];
                m.Materials.Add(mat);
                nodePtrs[i] = BitConverter.ToUInt32(data, o + 12);
            }
            bool scenarioColors = false;
            for (int t = 0; t < matCount; t++)
            {
                int nodePos = start + (int)nodePtrs[t];
                if (nodePos < 0 || nodePos + 4 > data.Length) continue;
                int totalBytes = BitConverter.ToUInt16(data, nodePos);
                byte segPlus1 = data[nodePos + 2];
                byte boneIds = data[nodePos + 3];
                int segs = segPlus1 + 1;
                int calc = 4 + boneIds;
                int parts = (calc + 15) / 16;
                int boneList = parts * 16 - 4;
                int cursor = nodePos + 4 + boneList;
                var verts = new List<float>(); var nrms = new List<float>(); var uvs = new List<float>(); var cols = new List<float>(); var idx = new List<uint>();
                uint counter = 0; bool inv = false;
                for (int s = 0; s < segs; s++)
                {
                    if (cursor + 0x30 > data.Length) break;
                    byte[] h1 = Slice(data, cursor, 0x10);
                    bool hasWeightMap = h1[12] == 0x00 && h1[14] > 1;
                    // Upstream: scenario-color segments are the non-weight path.
                    bool segmentScenarioColors = !hasWeightMap;
                    if (segmentScenarioColors) scenarioColors = true;
                    int skip = 0x10;
                    if (hasWeightMap)
                    {
                        // WeightMap variant: one weight table block, then second header.
                        int wmBytes = h1[0x0] * 0x10;
                        skip += wmBytes + 0x10;
                    }
                    int top2 = cursor + skip; int top3 = top2 + 0x10;
                    if (top3 + 0x10 > data.Length) break;
                    float conv = BitConverter.ToSingle(data, top2 + 0x0C);
                    int chunk = data[top3] * 0x10;
                    int lineCount = data[top2];
                    int chunkStart = top3 + 0x10;
                    if (chunkStart + chunk > data.Length) break;
                    for (int l = 0; l < lineCount; l++)
                    {
                        int o = chunkStart + l * 24;
                        if (o + 24 > data.Length) break;
                        float x = BitConverter.ToInt16(data, o) * conv / 100f;
                        float y = BitConverter.ToInt16(data, o + 2) * conv / 100f;
                        float z = BitConverter.ToInt16(data, o + 4) * conv / 100f;
                        int tu, tv; float nx, ny, nz; float r = 1, g = 1, b = 1, a = 1;
                        if (segmentScenarioColors)
                        {
                            tu = BitConverter.ToInt16(data, o + 8); tv = BitConverter.ToInt16(data, o + 10);
                            r = BitConverter.ToInt16(data, o + 16) / 128f; g = BitConverter.ToInt16(data, o + 18) / 128f;
                            b = BitConverter.ToInt16(data, o + 20) / 128f; a = BitConverter.ToInt16(data, o + 22) / 128f;
                            nx = 0; ny = 1; nz = 0;
                        }
                        else
                        {
                            tu = BitConverter.ToInt16(data, o + 16); tv = BitConverter.ToInt16(data, o + 18);
                            nx = BitConverter.ToInt16(data, o + 8); ny = BitConverter.ToInt16(data, o + 10); nz = BitConverter.ToInt16(data, o + 12);
                            float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz); if (len == 0) len = 1;
                            nx /= len; ny /= len; nz /= len;
                        }
                        verts.Add(x); verts.Add(y); verts.Add(z);
                        nrms.Add(nx); nrms.Add(ny); nrms.Add(nz);
                        uvs.Add(tu / 255f); uvs.Add(tv / 255f);
                        cols.Add(r); cols.Add(g); cols.Add(b); cols.Add(a);
                        ushort complement = BitConverter.ToUInt16(data, o + 14);
                        if (complement == 0 && counter >= 2)
                        {
                            uint a1 = counter - 2, b1 = counter - 1, c1 = counter;
                            if (!inv) { idx.Add(a1); idx.Add(b1); idx.Add(c1); inv = true; }
                            else { idx.Add(c1); idx.Add(b1); idx.Add(a1); inv = false; }
                        }
                        else inv = false;
                        counter++;
                    }
                    cursor = chunkStart + chunk + (s > 0 ? 0x10 : 0);
                }
                var part = new Re4MeshPart { MaterialIndex = t, Name = $"Material_{t}", Indices = idx.ToArray() };
                part.Vertices = verts.ToArray(); part.UniqueNormals = nrms.ToArray(); part.UniqueUvs = uvs.ToArray(); part.UniqueColors = cols.ToArray();
                part.Positions = part.Vertices; part.Normals = part.UniqueNormals; part.Uvs = part.UniqueUvs; part.Colors = part.UniqueColors;
                m.Parts.Add(part);
            }
            if (m.Parts.Count == 0) throw new InvalidDataException("PS2 BIN contained no decodable VIF segments.");
            m.TexCount = texCount; m.VertexScale = vScale; m.EndOffset = p;
            return m;
        }
        private static ushort LeU16(byte[] d, int o) => BitConverter.ToUInt16(d, o);
        private static uint LeU32(byte[] d, int o) => BitConverter.ToUInt32(d, o);
        private static byte[] Slice(byte[] d, int o, int n)
        {
            var r = new byte[n]; Array.Copy(d, o, r, 0, n); return r;
        }
    }
}

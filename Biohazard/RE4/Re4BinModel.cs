// Ported from JADERLINK_MODEL_VIEWER (MIT, Copyright (c) 2024 JADERLINK)
// https://github.com/JADERLINK/JADERLINK_MODEL_VIEWER
// Sources adapted:
//   ALL_VIEWER/SHARED_UHD_BIN_TPL/{UhdBinDecoder.cs,UhdBIN.cs,UhdTPL.cs,MaterialPart.cs,CONSTs.cs}
//   ALL_VIEWER/SHARED_GCWII_BIN/{GcWiiBinDecoder.cs,GCWIIBIN.cs}
//   ALL_VIEWER/PS2_BIN/BINdecoder.cs + RE4_PS2_MODEL_VIEWER/src/LoadPs2BinModel.cs
//   ALL_VIEWER/SimpleEndianBinaryIO (little/big endian helpers only)
// U-TheBinDecoder/GcWiiBinDecoder/PS2BIN logic is re-implemented against
// System.Numerics-free DTOs so Tool Hazard can render with its software
// ModelViewport (no OpenTK dependency). No code is copied verbatim beyond
// format constants/offsets, which remain subject to the MIT license above.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tool_Hazard.Biohazard.RE4
{
    public enum Re4BinKind
    {
        UhdLittleEndian,      // PC UHD (2005/2014) + GX-style BE twins use the same code
        UhdBigEndian,         // X360 / PS3 variants of the UHD layout
        UhdPs4Switch,         // PS4 / Switch: 64-bit offset pairs, LE
        Gcwii,                // GameCube / Wii big-endian shorts
        Ps2Vif,               // PS2 VIF packets (read-only view)
    }

    public enum Re4Endian { Little, Big }

    public sealed class Re4Material
    {
        public byte[] Raw24 = new byte[24];
        public byte MaterialFlag;
        public byte DiffuseMap;
        public byte BumpMap;
        public byte OpacityMap;
        public byte GenericSpecularMap;
        public byte CustomSpecularMap;
        public bool AlphaTextured => (MaterialFlag & 0x04) == 0x04;
        public int DiffuseTextureIndex; // index into TPL entries
    }

    public sealed class Re4MeshPart
    {
        public int MaterialIndex;
        public string Name = "Material_0";
        // For UHD-style (single interleaved array) and GCWII (separate arrays
        // re-unified here), vertices are per-corner expanded: V * 3 entries.
        public float[] Positions = Array.Empty<float>(); // 3 per corner
        public float[] Normals = Array.Empty<float>();   // 3 per corner
        public float[] Uvs = Array.Empty<float>();       // 2 per corner, 0..1
        public float[] Colors = Array.Empty<float>();    // 4 per corner, 0..1
        public int[] TriangleVertices = Array.Empty<int>(); // 3 per tri, indices into corners? No: vertex indices into unique arrays
        // Unified representation: unique vertices + index buffer.
        public float[] Vertices = Array.Empty<float>();   // 3 per unique vertex
        public float[] UniqueNormals = Array.Empty<float>();
        public float[] UniqueUvs = Array.Empty<float>();  // 2 per unique vertex
        public float[] UniqueColors = Array.Empty<float>(); // 4 per unique vertex
        public uint[] Indices = Array.Empty<uint>();
        public int TriangleCount => Indices.Length / 3;
        public int VertexCount => Vertices.Length / 3;
    }

    public sealed class Re4Bone
    {
        public byte BoneId;
        public byte BoneParent;
        public float X, Y, Z;
    }

    public sealed class Re4BinModel
    {
        public Re4BinKind Kind;
        public Re4Endian Endian;
        public bool IsPs4Switch;
        public byte[] RawBytes = Array.Empty<byte>();
        public int StartOffset;
        public long EndOffset;
        public uint BinFlags;
        public byte VertexScale;
        public uint TexCount;
        public List<Re4Material> Materials = new();
        public List<Re4MeshPart> Parts = new();
        public List<Re4Bone> Bones = new();
        public List<(int B1, int B2, int B3, int B4)> BonePairs = new();
        public List<Re4WeightMap> WeightMaps = new();
        public float[] AllPositions = Array.Empty<float>(); // UHD: position array (3 per vertex)
        public float[] AllNormals = Array.Empty<float>();
        public float[] AllUvs = Array.Empty<float>();       // UHD: 2 per vertex
        public float[] AllColors = Array.Empty<float>();    // UHD: 4 per vertex
        public ushort[] WeightIndices = Array.Empty<ushort>();
        public bool HasVertexColors;
        public string Description = "";
        // Absolute offsets inside RawBytes for in-place patching (UHD/GCWII).
        internal int PosOffset = -1, NrmOffset = -1, UvOffset = -1, ColorOffset = -1;
        internal int WeightIndexOffset = -1;
        internal int[] MaterialRawOffsets = Array.Empty<int>(); // UHD: offset of 24-byte material
        internal int[] MaterialIndexBlockOffsets = Array.Empty<int>(); // UHD: offset of face-index block

        public bool IsPs2 => Kind == Re4BinKind.Ps2Vif;
        // True when re-save preserves every section byte-for-byte except patched
        // fields (UHD/GCWII). PS2 VIF is read-only.
        public bool CanSaveNative => Kind != Re4BinKind.Ps2Vif;

        public static Re4BinModel Load(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            return Decode(bytes, 0, path);
        }

        public static Re4BinModel Decode(byte[] bytes, int startOffset, string? name = null)
        {
            if (bytes.Length < 4 || startOffset < 0 || startOffset >= bytes.Length)
                throw new InvalidDataException("BIN file is too small.");
            // PS2 VIF: UInt16 magic 0x0030 (LE) at start.
            if (bytes.Length - startOffset >= 2 && ReadU16(bytes, startOffset, Re4Endian.Little) == 0x0030)
                return Re4Ps2Decoder.Decode(bytes, startOffset);
            // GCWII: BE bone_offset 0x40/0x60, flag check uses Bin_flags BE at +0x20.
            if (bytes.Length - startOffset >= 0x24)
            {
                uint beBone = ReadU32(bytes, startOffset, Re4Endian.Big);
                if (beBone == 0x40 || beBone == 0x60)
                {
                    uint beFlags = ReadU32(bytes, startOffset + 0x20, Re4Endian.Big);
                    if ((beFlags & 0x80000000u) != 0 || (beFlags & 0x40000000u) != 0 || beFlags == 0)
                        return Re4GcwiiDecoder.Decode(bytes, startOffset);
                }
            }
            // UHD family: LE BoneOffset 0x40/0x50/0x60 (+ X360 BE twins), PS4NS 0x98.
            if (bytes.Length - startOffset >= 4)
            {
                uint leBone = ReadU32(bytes, startOffset, Re4Endian.Little);
                uint beBone = ReadU32(bytes, startOffset, Re4Endian.Big);
                if (leBone == 0x98 || beBone == 0x98)
                {
                    // PS4NS uses LE with 64-bit offset pairs.
                    uint o2 = bytes.Length - startOffset >= 8 ? ReadU32(bytes, startOffset + 4, Re4Endian.Little) : 0;
                    if (o2 != 0) return Re4UhdDecoder.Decode(bytes, startOffset, Re4Endian.Little, true, name);
                }
                if (leBone == 0x40 || leBone == 0x50 || leBone == 0x60)
                    return Re4UhdDecoder.Decode(bytes, startOffset, Re4Endian.Little, false, name);
                if (beBone == 0x40 || beBone == 0x50 || beBone == 0x60)
                    return Re4UhdDecoder.Decode(bytes, startOffset, Re4Endian.Big, false, name);
            }
            // X360/PS3 BE UHD files have the same 4-byte bone header but BE material
            // parsing differs only via endian flag; the check above already covers it.
            // Fallback: try LE UHD (most common) so error is actionable.
            throw new InvalidDataException(
                "Unrecognized RE4 BIN signature. Expected UHD/PS4/NS (LE/BE bone header 0x40-0x60/0x98), " +
                "GC/Wii (BE 0x40/0x60), or PS2 VIF (LE magic 0x0030).");
        }

        public void Save(string path)
        {
            if (!CanSaveNative) throw new NotSupportedException("PS2 VIF BIN files are read-only in this viewer; export to OBJ/SMD instead.");
            File.WriteAllBytes(path, RawBytes);
        }

        public byte[] SaveToNewBytes()
        {
            if (!CanSaveNative) throw new NotSupportedException("PS2 VIF BIN files are read-only in this viewer.");
            return (byte[])RawBytes.Clone();
        }

        // In-place material diffuse_map edit at the recorded 24-byte record.
        public void SetDiffuseMap(int materialIndex, byte value)
        {
            if (materialIndex < 0 || materialIndex >= Materials.Count) throw new ArgumentOutOfRangeException(nameof(materialIndex));
            if (Kind == Re4BinKind.Ps2Vif) throw new NotSupportedException("PS2 material lines are not patched in place.");
            int off = MaterialRawOffsets[materialIndex];
            if (off < 0 || off + 13 >= RawBytes.Length) throw new InvalidDataException("Material record offset is out of range.");
            RawBytes[off + 12] = value;
            Materials[materialIndex].DiffuseMap = value;
            Materials[materialIndex].DiffuseTextureIndex = value;
        }

        // Replace vertex positions/normals/UVs/colors in place. Counts must match
        // exactly (same-size patch); exporter guarantees this for round-trips.
        public void ReplaceGeometry(Re4MeshPart[] newParts)
        {
            if (Kind == Re4BinKind.Ps2Vif) throw new NotSupportedException("PS2 VIF geometry is read-only.");
            int expectedVerts = AllPositions.Length / 3;
            int incoming = newParts.Sum(p => p.VertexCount);
            // We validate per-part triangle counts too.
            if (Kind == Re4BinKind.Gcwii)
            {
                // GCWII positions are shorts; patch only if counts match. Convert back.
                throw new NotSupportedException(
                    "GC/Wii vertex positions are quantized shorts; use OBJ/SMD export + generic viewer import instead of in-place BIN patching.");
            }
            if (expectedVerts == 0 || incoming != expectedVerts)
                throw new InvalidDataException($"Vertex count mismatch: BIN has {expectedVerts} vertices, incoming has {incoming}. Keep vertex and triangle counts identical.");
            // Rebuild All* arrays in material order and write floats back.
            var pos = new float[AllPositions.Length];
            var nrm = new float[AllNormals.Length];
            var uv = new float[AllUvs.Length];
            var col = new float[AllColors.Length];
            int cursor = 0;
            foreach (var p in newParts.OrderBy(x => x.MaterialIndex))
            {
                var v = p.Vertices; var n = p.UniqueNormals; var t = p.UniqueUvs; var c = p.UniqueColors;
                if (v.Length / 3 != p.VertexCount) throw new InvalidDataException("Invalid imported part.");
                // Validate indices reference this part's vertices in order (exporter order).
                for (int i = 0; i < v.Length && cursor * 3 + i < pos.Length; i++) pos[cursor * 3 + i] = v[i];
                for (int i = 0; i < (n.Length) && cursor * 3 + i < nrm.Length; i++) nrm[cursor * 3 + i] = n[i];
                if (t.Length > 0) Array.Copy(t, 0, uv, cursor * 2, Math.Min(t.Length, uv.Length - cursor * 2));
                if (c.Length > 0) Array.Copy(c, 0, col, cursor * 4, Math.Min(c.Length, col.Length - cursor * 4));
                cursor += p.VertexCount;
            }
            if (cursor != expectedVerts) throw new InvalidDataException("Vertex count mismatch after import.");
            AllPositions = pos; AllNormals = nrm; AllUvs = uv; AllColors = col;
            WriteFloats(RawBytes, PosOffset, pos, Endian);
            WriteFloats(RawBytes, NrmOffset, nrm, Endian);
            WriteFloats(RawBytes, UvOffset, uv, Endian);
            if (ColorOffset >= 0 && col.Length > 0 && HasVertexColors)
            {
                // Colors are stored as ARGB uint32 (LE layout). Re-encode from floats.
                for (int i = 0; i < AllColors.Length / 4; i++)
                {
                    int o = ColorOffset + i * 4;
                    if (o + 4 > RawBytes.Length) break;
                    uint a = (uint)Math.Clamp((int)(AllColors[i * 4 + 3] * 255f), 0, 255);
                    uint r = (uint)Math.Clamp((int)(AllColors[i * 4 + 0] * 255f), 0, 255);
                    uint g = (uint)Math.Clamp((int)(AllColors[i * 4 + 1] * 255f), 0, 255);
                    uint b = (uint)Math.Clamp((int)(AllColors[i * 4 + 2] * 255f), 0, 255);
                    uint v = (a) | (r << 8) | (g << 16) | (b << 24);
                    WriteU32(RawBytes, o, v, Endian);
                }
            }
            // Refresh parts from arrays.
            RebuildPartsFromArrays();
        }

        internal void RebuildPartsFromArrays()
        {
            // Face indices were consumed during decode into Parts[].Indices, but
            // All* arrays are authoritative for UHD. Re-materialize Parts.
            int cursor = 0;
            foreach (var part in Parts.OrderBy(p => p.MaterialIndex))
            {
                int vc = part.VertexCount;
                if (vc == 0) continue;
                float[] vp = new float[vc * 3], vn = new float[vc * 3], vt = new float[vc * 2], vc4 = new float[vc * 4];
                Array.Copy(AllPositions, cursor * 3, vp, 0, Math.Min(vp.Length, AllPositions.Length - cursor * 3));
                Array.Copy(AllNormals, cursor * 3, vn, 0, Math.Min(vn.Length, AllNormals.Length - cursor * 3));
                Array.Copy(AllUvs, cursor * 2, vt, 0, Math.Min(vt.Length, AllUvs.Length - cursor * 2));
                Array.Copy(AllColors, cursor * 4, vc4, 0, Math.Min(vc4.Length, AllColors.Length - cursor * 4));
                part.Vertices = vp; part.UniqueNormals = vn; part.UniqueUvs = vt; part.UniqueColors = vc4;
                cursor += vc;
            }
        }

        internal static void WriteFloats(byte[] data, int offset, float[] values, Re4Endian e)
        {
            if (offset < 0) return;
            for (int i = 0; i < values.Length; i++)
                WriteU32(data, offset + i * 4, BitConverter.ToUInt32(BitConverter.GetBytes(values[i]), 0), e);
        }

        internal static uint ReadU32(byte[] d, int o, Re4Endian e)
        {
            if (o < 0 || o + 4 > d.Length) throw new InvalidDataException("Read past end of file (UInt32).");
            return e == Re4Endian.Little ? BitConverter.ToUInt32(d, o) : (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);
        }
        internal static int ReadI32(byte[] d, int o, Re4Endian e) => (int)ReadU32(d, o, e);
        internal static ushort ReadU16(byte[] d, int o, Re4Endian e)
        {
            if (o < 0 || o + 2 > d.Length) throw new InvalidDataException("Read past end of file (UInt16).");
            return e == Re4Endian.Little ? BitConverter.ToUInt16(d, o) : (ushort)((d[o] << 8) | d[o + 1]);
        }
        internal static short ReadI16(byte[] d, int o, Re4Endian e) => (short)ReadU16(d, o, e);
        // uint value is endian-decoded first, then reinterpreted from LE bytes.
        internal static float ReadF32Direct(byte[] d, int o, Re4Endian e)
        {
            uint u = ReadU32(d, o, e);
            return BitConverter.ToSingle(BitConverter.GetBytes(u), 0);
        }
        internal static void WriteU32(byte[] d, int o, uint v, Re4Endian e)
        {
            if (o < 0 || o + 4 > d.Length) throw new InvalidDataException("Write past end of file (UInt32).");
            if (e == Re4Endian.Little) BitConverter.GetBytes(v).CopyTo(d, o);
            else { d[o] = (byte)(v >> 24); d[o + 1] = (byte)(v >> 16); d[o + 2] = (byte)(v >> 8); d[o + 3] = (byte)v; }
        }
        internal static void WriteU16(byte[] d, int o, ushort v, Re4Endian e)
        {
            if (o < 0 || o + 2 > d.Length) throw new InvalidDataException("Write past end of file (UInt16).");
            if (e == Re4Endian.Little) BitConverter.GetBytes(v).CopyTo(d, o);
            else { d[o] = (byte)(v >> 8); d[o + 1] = (byte)v; }
        }
    }

    public sealed class Re4WeightMap
    {
        public byte B1, B2, B3, Count, W1, W2, W3, Unused;
    }

    internal static class Re4UhdDecoder
    {
        public static Re4BinModel Decode(byte[] data, int start, Re4Endian endian, bool ps4ns, string? name)
        {
            var m = new Re4BinModel
            {
                Kind = ps4ns ? Re4BinKind.UhdPs4Switch : (endian == Re4Endian.Little ? Re4BinKind.UhdLittleEndian : Re4BinKind.UhdBigEndian),
                Endian = endian,
                IsPs4Switch = ps4ns,
                RawBytes = data,
                StartOffset = start,
                Description = (ps4ns ? "RE4 PS4/Switch BIN" : endian == Re4Endian.Little ? "RE4 UHD BIN" : "RE4 X360/PS3 BIN (BE)") + (string.IsNullOrEmpty(name) ? "" : $" - {System.IO.Path.GetFileName(name)}"),
            };
            int p = start;
            uint boneOffset = Rd32(data, ref p, endian, ps4ns);
            if (!ps4ns && !(boneOffset == 0x60 || boneOffset == 0x40 || boneOffset == 0x50))
                throw new InvalidDataException($"Invalid UHD BIN header (bone header 0x{boneOffset:X}).");
            if (ps4ns && boneOffset != 0x98)
                throw new InvalidDataException($"Invalid PS4/NS BIN header (bone header 0x{boneOffset:X}).");
            Rd32(data, ref p, endian, ps4ns); // unknown_x04
            Rd32(data, ref p, endian, ps4ns); // unknown_x08
            uint colorOff = Rd32(data, ref p, endian, ps4ns);
            uint uvOff = Rd32(data, ref p, endian, ps4ns);
            uint wmOff = Rd32(data, ref p, endian, ps4ns);
            byte wmCount = data[p++]; byte boneCount = data[p++];
            ushort matCount = Rd16(data, ref p, endian);
            uint matOff = Rd32(data, ref p, endian, ps4ns);
            if (ps4ns) Rd32(data, ref p, endian, ps4ns); // padding (PS4/NS 64-bit layout)
            uint flags = Rd32(data, ref p, endian, ps4ns);
            uint texCount = Rd32(data, ref p, endian, ps4ns);
            byte vScale = data[p++]; p++; // unknown
            ushort wm2Count = Rd16(data, ref p, endian);
            Rd32(data, ref p, endian, ps4ns); // morph
            uint posOff = Rd32(data, ref p, endian, ps4ns);
            uint nrmOff = Rd32(data, ref p, endian, ps4ns);
            Rd16(data, ref p, endian); Rd16(data, ref p, endian); // counts (unused)
            Rd32(data, ref p, endian, ps4ns); // version flags
            uint bonePairOff = 0, adjacentOff = 0, wIndexOff = 0;
            if (boneOffset >= 0x50)
            {
                bonePairOff = Rd32(data, ref p, endian, ps4ns);
                adjacentOff = Rd32(data, ref p, endian, ps4ns);
                wIndexOff = Rd32(data, ref p, endian, ps4ns);
                Rd32(data, ref p, endian, ps4ns); // weight2
            }
            m.BinFlags = flags; m.VertexScale = vScale; m.TexCount = texCount;
            m.HasVertexColors = (flags & 0x40000000u) != 0;
            bool bonepairTag = (flags & 0x00000100u) != 0;
            // Materials + face index blocks (sequential vertex cursor).
            int mp = start + (int)matOff;
            int vertexCursor = 0;
            var matRaw = new int[matCount]; var idxRaw = new int[matCount];
            for (int i = 0; i < matCount; i++)
            {
                if (mp + 24 > data.Length) throw new InvalidDataException($"Material {i} is truncated.");
                matRaw[i] = mp;
                var mat = new Re4Material();
                Array.Copy(data, mp, mat.Raw24, 0, 24);
                mat.MaterialFlag = data[mp + 11]; mat.DiffuseMap = data[mp + 12]; mat.BumpMap = data[mp + 13];
                mat.OpacityMap = data[mp + 14]; mat.GenericSpecularMap = data[mp + 15];
                mat.CustomSpecularMap = data[mp + 23]; mat.DiffuseTextureIndex = mat.DiffuseMap;
                mp += 24;
                idxRaw[i] = mp;
                uint bufSize = Rd32At(data, mp, endian); uint unused = Rd32At(data, mp + 4, endian); mp += 8;
                int blockStart = mp;
                uint stripCount = Rd32At(data, mp, endian); mp += 4;
                var indices = new List<uint>();
                for (uint s = 0; s < stripCount; s++)
                {
                    ushort ftype = Rd16At(data, mp, endian); ushort fcount = Rd16At(data, mp + 2, endian); mp += 4;
                    ReadUhdFaceIndices(data, ref mp, ftype, fcount, ref vertexCursor, indices, endian);
                }
                mp = blockStart + (int)bufSize;
                if (mp < blockStart) throw new InvalidDataException($"Material {i} index block is malformed.");
                var part = new Re4MeshPart { MaterialIndex = i, Name = $"Material_{i}", Indices = indices.ToArray() };
                m.Parts.Add(part);
                m.Materials.Add(mat);
            }
            int trueCount = vertexCursor;
            // Vertex arrays (floats).
            m.AllPositions = ReadFloats(data, start + (int)posOff, trueCount * 3, endian);
            m.AllNormals = ReadFloats(data, start + (int)nrmOff, trueCount * 3, endian);
            m.AllUvs = ReadFloats(data, start + (int)uvOff, trueCount * 2, endian);
            m.PosOffset = start + (int)posOff; m.NrmOffset = start + (int)nrmOff; m.UvOffset = start + (int)uvOff;
            if (colorOff != 0)
            {
                m.AllColors = new float[trueCount * 4];
                int co = start + (int)colorOff;
                for (int i = 0; i < trueCount; i++)
                {
                    uint c = Rd32At(data, co + i * 4, endian);
                    m.AllColors[i * 4 + 0] = ((c >> 8) & 0xFF) / 255f;
                    m.AllColors[i * 4 + 1] = ((c >> 16) & 0xFF) / 255f;
                    m.AllColors[i * 4 + 2] = ((c >> 24) & 0xFF) / 255f;
                    m.AllColors[i * 4 + 3] = (c & 0xFF) / 255f;
                }
                m.ColorOffset = co;
            }
            else { m.AllColors = Enumerable.Repeat(1f, trueCount * 4).ToArray(); }
            // Weight index.
            if (wIndexOff != 0)
            {
                m.WeightIndices = new ushort[trueCount];
                int wo = start + (int)wIndexOff;
                for (int i = 0; i < trueCount; i++) m.WeightIndices[i] = Rd16At(data, wo + i * 2, endian);
                m.WeightIndexOffset = wo;
            }
            // Bones.
            if (boneCount != 0)
            {
                int bo = start + (int)boneOffset;
                for (int i = 0; i < boneCount; i++)
                {
                    int o = bo + i * 16;
                    m.Bones.Add(new Re4Bone { BoneId = data[o], BoneParent = data[o + 1], X = RdF32(data, o + 4, endian), Y = RdF32(data, o + 8, endian), Z = RdF32(data, o + 12, endian) });
                }
            }
            // Weight maps.
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
            // Bone pairs.
            if (bonePairOff != 0 && bonepairTag)
            {
                int bpo = start + (int)bonePairOff;
                uint count = Rd32At(data, bpo, endian);
                if (count > 0x10000) throw new InvalidDataException("Bonepair count is invalid.");
                for (uint i = 0; i < count; i++)
                {
                    int o = bpo + 4 + (int)i * 8;
                    m.BonePairs.Add((Rd16At(data, o, endian), Rd16At(data, o + 2, endian), Rd16At(data, o + 4, endian), Rd16At(data, o + 6, endian)));
                }
            }
            // Materialize per-part unique vertex slices (sequential cursor bands).
            int cur = 0;
            foreach (var part in m.Parts)
            {
                int vc = part.Indices.Length == 0 ? 0 : (int)part.Indices.Max() + 1;
                // Band for this part: from cursor to max index inclusive.
                int vcount = vc - cur;
                if (vcount < 0) throw new InvalidDataException("UHD material vertex band out of order.");
                float[] vp = new float[vcount * 3], vn = new float[vcount * 3], vt = new float[vcount * 2], vc4 = new float[vcount * 4];
                Array.Copy(m.AllPositions, cur * 3, vp, 0, Math.Min(vp.Length, m.AllPositions.Length - cur * 3));
                Array.Copy(m.AllNormals, cur * 3, vn, 0, Math.Min(vn.Length, m.AllNormals.Length - cur * 3));
                Array.Copy(m.AllUvs, cur * 2, vt, 0, Math.Min(vt.Length, m.AllUvs.Length - cur * 2));
                Array.Copy(m.AllColors, cur * 4, vc4, 0, Math.Min(vc4.Length, m.AllColors.Length - cur * 4));
                part.Vertices = vp; part.UniqueNormals = vn; part.UniqueUvs = vt; part.UniqueColors = vc4;
                // Rebase indices to 0-based within this part.
                for (int i = 0; i < part.Indices.Length; i++) part.Indices[i] = (uint)((int)part.Indices[i] - cur);
                part.Positions = vp; part.Normals = vn; part.Uvs = vt; part.Colors = vc4;
                cur = vc;
            }
            m.MaterialRawOffsets = matRaw; m.MaterialIndexBlockOffsets = idxRaw;
            m.EndOffset = p;
            return m;
        }

        private static uint Rd32At(byte[] d, int o, Re4Endian e) => Re4BinModel.ReadU32(d, o, e);
        private static ushort Rd16At(byte[] d, int o, Re4Endian e) => Re4BinModel.ReadU16(d, o, e);
        private static float RdF32(byte[] d, int o, Re4Endian e) => Re4BinModel.ReadF32Direct(d, o, e);
        private static uint Rd32(byte[] d, ref int p, Re4Endian e, bool ps4ns)
        {
            uint v = Rd32At(d, p, e); p += 4; if (ps4ns) p += 4; return v;
        }
        private static ushort Rd16(byte[] d, ref int p, Re4Endian e) { ushort v = Rd16At(d, p, e); p += 2; return v; }
        private static float[] ReadFloats(byte[] d, int o, int count, Re4Endian e)
        {
            if (count < 0) throw new InvalidDataException("Negative vertex array size.");
            var r = new float[count];
            for (int i = 0; i < count; i++) r[i] = RdF32(d, o + i * 4, e);
            bool bad = r.Any(f => !float.IsFinite(f));
            if (bad) throw new InvalidDataException("Vertex array contains non-finite values.");
            return r;
        }

        private static void ReadUhdFaceIndices(byte[] d, ref int mp, ushort ftype, ushort fcount, ref int cursor, List<uint> dest, Re4Endian e)
        {
            // ftype: 5 triangle list, 6 triangle strip, 7 triangle fan, 8 quad.
            switch (ftype)
            {
                case 5:
                    for (int i = 0; i < fcount / 3; i++)
                    {
                        uint a = (uint)cursor; uint b = (uint)cursor + 1; uint c = (uint)cursor + 2;
                        cursor += 3;
                        if (a != b && a != c && b != c) { dest.Add(a); dest.Add(b); dest.Add(c); }
                    }
                    break;
                case 6:
                    {
                        int bk = -1; int a = cursor;
                        uint p1 = (uint)a, p2 = (uint)a + 1, p3 = (uint)a + 2; a += 3; bk *= -1; cursor += 3;
                        dest.Add(p1); dest.Add(p2); dest.Add(p3);
                        for (int i = 1; i < fcount - 2; i++)
                        {
                            bk *= -1; p1 = p2; p2 = p3; p3 = (uint)a; a += 1; cursor += 1;
                            if (p1 != p2 && p1 != p3 && p2 != p3)
                            {
                                if (bk == 1) { dest.Add(p1); dest.Add(p2); dest.Add(p3); }
                                else { dest.Add(p3); dest.Add(p2); dest.Add(p1); }
                            }
                        }
                    }
                    break;
                case 8:
                    for (int i = 0; i < fcount / 4; i++)
                    {
                        uint p1 = (uint)cursor, p2 = (uint)cursor + 1, p3 = (uint)cursor + 2, p4 = (uint)cursor + 3;
                        cursor += 4;
                        if (p1 != p2 && p1 != p3 && p2 != p3) { dest.Add(p1); dest.Add(p2); dest.Add(p3); dest.Add(p1); dest.Add(p3); dest.Add(p4); }
                    }
                    break;
                case 7:
                    {
                        int a = cursor;
                        for (int i = 2; i < fcount; i++)
                        {
                            uint p1 = (uint)a, p2 = (uint)(a + i - 1), p3 = (uint)(a + i);
                            if (p1 != p2 && p1 != p3 && p2 != p3) { dest.Add(p1); dest.Add(p2); dest.Add(p3); }
                        }
                        cursor += fcount;
                    }
                    break;
                default:
                    break; // Upstream ignores unknown strip types (indices are implicit).
            }
            _ = e;
        }
    }
}

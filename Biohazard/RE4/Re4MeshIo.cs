// Generic OBJ/SMD mesh I/O + Valve text-SMD parser.
// Adapted from JADERLINK_MODEL_VIEWER (MIT):
//   ALL_VIEWER/StudioMdlDataReader/SmdReader.cs (SMD_READER_LIB v1.1.1)
//   ALL_VIEWER/JADERLINK_MODEL_VIEWER/src/{LoadOBJ.cs,LoadSMD.cs} (UV/normal handling,
//   triangle fan triangulation, split-by-group behavior) and
//   ALL_VIEWER/CjClutter.ObjLoader.Loader (reference only — replaced by the
//   project's existing WavefrontObjFile parser to avoid a new dependency).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Tool_Hazard.Biohazard.RE4
{
    public sealed class Re4MeshIo
    {
        public static void ExportObj(Re4BinModel model, string path, string? textureBaseName = null)
        {
            using var w = new StreamWriter(path, false, new UTF8Encoding(false));
            w.WriteLine("# RE4 BIN export (Tool Hazard / JADERLINK_MODEL_VIEWER compatible)");
            w.WriteLine($"# {model.Description} | kind={model.Kind} endian={model.Endian}");
            int baseIndex = 1;
            foreach (var part in model.Parts.OrderBy(p => p.MaterialIndex))
            {
                w.WriteLine($"o Material_{part.MaterialIndex}");
                w.WriteLine($"usemtl Material_{part.MaterialIndex}");
                for (int i = 0; i < part.VertexCount; i++)
                    w.WriteLine(FormattableString.Invariant($"v {part.Vertices[i * 3]:R} {part.Vertices[i * 3 + 1]:R} {part.Vertices[i * 3 + 2]:R}"));
                for (int i = 0; i < part.VertexCount; i++)
                    w.WriteLine(FormattableString.Invariant($"vn {part.UniqueNormals[i * 3]:R} {part.UniqueNormals[i * 3 + 1]:R} {part.UniqueNormals[i * 3 + 2]:R}"));
                for (int i = 0; i < part.VertexCount; i++)
                    w.WriteLine(FormattableString.Invariant($"vt {part.UniqueUvs[i * 2]:R} {part.UniqueUvs[i * 2 + 1]:R}"));
                for (int t = 0; t < part.Indices.Length; t += 3)
                {
                    uint a = part.Indices[t], b = part.Indices[t + 1], c = part.Indices[t + 2];
                    if (a >= (uint)part.VertexCount || b >= (uint)part.VertexCount || c >= (uint)part.VertexCount) continue;
                    w.WriteLine($"f {baseIndex + (int)a}/{baseIndex + (int)a}/{baseIndex + (int)a} {baseIndex + (int)b}/{baseIndex + (int)b}/{baseIndex + (int)b} {baseIndex + (int)c}/{baseIndex + (int)c}/{baseIndex + (int)c}");
                }
                baseIndex += part.VertexCount;
            }
        }

        public static void ExportMtl(Re4BinModel model, string objPath, string? textureBaseName = null)
        {
            string mtlPath = Path.ChangeExtension(objPath, ".mtl");
            using var w = new StreamWriter(mtlPath, false, new UTF8Encoding(false));
            foreach (var part in model.Parts.OrderBy(p => p.MaterialIndex))
            {
                w.WriteLine($"newmtl Material_{part.MaterialIndex}");
                w.WriteLine("Ka 1.000 1.000 1.000");
                w.WriteLine("Kd 1.000 1.000 1.000");
                w.WriteLine("Ks 0.100 0.100 0.100");
                w.WriteLine("Ns 20.000");
                w.WriteLine("d 1.000");
                w.WriteLine("illum 2");
            }
        }

        public static void ExportSmd(Re4BinModel model, string path)
        {
            using var w = new StreamWriter(path, false, new UTF8Encoding(false));
            w.WriteLine("version 1");
            w.WriteLine("nodes");
            w.WriteLine("0 \"root\" -1");
            w.WriteLine("end");
            w.WriteLine("skeleton");
            w.WriteLine("time 0");
            w.WriteLine("0 0 0 0 0 0 0");
            w.WriteLine("end");
            w.WriteLine("triangles");
            foreach (var part in model.Parts.OrderBy(p => p.MaterialIndex))
            {
                for (int t = 0; t < part.Indices.Length; t += 3)
                {
                    w.WriteLine($"Material_{part.MaterialIndex}");
                    for (int c = 0; c < 3; c++)
                    {
                        int vi = (int)part.Indices[t + c];
                        if (vi < 0 || vi >= part.VertexCount) continue;
                        w.WriteLine(FormattableString.Invariant(
                            $"0 {part.Vertices[vi * 3]:R} {part.Vertices[vi * 3 + 1]:R} {part.Vertices[vi * 3 + 2]:R} {part.UniqueNormals[vi * 3]:R} {part.UniqueNormals[vi * 3 + 1]:R} {part.UniqueNormals[vi * 3 + 2]:R} {part.UniqueUvs[vi * 2]:R} {part.UniqueUvs[vi * 2 + 1]:R} 0"));
                    }
                }
            }
            w.WriteLine("end");
        }

        // Import OBJ via the project's existing WavefrontObjFile parser.
        // Returns one part per OBJ object; faces triangulated (fan), UVs as stored.
        public static List<Re4MeshPart> ImportObj(string path)
        {
            var obj = new Tool_Hazard.IntelOrca.Biohazard.WavefrontObjFile(path);
            if (obj.Objects.Count == 0) throw new InvalidDataException("OBJ contains no objects/faces.");
            var result = new List<Re4MeshPart>();
            for (int o = 0; o < obj.Objects.Count; o++)
            {
                var verts = new List<float>(); var nrms = new List<float>(); var uvs = new List<float>(); var cols = new List<float>();
                var idx = new List<uint>();
                void AddVertex(Tool_Hazard.IntelOrca.Biohazard.WavefrontObjFile.FaceVertex fv)
                {
                    if (fv.Vertex < 0 || fv.Vertex >= obj.Vertices.Count) throw new InvalidDataException($"OBJ vertex index {fv.Vertex} out of range.");
                    var p = obj.Vertices[fv.Vertex];
                    uint id = (uint)(verts.Count / 3);
                    verts.Add((float)p.x); verts.Add((float)p.y); verts.Add((float)p.z);
                    if (fv.Normal >= 0 && fv.Normal < obj.Normals.Count)
                    {
                        var n = obj.Normals[fv.Normal];
                        nrms.Add((float)n.x); nrms.Add((float)n.y); nrms.Add((float)n.z);
                    }
                    else { nrms.Add(0); nrms.Add(1); nrms.Add(0); }
                    if (fv.Texture >= 0 && fv.Texture < obj.TextureCoordinates.Count)
                    {
                        var t = obj.TextureCoordinates[fv.Texture];
                        uvs.Add((float)t.u); uvs.Add((float)t.v);
                    }
                    else { uvs.Add(0); uvs.Add(0); }
                    cols.Add(1); cols.Add(1); cols.Add(1); cols.Add(1);
                    idx.Add(id);
                }
                foreach (var tri in obj.Objects[o].Triangles)
                {
                    AddVertex(tri.a); AddVertex(tri.b); AddVertex(tri.c);
                }
                foreach (var quad in obj.Objects[o].Quads)
                {
                    AddVertex(quad.a); AddVertex(quad.b); AddVertex(quad.c);
                    AddVertex(quad.a); AddVertex(quad.c); AddVertex(quad.d);
                }
                var part = new Re4MeshPart { MaterialIndex = o, Name = $"Material_{o}", Indices = idx.ToArray() };
                part.Vertices = verts.ToArray(); part.UniqueNormals = nrms.ToArray(); part.UniqueUvs = uvs.ToArray(); part.UniqueColors = cols.ToArray();
                part.Positions = part.Vertices; part.Normals = part.UniqueNormals; part.Uvs = part.UniqueUvs; part.Colors = part.UniqueColors;
                result.Add(part);
            }
            return result;
        }

        public static List<Re4MeshPart> ImportSmd(string path)
        {
            var smd = Re4SmdTextReader.Read(path);
            if (smd.Triangles.Count == 0) throw new InvalidDataException("SMD contains no triangles.");
            var byMaterial = new Dictionary<string, Re4MeshPart>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            foreach (var tri in smd.Triangles)
            {
                string key = tri.Material ?? "Material_0";
                if (!byMaterial.TryGetValue(key, out var part))
                {
                    int mi = order.Count;
                    part = new Re4MeshPart { MaterialIndex = mi, Name = key };
                    byMaterial[key] = part; order.Add(key);
                }
                var verts = part.Vertices.ToList(); var nrms = part.UniqueNormals.ToList();
                var uvs = part.UniqueUvs.ToList(); var cols = part.UniqueColors.ToList(); var idx = part.Indices.ToList();
                foreach (var v in tri.Vertices)
                {
                    uint id = (uint)(verts.Count / 3);
                    verts.Add(v.X); verts.Add(v.Y); verts.Add(v.Z);
                    nrms.Add(v.Nx); nrms.Add(v.Ny); nrms.Add(v.Nz);
                    uvs.Add(v.U); uvs.Add(v.V);
                    cols.Add(1); cols.Add(1); cols.Add(1); cols.Add(1);
                    idx.Add(id);
                }
                part.Vertices = verts.ToArray(); part.UniqueNormals = nrms.ToArray();
                part.UniqueUvs = uvs.ToArray(); part.UniqueColors = cols.ToArray();
                part.Positions = part.Vertices; part.Normals = part.UniqueNormals;
                part.Uvs = part.UniqueUvs; part.Colors = part.UniqueColors; part.Indices = idx.ToArray();
            }
            return order.Select(k => byMaterial[k]).ToList();
        }
    }

    // Valve StudioMdlData text reader — minimal port of SMD_READER_LIB (MIT, JADERLINK).
    public static class Re4SmdTextReader
    {
        public sealed class Smd
        {
            public List<Re4SmdTriangle> Triangles = new();
            public List<(int Id, string Name, int Parent)> Nodes = new();
            public List<List<(int Bone, float X, float Y, float Z, float Rx, float Ry, float Rz)>> Times = new();
        }
        public sealed class Re4SmdTriangle
        {
            public string? Material;
            public List<Re4SmdVertex> Vertices = new();
        }
        public sealed class Re4SmdVertex
        {
            public int ParentBone; public float X, Y, Z, Nx, Ny, Nz, U, V;
            public List<(int Bone, float Weight)> Links = new();
        }

        public static Smd Read(string path)
        {
            using var r = File.OpenText(path);
            return Read(r);
        }
        public static Smd Read(StreamReader r)
        {
            var smd = new Smd();
            string? line;
            bool seenVersion = false;
            string status = "version"; // version|nodes|skeleton|triangles|end
            string? curMaterial = null;
            int triCount = 0, vertexCount = 0;
            while ((line = r.ReadLine()) != null)
            {
                string t = line.Trim();
                if (t.Length == 0 || t.StartsWith("//")) continue;
                if (!seenVersion)
                {
                    if (!t.ToLowerInvariant().StartsWith("version")) throw new InvalidDataException("Invalid SMD: must start with 'version 1'.");
                    seenVersion = true; continue;
                }
                if (status == "version")
                {
                    string low = t.ToLowerInvariant();
                    if (low.StartsWith("nodes")) status = "nodes";
                    else if (low.StartsWith("skeleton")) status = "skeleton";
                    else if (low.StartsWith("triangles")) status = "triangles";
                    else throw new InvalidDataException($"Unsupported SMD section: {t}");
                    continue;
                }
                if (t.ToLowerInvariant().StartsWith("end"))
                {
                    if (status == "triangles") { curMaterial = null; }
                    status = "version";
                    continue;
                }
                switch (status)
                {
                    case "nodes":
                        smd.Nodes.Add(ParseNode(t)); break;
                    case "skeleton":
                        if (t.ToLowerInvariant().StartsWith("time"))
                        {
                            smd.Times.Add(new List<(int, float, float, float, float, float, float)>());
                        }
                        else if (smd.Times.Count > 0)
                        {
                            smd.Times[^1].Add(ParseSkeleton(t));
                        }
                        break;
                    case "triangles":
                        {
                            // 1 material line + 3 vertex lines per triangle (upstream state machine).
                            if (curMaterial == null || triCount == 0)
                            {
                                curMaterial = line.Trim();
                                triCount++;
                                // Material line may be followed by 3 vertex lines; use verbatim trim.
                            }
                            else
                            {
                                // Need to distinguish material vs vertex: vertex lines start with a number.
                                if (IsVertexLine(t))
                                {
                                    var tri = GetOrCreateTriangle(smd, curMaterial!);
                                    tri.Vertices.Add(ParseVertex(t, vertexCount++));
                                    if (tri.Vertices.Count == 3)
                                    {
                                        curMaterial = null;
                                    }
                                }
                                else
                                {
                                    curMaterial = line.Trim();
                                }
                            }
                            break;
                        }
                }
            }
            if (!seenVersion) throw new InvalidDataException("Invalid SMD file.");
            if (status == "nodes" || status == "skeleton" || status == "triangles")
                throw new InvalidDataException("Invalid SMD: missing 'end'.");
            return smd;
        }

        private static Re4SmdTriangle GetOrCreateTriangle(Smd smd, string material)
        {
            var tri = new Re4SmdTriangle { Material = material };
            smd.Triangles.Add(tri);
            return tri;
        }

        private static bool IsVertexLine(string t)
        {
            if (t.Length == 0) return false;
            char c = t[0];
            return char.IsDigit(c) || c == '-' || c == '+';
        }

        private static (int Id, string Name, int Parent) ParseNode(string line)
        {
            var s = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int id = s.Length > 0 ? ParseInt(s[0]) : 0;
            string name = s.Length > 1 ? s[1].Trim('"') : "unknown";
            int parent = s.Length > 2 ? ParseInt(s[2]) : -1;
            return (id, name, parent);
        }
        private static (int, float, float, float, float, float, float) ParseSkeleton(string line)
        {
            var s = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int bone = s.Length > 0 ? ParseInt(s[0]) : 0;
            float[] f = Enumerable.Range(1, 6).Select(i => s.Length > i ? ParseFloat(s[i]) : 0f).ToArray();
            return (bone, f[0], f[1], f[2], f[3], f[4], f[5]);
        }
        private static Re4SmdVertex ParseVertex(string line, int _id)
        {
            var s = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var v = new Re4SmdVertex { ParentBone = s.Length > 0 ? ParseInt(s[0]) : 0 };
            if (s.Length > 3) { v.X = ParseFloat(s[1]); v.Y = ParseFloat(s[2]); v.Z = ParseFloat(s[3]); }
            if (s.Length > 6) { v.Nx = ParseFloat(s[4]); v.Ny = ParseFloat(s[5]); v.Nz = ParseFloat(s[6]); }
            if (s.Length > 8) { v.U = ParseFloat(s[7]); v.V = ParseFloat(s[8]); }
            int links = s.Length > 9 ? ParseInt(s[9]) : 0;
            for (int i = 0; i < links; i++)
            {
                int bone = s.Length > 10 + i * 2 ? ParseInt(s[10 + i * 2]) : 0;
                float w = s.Length > 11 + i * 2 ? ParseFloat(s[11 + i * 2]) : 0f;
                v.Links.Add((bone, w));
            }
            return v;
        }
        private static int ParseInt(string value)
        {
            var sb = new StringBuilder();
            foreach (char c in value)
            {
                if (char.IsDigit(c)) sb.Append(c);
                else if (c == '-' && sb.Length == 0) sb.Append('-');
            }
            return int.TryParse(sb.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : 0;
        }
        private static float ParseFloat(string value)
        {
            var sb = new StringBuilder(); bool dot = false;
            foreach (char c in value)
            {
                if (char.IsDigit(c)) sb.Append(c);
                else if (c == '-' && sb.Length == 0) sb.Append('-');
                else if (c == '.' && !dot) { sb.Append('.'); dot = true; }
            }
            return float.TryParse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : 0f;
        }
    }
}

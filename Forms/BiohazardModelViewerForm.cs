#nullable enable
using System.Numerics;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard;
using IntelOrca.Biohazard.Model;

namespace Tool_Hazard.Forms;

public sealed class BiohazardModelViewerForm : Form
{
    private readonly ModelViewport _viewport = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _mesh = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly CheckedListBox _parts = new() { Dock = DockStyle.Fill, CheckOnClick = true };
    private readonly Label _details = new() { AutoSize = false, Dock = DockStyle.Bottom, Height = 100, Padding = new Padding(6) };
    private ModelFile? _model;
    private TimFile? _texture;
    private string? _texturePath;
    private string? _path;

    public BiohazardModelViewerForm(string? initialPath = null)
    {
        Text = "Biohazard Model Viewer - RE1 / RE2 / RE3";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 500);
        Size = new Size(1050, 720);

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        var open = new ToolStripMenuItem("Open...", null, (_, _) => PromptOpen()) { ShortcutKeys = Keys.Control | Keys.O };
        var openTexture = new ToolStripMenuItem("Load TIM texture...", null, (_, _) => PromptTexture());
        var clearTexture = new ToolStripMenuItem("Clear texture", null, (_, _) => { _texture = null; _texturePath = null; LoadSelectedMesh(); });
        var exit = new ToolStripMenuItem("Exit", null, (_, _) => Close());
        file.DropDownItems.AddRange(new ToolStripItem[] { open, new ToolStripSeparator(), openTexture, clearTexture, new ToolStripSeparator(), exit });
        var view = new ToolStripMenuItem("View");
        view.DropDownItems.Add(new ToolStripMenuItem("Reset camera", null, (_, _) => _viewport.ResetCamera()) { ShortcutKeys = Keys.Control | Keys.R });
        view.DropDownItems.Add(new ToolStripMenuItem("Toggle wireframe", null, (_, _) => _viewport.Wireframe = !_viewport.Wireframe) { ShortcutKeys = Keys.Control | Keys.W });
        menu.Items.AddRange(new ToolStripItem[] { file, view });
        MainMenuStrip = menu;

        var sidebar = new Panel { Dock = DockStyle.Right, Width = 230, Padding = new Padding(8) };
        var meshLabel = new Label { Text = "Mesh", Dock = DockStyle.Top, Height = 22 };
        _mesh.Dock = DockStyle.Top;
        var partsLabel = new Label { Text = "Visible parts", Dock = DockStyle.Top, Height = 28, Padding = new Padding(0, 7, 0, 0) };
        sidebar.Controls.Add(_parts);
        sidebar.Controls.Add(partsLabel);
        sidebar.Controls.Add(_mesh);
        sidebar.Controls.Add(meshLabel);
        sidebar.Controls.Add(_details);

        Controls.Add(_viewport);
        Controls.Add(sidebar);
        Controls.Add(menu);

        _mesh.SelectedIndexChanged += (_, _) => LoadSelectedMesh();
        _parts.ItemCheck += (_, e) => BeginInvoke(new Action(() =>
        {
            _viewport.SetPartVisible(e.Index, e.NewValue == CheckState.Checked);
        }));

        Shown += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(initialPath)) Open(initialPath);
            else PromptOpen();
        };
    }

    private void PromptOpen()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open classic Biohazard model",
            Filter = "Biohazard models (*.emd;*.pld)|*.emd;*.pld|Enemy model (*.emd)|*.emd|Player model (*.pld)|*.pld|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) Open(dialog.FileName);
    }

    private void Open(string path)
    {
        try
        {
            ModelFile model = ModelFile.FromFile(path);
            _model = model;
            _path = path;
            LoadDefaultTexture(model, path);
            _mesh.BeginUpdate();
            _mesh.Items.Clear();
            int number = 0;
            for (int i = 0; i < model.NumChunks; i++)
            {
                if (model.GetChunkKind(i) == ModelFile.ChunkKind.Mesh)
                    _mesh.Items.Add($"Mesh {number++}");
            }
            _mesh.EndUpdate();
            if (_mesh.Items.Count == 0) throw new InvalidDataException("The model contains no mesh chunks.");
            _mesh.SelectedIndex = 0;
            Text = $"Biohazard Model Viewer - {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Open failed:\n{ex.Message}", "Biohazard Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PromptTexture()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Load PlayStation TIM texture",
            Filter = "PlayStation texture (*.tim)|*.tim|All files (*.*)|*.*",
            InitialDirectory = _path == null ? null : Path.GetDirectoryName(_path)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _texture = new TimFile(dialog.FileName);
            _texturePath = dialog.FileName;
            LoadSelectedMesh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Texture load failed:\n{ex.Message}", "Biohazard Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadDefaultTexture(ModelFile model, string modelPath)
    {
        _texture = null;
        _texturePath = null;
        try
        {
            if (model is PldFile pld)
            {
                _texture = pld.Tim;
                _texturePath = "Embedded TIM";
            }
            else
            {
                string path = Path.ChangeExtension(modelPath, ".tim");
                if (!File.Exists(path)) path = Path.ChangeExtension(modelPath, ".TIM");
                if (File.Exists(path))
                {
                    _texture = new TimFile(path);
                    _texturePath = path;
                }
            }
        }
        catch { _texture = null; _texturePath = null; }
    }

    private void LoadSelectedMesh()
    {
        if (_model == null || _mesh.SelectedIndex < 0) return;
        try
        {
            IModelMesh source = _model.GetMesh(_mesh.SelectedIndex);
            var converter = new MeshConverter();
            var mesh = (Md1)converter.ConvertMesh(source, IntelOrca.Biohazard.BioVersion.Biohazard2, remap: false);
            Emr? skeleton = TryGetSkeleton(_model);
            Matrix4x4[] partTransforms = BuildPartTransforms(skeleton, mesh.NumParts);
            var parts = new List<RenderPart>();
            int triangles = 0;
            for (int i = 0; i < mesh.NumParts; i++)
            {
                Md1.ObjectDescriptor triObject = mesh.Objects[i * 2];
                Md1.ObjectDescriptor quadObject = mesh.Objects[i * 2 + 1];
                Vector3[] vertices = mesh.GetPositionData(triObject)
                    .ToArray().Select(v =>
                    {
                        Vector3 p = Vector3.Transform(new Vector3(v.x, v.y, v.z), partTransforms[i]);
                        return new Vector3(p.X, -p.Y, p.Z);
                    }).ToArray();
                var faces = new List<RenderFace>();
                Md1.Triangle[] tris = mesh.GetTriangles(triObject).ToArray();
                Md1.TriangleTexture[] triTextures = mesh.GetTriangleTextures(triObject).ToArray();
                for (int f = 0; f < tris.Length; f++)
                {
                    Md1.Triangle t = tris[f]; Md1.TriangleTexture uv = triTextures[f];
                    faces.Add(new RenderFace(new[] { (int)t.v0, t.v1, t.v2 },
                        new[] { new Vector2(uv.u0, uv.v0), new Vector2(uv.u1, uv.v1), new Vector2(uv.u2, uv.v2) },
                        (byte)(uv.page & 0x0F), ClutFromId(uv.clutId, uv.page)));
                }
                Md1.Quad[] quads = mesh.GetQuads(quadObject).ToArray();
                Md1.QuadTexture[] quadTextures = mesh.GetQuadTextures(quadObject).ToArray();
                for (int f = 0; f < quads.Length; f++)
                {
                    Md1.Quad q = quads[f]; Md1.QuadTexture uv = quadTextures[f];
                    byte page = (byte)(uv.page & 0x0F); int clut = ClutFromId(uv.clutId, uv.page);
                    faces.Add(new RenderFace(new[] { (int)q.v0, q.v1, q.v2 },
                        new[] { new Vector2(uv.u0, uv.v0), new Vector2(uv.u1, uv.v1), new Vector2(uv.u2, uv.v2) }, page, clut));
                    faces.Add(new RenderFace(new[] { (int)q.v3, q.v2, q.v1 },
                        new[] { new Vector2(uv.u3, uv.v3), new Vector2(uv.u2, uv.v2), new Vector2(uv.u1, uv.v1) }, page, clut));
                }
                triangles += triObject.pri_count + quadObject.pri_count * 2;
                parts.Add(new RenderPart(vertices, faces));
            }

            _parts.Items.Clear();
            for (int i = 0; i < parts.Count; i++) _parts.Items.Add($"Part {i:D2}", true);
            _viewport.SetModel(parts, _texture);
            string texture = _texture == null ? "None" : $"{(_texturePath == "Embedded TIM" ? _texturePath : Path.GetFileName(_texturePath))} ({_texture.Width}x{_texture.Height})";
            _details.Text = $"Game: {VersionName(_model.Version)}\r\nType: {Path.GetExtension(_path!).TrimStart('.').ToUpperInvariant()}\r\nParts: {parts.Count:N0}  Triangles: {triangles:N0}\r\nTexture: {texture}\r\nCLUT: per polygon";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Mesh load failed:\n{ex.Message}", "Biohazard Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static int ClutFromId(ushort clutId, ushort page)
    {
        int y = (clutId >> 6) & 0x1FF;
        int index = y - 480;
        return index >= 0 && index < 256 ? index : page & 0x0F;
    }

    private static Emr? TryGetSkeleton(ModelFile model)
    {
        try { return model.GetEmr(0); }
        catch { return null; }
    }

    private static Matrix4x4[] BuildPartTransforms(Emr? skeleton, int partCount)
    {
        var result = Enumerable.Repeat(Matrix4x4.Identity, partCount).ToArray();
        if (skeleton == null || skeleton.NumParts == 0) return result;

        Emr.KeyFrame[] keyFrames = skeleton.KeyFrames;
        bool hasPose = keyFrames.Length != 0;
        var visited = new bool[Math.Min(partCount, skeleton.NumParts)];

        void Visit(int partIndex, Matrix4x4 parent)
        {
            if ((uint)partIndex >= (uint)visited.Length || visited[partIndex]) return;
            visited[partIndex] = true;

            Emr.Vector offset = partIndex == 0 && hasPose
                ? keyFrames[0].Offset
                : skeleton.GetRelativePosition(partIndex);
            Matrix4x4 local = Matrix4x4.Identity;
            if (hasPose)
            {
                Emr.Vector angle = keyFrames[0].GetAngle(partIndex);
                const float unit = MathF.PI * 2.0f / 4096.0f;
                local = Matrix4x4.CreateRotationZ(angle.z * unit)
                    * Matrix4x4.CreateRotationY(angle.y * unit)
                    * Matrix4x4.CreateRotationX(angle.x * unit);
            }
            local *= Matrix4x4.CreateTranslation(offset.x, offset.y, offset.z);
            result[partIndex] = local * parent;

            foreach (byte child in skeleton.GetArmatureParts(partIndex))
                Visit(child, result[partIndex]);
        }

        Visit(0, Matrix4x4.Identity);
        return result;
    }

    private static string VersionName(IntelOrca.Biohazard.BioVersion version) => version switch
    {
        IntelOrca.Biohazard.BioVersion.Biohazard1 => "Resident Evil 1",
        IntelOrca.Biohazard.BioVersion.Biohazard2 => "Resident Evil 2",
        IntelOrca.Biohazard.BioVersion.Biohazard3 => "Resident Evil 3",
        _ => version.ToString()
    };

    private sealed record RenderFace(int[] Indices, Vector2[] UV, byte Page, int Clut);

    private sealed record RenderPart(Vector3[] Vertices, List<RenderFace> Faces)
    {
        public bool Visible { get; set; } = true;
    }

    private sealed class ModelViewport : Control
    {
        private List<RenderPart> _parts = new();
        private float _yaw = -0.55f, _pitch = 0.25f, _zoom = 1;
        private Point _lastMouse;
        private bool _dragging;
        private Vector3 _center;
        private float _radius = 1;
        private bool _wireframe;
        private TimFile? _texture;

        public bool Wireframe { get => _wireframe; set { _wireframe = value; Invalidate(); } }

        public ModelViewport()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(28, 30, 34);
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        public void SetModel(List<RenderPart> parts, TimFile? texture)
        {
            _parts = parts;
            _texture = texture;
            Vector3[] all = parts.SelectMany(x => x.Vertices).ToArray();
            if (all.Length > 0)
            {
                Vector3 min = all.Aggregate(Vector3.Min), max = all.Aggregate(Vector3.Max);
                _center = (min + max) / 2;
                _radius = Math.Max(1, Vector3.Distance(min, max) / 2);
            }
            ResetCamera();
        }

        public void SetPartVisible(int index, bool visible)
        {
            if ((uint)index < (uint)_parts.Count) _parts[index].Visible = visible;
            Invalidate();
        }

        public void ResetCamera() { _yaw = -0.55f; _pitch = 0.25f; _zoom = 1; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e) { _dragging = e.Button == MouseButtons.Left; _lastMouse = e.Location; Capture = _dragging; }
        protected override void OnMouseUp(MouseEventArgs e) { _dragging = false; Capture = false; }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                _yaw += (e.X - _lastMouse.X) * 0.012f;
                _pitch = Math.Clamp(_pitch + (e.Y - _lastMouse.Y) * 0.012f, -1.5f, 1.5f);
                _lastMouse = e.Location;
                Invalidate();
            }
        }
        protected override void OnMouseWheel(MouseEventArgs e) { _zoom = Math.Clamp(_zoom * (e.Delta > 0 ? 1.12f : 0.89f), 0.08f, 12); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (_parts.Count == 0)
            {
                TextRenderer.DrawText(e.Graphics, "Open an EMD or PLD model", Font, ClientRectangle, Color.Gainsboro,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            Matrix4x4 rotation = Matrix4x4.CreateRotationY(_yaw) * Matrix4x4.CreateRotationX(_pitch);
            float scale = Math.Min(Width, Height) * 0.42f / _radius * _zoom;
            int pixelCount = Math.Max(1, Width * Height);
            int[] pixels = Enumerable.Repeat(BackColor.ToArgb(), pixelCount).ToArray();
            float[] depth = Enumerable.Repeat(float.PositiveInfinity, pixelCount).ToArray();
            for (int partIndex = 0; partIndex < _parts.Count; partIndex++)
            {
                RenderPart part = _parts[partIndex];
                if (!part.Visible) continue;
                Vector3[] transformed = part.Vertices.Select(v => Vector3.Transform(v - _center, rotation)).ToArray();
                foreach (RenderFace face in part.Faces)
                {
                    if (face.Indices.Any(i => (uint)i >= (uint)transformed.Length)) continue;
                    Vector3 a = transformed[face.Indices[0]], b = transformed[face.Indices[1]], c = transformed[face.Indices[2]];
                    Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                    float light = Math.Clamp(0.25f + Math.Abs(Vector3.Dot(normal, Vector3.Normalize(new Vector3(-0.4f, -0.7f, -1)))) * 0.75f, 0, 1);
                    Vector3[] screen = face.Indices.Select(i => new Vector3(Width / 2f + transformed[i].X * scale,
                        Height / 2f + transformed[i].Y * scale, transformed[i].Z)).ToArray();
                    RasterTriangle(pixels, depth, screen, face, light, ColorFromPart(partIndex));
                }
            }

            using (var bitmap = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                var bits = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
                Marshal.Copy(pixels, 0, bits.Scan0, pixels.Length);
                bitmap.UnlockBits(bits);
                e.Graphics.DrawImageUnscaled(bitmap, 0, 0);
            }
            TextRenderer.DrawText(e.Graphics, "Drag: orbit   Wheel: zoom   Ctrl+R: reset   Ctrl+W: wireframe", Font,
                new Point(10, Height - 26), Color.Silver);
        }

        private void RasterTriangle(int[] pixels, float[] depth, Vector3[] p, RenderFace face, float light, Color fallback)
        {
            if (Wireframe)
            {
                DrawWire(pixels, p, Color.Gainsboro.ToArgb());
                return;
            }
            float area = Edge(p[0], p[1], p[2].X, p[2].Y);
            if (Math.Abs(area) < 0.001f) return;
            int minX = Math.Clamp((int)MathF.Floor(p.Min(v => v.X)), 0, Width - 1);
            int maxX = Math.Clamp((int)MathF.Ceiling(p.Max(v => v.X)), 0, Width - 1);
            int minY = Math.Clamp((int)MathF.Floor(p.Min(v => v.Y)), 0, Height - 1);
            int maxY = Math.Clamp((int)MathF.Ceiling(p.Max(v => v.Y)), 0, Height - 1);
            Color flat = Color.FromArgb(255, (int)(fallback.R * light), (int)(fallback.G * light), (int)(fallback.B * light));

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float w0 = Edge(p[1], p[2], px, py) / area;
                float w1 = Edge(p[2], p[0], px, py) / area;
                float w2 = 1 - w0 - w1;
                if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                float z = w0 * p[0].Z + w1 * p[1].Z + w2 * p[2].Z;
                int index = y * Width + x;
                if (z >= depth[index]) continue;

                Color color = flat;
                if (_texture != null)
                {
                    int u = (int)MathF.Round(w0 * face.UV[0].X + w1 * face.UV[1].X + w2 * face.UV[2].X) + face.Page * 128;
                    int v = (int)MathF.Round(w0 * face.UV[0].Y + w1 * face.UV[1].Y + w2 * face.UV[2].Y);
                    u = ((u % _texture.Width) + _texture.Width) % _texture.Width;
                    v = ((v % _texture.Height) + _texture.Height) % _texture.Height;
                    try
                    {
                        uint argb = _texture.GetPixel(u, v, face.Clut);
                        color = Color.FromArgb(unchecked((int)argb));
                        if (color.A == 0) continue;
                        color = Color.FromArgb(color.A, (int)(color.R * light), (int)(color.G * light), (int)(color.B * light));
                    }
                    catch
                    {
                        try { color = Color.FromArgb(unchecked((int)_texture.GetPixel(u, v, face.Page))); }
                        catch { color = flat; }
                    }
                }
                depth[index] = z;
                pixels[index] = color.ToArgb();
            }
        }

        private void DrawWire(int[] pixels, Vector3[] p, int color)
        {
            for (int edge = 0; edge < 3; edge++)
            {
                int x0 = (int)p[edge].X, y0 = (int)p[edge].Y;
                int x1 = (int)p[(edge + 1) % 3].X, y1 = (int)p[(edge + 1) % 3].Y;
                int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, error = dx + dy;
                while (true)
                {
                    if ((uint)x0 < (uint)Width && (uint)y0 < (uint)Height) pixels[y0 * Width + x0] = color;
                    if (x0 == x1 && y0 == y1) break;
                    int e2 = error * 2;
                    if (e2 >= dy) { error += dy; x0 += sx; }
                    if (e2 <= dx) { error += dx; y0 += sy; }
                }
            }
        }

        private static float Edge(Vector3 a, Vector3 b, float x, float y) => (x - a.X) * (b.Y - a.Y) - (y - a.Y) * (b.X - a.X);

        private static Color ColorFromPart(int index)
        {
            Color[] colors = { Color.SteelBlue, Color.IndianRed, Color.SeaGreen, Color.Goldenrod, Color.MediumPurple, Color.CadetBlue };
            return colors[index % colors.Length];
        }
    }
}

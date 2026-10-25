#nullable enable
using System.Numerics;
using IntelOrca.Biohazard.Model;

namespace Tool_Hazard.Forms;

public sealed class BiohazardModelViewerForm : Form
{
    private readonly ModelViewport _viewport = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _mesh = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly CheckedListBox _parts = new() { Dock = DockStyle.Fill, CheckOnClick = true };
    private readonly Label _details = new() { AutoSize = false, Dock = DockStyle.Bottom, Height = 100, Padding = new Padding(6) };
    private ModelFile? _model;
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
        var exit = new ToolStripMenuItem("Exit", null, (_, _) => Close());
        file.DropDownItems.AddRange(new ToolStripItem[] { open, new ToolStripSeparator(), exit });
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

    private void LoadSelectedMesh()
    {
        if (_model == null || _mesh.SelectedIndex < 0) return;
        try
        {
            IModelMesh source = _model.GetMesh(_mesh.SelectedIndex);
            var converter = new MeshConverter();
            var mesh = (Md1)converter.ConvertMesh(source, IntelOrca.Biohazard.BioVersion.Biohazard2, remap: false);
            Emr? skeleton = TryGetSkeleton(_model);
            var parts = new List<RenderPart>();
            int triangles = 0;
            for (int i = 0; i < mesh.NumParts; i++)
            {
                Md1.ObjectDescriptor triObject = mesh.Objects[i * 2];
                Md1.ObjectDescriptor quadObject = mesh.Objects[i * 2 + 1];
                Vector3 translation = Vector3.Zero;
                if (skeleton != null && i < skeleton.NumParts)
                {
                    Emr.Vector p = skeleton.GetFinalPosition(i);
                    translation = new Vector3(p.x, p.y, p.z);
                }
                Vector3[] vertices = mesh.GetPositionData(triObject)
                    .ToArray().Select(v => new Vector3(v.x, -v.y, v.z) + new Vector3(translation.X, -translation.Y, translation.Z)).ToArray();
                var faces = new List<int[]>();
                foreach (Md1.Triangle t in mesh.GetTriangles(triObject)) faces.Add(new[] { (int)t.v0, t.v1, t.v2 });
                foreach (Md1.Quad q in mesh.GetQuads(quadObject)) faces.Add(new[] { (int)q.v0, q.v1, q.v3, q.v2 });
                triangles += triObject.pri_count + quadObject.pri_count * 2;
                parts.Add(new RenderPart(vertices, faces));
            }

            _parts.Items.Clear();
            for (int i = 0; i < parts.Count; i++) _parts.Items.Add($"Part {i:D2}", true);
            _viewport.SetModel(parts);
            _details.Text = $"Game: {VersionName(_model.Version)}\r\nType: {Path.GetExtension(_path!).TrimStart('.').ToUpperInvariant()}\r\nParts: {parts.Count:N0}\r\nTriangles: {triangles:N0}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Mesh load failed:\n{ex.Message}", "Biohazard Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Emr? TryGetSkeleton(ModelFile model)
    {
        try { return model.GetEmr(0); }
        catch { return null; }
    }

    private static string VersionName(IntelOrca.Biohazard.BioVersion version) => version switch
    {
        IntelOrca.Biohazard.BioVersion.Biohazard1 => "Resident Evil 1",
        IntelOrca.Biohazard.BioVersion.Biohazard2 => "Resident Evil 2",
        IntelOrca.Biohazard.BioVersion.Biohazard3 => "Resident Evil 3",
        _ => version.ToString()
    };

    private sealed record RenderPart(Vector3[] Vertices, List<int[]> Faces)
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

        public bool Wireframe { get => _wireframe; set { _wireframe = value; Invalidate(); } }

        public ModelViewport()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(28, 30, 34);
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        public void SetModel(List<RenderPart> parts)
        {
            _parts = parts;
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
            var polygons = new List<(float Depth, PointF[] Points, Color Color)>();
            for (int partIndex = 0; partIndex < _parts.Count; partIndex++)
            {
                RenderPart part = _parts[partIndex];
                if (!part.Visible) continue;
                Vector3[] transformed = part.Vertices.Select(v => Vector3.Transform(v - _center, rotation)).ToArray();
                foreach (int[] face in part.Faces)
                {
                    if (face.Any(i => (uint)i >= (uint)transformed.Length)) continue;
                    Vector3 a = transformed[face[0]], b = transformed[face[1]], c = transformed[face[2]];
                    Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                    float light = Math.Clamp(0.25f + Math.Abs(Vector3.Dot(normal, Vector3.Normalize(new Vector3(-0.4f, -0.7f, -1)))) * 0.75f, 0, 1);
                    Color baseColor = ColorFromPart(partIndex);
                    Color shade = Color.FromArgb((int)(baseColor.R * light), (int)(baseColor.G * light), (int)(baseColor.B * light));
                    PointF[] points = face.Select(i => new PointF(Width / 2f + transformed[i].X * scale, Height / 2f + transformed[i].Y * scale)).ToArray();
                    polygons.Add((face.Average(i => transformed[i].Z), points, shade));
                }
            }
            foreach (var polygon in polygons.OrderByDescending(x => x.Depth))
            {
                if (!Wireframe) using (var brush = new SolidBrush(polygon.Color)) e.Graphics.FillPolygon(brush, polygon.Points);
                using var pen = new Pen(Wireframe ? Color.Gainsboro : Color.FromArgb(90, 10, 10, 10), Wireframe ? 1 : 0.6f);
                e.Graphics.DrawPolygon(pen, polygon.Points);
            }
            TextRenderer.DrawText(e.Graphics, "Drag: orbit   Wheel: zoom   Ctrl+R: reset   Ctrl+W: wireframe", Font,
                new Point(10, Height - 26), Color.Silver);
        }

        private static Color ColorFromPart(int index)
        {
            Color[] colors = { Color.SteelBlue, Color.IndianRed, Color.SeaGreen, Color.Goldenrod, Color.MediumPurple, Color.CadetBlue };
            return colors[index % colors.Length];
        }
    }
}

#nullable enable
using System.Numerics;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;
using System.Text.Json;
using IntelOrca.Biohazard.Model;
using Tool_Hazard.IntelOrca.Biohazard;
using Tool_Hazard.IntelOrca.Biohazard.Model;
using Tool_Hazard.Biohazard.RE4;
using Tool_Hazard.White_Day;

namespace Tool_Hazard.Forms;

public sealed class BiohazardModelViewerForm : Form
{
    private readonly ModelViewport _viewport = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _mesh = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly CheckedListBox _parts = new() { Dock = DockStyle.Fill, CheckOnClick = true };
    private readonly Label _details = new() { AutoSize = false, Dock = DockStyle.Bottom, Height = 100, Padding = new Padding(6) };
    private readonly ComboBox _animation = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly Button _playPause = new() { Text = "Play", AutoSize = true };
    private readonly TrackBar _timeline = new() { Minimum = 0, Maximum = 0, TickStyle = TickStyle.None, Width = 330, AutoSize = false, Height = 28 };
    private readonly Label _frameLabel = new() { Text = "0 / 0", AutoSize = true, Padding = new Padding(4, 6, 0, 0) };
    private readonly NumericUpDown _speed = new() { Minimum = 10, Maximum = 400, Value = 100, Increment = 10, Width = 62 };
    private readonly System.Windows.Forms.Timer _animationTimer = new() { Interval = 33 };
    private ModelFile? _model;
    private SurvivorNab? _survivor;
    private SurvivorHmd? _survivorHmd;
    private Re4BinModel? _re4Bin;
    private Re4PmdModel? _re4Pmd;
    private Re4ScenarioSmd? _re4Scenario;
    private Re4TplDocument? _re4Tpl;
    private Re4PackDocument? _re4Pack;
    private readonly Dictionary<string, Bitmap> _re4PackTextures = new(StringComparer.OrdinalIgnoreCase);
    private List<Re4MeshPart>? _re4GenericParts;
    private string? _re4KindLabel;
    private WhiteDayPetModel? _petModel;
    private string? _petKindLabel;
    private readonly Dictionary<string, Bitmap> _petTextures = new(StringComparer.OrdinalIgnoreCase);
    private TimFile? _texture;
    private string? _texturePath;
    private string? _path;
    private IEdd? _edd;
    private Emr? _skeleton;
    private readonly List<AnimationChoice> _animations = new();
    private List<RenderPart> _renderParts = new();
    private int _animationTime;
    private bool _updatingTimeline;

    public BiohazardModelViewerForm(string? initialPath = null)
    {
        Text = "Biohazard Model Viewer - RE1 / RE2 / RE3 / Survivor PC / RE4 / White Day";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 500);
        Size = new Size(1050, 720);

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        var open = new ToolStripMenuItem("Open...", null, (_, _) => PromptOpen()) { ShortcutKeys = Keys.Control | Keys.O };
        var saveAs = new ToolStripMenuItem("Save model as...", null, (_, _) => SaveModelAs()) { ShortcutKeys = Keys.Control | Keys.Shift | Keys.S };
        var openTexture = new ToolStripMenuItem("Load TIM texture...", null, (_, _) => PromptTexture());
        var clearTexture = new ToolStripMenuItem("Clear texture", null, (_, _) => { _texture = null; _texturePath = null; LoadSelectedMesh(); });
        var exit = new ToolStripMenuItem("Exit", null, (_, _) => Close());
        file.DropDownItems.AddRange(new ToolStripItem[] { open, saveAs, new ToolStripSeparator(), openTexture, clearTexture, new ToolStripSeparator(), exit });
        var view = new ToolStripMenuItem("View");
        var smooth = new ToolStripMenuItem("Smooth shading") { CheckOnClick = true, Checked = true };
        smooth.CheckedChanged += (_, _) => _viewport.SmoothShading = smooth.Checked;
        var textures = new ToolStripMenuItem("Show textures") { CheckOnClick = true, Checked = true };
        textures.CheckedChanged += (_, _) => _viewport.TexturesVisible = textures.Checked;
        view.DropDownItems.AddRange(new ToolStripItem[] { smooth, textures, new ToolStripSeparator() });
        view.DropDownItems.Add(new ToolStripMenuItem("Reset camera", null, (_, _) => _viewport.ResetCamera()) { ShortcutKeys = Keys.Control | Keys.R });
        view.DropDownItems.Add(new ToolStripMenuItem("Toggle wireframe", null, (_, _) => _viewport.Wireframe = !_viewport.Wireframe) { ShortcutKeys = Keys.Control | Keys.W });
        var meshMenu = new ToolStripMenuItem("Mesh");
        meshMenu.DropDownItems.AddRange(new ToolStripItem[] {
            new ToolStripMenuItem("Export whole mesh (OBJ)...", null, (_, _) => ExportMesh(false, false)),
            new ToolStripMenuItem("Export selected part (OBJ)...", null, (_, _) => ExportMesh(true, false)),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Export whole mesh (native)...", null, (_, _) => ExportMesh(false, true)),
            new ToolStripMenuItem("Export selected part (native)...", null, (_, _) => ExportMesh(true, true)),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Replace whole mesh from OBJ...", null, (_, _) => ReplaceMesh(false, false)),
            new ToolStripMenuItem("Replace selected part from OBJ...", null, (_, _) => ReplaceMesh(true, false)),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Replace whole mesh from native...", null, (_, _) => ReplaceMesh(false, true)),
            new ToolStripMenuItem("Replace selected part from native...", null, (_, _) => ReplaceMesh(true, true))
        });
        var textureMenu = new ToolStripMenuItem("Texture");
        textureMenu.DropDownItems.AddRange(new ToolStripItem[] {
            new ToolStripMenuItem("View texture...", null, (_, _) => ViewTexture()),
            new ToolStripMenuItem("Export texture image...", null, (_, _) => ExportTexture()),
            new ToolStripMenuItem("Replace/import texture image...", null, (_, _) => ReplaceTextureImage())
        });
        var animationMenu = new ToolStripMenuItem("Animation");
        animationMenu.DropDownItems.AddRange(new ToolStripItem[] {
            new ToolStripMenuItem("Export current animation for Blender (BVH)...", null, (_, _) => ExportBvhAnimation()),
            new ToolStripMenuItem("Export current animation (skeletal JSON)...", null, (_, _) => ExportSkeletalAnimation()),
            new ToolStripMenuItem("Export current native animation bank...", null, (_, _) => ExportNativeAnimationBank()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Replace classic native animation bank...", null, (_, _) => ReplaceClassicAnimationBank()),
            new ToolStripMenuItem("Replace compatible Survivor NAC bank...", null, (_, _) => ReplaceSurvivorAnimationBank())
        });
        menu.Items.AddRange(new ToolStripItem[] { file, view, meshMenu, textureMenu, animationMenu });
        MainMenuStrip = menu;

        var sidebar = new Panel { Dock = DockStyle.Right, Width = 230, Padding = new Padding(8) };
        var playback = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(6, 5, 6, 4), WrapContents = false };
        var previous = new Button { Text = "◀", AutoSize = true };
        var next = new Button { Text = "▶", AutoSize = true };
        playback.Controls.AddRange(new Control[] { new Label { Text = "Animation", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, _animation, previous, _playPause, next, _timeline, _frameLabel, new Label { Text = "Speed %", AutoSize = true, Padding = new Padding(8, 7, 0, 0) }, _speed });
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
        Controls.Add(playback);
        Controls.Add(menu);

        _mesh.SelectedIndexChanged += (_, _) => LoadSelectedMesh();
        _parts.ItemCheck += (_, e) => BeginInvoke(new Action(() =>
        {
            _viewport.SetPartVisible(e.Index, e.NewValue == CheckState.Checked);
        }));
        _animation.SelectedIndexChanged += (_, _) => SelectAnimation();
        _playPause.Click += (_, _) => SetPlaying(!_animationTimer.Enabled);
        previous.Click += (_, _) => StepAnimation(-1);
        next.Click += (_, _) => StepAnimation(1);
        _timeline.ValueChanged += (_, _) =>
        {
            if (_updatingTimeline) return;
            _animationTime = _timeline.Value;
            ApplyCurrentPose();
        };
        _animationTimer.Tick += (_, _) => StepAnimation(1);

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
            Title = "Open Biohazard model (classic or RE4 / White Day PET)",
            Filter = "All supported models (*.emd;*.pld;*.nab;*.hmd;*.bin;*.pmd;*.smx;*.smd;*.obj;*.pet)|*.emd;*.pld;*.nab;*.hmd;*.bin;*.pmd;*.smx;*.smd;*.obj;*.pet|Biohazard models (*.emd;*.pld;*.nab;*.hmd)|*.emd;*.pld;*.nab;*.hmd|RE4 models (*.bin;*.pmd;*.smx)|*.bin;*.pmd;*.smx|Scenario / generic (*.smd;*.obj)|*.smd;*.obj|White Day PET (*.pet)|*.pet|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) Open(dialog.FileName);
    }

    private void Open(string path)
    {
        try
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".bin" || ext == ".pmd" || ext == ".smx" || ext == ".obj" || (ext == ".smd" && Re4Detect.IsTextSmd(path)))
            {
                OpenRe4(path);
                return;
            }
            if (Path.GetExtension(path).Equals(".pet", StringComparison.OrdinalIgnoreCase))
            {
                OpenPet(path);
                return;
            }
            if (ext == ".smd" && Re4ScenarioSmd.IsScenarioSmd(File.ReadAllBytes(path)))
            {
                OpenRe4(path);
                return;
            }
            if (Path.GetExtension(path).Equals(".nab", StringComparison.OrdinalIgnoreCase))
            {
                OpenSurvivor(path);
                return;
            }
            if (Path.GetExtension(path).Equals(".hmd", StringComparison.OrdinalIgnoreCase))
            {
                OpenSurvivorHmd(path);
                return;
            }
            ClearRe4State();
            ModelFile model = ModelFile.FromFile(path);
            _model = model;
            _survivor = null;
            _survivorHmd = null;
            _path = path;
            SetPlaying(false);
            LoadAnimations(model);
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

    private void OpenSurvivor(string path)
    {
        SurvivorNab survivor = SurvivorNab.FromFile(path);
        _survivor = survivor;
        _survivorHmd = null;
        _model = null;
        _path = path;
        _texture = null;
        _texturePath = "Embedded NAB texture";
        SetPlaying(false);
        _animations.Clear();
        _animation.Items.Clear();
        if (survivor.HasAnimations)
        {
            for (int i = 0; i < survivor.SelectorCount; i++)
                _animation.Items.Add($"Selector {i:X2} ({survivor.GetAnimationDuration(i)} frames)");
        }
        _animation.Enabled = _playPause.Enabled = _timeline.Enabled = _animation.Items.Count != 0;
        if (_animation.Items.Count != 0) _animation.SelectedIndex = 0;
        else UpdateTimeline(0, 0);
        _mesh.Items.Clear();
        _mesh.Items.Add("Survivor PC mesh");
        _mesh.SelectedIndex = 0;
        LoadSurvivorMesh();
        if (survivor.HasAnimations) ApplyCurrentPose();
        Text = $"Biohazard Model Viewer - {Path.GetFileName(path)}";
    }

    private void OpenSurvivorHmd(string path)
    {
        SurvivorHmd hmd = SurvivorHmd.FromFile(path);
        _survivorHmd = hmd;
        _survivor = null;
        _model = null;
        _path = path;
        _texture = null;
        _texturePath = null;
        SetPlaying(false);
        _animations.Clear();
        _animation.Items.Clear();
        _animation.Enabled = _playPause.Enabled = _timeline.Enabled = false;
        UpdateTimeline(0, 0);
        _mesh.Items.Clear();
        _mesh.Items.Add("HMD scene / actor data");
        _mesh.SelectedIndex = 0;
        LoadSurvivorHmd();
        Text = $"Biohazard Model Viewer - {Path.GetFileName(path)}";
    }

    // ---- RE4 (JADERLINK) integration ----
    // Decoders live in Biohazard/RE4/*.cs (MIT). The viewer renders via the
    // existing software ModelViewport by mapping RE4 parts onto RenderPart and
    // RE4 PACK textures onto a single BitmapTexture atlas (UVs pre-scaled).

    private void ClearRe4State()
    {
        _re4Bin = null; _re4Pmd = null; _re4Scenario = null; _re4GenericParts = null;
        _re4KindLabel = null;
    }

    private static class Re4Detect
    {
        public static bool IsTextSmd(string path)
        {
            try
            {
                using var r = File.OpenText(path);
                for (int i = 0; i < 8; i++)
                {
                    string? line = r.ReadLine();
                    if (line == null) return false;
                    string t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("//")) continue;
                    return t.ToLowerInvariant().StartsWith("version");
                }
            }
            catch { }
            return false;
        }
    }

    private void OpenRe4(string path)
    {
        ClearRe4State();
        _model = null; _survivor = null; _survivorHmd = null;
        _texture = null; _texturePath = null;
        _path = path;
        SetPlaying(false);
        _animations.Clear(); _animation.Items.Clear();
        _animation.Enabled = _playPause.Enabled = _timeline.Enabled = false;
        UpdateTimeline(0, 0);
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".bin")
        {
            _re4Bin = Re4BinModel.Load(path);
            _re4KindLabel = _re4Bin.Description;
            _re4Tpl = TryAutoLoadTpl(path);
        }
        else if (ext == ".pmd")
        {
            _re4Pmd = Re4PmdModel.Load(path);
            _re4KindLabel = $"RE4 2007 PMD - {Path.GetFileName(path)}";
            TryAutoLoadPackTexturesForPmd(path);
        }
        else if (ext == ".smx")
        {
            var entries = Re4SmxModel.Load(path);
            _re4GenericParts = new List<Re4MeshPart>();
            _re4KindLabel = $"RE4 SMX - {Path.GetFileName(path)} ({entries.Count} entries)";
            ShowRe4Info($"SMX opened: {entries.Count} entries.\r\nMode values: {string.Join(", ", entries.Select(e => e.Mode))}\r\nThis is a metadata-only preview; load a BIN/PMD for geometry.");
            _mesh.Items.Clear();
            _mesh.Items.Add("SMX metadata (no geometry)");
            _mesh.SelectedIndex = 0;
            _details.Text = $"Game: Resident Evil 4\r\nType: SMX metadata\r\nEntries: {entries.Count:N0}\r\nFirst entry: Model={entries.FirstOrDefault()?.UseSmxId} Mode={entries.FirstOrDefault()?.Mode}";
            _renderParts = new List<RenderPart>();
            _viewport.SetModel(new List<RenderPart>(), null);
            Text = $"Biohazard Model Viewer - {Path.GetFileName(path)}";
            return;
        }
        else if (ext == ".obj")
        {
            _re4GenericParts = Re4MeshIo.ImportObj(path);
            _re4KindLabel = $"OBJ - {Path.GetFileName(path)}";
        }
        else if (ext == ".smd")
        {
            if (Re4Detect.IsTextSmd(path))
            {
                _re4GenericParts = Re4MeshIo.ImportSmd(path);
                _re4KindLabel = $"Text SMD - {Path.GetFileName(path)}";
            }
            else
            {
                _re4Scenario = Re4ScenarioSmd.Load(path);
                _re4KindLabel = $"Scenario SMD - {Path.GetFileName(path)} ({_re4Scenario.Lines.Count} lines, BINs: {string.Join(",", _re4Scenario.Bins.Keys)})";
                // Show first embedded BIN if present.
                if (_re4Scenario.Bins.Count > 0) _re4Bin = _re4Scenario.Bins.OrderBy(k => k.Key).First().Value;
                else throw new InvalidDataException("Scenario SMD contains no embedded BINs.");
            }
        }
        else
        {
            throw new InvalidDataException($"Unsupported RE4 file: {ext}");
        }
        _mesh.BeginUpdate();
        _mesh.Items.Clear();
        var parts = CurrentRe4Parts();
        for (int i = 0; i < parts.Count; i++) _mesh.Items.Add(parts[i].Name);
        if (parts.Count == 0) _mesh.Items.Add("(no parts)");
        _mesh.EndUpdate();
        if (parts.Count > 0) _mesh.SelectedIndex = 0;
        else LoadRe4SelectedMesh();
        Text = $"Biohazard Model Viewer - {Path.GetFileName(path)}";
    }

    private List<Re4MeshPart> CurrentRe4Parts()
    {
        if (_re4Bin != null) return _re4Bin.Parts;
        if (_re4Pmd != null) return _re4Pmd.Parts;
        if (_re4GenericParts != null) return _re4GenericParts;
        return new List<Re4MeshPart>();
    }

    private Re4TplDocument? TryAutoLoadTpl(string binPath)
    {
        string dir = Path.GetDirectoryName(binPath) ?? ".";
        string baseName = Path.GetFileNameWithoutExtension(binPath);
        foreach (string cand in new[] { Path.Combine(dir, baseName + ".TPL"), Path.Combine(dir, baseName + ".tpl") })
        {
            if (File.Exists(cand))
            {
                try { return Re4TplModel.Load(cand); } catch { }
            }
        }
        // Any .tpl in the folder as fallback.
        try
        {
            string? any = Directory.GetFiles(dir, "*.tpl").FirstOrDefault();
            if (any != null) return Re4TplModel.Load(any);
        }
        catch { }
        return null;
    }

    private void TryAutoLoadPackTexturesForPmd(string pmdPath)
    {
        string dir = Path.GetDirectoryName(pmdPath) ?? ".";
        if (_re4Pmd == null) return;
        foreach (string name in _re4Pmd.Materials.Select(m => m.TextureName).Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            string full = Path.IsPathRooted(name) ? name : Path.Combine(dir, name);
            if (!File.Exists(full)) continue;
            try
            {
                byte[] bytes = File.ReadAllBytes(full);
                var bmp = Re4TextureCodec.DecodeToBitmap(bytes, out _);
                if (bmp != null) _re4PackTextures[Path.GetFileName(name).ToLowerInvariant()] = bmp;
            }
            catch { }
        }
    }

    private void PromptRe4Open(string kind)
    {
        using var dialog = new OpenFileDialog
        {
            Title = kind switch { "bin" => "Open RE4 BIN", "pmd" => "Open RE4 2007 PMD", "smd" => "Open scenario SMD / text SMD", "tpl" => "Load RE4 TPL", _ => "Open RE4 file" },
            Filter = kind switch
            {
                "bin" => "RE4 BIN (*.bin)|*.bin|All files (*.*)|*.*",
                "pmd" => "RE4 PMD (*.pmd)|*.pmd|All files (*.*)|*.*",
                "smd" => "Scenario SMD (*.smd)|*.smd|Text SMD / OBJ (*.smd;*.obj)|*.smd;*.obj|All files (*.*)|*.*",
                "tpl" => "RE4 TPL (*.tpl)|*.tpl|All files (*.*)|*.*",
                _ => "All files (*.*)|*.*"
            },
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (kind == "tpl")
            {
                _re4Tpl = Re4TplModel.Load(dialog.FileName);
                _details.Text = $"Game: Resident Evil 4\r\nType: TPL\r\n{_re4Tpl.Description}\r\nEntries: {_re4Tpl.Entries.Count:N0}";
                MessageBox.Show(this, $"Loaded TPL: {_re4Tpl.Entries.Count} entries.\r\nOpen the matching BIN to view textured mesh.", "RE4 TPL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            OpenRe4(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"RE4 open failed:\n{ex.Message}", "Biohazard Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PromptRe4Pack()
    {
        using var dialog = new OpenFileDialog { Title = "Load RE4 PACK textures", Filter = "RE4 PACK (*.pack)|*.pack|All files (*.*)|*.*", Multiselect = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            foreach (string f in dialog.FileNames)
            {
                var pack = Re4PackDocument.Load(f);
                _re4Pack = pack;
                foreach (var ent in pack.Entries)
                {
                    var bmp = Re4TextureCodec.DecodeToBitmap(ent.ImageBytes, out _);
                    if (bmp != null) _re4PackTextures[ent.TextureKey] = bmp;
                }
            }
            MessageBox.Show(this, $"Loaded {_re4PackTextures.Count} PACK textures from {_re4Pack?.Description}.", "RE4 PACK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (_re4Bin != null || _re4Pmd != null || _re4GenericParts != null) LoadRe4SelectedMesh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"PACK load failed:\n{ex.Message}", "Biohazard Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PromptRe4Smx()
    {
        using var dialog = new OpenFileDialog { Title = "Inspect RE4 SMX", Filter = "SMX (*.smx)|*.smx" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var entries = Re4SmxModel.Load(dialog.FileName);
            MessageBox.Show(this, $"SMX entries: {entries.Count}\nFirst: Model={entries[0].UseSmxId} Mode={entries[0].Mode}", "RE4 SMX", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"SMX failed:\n{ex.Message}", "Biohazard Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowRe4Info(string text) => MessageBox.Show(this, text, "RE4", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void LoadRe4SelectedMesh()
    {
        if (_mesh.SelectedIndex < 0) return;
        try
        {
            var parts = CurrentRe4Parts();
            if (parts.Count == 0) return;
            int sel = Math.Clamp(_mesh.SelectedIndex, 0, parts.Count - 1);
            // Show all parts, highlight selection in details (viewport shows full mesh).
            var (renderParts, texture) = BuildRe4RenderParts(parts);
            _renderParts = renderParts;
            _parts.Items.Clear();
            for (int i = 0; i < renderParts.Count; i++) _parts.Items.Add($"Part {i:D2} {(i == sel ? "(selected)" : "")}", true);
            _viewport.SetModel(renderParts, texture);
            string matInfo = "";
            if (_re4Bin != null)
            {
                matInfo = $"\r\nMaterials: {_re4Bin.Materials.Count}  TexCount: {_re4Bin.TexCount}  Flags: 0x{_re4Bin.BinFlags:X8}";
                if (_re4Bin.Materials.Count > sel)
                {
                    var mat = _re4Bin.Materials[sel];
                    matInfo += $"\r\nSelected material: diffuse_map={mat.DiffuseMap} alpha={mat.AlphaTextured}";
                }
            }
            int tris = parts.Sum(p => p.TriangleCount);
            string texInfo = ResolveRe4TextureDescription(parts);
            _details.Text = $"Game: Resident Evil 4\r\nType: {_re4KindLabel}\r\nParts: {parts.Count:N0}  Triangles: {tris:N0}  Bones: {(_re4Bin?.Bones.Count ?? 0):N0}{matInfo}\r\nTexture: {texInfo}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"RE4 mesh load failed:\n{ex.Message}", "Biohazard Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string ResolveRe4TextureDescription(List<Re4MeshPart> parts)
    {
        if (_re4PackTextures.Count == 0) return "None (load PACK textures)";
        if (_re4Pmd != null)
        {
            var names = _re4Pmd.Materials.Select(m => m.TextureName).Distinct().ToList();
            return $"PMD TGA ({string.Join(", ", names)})";
        }
        if (_re4Bin != null)
        {
            var keys = parts.Select(p =>
            {
                int tplIndex = p.MaterialIndex;
                if (_re4Tpl != null && tplIndex < _re4Tpl.Entries.Count) return _re4Tpl.Entries[tplIndex].TextureKey;
                return $"material {p.MaterialIndex}";
            }).Distinct().ToList();
            return $"PACK atlas ({string.Join(", ", keys)})";
        }
        return $"PACK ({_re4PackTextures.Count} in memory)";
    }

    private (List<RenderPart> Parts, IViewerTexture? Texture) BuildRe4RenderParts(List<Re4MeshPart> parts)
    {
        // Build a side-by-side atlas: each material's texture at its own X offset.
        var materialBitmaps = BuildMaterialBitmaps(parts);
        int totalW = materialBitmaps.Sum(b => b.Width);
        int maxH = materialBitmaps.Count == 0 ? 1 : materialBitmaps.Max(b => b.Height);
        if (materialBitmaps.Count == 0 || totalW == 0)
        {
            return (BuildFlatRe4Parts(parts), null);
        }
        var atlas = new Bitmap(Math.Max(1, totalW), Math.Max(1, maxH), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var offsets = new int[materialBitmaps.Count];
        int cursorX = 0;
        for (int i = 0; i < materialBitmaps.Count; i++)
        {
            offsets[i] = cursorX;
            using var g = System.Drawing.Graphics.FromImage(atlas);
            g.DrawImage(materialBitmaps[i], cursorX, 0);
            cursorX += materialBitmaps[i].Width;
        }
        var tex = new Re4BitmapTexture(atlas, offsets, materialBitmaps.Select(b => b.Width).ToArray(), materialBitmaps.Select(b => b.Height).ToArray());
        var renderParts = new List<RenderPart>();
        for (int pi = 0; pi < parts.Count; pi++)
        {
            var part = parts[pi];
            int mat = Math.Clamp(part.MaterialIndex, 0, materialBitmaps.Count - 1);
            var localVerts = new System.Numerics.Vector3[part.VertexCount];
            for (int i = 0; i < part.VertexCount; i++)
                localVerts[i] = new System.Numerics.Vector3(part.Vertices[i * 3], part.Vertices[i * 3 + 1], part.Vertices[i * 3 + 2]);
            var normals = new System.Numerics.Vector3[part.VertexCount];
            for (int i = 0; i < part.VertexCount; i++)
                normals[i] = new System.Numerics.Vector3(part.UniqueNormals[i * 3], part.UniqueNormals[i * 3 + 1], part.UniqueNormals[i * 3 + 2]);
            var faces = new List<RenderFace>();
            for (int t = 0; t < part.Indices.Length; t += 3)
            {
                int a = (int)part.Indices[t], b = (int)part.Indices[t + 1], c = (int)part.Indices[t + 2];
                if ((uint)a >= (uint)part.VertexCount || (uint)b >= (uint)part.VertexCount || (uint)c >= (uint)part.VertexCount) continue;
                faces.Add(new RenderFace(new[] { a, b, c }, new[]
                {
                    new Vector2(offsets[mat] + part.UniqueUvs[a * 2] * materialBitmaps[mat].Width, part.UniqueUvs[a * 2 + 1] * maxH),
                    new Vector2(offsets[mat] + part.UniqueUvs[b * 2] * materialBitmaps[mat].Width, part.UniqueUvs[b * 2 + 1] * maxH),
                    new Vector2(offsets[mat] + part.UniqueUvs[c * 2] * materialBitmaps[mat].Width, part.UniqueUvs[c * 2 + 1] * maxH),
                }, 0, 0));
            }
            var rp = new RenderPart(localVerts, faces, normals);
            if (part.UniqueColors.Length >= 4 && part.UniqueColors.Any(v => v != 1f))
            {
                // Bake vertex colors into faces via VtColors; rasterizer already modulates.
                var colored = new List<RenderFace>();
                for (int t = 0; t < part.Indices.Length; t += 3)
                {
                    int a = (int)part.Indices[t], b = (int)part.Indices[t + 1], c = (int)part.Indices[t + 2];
                    if ((uint)a >= (uint)part.VertexCount || (uint)b >= (uint)part.VertexCount || (uint)c >= (uint)part.VertexCount) continue;
                    Color ca = Re4Color(part, a), cb = Re4Color(part, b), cc = Re4Color(part, c);
                    colored.Add(new RenderFace(new[] { a, b, c }, new[]
                    {
                        new Vector2(offsets[mat] + part.UniqueUvs[a * 2] * materialBitmaps[mat].Width, part.UniqueUvs[a * 2 + 1] * maxH),
                        new Vector2(offsets[mat] + part.UniqueUvs[b * 2] * materialBitmaps[mat].Width, part.UniqueUvs[b * 2 + 1] * maxH),
                        new Vector2(offsets[mat] + part.UniqueUvs[c * 2] * materialBitmaps[mat].Width, part.UniqueUvs[c * 2 + 1] * maxH),
                    }, 0, 0, new[] { ca, cb, cc }));
                }
                rp = new RenderPart(localVerts, colored, normals);
            }
            rp.ApplyTransform(System.Numerics.Matrix4x4.Identity);
            renderParts.Add(rp);
        }
        return (renderParts, tex);
    }

    private static Color Re4Color(Re4MeshPart part, int vi)
    {
        if (vi * 4 + 3 >= part.UniqueColors.Length) return Color.White;
        return Color.FromArgb(255,
            (int)Math.Clamp(part.UniqueColors[vi * 4] * 255, 0, 255),
            (int)Math.Clamp(part.UniqueColors[vi * 4 + 1] * 255, 0, 255),
            (int)Math.Clamp(part.UniqueColors[vi * 4 + 2] * 255, 0, 255));
    }

    private List<RenderPart> BuildFlatRe4Parts(List<Re4MeshPart> parts)
    {
        var renderParts = new List<RenderPart>();
        foreach (var part in parts)
        {
            var localVerts = new System.Numerics.Vector3[part.VertexCount];
            for (int i = 0; i < part.VertexCount; i++)
                localVerts[i] = new System.Numerics.Vector3(part.Vertices[i * 3], part.Vertices[i * 3 + 1], part.Vertices[i * 3 + 2]);
            var normals = new System.Numerics.Vector3[part.VertexCount];
            for (int i = 0; i < part.VertexCount; i++)
                normals[i] = new System.Numerics.Vector3(part.UniqueNormals[i * 3], part.UniqueNormals[i * 3 + 1], part.UniqueNormals[i * 3 + 2]);
            var faces = new List<RenderFace>();
            for (int t = 0; t < part.Indices.Length; t += 3)
            {
                int a = (int)part.Indices[t], b = (int)part.Indices[t + 1], c = (int)part.Indices[t + 2];
                if ((uint)a >= (uint)part.VertexCount || (uint)b >= (uint)part.VertexCount || (uint)c >= (uint)part.VertexCount) continue;
                faces.Add(new RenderFace(new[] { a, b, c }, new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero }, 0, -1));
            }
            var rp = new RenderPart(localVerts, faces, normals);
            rp.ApplyTransform(System.Numerics.Matrix4x4.Identity);
            renderParts.Add(rp);
        }
        return renderParts;
    }

    private List<Bitmap> BuildMaterialBitmaps(List<Re4MeshPart> parts)
    {
        var list = new List<Bitmap>();
        if (_re4Pmd != null)
        {
            foreach (var part in parts)
            {
                int idx = part.MaterialIndex;
                string? name = idx >= 0 && idx < _re4Pmd.Materials.Count ? Path.GetFileName(_re4Pmd.Materials[idx].TextureName).ToLowerInvariant() : null;
                if (name != null && _re4PackTextures.TryGetValue(name, out var bmp)) list.Add(new Bitmap(bmp));
                else list.Add(new Bitmap(1, 1));
            }
            return list;
        }
        foreach (var part in parts)
        {
            string? key = null;
            if (_re4Tpl != null && part.MaterialIndex < _re4Tpl.Entries.Count) key = _re4Tpl.Entries[part.MaterialIndex].TextureKey;
            if (key != null && _re4PackTextures.TryGetValue(key, out var bmp)) list.Add(new Bitmap(bmp));
            else if (_re4PackTextures.Count > 0 && key == null) list.Add(new Bitmap(_re4PackTextures.Values.First()));
            else list.Add(new Bitmap(1, 1));
        }
        return list;
    }

    private void ExportRe4Mesh(string format)
    {
        try
        {
            if (_re4Scenario != null && _re4Scenario.Bins.Count > 0)
            {
                using var dlg = new SaveFileDialog { Title = "Export scenario as placed OBJ", Filter = "Wavefront OBJ (*.obj)|*.obj", FileName = Path.GetFileNameWithoutExtension(_path ?? "scenario") + "_placed.obj" };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _re4Scenario.ExportPlacedObj(dlg.FileName);
                return;
            }
            if (_re4Bin != null)
            {
                using var dlg = new SaveFileDialog { Title = $"Export RE4 mesh ({format.ToUpperInvariant()})", Filter = format == "obj" ? "Wavefront OBJ (*.obj)|*.obj" : "StudioMDL Data (*.smd)|*.smd", FileName = Path.GetFileNameWithoutExtension(_path ?? "re4") + "." + format };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (format == "obj") { Re4MeshIo.ExportObj(_re4Bin, dlg.FileName); Re4MeshIo.ExportMtl(_re4Bin, dlg.FileName); }
                else Re4MeshIo.ExportSmd(_re4Bin, dlg.FileName);
                return;
            }
            if (_re4Pmd != null)
            {
                using var dlg = new SaveFileDialog { Title = "Export PMD mesh (OBJ)", Filter = "Wavefront OBJ (*.obj)|*.obj", FileName = Path.GetFileNameWithoutExtension(_path ?? "pmd") + ".obj" };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _re4Pmd.ExportObj(dlg.FileName);
                return;
            }
            MessageBox.Show(this, "Open an RE4 BIN, 2007 PMD, or scenario SMD first.", "RE4", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"RE4 mesh export failed:\n{ex.Message}", "Biohazard Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportRe4Mesh()
    {
        using var dialog = new OpenFileDialog { Title = "Import OBJ / SMD mesh", Filter = "Wavefront OBJ (*.obj)|*.obj|StudioMDL Data (*.smd)|*.smd|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            var imported = ext == ".smd" ? Re4MeshIo.ImportSmd(dialog.FileName) : Re4MeshIo.ImportObj(dialog.FileName);
            if (_re4Bin != null && _re4Bin.CanSaveNative)
            {
                // Native in-place patch (same counts only). This is the "textures only
                // native" path: geometry round-trips losslessly; new geometry with
                // different counts becomes a generic view instead.
                try
                {
                    _re4Bin.ReplaceGeometry(imported.ToArray());
                    _re4KindLabel = _re4Bin.Description + " (patched from OBJ/SMD)";
                    LoadRe4SelectedMesh();
                    MessageBox.Show(this, "Native BIN geometry patched in place. Use File > Save model as... to write the BIN.", "RE4 import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                catch (InvalidDataException ex)
                {
                    MessageBox.Show(this, $"{ex.Message}\n\nShowing as a generic mesh view instead (native BIN left unchanged).\n\nTip: export the current BIN to OBJ, edit in Blender, and re-import without changing vertex/triangle counts for a native patch.", "RE4 import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            if (_re4Bin != null && !_re4Bin.CanSaveNative)
                MessageBox.Show(this, "PS2 VIF BIN is read-only; showing imported mesh as a generic view.", "RE4 import", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _re4Bin = null; _re4Pmd = null;
            _re4GenericParts = imported;
            _re4KindLabel = $"Imported {Path.GetFileName(dialog.FileName)}";
            _mesh.BeginUpdate(); _mesh.Items.Clear();
            foreach (var p in imported) _mesh.Items.Add(p.Name);
            _mesh.EndUpdate();
            if (imported.Count > 0) _mesh.SelectedIndex = 0;
            else LoadRe4SelectedMesh();
            Text = $"Biohazard Model Viewer - {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"RE4 mesh import failed:\n{ex.Message}", "Biohazard Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportRe4Texture()
    {
        try
        {
            if (_re4PackTextures.Count == 0)
            {
                MessageBox.Show(this, "No PACK textures loaded. Use RE4 (JADERLINK) > Load RE4 PACK textures first.", "RE4", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int sel = Math.Clamp(_mesh.SelectedIndex, 0, Math.Max(0, CurrentRe4Parts().Count - 1));
            var parts = CurrentRe4Parts();
            var bitmaps = BuildMaterialBitmaps(parts);
            Bitmap? bmp = sel < bitmaps.Count ? bitmaps[sel] : _re4PackTextures.Values.First();
            using var dlg = new SaveFileDialog { Title = "Export RE4 texture", Filter = "PNG image (*.png)|*.png|Bitmap image (*.bmp)|*.bmp", FileName = $"re4_material_{sel}.png" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            bmp.Save(dlg.FileName, Path.GetExtension(dlg.FileName).Equals(".bmp", StringComparison.OrdinalIgnoreCase) ? System.Drawing.Imaging.ImageFormat.Bmp : System.Drawing.Imaging.ImageFormat.Png);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"RE4 texture export failed:\n{ex.Message}", "Biohazard Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ReplaceRe4PackTexture()
    {
        using var dialog = new OpenFileDialog { Title = "Replace PACK texture from PNG/BMP", Filter = "Images (*.png;*.bmp)|*.png;*.bmp|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (_re4Pack == null)
            {
                MessageBox.Show(this, "No PACK loaded. Load a PACK first; standalone PNG textures cannot be played back as native PACK entries.", "RE4", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var bmp = new Bitmap(dialog.FileName);
            int sel = Math.Clamp(_mesh.SelectedIndex, 0, Math.Max(0, CurrentRe4Parts().Count - 1));
            var parts = CurrentRe4Parts();
            Re4PackEntry? target = null;
            if (_re4Bin != null && _re4Tpl != null && sel < parts.Count)
            {
                int tplIndex = parts[sel].MaterialIndex;
                if (tplIndex < _re4Tpl.Entries.Count)
                {
                    string key = _re4Tpl.Entries[tplIndex].TextureKey;
                    target = _re4Pack.Entries.FirstOrDefault(e => e.TextureKey.Equals(key, StringComparison.OrdinalIgnoreCase));
                }
            }
            target ??= _re4Pack.Entries.FirstOrDefault();
            if (target == null) throw new InvalidOperationException("PACK contains no entries.");
            byte[] encoded = target.Format == "TGA" ? Re4TextureCodec.EncodeTgaUncompressed(bmp) : Re4TextureCodec.EncodeDdsUncompressed(bmp);
            if (target.Format == "GNF")
                throw new NotSupportedException("GNF textures are raw passthrough (Scarlet decode not vendored). Replace the GNF bytes in a hex editor or use the upstream RE4_PS4NS viewer.");
            target.ImageBytes = encoded;
            target.Format = Re4PackDocument.DetectFormat(encoded);
            _re4PackTextures[target.TextureKey] = new Bitmap(bmp);
            if (_re4Bin != null || _re4Pmd != null || _re4GenericParts != null) LoadRe4SelectedMesh();
            MessageBox.Show(this, $"Replaced {target.TextureKey} (now {encoded.Length} bytes, {target.Format}). Use Save RE4 as... to write the PACK.", "RE4 texture", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"RE4 texture replacement failed:\n{ex.Message}", "Biohazard Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveRe4As()
    {
        try
        {
            if (_re4Bin != null)
            {
                using var dlg = new SaveFileDialog { FileName = Path.GetFileName(_path ?? "model.bin"), Filter = "RE4 BIN (*.bin)|*.bin|All files (*.*)|*.*" };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _re4Bin.Save(dlg.FileName);
                if (_re4Tpl != null) Re4TplModel.Save(_re4Tpl, Path.ChangeExtension(dlg.FileName, ".tpl"));
                return;
            }
            if (_re4Pmd != null)
            {
                using var dlg = new SaveFileDialog { FileName = Path.GetFileName(_path ?? "model.pmd"), Filter = "RE4 PMD (*.pmd)|*.pmd" };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _re4Pmd.Save(dlg.FileName);
                return;
            }
            if (_re4Pack != null)
            {
                using var dlg = new SaveFileDialog { FileName = Path.GetFileName(_re4Pack.SourcePath), Filter = "RE4 PACK (*.pack)|*.pack" };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _re4Pack.Save(dlg.FileName);
                return;
            }
            if (_re4Scenario != null)
            {
                using var dlg = new SaveFileDialog { FileName = Path.GetFileName(_path ?? "scenario.smd"), Filter = "Scenario SMD (*.smd)|*.smd" };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _re4Scenario.Save(dlg.FileName);
                return;
            }
            MessageBox.Show(this, "No RE4 asset open.", "RE4", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"RE4 save failed:\n{ex.Message}", "Biohazard Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ---- White Day PET integration ----
    // PET files use the Ntreev PangYa .mpet/.pet block format (TEXT/BONE/MESH/...)
    // decoded by WhiteDayPetModel in Tool_Hazard.White_Day. Textures are separate
    // .jpg/.bmp files referenced by name, loaded from the PET file's folder.

    private void OpenPet(string path)
    {
        try
        {
            ClearPetState();
            _model = null; _survivor = null; _survivorHmd = null;
            _re4Bin = null; _re4Pmd = null; _re4GenericParts = null; _re4Scenario = null;
            _texture = null; _texturePath = null;
            _re4PackTextures.Clear();
            _path = path;
            SetPlaying(false);
            _animations.Clear(); _animation.Items.Clear();
            _animation.Enabled = _playPause.Enabled = _timeline.Enabled = false;
            UpdateTimeline(0, 0);

            _petModel = WhiteDayPetModel.Load(path);
            _petKindLabel = $"White Day PET - {Path.GetFileName(path)}";
            LoadPetTextures(path);

            _mesh.BeginUpdate();
            _mesh.Items.Clear();
            for (int i = 0; i < _petModel.Meshes.Count; i++) _mesh.Items.Add(_petModel.Meshes[i].Name);
            if (_petModel.Meshes.Count == 0) _mesh.Items.Add("(no meshes)");
            _mesh.EndUpdate();
            if (_petModel.Meshes.Count > 0)
            {
                _mesh.SelectedIndex = 0;
                LoadPetSelectedMesh();
            }
            else
            {
                LoadPetSelectedMesh();
            }
            Text = $"Biohazard Model Viewer - {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"PET open failed:\n{ex.Message}", "Biohazard Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearPetState()
    {
        _petModel = null;
        _petKindLabel = null;
        _petTextures.Clear();
    }

    private void LoadPetTextures(string petPath)
    {
        if (_petModel == null) return;
        string dir = Path.GetDirectoryName(petPath) ?? ".";
        foreach (string name in _petModel.Textures)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            string full = Path.IsPathRooted(name) ? name : Path.Combine(dir, name);
            if (!File.Exists(full))
            {
                string? found = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
                if (found == null) continue;
                full = found;
            }
            try
            {
                using var bmp = new Bitmap(full);
                _petTextures[Path.GetFileName(name).ToLowerInvariant()] = new Bitmap(bmp);
            }
            catch { /* skip unreadable textures */ }
        }
    }

    private List<RenderPart> BuildPetRenderParts(List<WhiteDayPetMesh> meshes)
    {
        var renderParts = new List<RenderPart>();
        foreach (var mesh in meshes)
        {
            if (mesh.Vertices.Count == 0 || mesh.Polygons.Count == 0) continue;

            // Bone-weight skin the vertices to the first bone with weight > 0.
            var localVerts = new Vector3[mesh.Vertices.Count];
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                var v = mesh.Vertices[i];
                // Use raw bind-pose vertex positions for viewing; bone
                // weights are only needed for animation playback.
                localVerts[i] = new Vector3(v.Position.X, v.Position.Y, v.Position.Z);
            }

            var localNormals = new Vector3[mesh.Vertices.Count];
            for (int i = 0; i < mesh.Vertices.Count; i++)
                localNormals[i] = Vector3.UnitY;

            var faces = new List<RenderFace>();
            foreach (var poly in mesh.Polygons)
            {
                if (poly.Indices.Length < 3) continue;
                var faceIndices = new int[3];
                var faceUvs = new Vector2[3];
                for (int c = 0; c < 3; c++)
                {
                    faceIndices[c] = (int)poly.Indices[c].Index;
                    faceUvs[c] = new Vector2(poly.Indices[c].Uv.X, 1f - poly.Indices[c].Uv.Y);
                }
                faces.Add(new RenderFace(faceIndices, faceUvs, 0, 0));
                // If quad, add second triangle.
                if (poly.Indices.Length == 4)
                {
                    faces.Add(new RenderFace(
                        new[] { (int)poly.Indices[2].Index, (int)poly.Indices[1].Index, (int)poly.Indices[3].Index },
                        new[] { faceUvs[2], faceUvs[1], faceUvs[3] }, 0, 0));
                }
            }

            var rp = new RenderPart(localVerts, faces, localNormals);
            rp.ApplyTransform(Matrix4x4.Identity);
            renderParts.Add(rp);
        }
        return renderParts;
    }

    private void LoadPetSelectedMesh()
    {
        if (_petModel == null) return;
        var meshes = _petModel.Meshes;
        if (meshes.Count == 0) return;
        int sel = Math.Clamp(_mesh.SelectedIndex, 0, meshes.Count - 1);
        var parts = BuildPetRenderParts(new List<WhiteDayPetMesh> { meshes[sel] });
        _renderParts = parts;
        _parts.Items.Clear();
        for (int i = 0; i < parts.Count; i++) _parts.Items.Add($"Part {i:D2}", true);
        var tex = ResolvePetTexture(meshes[sel]);
        _viewport.SetModel(parts, tex);
        int tris = parts.Sum(p => p.Faces.Count);
        int verts = parts.Sum(p => p.LocalVertices.Length);
        string texDesc = tex is PetBitmapTexture pbtDesc ? pbtDesc.Description ?? "None" : "None";
        _details.Text = $"Game: White Day (2001)\r\nType: PET model\r\n{_petKindLabel}\r\nMeshes: {meshes.Count:N0}  Vertices: {verts:N0}  Triangles: {tris:N0}  Bones: {_petModel.Bones.Count:N0}\r\nTexture: {texDesc}";
    }

    private IViewerTexture? ResolvePetTexture(WhiteDayPetMesh mesh)
    {
        if (_petTextures.Count == 0) return null;
        byte texIndex = mesh.TexMap.Length > 0 ? mesh.TexMap[0] : (byte)0;
        string? key = null;
        if (_petModel != null && texIndex < _petModel.Textures.Count)
            key = _petModel.Textures[texIndex];
        if (key != null && _petTextures.TryGetValue(key.ToLowerInvariant(), out var bmp))
            return new PetBitmapTexture(bmp) { Description = key };
        return null;
    }

    private sealed class PetBitmapTexture : IViewerTexture
    {
        private readonly Bitmap _bmp;
        public string? Description;
        public int Width => _bmp.Width;
        public int Height => _bmp.Height;
        public int PaletteCount => 1;
        public PetBitmapTexture(Bitmap bmp) => _bmp = bmp;
        public uint GetPixel(int x, int y, int palette)
        {
            x = Math.Clamp(x, 0, _bmp.Width - 1);
            y = Math.Clamp(y, 0, _bmp.Height - 1);
            return unchecked((uint)_bmp.GetPixel(x, y).ToArgb());
        }
    }

    private sealed class Re4BitmapTexture : IViewerTexture
    {
        private readonly Bitmap _atlas;
        private readonly int[] _offsets;
        private readonly int[] _widths;
        public string? Description;
        public int Width => _atlas.Width;
        public int Height => _atlas.Height;
        public int PaletteCount => 1;
        public Re4BitmapTexture(Bitmap atlas, int[] offsets, int[] widths, int[] heights)
        {
            _atlas = atlas; _offsets = offsets;
            _widths = widths;
            _ = heights;
        }
        public uint GetPixel(int x, int y, int palette)
        {
            x = Math.Clamp(x, 0, _atlas.Width - 1);
            y = Math.Clamp(y, 0, _atlas.Height - 1);
            return unchecked((uint)_atlas.GetPixel(x, y).ToArgb());
        }
        public int[] Offsets => _offsets;
        public int[] Widths => _widths;
    }

    private static int ReadTimOrgX(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var buf = new byte[14];
            if (fs.Read(buf, 0, buf.Length) < buf.Length) return -1;
            return BitConverter.ToUInt16(buf, 12);
        }
        catch { return -1; }
    }

    private static Dictionary<string, string> ParseRsd(string path)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('@')) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            map[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return map;
    }

    private static string? FindSibling(string dir, string baseName, string extension)
    {
        string direct = Path.Combine(dir, baseName + extension);
        if (File.Exists(direct)) return direct;
        foreach (string f in Directory.Exists(dir) ? Directory.GetFiles(dir, "*" + extension) : Array.Empty<string>())
            if (string.Equals(Path.GetFileNameWithoutExtension(f), baseName, StringComparison.OrdinalIgnoreCase))
                return f;
        return null;
    }

    // Builds textured render parts for an HMD. The GPU packets are decoded
    // directly: only the packet TPAGE word records which TIM slot each
    // polygon samples. When the folder still contains the Meditor sources
    // (.PLY/.MAT + .RSD/.TIM), those are used as a fallback if packet
    // decoding yields nothing.
    private static (List<RenderPart> Parts, IViewerTexture? Texture, string TextureDescription) LoadHmdTexturedParts(SurvivorHmd hmd, string hmdPath)
    {
        string dir = Path.GetDirectoryName(hmdPath) ?? ".";
        string baseName = Path.GetFileNameWithoutExtension(hmdPath);
        string? rsdPath = FindSibling(dir, baseName, ".RSD")
            ?? Directory.GetFiles(dir, "*.RSD").FirstOrDefault(f => true);
        Dictionary<string, string>? rsd = null;
        try { if (rsdPath != null) rsd = ParseRsd(rsdPath); } catch { rsd = null; }

        // Collect texture slots: RSD TEX[0..] order, else *.TIM in the folder.
        var texNames = new List<string>();
        if (rsd != null)
        {
            for (int i = 0; ; i++)
            {
                if (!rsd.TryGetValue($"TEX[{i}]", out string? name)) break;
                texNames.Add(name);
            }
        }
        if (texNames.Count == 0)
            texNames = Directory.GetFiles(dir, "*.TIM").Select(Path.GetFileName).Where(n => n != null).Cast<string>().ToList();

        var slots = new List<(string Name, TimFile Tim, int OrgX)>();
        foreach (string name in texNames)
        {
            string full = Path.IsPathRooted(name) ? name : Path.Combine(dir, name);
            if (!File.Exists(full))
            {
                // Case-insensitive fallback for dumps checked out on any casing.
                string? found = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
                if (found == null) continue;
                full = found;
            }
            try
            {
                slots.Add((Path.GetFileName(full), new TimFile(full), ReadTimOrgX(full)));
            }
            catch { /* skip unreadable TIM */ }
        }

        HmdTimAtlas? atlas = slots.Count > 0 ? new HmdTimAtlas(slots) : null;

        // Preferred path: decode the GPU packets directly. Only the packet
        // TPAGE word says which TIM slot each polygon samples, so this is the
        // authoritative source for texturing.
        Exception? packetError = null;
        try
        {
            var packetParts = hmd.CreateRenderParts(tpageX => atlas?.PageForTpageX(tpageX) ?? tpageX);
            if (packetParts.Count > 0)
            {
                string packetDesc = atlas != null
                    ? $"Packet UV + {atlas.SlotCount} TIM(s) ({string.Join(", ", atlas.Names)})"
                    : "Packet geometry (no TIM found)";
                return (packetParts, atlas, packetDesc);
            }
        }
        catch (Exception ex) { packetError = ex; }

        // Fallback: rebuild from the Meditor .PLY/.MAT sources. These do not
        // record the texture page, so borrow it from the packet vertex keys.
        string? plyPath = null, matPath = null;
        if (rsd != null)
        {
            if (rsd.TryGetValue("PLY", out string? plyName))
                plyPath = Path.Combine(dir, plyName);
            if (rsd.TryGetValue("MAT", out string? matName))
                matPath = Path.Combine(dir, matName);
        }
        plyPath ??= FindSibling(dir, baseName, ".PLY");
        matPath ??= FindSibling(dir, baseName, ".MAT");
        if (plyPath != null && matPath != null && File.Exists(plyPath) && File.Exists(matPath) && atlas != null)
        {
            try
            {
                var pageMap = hmd.BuildTexturePageMap(tpageX => atlas.PageForTpageX(tpageX));
                var parts = PlyMatParts.Load(plyPath, matPath, pageMap);
                string desc = $"PLY/MAT + {atlas.SlotCount} TIM(s) ({string.Join(", ", atlas.Names)})";
                return (parts, atlas, desc);
            }
            catch { /* fall through to the packet error */ }
        }

        if (packetError != null) throw packetError;
        throw new InvalidDataException("No renderable geometry was found in this HMD.");
    }

    private void LoadSurvivorHmd()
    {
        if (_survivorHmd == null) return;
        var (parts, texture, textureDesc) = LoadHmdTexturedParts(_survivorHmd, _path!);
        _renderParts = parts;
        _parts.Items.Clear();
        for (int i = 0; i < _renderParts.Count; i++) _parts.Items.Add($"Block {i:D2}", true);
        _viewport.SetModel(_renderParts, texture);
        string pointers = string.Join(", ", _survivorHmd.BlockPointers.Select((p, i) => $"{i}:{p:X8}"));
        int triangles = _renderParts.Sum(p => p.Faces.Count);
        _details.Text = $"Game: Resident Evil Survivor PC\r\nType: HMD scene / NPC model\r\nBlocks: {_survivorHmd.BlockCount:N0}  Render parts: {_renderParts.Count:N0}  Triangles: {triangles:N0}\r\nTexture: {textureDesc}\r\nPrimitive header: 0x{_survivorHmd.PrimitiveHeaderOffset:X}\r\nBlock pointers (word offsets): {pointers}";
    }

    private void LoadSurvivorMesh()
    {
        if (_survivor == null) return;
        _renderParts = _survivor.CreateRenderParts();
        _parts.Items.Clear();
        for (int i = 0; i < _renderParts.Count; i++) _parts.Items.Add($"Part {i:D2}", true);
        _viewport.SetModel(_renderParts, _survivor.Texture);
        _details.Text = $"Game: Resident Evil Survivor PC\r\nType: NAB + NAC\r\nParts: {_renderParts.Count:N0}  Triangles: {_survivor.TriangleCount:N0}\r\nTexture: Embedded {_survivor.Texture.Width}x{_survivor.Texture.Height} A1R5G5B5\r\nAnimation bank: {(_survivor.HasAnimationBank ? Path.GetFileName(_survivor.AnimationPath) : "Not found")}  Selectors: {_survivor.SelectorCount}";
    }

    private void LoadAnimations(ModelFile model)
    {
        _edd = null;
        _skeleton = TryGetSkeleton(model);
        _animations.Clear();

        _animation.BeginUpdate();
        _animation.Items.Clear();
        if (_skeleton != null)
        {
            // Probe by chunk count instead of throwing past the last bank.
            int banks = Math.Min(
                CountChunksOfKind(model, ModelFile.ChunkKind.Animation),
                CountChunksOfKind(model, ModelFile.ChunkKind.Armature));
            for (int bank = 0; bank < banks; bank++)
            {
                IEdd edd = model.GetEdd(bank);
                Emr keyframes = model.GetEmr(bank);
                Emr rig = _skeleton.WithKeyframes(keyframes);
                for (int i = 0; i < edd.AnimationCount; i++)
                {
                    _animations.Add(new AnimationChoice(edd, rig, bank, i));
                    _animation.Items.Add($"Bank {bank} / Animation {i:D2} ({edd.GetAnimationDuration(i)} frames)");
                }
            }
        }
        _animation.EndUpdate();
        _animation.Enabled = _animation.Items.Count != 0;
        _playPause.Enabled = _animation.Items.Count != 0;
        _timeline.Enabled = _animation.Items.Count != 0;
        if (_animation.Items.Count != 0) _animation.SelectedIndex = 0;
        else UpdateTimeline(0, 0);
    }

    private void SelectAnimation()
    {
        SetPlaying(false);
        _animationTime = 0;
        AnimationChoice? choice = CurrentAnimation;
        if (choice != null)
        {
            _edd = choice.Edd;
            _skeleton = choice.Skeleton;
        }
        int duration = CurrentAnimationDuration;
        _timeline.Maximum = Math.Max(0, duration - 1);
        UpdateTimeline(0, duration);
        ApplyCurrentPose();
    }

    private AnimationChoice? CurrentAnimation => (uint)_animation.SelectedIndex < (uint)_animations.Count
        ? _animations[_animation.SelectedIndex] : null;

    private int CurrentAnimationDuration => CurrentAnimation is AnimationChoice choice
        ? choice.Edd.GetAnimationDuration(choice.AnimationIndex)
        : _survivor != null && (uint)_animation.SelectedIndex < (uint)_survivor.SelectorCount
            ? _survivor.GetAnimationDuration(_animation.SelectedIndex) : 0;

    private void SetPlaying(bool playing)
    {
        if (playing && CurrentAnimationDuration > 1) _animationTimer.Start();
        else _animationTimer.Stop();
        _playPause.Text = _animationTimer.Enabled ? "Pause" : "Play";
    }

    private void StepAnimation(int delta)
    {
        int duration = CurrentAnimationDuration;
        if (duration == 0) return;
        _animationTime = (_animationTime + delta + duration) % duration;
        ApplyCurrentPose();
    }

    private void UpdateTimeline(int time, int duration)
    {
        _updatingTimeline = true;
        _timeline.Value = Math.Clamp(time, _timeline.Minimum, _timeline.Maximum);
        _updatingTimeline = false;
        _frameLabel.Text = duration == 0 ? "0 / 0" : $"{time + 1} / {duration}";
    }

    private void ApplyCurrentPose()
    {
        if (_renderParts.Count == 0) return;
        int duration = CurrentAnimationDuration;
        if (_survivor != null)
        {
            if (duration != 0)
                _survivor.ApplyPose(_renderParts, _animation.SelectedIndex, Math.Clamp(_animationTime, 0, duration - 1));
            _viewport.RefitModel();
            UpdateTimeline(_animationTime, duration);
            _viewport.Invalidate();
            _animationTimer.Interval = Math.Max(8, (int)Math.Round(33.333 * 100.0 / (double)_speed.Value));
            return;
        }
        if (_skeleton == null) return;
        int keyFrame = 0;
        if (CurrentAnimation is AnimationChoice choice && duration != 0)
            keyFrame = choice.Edd.GetFrameIndex(choice.AnimationIndex, Math.Clamp(_animationTime, 0, duration - 1));
        Matrix4x4[] transforms = BuildPartTransforms(_skeleton, _renderParts.Count, keyFrame);
        for (int i = 0; i < _renderParts.Count; i++)
            _renderParts[i].ApplyTransform(transforms[i]);
        UpdateTimeline(_animationTime, duration);
        _viewport.Invalidate();
        _animationTimer.Interval = Math.Max(8, (int)Math.Round(33.333 * 100.0 / (double)_speed.Value));
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

    private IViewerTexture? CurrentTexture
    {
        get
        {
            if (_survivor != null) return _survivor.Texture;
            return _texture == null ? null : new TimTexture(_texture);
        }
    }

    private Bitmap? CreateTextureBitmap()
    {
        IViewerTexture? texture = CurrentTexture;
        if (texture == null) return null;
        if (_survivor == null && _texture != null && _texture.HasCluts)
        {
            var pages = GetTimAtlasPages(_texture);
            var atlas = new Bitmap(checked(pages.Count * 128), texture.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (int page = 0; page < pages.Count; page++)
                for (int y = 0; y < texture.Height; y++)
                    for (int x = 0; x < 128; x++)
                        if (pages[page].X + x < texture.Width)
                            atlas.SetPixel(page * 128 + x, y, Color.FromArgb(unchecked((int)texture.GetPixel(pages[page].X + x, y, pages[page].Clut))));
            return atlas;
        }
        int palettes = Math.Max(1, texture.PaletteCount);
        var bitmap = new Bitmap(texture.Width, checked(texture.Height * palettes), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (int palette = 0; palette < palettes; palette++)
            for (int y = 0; y < texture.Height; y++)
                for (int x = 0; x < texture.Width; x++)
                    bitmap.SetPixel(x, palette * texture.Height + y, Color.FromArgb(unchecked((int)texture.GetPixel(x, y, palette))));
        return bitmap;
    }

    private List<(int X, int Clut)> GetTimAtlasPages(TimFile tim)
    {
        var pages = new HashSet<(int X, int Clut)>();
        foreach (RenderFace face in _renderParts.SelectMany(part => part.Faces))
        {
            int clut = face.Clut >= 0 && face.Clut < tim.ClutCount ? face.Clut : face.Page;
            if (clut < 0 || clut >= tim.ClutCount) continue;
            int first = (int)MathF.Floor(face.UV.Min(uv => uv.X)) + face.Page * 128;
            int last = (int)MathF.Ceiling(face.UV.Max(uv => uv.X)) + face.Page * 128;
            for (int u = first; u <= last; u++)
            {
                int wrapped = ((u % tim.Width) + tim.Width) % tim.Width;
                pages.Add((wrapped / 128 * 128, clut));
            }
        }
        if (pages.Count == 0) pages.Add((0, 0));
        return pages.OrderBy(p => p.Clut).ThenBy(p => p.X).ToList();
    }

    private void ViewTexture()
    {
        if (_petModel != null)
        {
            try
            {
                if (_petTextures.Count == 0)
                {
                    MessageBox.Show(this, "No PET textures loaded. Make sure texture files are beside the PET.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int sel = Math.Clamp(_mesh.SelectedIndex, 0, Math.Max(0, _petModel.Meshes.Count - 1));
                var tex = ResolvePetTexture(_petModel.Meshes[sel]);
                if (tex is PetBitmapTexture pbt && pbt.Width > 1 && pbt.Height > 1)
                {
                    var petPreview = new Form { Text = $"PET texture - {pbt.Width}x{pbt.Height}", StartPosition = FormStartPosition.CenterParent,
                        ClientSize = new Size(Math.Clamp(pbt.Width * 2, 480, 1100), Math.Clamp(pbt.Height * 2, 360, 800)) };
                    var petPicture = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 32, 32), SizeMode = PictureBoxSizeMode.Zoom, Image = new Bitmap(pbt.Width, pbt.Height) };
                    petPreview.FormClosed += (_, _) => petPicture.Image?.Dispose();
                    petPreview.Controls.Add(petPicture);
                    petPreview.Show(this);
                }
                else
                {
                    MessageBox.Show(this, "No PET texture is bound to this mesh.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Texture view failed:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
        if (_re4Bin != null || _re4Pmd != null || _re4GenericParts != null || _re4Scenario != null)
        {
            try
            {
                var parts = CurrentRe4Parts();
                var bitmaps = BuildMaterialBitmaps(parts);
                if (bitmaps.Count == 0 || bitmaps.All(b => b.Width == 1 && b.Height == 1))
                {
                    MessageBox.Show(this, "No PACK textures are bound to this mesh. Load matching PACK textures first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int totalW = bitmaps.Sum(b => b.Width), maxH = bitmaps.Max(b => b.Height);
                using var atlas = new Bitmap(Math.Max(1, totalW), Math.Max(1, maxH));
                using (var g = System.Drawing.Graphics.FromImage(atlas))
                {
                    int x = 0;
                    foreach (var b in bitmaps) { g.DrawImage(b, x, 0); x += b.Width; }
                }
                var re4preview = new Form { Text = $"RE4 texture atlas - {atlas.Width}x{atlas.Height}", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(Math.Clamp(atlas.Width * 2, 480, 1100), Math.Clamp(atlas.Height * 2, 360, 800)) };
                var re4picture = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 32, 32), SizeMode = PictureBoxSizeMode.Zoom, Image = new Bitmap(atlas) };
                re4preview.FormClosed += (_, _) => re4picture.Image?.Dispose();
                re4preview.Controls.Add(re4picture);
                re4preview.Show(this);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Texture view failed:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
        using Bitmap? bitmap = CreateTextureBitmap();
        if (bitmap == null)
        {
            MessageBox.Show(this, "The current model has no texture loaded.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var preview = new Form { Text = $"Texture atlas - {bitmap.Width}x{bitmap.Height}", StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(Math.Clamp(bitmap.Width * 2, 480, 1100), Math.Clamp(bitmap.Height * 2, 360, 800)) };
        var picture = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 32, 32), SizeMode = PictureBoxSizeMode.Zoom,
            Image = new Bitmap(bitmap) };
        preview.FormClosed += (_, _) => picture.Image?.Dispose();
        preview.Controls.Add(picture);
        preview.Show(this);
    }

    private void ExportTexture()
    {
        if (_petModel != null)
        {
            try
            {
                if (_petTextures.Count == 0)
                {
                    MessageBox.Show(this, "No PET textures loaded.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int sel = Math.Clamp(_mesh.SelectedIndex, 0, Math.Max(0, _petModel.Meshes.Count - 1));
                var tex = ResolvePetTexture(_petModel.Meshes[sel]);
                if (tex is PetBitmapTexture pbt)
                {
                    using var saveDlg = new SaveFileDialog { Title = "Export PET texture", Filter = "PNG image (*.png)|*.png|Bitmap image (*.bmp)|*.bmp",
                        FileName = $"pet_texture_{sel}.png" };
                    if (saveDlg.ShowDialog(this) != DialogResult.OK) return;
                    using var bmp = new Bitmap(pbt.Width, pbt.Height);
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                        g.DrawImage(new Bitmap(_petTextures.Values.First()), 0, 0);
                    bmp.Save(saveDlg.FileName, Path.GetExtension(saveDlg.FileName).Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                        ? System.Drawing.Imaging.ImageFormat.Bmp : System.Drawing.Imaging.ImageFormat.Png);
                }
                else
                {
                    MessageBox.Show(this, "No PET texture is bound to this mesh.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"PET texture export failed:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
        if (_re4Bin != null || _re4Pmd != null || _re4GenericParts != null || _re4Scenario != null)
        {
            ExportRe4Texture();
            return;
        }
        using Bitmap? bitmap = CreateTextureBitmap();
        if (bitmap == null)
        {
            MessageBox.Show(this, "The current model has no texture loaded.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new SaveFileDialog { Title = "Export texture image", Filter = "PNG image (*.png)|*.png|Bitmap image (*.bmp)|*.bmp",
            FileName = Path.GetFileNameWithoutExtension(_path ?? "texture") + "_texture.png" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        bitmap.Save(dialog.FileName, Path.GetExtension(dialog.FileName).Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            ? System.Drawing.Imaging.ImageFormat.Bmp : System.Drawing.Imaging.ImageFormat.Png);
    }

    private void ReplaceTextureImage()
    {
        using var dialog = new OpenFileDialog { Title = "Replace texture from PNG/BMP", Filter = "Images (*.png;*.bmp)|*.png;*.bmp|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var bitmap = new Bitmap(dialog.FileName);
            if (_petModel != null)
            {
                int sel = Math.Clamp(_mesh.SelectedIndex, 0, Math.Max(0, _petModel.Meshes.Count - 1));
                var tex = ResolvePetTexture(_petModel.Meshes[sel]);
                if (tex is PetBitmapTexture pbt)
                {
                    string key = pbt.Description ?? (_petModel.Meshes[sel].TexMap.Length > 0 ? _petModel.Textures[Math.Clamp(_petModel.Meshes[sel].TexMap[0], 0, _petModel.Textures.Count - 1)] : "");
                    _petTextures[key.ToLowerInvariant()] = new Bitmap(bitmap);
                    LoadPetSelectedMesh();
                    MessageBox.Show(this, $"Replaced PET texture '{key}'.", "PET texture", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this, "No PET texture is bound to this mesh.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }
            if (_survivor != null)
            {
                _survivor.Texture.Replace(bitmap, _survivor.Data);
                _survivor.CommitTextureEdits();
                LoadSurvivorMesh();
            }
            else
            {
                if (_texture == null || _model == null) throw new InvalidOperationException("Load a TIM texture before importing an image.");
                if (_texture.HasCluts) ImportTimPages(_texture, bitmap, GetTimAtlasPages(_texture));
                else ImportTimAtlas(_texture, bitmap);
                if (string.Equals(_texturePath, "Embedded TIM", StringComparison.Ordinal))
                    _model.SetTim(0, _texture);
                else if (!string.IsNullOrWhiteSpace(_texturePath))
                    _texture.Save(_texturePath);
                LoadSelectedMesh();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Texture replacement failed:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportTimPages(TimFile tim, Bitmap bitmap, List<(int X, int Clut)> pages)
    {
        if (bitmap.Width != pages.Count * 128 || bitmap.Height != tim.Height)
            throw new InvalidDataException($"Expected an atlas of {pages.Count * 128}x{tim.Height}. Export the current texture to obtain the correct layout.");

        // TIM palettes share pixel indices across pages and CLUTs. Do not silently
        // change those indices or average conflicting colours during import.
        var colours = new Dictionary<(int Clut, int Index), ushort>();
        var originalPalettes = pages.Select(p => p.Clut).Distinct().ToDictionary(c => c, tim.GetPalette);
        for (int page = 0; page < pages.Count; page++)
            for (int y = 0; y < tim.Height; y++)
                for (int x = 0; x < 128 && pages[page].X + x < tim.Width; x++)
                {
                    int sourceX = pages[page].X + x;
                    var key = (pages[page].Clut, (int)tim.GetRawPixel(sourceX, y));
                    Color color = bitmap.GetPixel(page * 128 + x, y);
                    // Preserve the exact original PS1 colour when an exported pixel
                    // is unchanged, including its semi-transparency bit.
                    ushort value = unchecked((uint)color.ToArgb()) == tim.GetPixel(sourceX, y, key.Clut)
                        ? originalPalettes[key.Clut][key.Item2]
                        : TimFile.Convert32to16(unchecked((uint)color.ToArgb()));
                    if (colours.TryGetValue(key, out ushort existing) && existing != value)
                    {
                        if (MessageBox.Show(this, "This image needs new pixel indices and fewer colours to fit the TIM palette. Rebuild the displayed pages using nearest palette colours? Colours may change, and other meshes or CLUTs using the same pixels may be affected. Keep a backup of the model and texture.\n\nFor higher-quality textures, Classic Rebirth can load PNGs; HD Loader can dump and load WebP textures.",
                            "Import indexed texture", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                            QuantizeTimPages(tim, bitmap, pages);
                        else
                            throw new OperationCanceledException("Texture import cancelled; no changes were made.");
                        return;
                    }
                    colours[key] = value;
                }
        foreach (var group in colours.GroupBy(x => x.Key.Clut))
        {
            ushort[] palette = tim.GetPalette(group.Key);
            foreach (var entry in group) palette[entry.Key.Index] = entry.Value;
            tim.SetPalette(group.Key, palette);
        }
    }

    private static void QuantizeTimPages(TimFile tim, Bitmap bitmap, List<(int X, int Clut)> pages)
    {
        if (pages.GroupBy(p => p.X).All(g => g.Select(p => p.Clut).Distinct().Count() == 1))
        {
            QuantizeIndependentTimPages(tim, bitmap, pages);
            return;
        }
        int[] cluts = pages.Select(p => p.Clut).Distinct().OrderBy(c => c).ToArray();
        ushort[][] palettes = cluts.Select(tim.GetPalette).ToArray();
        int count = checked(tim.Width * tim.Height);
        var pixels = new ushort[count][];
        var covered = new bool[count];
        var written = new bool[count][];
        for (int page = 0; page < pages.Count; page++)
        {
            int palette = Array.IndexOf(cluts, pages[page].Clut);
            for (int y = 0; y < tim.Height; y++)
                for (int x = 0; x < 128 && pages[page].X + x < tim.Width; x++)
                {
                    int sx = pages[page].X + x, pos = y * tim.Width + sx;
                    if (!covered[pos])
                    {
                        covered[pos] = true;
                        int oldIndex = tim.GetRawPixel(sx, y);
                        pixels[pos] = palettes.Select(p => p[oldIndex]).ToArray();
                        written[pos] = new bool[cluts.Length];
                    }
                    uint argb = unchecked((uint)bitmap.GetPixel(page * 128 + x, y).ToArgb());
                    ushort color = argb == tim.GetPixel(sx, y, pages[page].Clut)
                        ? palettes[palette][tim.GetRawPixel(sx, y)] : TimFile.Convert32to16(argb);
                    if (written[pos][palette] && pixels[pos][palette] != color)
                        throw new InvalidDataException("The image contains different colours for the same texture pixel and CLUT on overlapping pages.");
                    pixels[pos][palette] = color;
                    written[pos][palette] = true;
                }
        }

        // Entries used by pixels outside the atlas must remain unchanged.
        var reserved = new bool[tim.ColoursPerClut];
        reserved[0] = true;
        for (int pos = 0; pos < count; pos++)
            if (!covered[pos]) reserved[tim.GetRawPixel(pos % tim.Width, pos / tim.Width)] = true;

        var histogram = new Dictionary<string, int>();
        foreach (ushort[]? pixel in pixels)
            if (pixel != null)
            {
                string key = new string(pixel.Select(c => (char)c).ToArray());
                histogram.TryGetValue(key, out int n);
                histogram[key] = n + 1;
            }

        // Weighted median-cut over the combined CLUT colours. A single index is
        // shared by every CLUT, so colours must be quantized as tuples.
        var boxes = new List<List<KeyValuePair<string, int>>> { histogram.ToList() };
        int free = reserved.Count(r => !r);
        while (boxes.Count < free)
        {
            int bestBox = -1, bestChannel = -1, bestRange = 0;
            for (int b = 0; b < boxes.Count; b++)
                for (int channel = 0; channel < cluts.Length * 4; channel++)
                {
                    int min = 255, max = 0;
                    foreach (var entry in boxes[b])
                    {
                        int v = TimChannel(entry.Key[channel / 4], channel % 4);
                        min = Math.Min(min, v); max = Math.Max(max, v);
                    }
                    int range = max - min;
                    if (boxes[b].Count > 1 && range > bestRange) { bestBox = b; bestChannel = channel; bestRange = range; }
                }
            if (bestBox < 0) break;
            var sorted = boxes[bestBox].OrderBy(e => TimChannel(e.Key[bestChannel / 4], bestChannel % 4)).ToList();
            int half = sorted.Sum(e => (long)e.Value) > int.MaxValue ? int.MaxValue : (int)(sorted.Sum(e => (long)e.Value) / 2);
            int sum = 0, split = 0;
            while (split < sorted.Count - 1 && sum < half) sum += sorted[split++].Value;
            split = Math.Clamp(split, 1, sorted.Count - 1);
            boxes[bestBox] = sorted.GetRange(0, split);
            boxes.Add(sorted.GetRange(split, sorted.Count - split));
        }

        var candidates = new List<(int Index, ushort[] Colors)>();
        for (int i = 0; i < reserved.Length; i++)
            if (reserved[i]) candidates.Add((i, palettes.Select(p => p[i]).ToArray()));
        int slot = 0;
        foreach (var box in boxes.Take(free))
        {
            while (reserved[slot]) slot++;
            var colors = new ushort[cluts.Length];
            long total = box.Sum(e => (long)e.Value);
            for (int c = 0; c < cluts.Length; c++)
            {
                int[] average = new int[4];
                for (int ch = 0; ch < 4; ch++)
                    average[ch] = (int)((box.Sum(e => (long)TimChannel(e.Key[c], ch) * e.Value) + total / 2) / total);
                colors[c] = (ushort)((average[0] >= 16 ? 0x8000 : 0) | (average[1] << 10) | (average[2] << 5) | average[3]);
            }
            candidates.Add((slot++, colors));
        }
        var mapping = new Dictionary<string, int>(histogram.Count);
        foreach (string key in histogram.Keys)
        {
            long best = long.MaxValue;
            int bestIndex = 0;
            foreach (var candidate in candidates)
            {
                long distance = 0;
                for (int c = 0; c < cluts.Length; c++)
                    for (int ch = 0; ch < 4; ch++)
                    {
                        int delta = TimChannel(key[c], ch) - TimChannel(candidate.Colors[c], ch);
                        distance += (long)delta * delta * (ch == 0 ? 4 : 1);
                    }
                if (distance < best) { best = distance; bestIndex = candidate.Index; }
            }
            mapping.Add(key, bestIndex);
        }
        foreach (var candidate in candidates)
            if (!reserved[candidate.Index])
                for (int c = 0; c < cluts.Length; c++) palettes[c][candidate.Index] = candidate.Colors[c];
        for (int c = 0; c < cluts.Length; c++) tim.SetPalette(cluts[c], palettes[c]);
        for (int pos = 0; pos < count; pos++)
            if (covered[pos])
                tim.SetRawPixel(pos % tim.Width, pos / tim.Width, (byte)mapping[new string(pixels[pos].Select(c => (char)c).ToArray())]);
    }

    private static void QuantizeIndependentTimPages(TimFile tim, Bitmap bitmap, List<(int X, int Clut)> pages)
    {
        var covered = new bool[tim.Width * tim.Height];
        foreach (var page in pages)
            for (int y = 0; y < tim.Height; y++)
                for (int x = page.X; x < Math.Min(page.X + 128, tim.Width); x++)
                    covered[y * tim.Width + x] = true;

        var reserved = new bool[tim.ColoursPerClut];
        reserved[0] = true;
        for (int i = 0; i < covered.Length; i++)
            if (!covered[i]) reserved[tim.GetRawPixel(i % tim.Width, i / tim.Width)] = true;

        // Calculate all palettes and assignments before changing the TIM.
        var newPalettes = new Dictionary<int, ushort[]>();
        var assignments = new Dictionary<int, byte>();
        foreach (int clut in pages.Select(p => p.Clut).Distinct())
        {
            ushort[] palette = tim.GetPalette(clut);
            var histogram = new Dictionary<ushort, int>();
            for (int page = 0; page < pages.Count; page++)
            {
                if (pages[page].Clut != clut) continue;
                for (int y = 0; y < tim.Height; y++)
                    for (int x = 0; x < 128 && pages[page].X + x < tim.Width; x++)
                    {
                        ushort color = TimFile.Convert32to16(unchecked((uint)bitmap.GetPixel(page * 128 + x, y).ToArgb()));
                        histogram.TryGetValue(color, out int n);
                        histogram[color] = n + 1;
                    }
            }

            int free = reserved.Count(r => !r);
            var boxes = new List<List<KeyValuePair<ushort, int>>> { histogram.ToList() };
            while (boxes.Count < free)
            {
                int bestBox = -1, bestChannel = -1;
                long bestScore = 0;
                for (int b = 0; b < boxes.Count; b++)
                {
                    if (boxes[b].Count < 2) continue;
                    long population = boxes[b].Sum(e => (long)e.Value);
                    for (int ch = 0; ch < 4; ch++)
                    {
                        int channel = ch;
                        int range = boxes[b].Max(e => TimChannel(e.Key, channel)) - boxes[b].Min(e => TimChannel(e.Key, channel));
                        long score = (long)range * population;
                        if (score > bestScore) { bestBox = b; bestChannel = ch; bestScore = score; }
                    }
                }
                if (bestBox < 0) break;
                int selectedChannel = bestChannel;
                var sorted = boxes[bestBox].OrderBy(e => TimChannel(e.Key, selectedChannel)).ToList();
                long half = sorted.Sum(e => (long)e.Value) / 2, sum = 0;
                int split = 0;
                while (split < sorted.Count - 1 && sum < half) sum += sorted[split++].Value;
                split = Math.Clamp(split, 1, sorted.Count - 1);
                boxes[bestBox] = sorted.GetRange(0, split);
                boxes.Add(sorted.GetRange(split, sorted.Count - split));
            }

            var indices = Enumerable.Range(0, reserved.Length).Where(i => reserved[i]).ToList();
            int slot = 0;
            foreach (var box in boxes.Take(free))
            {
                while (reserved[slot]) slot++;
                long total = box.Sum(e => (long)e.Value);
                int[] avg = new int[4];
                for (int ch = 0; ch < 4; ch++)
                {
                    int channel = ch;
                    avg[ch] = (int)((box.Sum(e => (long)TimChannel(e.Key, channel) * e.Value) + total / 2) / total);
                }
                palette[slot] = (ushort)((avg[0] >= 16 ? 0x8000 : 0) | (avg[1] << 10) | (avg[2] << 5) | avg[3]);
                indices.Add(slot++);
            }
            var nearest = new Dictionary<ushort, byte>();
            foreach (ushort color in histogram.Keys)
            {
                int best = int.MaxValue, index = 0;
                foreach (int candidate in indices)
                {
                    int distance = 0;
                    for (int ch = 0; ch < 4; ch++)
                    {
                        int delta = TimChannel(color, ch) - TimChannel(palette[candidate], ch);
                        distance += delta * delta * (ch == 0 ? 4 : 1);
                    }
                    if (distance < best) { best = distance; index = candidate; }
                }
                nearest[color] = (byte)index;
            }
            for (int page = 0; page < pages.Count; page++)
            {
                if (pages[page].Clut != clut) continue;
                for (int y = 0; y < tim.Height; y++)
                    for (int x = 0; x < 128 && pages[page].X + x < tim.Width; x++)
                    {
                        ushort color = TimFile.Convert32to16(unchecked((uint)bitmap.GetPixel(page * 128 + x, y).ToArgb()));
                        assignments[y * tim.Width + pages[page].X + x] = nearest[color];
                    }
            }
            newPalettes.Add(clut, palette);
        }
        foreach (var entry in newPalettes) tim.SetPalette(entry.Key, entry.Value);
        foreach (var entry in assignments) tim.SetRawPixel(entry.Key % tim.Width, entry.Key / tim.Width, entry.Value);
    }

    private static int TimChannel(int color, int channel) => channel switch
    {
        0 => (color & 0x8000) != 0 ? 31 : 0,
        1 => (color >> 10) & 31,
        2 => (color >> 5) & 31,
        _ => color & 31
    };

    private static void ImportTimAtlas(TimFile tim, Bitmap bitmap)
    {
        if (bitmap.Width != tim.Width || bitmap.Height % tim.Height != 0)
            throw new InvalidDataException($"Expected {tim.Width}x{tim.Height} for one CLUT, or a vertical multiple of that height for a CLUT atlas.");
        int suppliedCluts = bitmap.Height / tim.Height;
        if (suppliedCluts > tim.ClutCount)
            throw new InvalidDataException($"The image contains {suppliedCluts} CLUT layers, but this TIM has {tim.ClutCount}.");
        if (!tim.HasCluts)
        {
            for (int y = 0; y < tim.Height; y++)
                for (int x = 0; x < tim.Width; x++)
                    tim.SetPixel(x, y, 0, unchecked((uint)bitmap.GetPixel(x, y).ToArgb()));
            return;
        }

        // The first layer defines the shared pixel indices. Remaining layers
        // recolour those same indices, which is how multi-CLUT TIMs work.
        ushort[] firstPalette = BuildTimPalette(bitmap, new Rectangle(0, 0, tim.Width, tim.Height), tim.ColoursPerClut);
        tim.SetPalette(0, firstPalette);
        for (int y = 0; y < tim.Height; y++)
            for (int x = 0; x < tim.Width; x++)
                tim.SetPixel(x, y, 0, unchecked((uint)bitmap.GetPixel(x, y).ToArgb()));

        for (int clut = 1; clut < suppliedCluts; clut++)
        {
            var sums = new long[tim.ColoursPerClut, 4];
            var counts = new int[tim.ColoursPerClut];
            for (int y = 0; y < tim.Height; y++)
                for (int x = 0; x < tim.Width; x++)
                {
                    int index = tim.GetRawPixel(x, y);
                    Color c = bitmap.GetPixel(x, clut * tim.Height + y);
                    sums[index, 0] += c.A; sums[index, 1] += c.R; sums[index, 2] += c.G; sums[index, 3] += c.B; counts[index]++;
                }
            ushort[] palette = tim.GetPalette(clut);
            for (int i = 0; i < palette.Length; i++)
                if (counts[i] != 0)
                {
                    Color c = Color.FromArgb((int)(sums[i, 0] / counts[i]), (int)(sums[i, 1] / counts[i]),
                        (int)(sums[i, 2] / counts[i]), (int)(sums[i, 3] / counts[i]));
                    palette[i] = TimFile.Convert32to16(unchecked((uint)c.ToArgb()));
                }
            tim.SetPalette(clut, palette);
        }
    }

    private static ushort[] BuildTimPalette(Bitmap bitmap, Rectangle area, int capacity)
    {
        var result = new ushort[capacity];
        var seen = new HashSet<ushort>();
        int next = 1; // PS1 palette entry zero remains transparent.
        for (int y = area.Top; y < area.Bottom && next < capacity; y++)
            for (int x = area.Left; x < area.Right && next < capacity; x++)
            {
                ushort colour = TimFile.Convert32to16(unchecked((uint)bitmap.GetPixel(x, y).ToArgb()));
                if (colour != 0 && seen.Add(colour)) result[next++] = colour;
            }
        return result;
    }

    private void ExportSkeletalAnimation()
    {
        if (_survivor != null)
        {
            ExportSurvivorSkeletalAnimation();
            return;
        }
        AnimationChoice? choice = CurrentAnimation;
        if (choice == null || _skeleton == null)
        {
            MessageBox.Show(this, "Select an RE1, RE2, or RE3 animation first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = "Export skeletal animation",
            Filter = "Biohazard skeletal animation (*.bhskel.json)|*.bhskel.json|JSON (*.json)|*.json",
            FileName = $"{Path.GetFileNameWithoutExtension(_path)}_bank{choice.Bank}_anim{choice.AnimationIndex:D2}.bhskel.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        int duration = choice.Edd.GetAnimationDuration(choice.AnimationIndex);
        var frames = new float[duration][][];
        for (int time = 0; time < duration; time++)
        {
            int keyFrame = choice.Edd.GetFrameIndex(choice.AnimationIndex, time);
            Matrix4x4[] matrices = BuildPartTransforms(choice.Skeleton, choice.Skeleton.NumParts, keyFrame);
            frames[time] = matrices.Select(MatrixValues).ToArray();
        }
        var document = new
        {
            format = "Tool Hazard skeletal animation",
            version = 1,
            sourceGame = VersionName(choice.Skeleton.Version),
            sourceModel = Path.GetFileName(_path),
            sourceBank = choice.Bank,
            sourceAnimation = choice.AnimationIndex,
            framesPerSecond = 30,
            boneCount = choice.Skeleton.NumParts,
            frames
        };
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void ExportBvhAnimation()
    {
        if (_survivor != null)
        {
            ExportSurvivorBvhAnimation();
            return;
        }
        AnimationChoice? choice = CurrentAnimation;
        if (choice == null || _skeleton == null)
        {
            MessageBox.Show(this, "Select an RE1, RE2, or RE3 animation first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = "Export animation for Blender",
            Filter = "Biovision Hierarchy (*.bvh)|*.bvh",
            FileName = $"{Path.GetFileNameWithoutExtension(_path)}_bank{choice.Bank}_anim{choice.AnimationIndex:D2}.bvh"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        Emr skeleton = choice.Skeleton;
        var orderedBones = new List<int>();
        var sb = new StringBuilder("HIERARCHY\n");
        void WriteBone(int bone, int depth, bool root)
        {
            orderedBones.Add(bone);
            string indent = new(' ', depth * 2);
            sb.Append(indent).Append(root ? "ROOT " : "JOINT ").Append("bone_").Append(bone.ToString("D2")).Append('\n');
            sb.Append(indent).Append("{\n");
            Emr.Vector offset = root ? new Emr.Vector() : skeleton.GetRelativePosition(bone);
            sb.Append(indent).Append("  OFFSET ").Append(BvhNumber(offset.x)).Append(' ').Append(BvhNumber(offset.y)).Append(' ').Append(BvhNumber(offset.z)).Append('\n');
            sb.Append(indent).Append(root ? "  CHANNELS 6 Xposition Yposition Zposition Zrotation Yrotation Xrotation\n" : "  CHANNELS 3 Zrotation Yrotation Xrotation\n");
            byte[] children = skeleton.GetArmatureParts(bone).ToArray();
            foreach (byte child in children) WriteBone(child, depth + 1, false);
            if (children.Length == 0)
            {
                sb.Append(indent).Append("  End Site\n").Append(indent).Append("  {\n")
                    .Append(indent).Append("    OFFSET 0 0 0\n").Append(indent).Append("  }\n");
            }
            sb.Append(indent).Append("}\n");
        }
        WriteBone(0, 0, true);

        int duration = choice.Edd.GetAnimationDuration(choice.AnimationIndex);
        sb.Append("MOTION\nFrames: ").Append(duration).Append("\nFrame Time: 0.0333333333\n");
        const double angleScale = 360.0 / 4096.0;
        for (int time = 0; time < duration; time++)
        {
            int keyFrameIndex = choice.Edd.GetFrameIndex(choice.AnimationIndex, time);
            Emr.KeyFrame frame = skeleton.KeyFrames[Math.Clamp(keyFrameIndex, 0, skeleton.KeyFrames.Length - 1)];
            for (int i = 0; i < orderedBones.Count; i++)
            {
                int bone = orderedBones[i];
                if (i == 0)
                    sb.Append(BvhNumber(frame.Offset.x)).Append(' ').Append(BvhNumber(frame.Offset.y)).Append(' ').Append(BvhNumber(frame.Offset.z)).Append(' ');
                Emr.Vector angle = frame.GetAngle(bone);
                sb.Append(BvhNumber(angle.z * angleScale)).Append(' ').Append(BvhNumber(angle.y * angleScale)).Append(' ').Append(BvhNumber(angle.x * angleScale));
                if (i + 1 != orderedBones.Count) sb.Append(' ');
            }
            sb.Append('\n');
        }
        File.WriteAllText(dialog.FileName, sb.ToString());
    }

    private static string BvhNumber(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private void ExportNativeAnimationBank()
    {
        if (_survivor != null)
        {
            if (!_survivor.HasAnimationBank)
            {
                MessageBox.Show(this, "No matching Survivor NAC animation bank was found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var nacDialog = new SaveFileDialog
            {
                Title = "Export native Survivor animation bank",
                Filter = "Survivor animation bank (*.nac)|*.nac",
                FileName = Path.GetFileName(_survivor.AnimationPath)
            };
            if (nacDialog.ShowDialog(this) == DialogResult.OK)
                _survivor.SaveAnimationBank(nacDialog.FileName);
            return;
        }
        AnimationChoice? choice = CurrentAnimation;
        if (choice == null)
        {
            MessageBox.Show(this, "Select an RE1, RE2, or RE3 animation first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = "Export native animation bank",
            Filter = "Biohazard animation index (*.edd)|*.edd",
            FileName = $"{Path.GetFileNameWithoutExtension(_path)}_bank{choice.Bank}.edd"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllBytes(dialog.FileName, choice.Edd.Data.ToArray());
        File.WriteAllBytes(Path.ChangeExtension(dialog.FileName, ".emr"), _model!.GetEmr(choice.Bank).Data.ToArray());
    }

    private void ReplaceSurvivorAnimationBank()
    {
        if (_survivor == null)
        {
            MessageBox.Show(this, "Open a Survivor NAB model first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new OpenFileDialog { Title = "Select the source NAC (matching NAB required)", Filter = "Survivor animation bank (*.nac)|*.nac" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            string nabPath = Path.ChangeExtension(dialog.FileName, ".NAB");
            if (!File.Exists(nabPath)) throw new FileNotFoundException("The matching source NAB was not found.", nabPath);
            SurvivorNab source = SurvivorNab.FromFile(nabPath);
            _survivor.ValidateAnimationBankCompatibility(source);
            if (MessageBox.Show(this, "Replace the current NAC in memory with this compatible bank? Matching selector tables do not guarantee that bone meanings or gameplay events are compatible. Save to a new NAB/NAC pair and keep a backup.",
                "Replace Survivor animation bank", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            _survivor.ReplaceAnimationBank(source);
            SetPlaying(false);
            _animation.Items.Clear();
            for (int i = 0; i < _survivor.SelectorCount; i++)
                _animation.Items.Add($"Selector {i:X2} ({_survivor.GetAnimationDuration(i)} frames)");
            _animation.Enabled = _playPause.Enabled = _timeline.Enabled = _animation.Items.Count != 0;
            if (_animation.Items.Count != 0) _animation.SelectedIndex = 0;
            else UpdateTimeline(0, 0);
            LoadSurvivorMesh();
            ApplyCurrentPose();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Survivor animation replacement failed:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ReplaceClassicAnimationBank()
    {
        if (_survivor != null)
        {
            MessageBox.Show(this, "This option replaces RE1–RE3 EDD/EMR banks only. Survivor NAC replacement and cross-game conversion are not yet supported.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        AnimationChoice? choice = CurrentAnimation;
        if (_model == null || choice == null)
        {
            MessageBox.Show(this, "Select a classic model animation bank first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new OpenFileDialog { Title = "Select a same-game EDD animation bank (with matching EMR)", Filter = "Biohazard animation index (*.edd)|*.edd" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            string emrPath = Path.ChangeExtension(dialog.FileName, ".emr");
            if (!File.Exists(emrPath)) throw new FileNotFoundException("The matching .emr file was not found.", emrPath);
            IEdd incomingEdd = _model.Version == BioVersion.Biohazard3
                ? new Edd2(File.ReadAllBytes(dialog.FileName))
                : new Edd1(_model.Version, File.ReadAllBytes(dialog.FileName));
            var incomingEmr = new Emr(_model.Version, File.ReadAllBytes(emrPath));
            ValidateClassicAnimationBank(incomingEdd, incomingEmr, _model.GetEmr(choice.Bank), _model.GetEmr(0));
            if (MessageBox.Show(this, $"Replace bank {choice.Bank} with {incomingEdd.AnimationCount} animations? Use a bank from the same game and a compatible skeleton. Bone order and game-specific animation events cannot be verified automatically. Keep a backup and save the model to a new file.",
                "Replace animation bank", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            int bank = choice.Bank;
            _model.SetEdd(bank, incomingEdd);
            _model.SetEmr(bank, incomingEmr);
            SetPlaying(false);
            LoadAnimations(_model);
            LoadSelectedMesh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Animation bank replacement failed:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ValidateClassicAnimationBank(IEdd edd, Emr incoming, Emr target, Emr rig)
    {
        if (incoming.Data.Length < 8 || incoming.NumParts != rig.NumParts || incoming.KeyFrameSize == 0 || incoming.KeyFrameSize != target.KeyFrameSize ||
            incoming.KeyFrameOffset > incoming.Data.Length)
            throw new InvalidDataException("The EMR has an incompatible skeleton or keyframe format.");
        if (incoming.NumArmatures != target.NumArmatures)
            throw new InvalidDataException("The EMR skeleton hierarchy does not match the target bank.");
        if (incoming.NumArmatures != 0)
        {
            for (int i = 0; i < incoming.NumParts; i++)
                if (!incoming.GetArmatureParts(i).SequenceEqual(target.GetArmatureParts(i)))
                    throw new InvalidDataException($"The EMR skeleton hierarchy differs at bone {i}.");
        }
        int frames = incoming.KeyFrames.Length;
        if (frames == 0 || edd.AnimationCount == 0)
            throw new InvalidDataException("The animation bank has no keyframes or animations.");
        for (int animation = 0; animation < edd.AnimationCount; animation++)
            for (int frame = 0; frame < edd.GetAnimationDuration(animation); frame++)
                if ((uint)edd.GetFrameIndex(animation, frame) >= (uint)frames)
                    throw new InvalidDataException($"Animation {animation} references a missing EMR keyframe.");
    }

    private void ExportSurvivorSkeletalAnimation()
    {
        if (_survivor == null || !_survivor.HasAnimations || _animation.SelectedIndex < 0) return;
        int selector = _animation.SelectedIndex;
        using var dialog = new SaveFileDialog
        {
            Title = "Export Survivor skeletal animation",
            Filter = "Biohazard skeletal animation (*.bhskel.json)|*.bhskel.json|JSON (*.json)|*.json",
            FileName = $"{Path.GetFileNameWithoutExtension(_path)}_selector{selector:X2}.bhskel.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        int duration = _survivor.GetAnimationDuration(selector);
        var frames = new float[duration][][];
        for (int time = 0; time < duration; time++)
            frames[time] = _survivor.GetGlobalPose(selector, time).Select(MatrixValues).ToArray();
        var document = new
        {
            format = "Tool Hazard skeletal animation",
            version = 1,
            sourceGame = "Biohazard Gun Survivor PC",
            sourceModel = Path.GetFileName(_path),
            sourceAnimation = selector,
            framesPerSecond = 30,
            boneCount = _survivor.NodeCount,
            parents = Enumerable.Range(0, _survivor.NodeCount).Select(_survivor.GetParentIndex).ToArray(),
            frames
        };
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void ExportSurvivorBvhAnimation()
    {
        if (_survivor == null || !_survivor.HasAnimations || _animation.SelectedIndex < 0) return;
        int selector = _animation.SelectedIndex;
        using var dialog = new SaveFileDialog
        {
            Title = "Export Survivor animation for Blender",
            Filter = "Biovision Hierarchy (*.bvh)|*.bvh",
            FileName = $"{Path.GetFileNameWithoutExtension(_path)}_selector{selector:X2}.bvh"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var orderedBones = new List<int>();
        var sb = new StringBuilder("HIERARCHY\n");
        void WriteBone(int bone, int depth, bool root)
        {
            orderedBones.Add(bone);
            string indent = new(' ', depth * 2);
            sb.Append(indent).Append(root ? "ROOT " : "JOINT ").Append("bone_").Append(bone.ToString("D2")).Append('\n');
            sb.Append(indent).Append("{\n");
            Matrix4x4 bind = _survivor.GetBindLocal(bone);
            sb.Append(indent).Append("  OFFSET ").Append(BvhNumber(bind.M41)).Append(' ').Append(BvhNumber(bind.M42)).Append(' ').Append(BvhNumber(bind.M43)).Append('\n');
            sb.Append(indent).Append(root ? "  CHANNELS 6 Xposition Yposition Zposition Zrotation Yrotation Xrotation\n" : "  CHANNELS 3 Zrotation Yrotation Xrotation\n");
            int[] children = Enumerable.Range(0, _survivor.NodeCount).Where(x => _survivor.GetParentIndex(x) == bone).ToArray();
            foreach (int child in children) WriteBone(child, depth + 1, false);
            if (children.Length == 0)
                sb.Append(indent).Append("  End Site\n").Append(indent).Append("  {\n").Append(indent).Append("    OFFSET 0 0 0\n").Append(indent).Append("  }\n");
            sb.Append(indent).Append("}\n");
        }
        int root = Enumerable.Range(0, _survivor.NodeCount).FirstOrDefault(x => _survivor.GetParentIndex(x) < 0);
        WriteBone(root, 0, true);
        int duration = _survivor.GetAnimationDuration(selector);
        sb.Append("MOTION\nFrames: ").Append(duration).Append("\nFrame Time: 0.0333333333\n");
        for (int time = 0; time < duration; time++)
        {
            Matrix4x4[] local = _survivor.GetLocalPose(selector, time);
            for (int i = 0; i < orderedBones.Count; i++)
            {
                Matrix4x4 m = local[orderedBones[i]];
                if (i == 0) sb.Append(BvhNumber(m.M41)).Append(' ').Append(BvhNumber(m.M42)).Append(' ').Append(BvhNumber(m.M43)).Append(' ');
                Vector3 euler = SurvivorEulerZyx(m);
                sb.Append(BvhNumber(euler.Z)).Append(' ').Append(BvhNumber(euler.Y)).Append(' ').Append(BvhNumber(euler.X));
                if (i + 1 != orderedBones.Count) sb.Append(' ');
            }
            sb.Append('\n');
        }
        File.WriteAllText(dialog.FileName, sb.ToString());
    }

    private static Vector3 SurvivorEulerZyx(Matrix4x4 m)
    {
        const float radiansToDegrees = 180f / MathF.PI;
        float y = MathF.Asin(Math.Clamp(m.M13, -1f, 1f));
        float cosine = MathF.Cos(y);
        float x, z;
        if (MathF.Abs(cosine) > 0.00001f)
        {
            x = MathF.Atan2(-m.M23, m.M33);
            z = MathF.Atan2(-m.M12, m.M11);
        }
        else
        {
            x = 0;
            z = MathF.Atan2(m.M21, m.M22);
        }
        return new Vector3(x * radiansToDegrees, y * radiansToDegrees, z * radiansToDegrees);
    }

    private static float[] MatrixValues(Matrix4x4 m) => new[]
    {
        m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44
    };

    private void SaveModelAs()
    {
        if (_re4Bin != null || _re4Pmd != null || _re4Pack != null || _re4Scenario != null)
        {
            SaveRe4As();
            return;
        }
        if (_survivor != null && _path != null)
        {
            using var survivorDialog = new SaveFileDialog { FileName = Path.GetFileName(_path), Filter = "Survivor model (*.nab)|*.nab" };
            if (survivorDialog.ShowDialog(this) == DialogResult.OK)
            {
                _survivor.Save(survivorDialog.FileName);
                if (_survivor.HasAnimationBank)
                    _survivor.SaveAnimationBank(Path.ChangeExtension(survivorDialog.FileName, ".NAC"));
            }
            return;
        }
        if (_petModel != null && _path != null)
        {
            using var petDialog = new SaveFileDialog { FileName = Path.GetFileName(_path), Filter = "White Day PET (*.pet)|*.pet|All files (*.*)|*.*" };
            if (petDialog.ShowDialog(this) == DialogResult.OK)
            {
                SavePet(petDialog.FileName);
            }
            return;
        }
        if (_model == null || _path == null) return;
        using var dialog = new SaveFileDialog { FileName = Path.GetFileName(_path), Filter = "Biohazard model (*.emd;*.pld)|*.emd;*.pld|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) _model.Save(dialog.FileName);
    }

    private int SelectedPartIndex()
    {
        int index = _parts.SelectedIndex;
        if (index < 0) MessageBox.Show(this, "Select a part in the Visible parts list first.", "Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return index;
    }

    private IModelMesh? GetEditableMesh(bool selectedPart)
    {
        if (_model == null || _mesh.SelectedIndex < 0) return null;
        IModelMesh source = _model.GetMesh(_mesh.SelectedIndex);
        if (!selectedPart) return source;
        int selected = SelectedPartIndex();
        if (selected < 0 || selected >= source.NumParts) return null;
        IModelMeshBuilder builder = source.ToBuilder();
        for (int i = builder.Count - 1; i >= 0; i--) if (i != selected) builder.RemoveAt(i);
        return builder.ToMesh();
    }

    private void ExportMesh(bool selectedPart, bool native)
    {
        if (_re4Bin != null || _re4Pmd != null || _re4GenericParts != null || _re4Scenario != null)
        {
            if (native)
            {
                if (_re4Bin != null && !_re4Bin.CanSaveNative)
                {
                    MessageBox.Show(this, "PS2 VIF BIN has no native mesh writer; export to OBJ instead.", "Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                SaveRe4As();
                return;
            }
            ExportRe4Mesh("obj");
            return;
        }
        if (_survivor != null)
        {
            int selected = selectedPart ? SelectedPartIndex() : -1;
            if (selectedPart && selected < 0) return;
            if (native && !selectedPart)
            {
                using var nabDialog = new SaveFileDialog { FileName = Path.GetFileName(_path), Filter = "Survivor model (*.nab)|*.nab" };
                if (nabDialog.ShowDialog(this) == DialogResult.OK)
                {
                    _survivor.Save(nabDialog.FileName);
                    if (_survivor.HasAnimationBank)
                        _survivor.SaveAnimationBank(Path.ChangeExtension(nabDialog.FileName, ".NAC"));
                }
                return;
            }
            string extension = native ? "md2" : "obj";
            using var survivorDialog = new SaveFileDialog
            {
                FileName = selectedPart ? $"survivor_part_{selected:D2}.{extension}" : $"{Path.GetFileNameWithoutExtension(_path)}.{extension}",
                Filter = native ? "RE3-compatible mesh (*.md2)|*.md2" : "Wavefront OBJ (*.obj)|*.obj"
            };
            if (survivorDialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                if (native) File.WriteAllBytes(survivorDialog.FileName, _survivor.ToMd2(selected).Data.ToArray());
                else _survivor.ExportObj(survivorDialog.FileName, selected);
            }
            catch (Exception ex) { MessageBox.Show(this, $"Mesh export failed:\n{ex.Message}", "Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            return;
        }
        if (_petModel != null)
        {
            ExportPetMesh(selectedPart);
            return;
        }
        IModelMesh? export = GetEditableMesh(selectedPart);
        if (export == null) return;
        string ext = native ? NativeExtension(export.Version) : "obj";
        using var dialog = new SaveFileDialog { FileName = selectedPart ? $"part_{_parts.SelectedIndex:D2}.{ext}" : $"mesh.{ext}", Filter = native ? $"Native mesh (*.{ext})|*.{ext}|All files (*.*)|*.*" : "Wavefront OBJ (*.obj)|*.obj" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (native) File.WriteAllBytes(dialog.FileName, export.Data.ToArray());
        else new ObjExporter().Export(export, dialog.FileName, TexturePages);
    }

    private void ReplaceMesh(bool selectedPart, bool native)
    {
        if (_re4Bin != null || _re4Pmd != null || _re4GenericParts != null || _re4Scenario != null)
        {
            ImportRe4Mesh();
            return;
        }
        if (_survivor != null)
        {
            ReplaceSurvivorMesh(selectedPart, native);
            return;
        }
        if (_petModel != null)
        {
            ReplacePetMesh(selectedPart, native);
            return;
        }
        if (_model == null || _mesh.SelectedIndex < 0) return;
        IModelMesh current = _model.GetMesh(_mesh.SelectedIndex);
        string ext = native ? NativeExtension(current.Version) : "obj";
        using var dialog = new OpenFileDialog { Filter = native ? $"Native mesh (*.{ext})|*.{ext}|All files (*.*)|*.*" : "Wavefront OBJ (*.obj)|*.obj" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            IModelMesh incoming = native ? ReadNativeMesh(current.Version, dialog.FileName) : new ObjImporter().Import(current.Version, dialog.FileName, TexturePages);
            IModelMesh replacement = incoming;
            if (selectedPart)
            {
                int selected = SelectedPartIndex();
                if (selected < 0 || selected >= current.NumParts || incoming.NumParts == 0) return;
                IModelMeshBuilder target = current.ToBuilder();
                target[selected] = incoming.ToBuilder()[0];
                replacement = target.ToMesh();
            }
            _model.SetMesh(_mesh.SelectedIndex, replacement);
            LoadSelectedMesh();
        }
        catch (Exception ex) { MessageBox.Show(this, $"Mesh replacement failed:\n{ex.Message}", "Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ReplaceSurvivorMesh(bool selectedPart, bool native)
    {
        if (_survivor == null) return;
        using var dialog = new OpenFileDialog
        {
            Filter = native
                ? "Supported native model (*.nab;*.emd;*.pld;*.md2)|*.nab;*.emd;*.pld;*.md2|All files (*.*)|*.*"
                : "Wavefront OBJ (*.obj)|*.obj"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            int selected = selectedPart ? SelectedPartIndex() : -1;
            if (selectedPart && selected < 0) return;
            string extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            if (extension == ".nab")
            {
                SurvivorNab incoming = SurvivorNab.FromFile(dialog.FileName);
                _survivor.ReplaceWith(incoming, selected);
            }
            else if (!native)
            {
                _survivor.ReplaceFromObj(dialog.FileName, selected);
            }
            else
            {
                IModelMesh incoming;
                Emr? incomingSkeleton = null;
                if (extension is ".emd" or ".pld")
                {
                    ModelFile model = ModelFile.FromFile(dialog.FileName);
                    incoming = model.GetMesh(0);
                    incomingSkeleton = TryGetSkeleton(model);
                    if (!selectedPart && incoming is Md1 re2Mesh)
                    {
                        TimFile? sourceTexture = null;
                        try { sourceTexture = model.GetTim(0); }
                        catch
                        {
                            string timPath = Path.ChangeExtension(dialog.FileName, ".TIM");
                            if (File.Exists(timPath)) sourceTexture = new TimFile(timPath);
                        }
                        if (sourceTexture != null && MessageBox.Show(this,
                            "Build a new Survivor texture from the RE2 model's TIM, preserving per-polygon texture pages and CLUTs? This replaces the current Survivor texture and may create a larger surface. Save to a new NAB and test in-game.",
                            "Import RE2 mesh and texture", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            _survivor.ReplaceFromRe2WithTexture(re2Mesh, incomingSkeleton, sourceTexture);
                            LoadSurvivorMesh();
                            return;
                        }
                    }
                }
                else if (extension == ".md2") incoming = new Md2(File.ReadAllBytes(dialog.FileName));
                else incoming = new ObjImporter().Import(BioVersion.Biohazard3, dialog.FileName, Math.Max(1, _survivor.Texture.Width / 128));
                _survivor.ReplaceFromClassic(incoming, incomingSkeleton, selected);
            }
            LoadSurvivorMesh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Survivor mesh replacement failed:\n{ex.Message}", "Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportPetMesh(bool selectedPart)
    {
        if (_petModel == null || _petModel.Meshes.Count == 0) return;
        int sel = selectedPart ? SelectedPartIndex() : -1;
        if (selectedPart && sel < 0) return;
        var meshes = selectedPart ? new List<WhiteDayPetMesh> { _petModel.Meshes[sel] } : _petModel.Meshes;
        string defaultName = selectedPart ? $"pet_part_{sel:D2}.obj" : $"{Path.GetFileNameWithoutExtension(_path ?? "pet")}.obj";
        using var dialog = new SaveFileDialog { FileName = defaultName, Filter = "Wavefront OBJ (*.obj)|*.obj" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var writer = new StreamWriter(dialog.FileName, false, new UTF8Encoding(false));
            writer.WriteLine("# White Day PET mesh export");
            int vertexOffset = 1;
            foreach (var mesh in meshes)
            {
                writer.WriteLine($"o {mesh.Name}");
                // Vertices (skinned to first bone)
                for (int i = 0; i < mesh.Vertices.Count; i++)
                {
                    var v = mesh.Vertices[i];
                    Vector3 pos = new(v.Position.X, v.Position.Y, v.Position.Z);
                    if (v.BoneWeights.Count > 0)
                    {
                        var first = v.BoneWeights[0];
                        if (first.Weight > 0 && first.BoneId < _petModel.Bones.Count)
                            pos = Vector3.Transform(pos, _petModel.Bones[first.BoneId].Matrix);
                    }
                    writer.WriteLine(FormattableString.Invariant($"v {pos.X:R} {pos.Y:R} {pos.Z:R}"));
                }
                // UVs
                foreach (var poly in mesh.Polygons)
                {
                    foreach (var idx in poly.Indices)
                        writer.WriteLine(FormattableString.Invariant($"vt {idx.Uv.X:R} {1f - idx.Uv.Y:R}"));
                }
                // Normals
                foreach (var poly in mesh.Polygons)
                {
                    foreach (var idx in poly.Indices)
                        writer.WriteLine(FormattableString.Invariant($"vn {idx.Normal.X:R} {idx.Normal.Y:R} {idx.Normal.Z:R}"));
                }
                // Faces
                int uvOffset = 0;
                foreach (var poly in mesh.Polygons)
                {
                    uvOffset += poly.Indices.Length;
                }
                uvOffset = 0;
                foreach (var poly in mesh.Polygons)
                {
                    writer.Write("f");
                    for (int c = 0; c < poly.Indices.Length; c++)
                    {
                        int vi = vertexOffset + (int)poly.Indices[c].Index;
                        int vti = vertexOffset + uvOffset + c;
                        int vni = vertexOffset + uvOffset + c;
                        writer.Write($" {vi}/{vti}/{vni}");
                    }
                    writer.WriteLine();
                    if (poly.Indices.Length == 4)
                    {
                        // Second triangle for quad
                        writer.Write($"f {vertexOffset + (int)poly.Indices[2].Index}/{vertexOffset + uvOffset + 2}/{vertexOffset + uvOffset + 2} ");
                        writer.Write($"{vertexOffset + (int)poly.Indices[1].Index}/{vertexOffset + uvOffset + 1}/{vertexOffset + uvOffset + 1} ");
                        writer.WriteLine($"{vertexOffset + (int)poly.Indices[3].Index}/{vertexOffset + uvOffset + 3}/{vertexOffset + uvOffset + 3}");
                    }
                    uvOffset += poly.Indices.Length;
                }
                vertexOffset += mesh.Vertices.Count;
            }
            MessageBox.Show(this, $"PET mesh exported to {dialog.FileName}.", "PET Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"PET mesh export failed:\n{ex.Message}", "Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ReplacePetMesh(bool selectedPart, bool native)
    {
        if (_petModel == null) return;
        using var dialog = new OpenFileDialog { Filter = native ? "Wavefront OBJ (*.obj)|*.obj|All files (*.*)|*.*" : "Wavefront OBJ (*.obj)|*.obj|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            if (ext != ".obj") throw new InvalidDataException("Only OBJ import is supported for PET.");
            var imported = new WavefrontObjFile(dialog.FileName);
            // Rebuild PET meshes from imported OBJ (simple replacement of all meshes with the imported geometry).
            var newMeshes = new List<WhiteDayPetMesh>();
            foreach (var obj in imported.Objects)
            {
                var mesh = new WhiteDayPetMesh { Name = obj.Name };
                var verts = new List<WhiteDayPetVertex>();
                var uvs = new List<Vector2>();
                var norms = new List<Vector3>();
                foreach (var tri in obj.Triangles)
                {
                    AddVertex(verts, uvs, norms, imported, tri.a); AddVertex(verts, uvs, norms, imported, tri.b); AddVertex(verts, uvs, norms, imported, tri.c);
                }
                foreach (var quad in obj.Quads)
                {
                    AddVertex(verts, uvs, norms, imported, quad.a); AddVertex(verts, uvs, norms, imported, quad.b); AddVertex(verts, uvs, norms, imported, quad.c);
                    AddVertex(verts, uvs, norms, imported, quad.c); AddVertex(verts, uvs, norms, imported, quad.d); AddVertex(verts, uvs, norms, imported, quad.a);
                }
                mesh.Vertices = verts;
                // Build polygons: each group of 3 vertices = one triangle
                var polys = new List<WhiteDayPetPolygon>();
                for (int i = 0; i < verts.Count; i += 3)
                {
                    var poly = new WhiteDayPetPolygon
                    {
                        Indices = new WhiteDayPetPolygonIndex[3]
                        {
                            new() { Index = (uint)i, Normal = norms[i], Uv = uvs[i] },
                            new() { Index = (uint)(i + 1), Normal = norms[i + 1], Uv = uvs[i + 1] },
                            new() { Index = (uint)(i + 2), Normal = norms[i + 2], Uv = uvs[i + 2] },
                        }
                    };
                    polys.Add(poly);
                }
                mesh.Polygons = polys;
                mesh.TexMap = new byte[polys.Count];
                newMeshes.Add(mesh);
            }
            _petModel.Meshes = newMeshes;
            LoadPetSelectedMesh();
            MessageBox.Show(this, "PET mesh replaced from OBJ. Save to write the PET file.", "PET Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"PET mesh import failed:\n{ex.Message}", "Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static void AddVertex(List<WhiteDayPetVertex> verts, List<Vector2> uvs, List<Vector3> norms, WavefrontObjFile obj, WavefrontObjFile.FaceVertex fv)
    {
        var p = obj.Vertices[fv.Vertex];
        var n = obj.Normals[fv.Normal];
        var t = obj.TextureCoordinates[fv.Texture];
        verts.Add(new WhiteDayPetVertex { Position = new Vector4((float)p.x, (float)p.y, (float)p.z, 1f), BoneWeights = new List<(byte, byte)> { (255, 0) } });
        uvs.Add(new Vector2((float)t.u, (float)t.v));
        norms.Add(new Vector3((float)n.x, (float)n.y, (float)n.z));
    }

    private void SavePet(string path)
    {
        if (_petModel == null) return;
        try
        {
            using var fs = new FileStream(path, FileMode.Create);
            using var bw = new BinaryWriter(fs);

            // TEXT block
            var textMs = new MemoryStream();
            using var textWriter = new BinaryWriter(textMs);
            textWriter.Write(_petModel.Textures.Count);
            foreach (var tn in _petModel.Textures)
            {
                var nameBytes = Encoding.GetEncoding("EUC-KR").GetBytes(tn);
                var padded = new byte[44];
                Array.Copy(nameBytes, padded, Math.Min(nameBytes.Length, 43));
                textWriter.Write(padded);
            }
            WriteBlock(bw, "TEXT", textMs.ToArray());

            // BONE block
            var boneMs = new MemoryStream();
            using var boneWriter = new BinaryWriter(boneMs);
            boneWriter.Write((byte)_petModel.Bones.Count);
            foreach (var bone in _petModel.Bones)
            {
                WriteCString(boneWriter, bone.Name);
                boneWriter.Write(bone.Parent);
                // Store matrix as 12 floats in column-major order (DirectX style)
                // Matrix4x4 is row-major: M11 M12 M13 M14 / M21 M22 M23 M24 / M31 M32 M33 M34 / M41 M42 M43 M44
                // Column-major: [M11 M21 M31 M41] [M12 M22 M32 M42] [M13 M23 M33 M43] [M14 M24 M34 M44]
                // But we only store 12 floats (3x4): columns 0,1,2 and translation column
                float[] m = new float[12];
                m[0] = bone.Matrix.M11; m[1] = bone.Matrix.M21; m[2] = bone.Matrix.M31; m[3] = bone.Matrix.M41;
                m[4] = bone.Matrix.M12; m[5] = bone.Matrix.M22; m[6] = bone.Matrix.M32; m[7] = bone.Matrix.M42;
                m[8] = bone.Matrix.M13; m[9] = bone.Matrix.M23; m[10] = bone.Matrix.M33; m[11] = bone.Matrix.M43;
                foreach (float f in m) boneWriter.Write(f);
            }
            WriteBlock(bw, "BONE", boneMs.ToArray());

            // MESH block
            var meshMs = new MemoryStream();
            using var meshWriter = new BinaryWriter(meshMs);
            meshWriter.Write((byte)_petModel.Meshes.Count);
            meshWriter.Write(0u); // meshid
            meshWriter.Write(0u); meshWriter.Write(0u); meshWriter.Write(0u); meshWriter.Write(0u); // unknown
            foreach (var mesh in _petModel.Meshes)
            {
                meshWriter.Write(mesh.Vertices.Count);
                foreach (var v in mesh.Vertices)
                {
                    meshWriter.Write(v.Position.X); meshWriter.Write(v.Position.Y); meshWriter.Write(v.Position.Z); meshWriter.Write(v.Position.W);
                    // Write bone weights: at least 2 pairs, sum = 0xFF
                    if (v.BoneWeights.Count > 0)
                    {
                        var first = v.BoneWeights[0];
                        meshWriter.Write(first.Weight); meshWriter.Write(first.BoneId);
                    }
                    else
                    {
                        meshWriter.Write((byte)255); meshWriter.Write((byte)0);
                    }
                    // Pad to at least 2 weights
                    while (v.BoneWeights.Count < 2)
                    {
                        meshWriter.Write((byte)0); meshWriter.Write((byte)0);
                    }
                }
                meshWriter.Write(mesh.Polygons.Count);
                foreach (var poly in mesh.Polygons)
                {
                    foreach (var idx in poly.Indices)
                    {
                        meshWriter.Write(idx.Index);
                        meshWriter.Write(idx.Normal.X); meshWriter.Write(idx.Normal.Y); meshWriter.Write(idx.Normal.Z);
                        meshWriter.Write(idx.Uv.X); meshWriter.Write(idx.Uv.Y);
                    }
                }
                foreach (var t in mesh.TexMap) meshWriter.Write(t);
            }
            WriteBlock(bw, "MESH", meshMs.ToArray());

            MessageBox.Show(this, $"PET saved to {path}.", "PET Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"PET save failed:\n{ex.Message}", "Model Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static void WriteBlock(BinaryWriter bw, string id, byte[] data)
    {
        bw.Write(Encoding.ASCII.GetBytes(id));
        bw.Write(data.Length);
        bw.Write(data);
    }

    static void WriteCString(BinaryWriter bw, string s)
    {
        foreach (char c in s) bw.Write((byte)c);
        bw.Write((byte)0);
    }

    private int TexturePages => Math.Max(1, (_texture?.Width ?? 256) / 128);
    private static string NativeExtension(BioVersion version) => version switch { BioVersion.Biohazard1 => "tmd", BioVersion.Biohazard2 => "md1", _ => "md2" };
    private static IModelMesh ReadNativeMesh(BioVersion version, string path)
    {
        byte[] data = File.ReadAllBytes(path);
        return version switch { BioVersion.Biohazard1 => new Tmd(data), BioVersion.Biohazard2 => new Md1(data), _ => new Md2(data) };
    }

    private void LoadDefaultTexture(ModelFile model, string modelPath)
    {
        _texture = null;
        _texturePath = null;
        if (CountChunksOfKind(model, ModelFile.ChunkKind.Texture) > 0)
        {
            _texture = model.GetTim(0);
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

    private void LoadSelectedMesh()
    {
        if (_survivorHmd != null) { LoadSurvivorHmd(); return; }
        if (_survivor != null) { LoadSurvivorMesh(); return; }
        if (_petModel != null) { LoadPetSelectedMesh(); return; }
        if (_re4Bin != null || _re4Pmd != null || _re4GenericParts != null || _re4Scenario != null) { LoadRe4SelectedMesh(); return; }
        if (_model == null || _mesh.SelectedIndex < 0) return;
        try
        {
            IModelMesh source = _model.GetMesh(_mesh.SelectedIndex);
            var converter = new MeshConverter();
            var mesh = (Md1)converter.ConvertMesh(source, IntelOrca.Biohazard.BioVersion.Biohazard2, remap: false);
            Emr? skeleton = _skeleton ?? TryGetSkeleton(_model);
            int keyFrame = 0;
            if (CurrentAnimation is AnimationChoice choice && CurrentAnimationDuration != 0)
                keyFrame = choice.Edd.GetFrameIndex(choice.AnimationIndex, _animationTime);
            Matrix4x4[] partTransforms = BuildPartTransforms(skeleton, mesh.NumParts, keyFrame);
            var parts = new List<RenderPart>();
            int triangles = 0;
            for (int i = 0; i < mesh.NumParts; i++)
            {
                Md1.ObjectDescriptor triObject = mesh.Objects[i * 2];
                Md1.ObjectDescriptor quadObject = mesh.Objects[i * 2 + 1];
                Vector3[] localVertices = mesh.GetPositionData(triObject)
                    .ToArray().Select(v => new Vector3(v.x, v.y, v.z)).ToArray();
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
                var part = new RenderPart(localVertices, faces);
                part.ApplyTransform(partTransforms[i]);
                parts.Add(part);
            }

            int boneCount = Math.Min(parts.Count, skeleton?.NumParts ?? parts.Count);
            _parts.Items.Clear();
            for (int i = 0; i < parts.Count; i++)
            {
                bool bound = i < boneCount;
                parts[i].Visible = bound;
                _parts.Items.Add(bound ? $"Part {i:D2}" : $"Part {i:D2} (unbound)", bound);
            }
            _viewport.SetModel(parts, _texture == null ? null : new TimTexture(_texture));
            _renderParts = parts;
            string texture = _texture == null ? "None" : $"{(_texturePath == "Embedded TIM" ? _texturePath : Path.GetFileName(_texturePath))} ({_texture.Width}x{_texture.Height})";
            _details.Text = $"Game: {VersionName(_model.Version)}\r\nType: {Path.GetExtension(_path!).TrimStart('.').ToUpperInvariant()}\r\nParts: {parts.Count:N0}  Bones: {boneCount:N0}  Triangles: {triangles:N0}\r\nTexture: {texture}\r\nCLUT: per polygon";
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

    private static int CountChunksOfKind(ModelFile model, ModelFile.ChunkKind kind)
    {
        int count = 0;
        for (int i = 0; i < model.NumChunks; i++)
            if (model.GetChunkKind(i) == kind) count++;
        return count;
    }

    private static Emr? TryGetSkeleton(ModelFile model)
    {
        // Avoid exception-driven control flow: under the VS debugger each
        // first-chance throw halts execution and looks like a hang.
        if (CountChunksOfKind(model, ModelFile.ChunkKind.Armature) == 0) return null;
        return model.GetEmr(0);
    }

    private static Matrix4x4[] BuildPartTransforms(Emr? skeleton, int partCount, int keyFrameIndex)
    {
        var result = Enumerable.Repeat(Matrix4x4.Identity, partCount).ToArray();
        if (skeleton == null || skeleton.NumParts == 0) return result;

        Emr.KeyFrame[] keyFrames = skeleton.KeyFrames;
        bool hasPose = keyFrames.Length != 0;
        int poseIndex = Math.Clamp(keyFrameIndex, 0, Math.Max(0, keyFrames.Length - 1));
        var visited = new bool[Math.Min(partCount, skeleton.NumParts)];

        void Visit(int partIndex, Matrix4x4 parent)
        {
            if ((uint)partIndex >= (uint)visited.Length || visited[partIndex]) return;
            visited[partIndex] = true;

            Emr.Vector offset = partIndex == 0 && hasPose
                ? keyFrames[poseIndex].Offset
                : skeleton.GetRelativePosition(partIndex);
            Matrix4x4 local = Matrix4x4.Identity;
            if (hasPose)
            {
                Emr.Vector angle = keyFrames[poseIndex].GetAngle(partIndex);
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

    private interface IViewerTexture
    {
        int Width { get; }
        int Height { get; }
        int PaletteCount { get; }
        uint GetPixel(int x, int y, int palette);
    }

    private sealed class TimTexture(TimFile tim) : IViewerTexture
    {
        public int Width => tim.Width;
        public int Height => tim.Height;
        public int PaletteCount => tim.ClutCount;
        public uint GetPixel(int x, int y, int palette) => tim.GetPixel(x, y, palette);
    }

    // Side-by-side atlas of the .TIM files referenced by an HMD's .RSD
    // manifest. Each TIM occupies a 128-pixel-wide slot so the viewport's
    // Page*128 addressing works unchanged. Slot order follows manifest
    // (TEX) order; PageForOrgX maps a PSX texture page (VRAM X / 64) to the
    // slot whose TIM was uploaded at that VRAM position.
    private sealed class HmdTimAtlas : IViewerTexture
    {
        private readonly TimFile[] _tims;
        private readonly Dictionary<int, int> _orgXToPage = new();
        public int Width => checked(Math.Max(128, 128 * _tims.Length));
        public int Height => _tims.Length == 0 ? 1 : _tims.Max(t => t.Height);
        public int PaletteCount => _tims.Length == 0 ? 1 : _tims.Max(t => Math.Max(1, t.ClutCount));
        public int SlotCount => _tims.Length;
        public string[] Names { get; }

        public HmdTimAtlas(List<(string Name, TimFile Tim, int OrgX)> slots)
        {
            _tims = slots.Select(s => s.Tim).ToArray();
            Names = slots.Select(s => s.Name).ToArray();
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].OrgX >= 0 && !_orgXToPage.ContainsKey(slots[i].OrgX))
                    _orgXToPage[slots[i].OrgX] = i;
        }

        public int PageForOrgX(int orgX) => _orgXToPage.TryGetValue(orgX, out int page) ? page : 0;
        public int PageForTpageX(int tpageX) => PageForOrgX(tpageX * 64);

        public uint GetPixel(int x, int y, int palette)
        {
            int slot = x / 128, localX = x % 128;
            if ((uint)slot >= (uint)_tims.Length) return 0;
            var tim = _tims[slot];
            if (localX >= tim.Width || y >= tim.Height) return 0;
            return tim.GetPixel(localX, y, palette);
        }
    }

    private sealed class SurvivorTexture : IViewerTexture
    {
        private readonly ushort[] _pixels;
        private readonly (int Offset, int Width, int Height)[] _surfaces;
        public int SurfaceCount => _surfaces.Length;
        public int FirstSurfaceOffset => _surfaces[0].Offset;
        public int Width { get; }
        public int Height { get; }
        public int PaletteCount => 1;

        public SurvivorTexture(byte[] data, IReadOnlyList<(int Offset, int Width, int Height)> surfaces)
        {
            _surfaces = surfaces.ToArray();
            Width = surfaces.Sum(x => x.Width);
            Height = surfaces.Max(x => x.Height);
            _pixels = new ushort[Width * Height];
            int destinationX = 0;
            foreach ((int offset, int width, int height) in surfaces)
            {
                if (offset < 0 || offset + width * height * 2 > data.Length)
                    throw new InvalidDataException("The NAB embedded texture is truncated.");
                for (int y = 0; y < height; y++)
                    Buffer.BlockCopy(data, offset + y * width * 2, _pixels, (y * Width + destinationX) * 2, width * 2);
                destinationX += width;
            }
        }

        public uint GetPixel(int x, int y, int palette)
        {
            ushort value = _pixels[y * Width + x];
            int r = ((value >> 10) & 31) * 255 / 31;
            int g = ((value >> 5) & 31) * 255 / 31;
            int b = (value & 31) * 255 / 31;
            return 0xFF000000u | (uint)(r << 16) | (uint)(g << 8) | (uint)b;
        }

        public SurvivorTexture Rebase(byte[] data, int delta) => new SurvivorTexture(data,
            _surfaces.Select(s => (checked(s.Offset + delta), s.Width, s.Height)).ToArray());

        public void Replace(Bitmap bitmap, byte[] data)
        {
            if (bitmap.Width != Width || bitmap.Height != Height)
                throw new InvalidDataException($"The replacement must be exactly {Width}x{Height} pixels (the exported atlas size).");
            int sourceX = 0;
            foreach ((int offset, int width, int height) in _surfaces)
            {
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Color color = bitmap.GetPixel(sourceX + x, y);
                    int pixel = y * Width + sourceX + x;
                    // Preserve the original A1 transparency bit. The viewer displays
                    // RGB only, but the game uses this bit when rendering the texture.
                    ushort value = (ushort)((_pixels[pixel] & 0x8000) | ((color.R * 31 / 255) << 10) | ((color.G * 31 / 255) << 5) | (color.B * 31 / 255));
                    _pixels[pixel] = value;
                    int destination = offset + (y * width + x) * 2;
                    data[destination] = (byte)value;
                    data[destination + 1] = (byte)(value >> 8);
                }
                sourceX += width;
            }
        }
    }

    private sealed class SurvivorNab
    {
        private const int VertexStride = 36;
        private const int SurfaceFormatOffset = 360;
        private const int SurfaceWidthOffset = 364;
        private const int SurfaceHeightOffset = 368;
        private const int SurfaceDataOffset = 372;
        private byte[] _data;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Vector2[] _uv;
        private SurvivorGroup[] _groups;
        private int _originalVertexEnd;
        private int _originalGroupCountOffset;
        private byte[] _suffix;
        private int _metadataVertexCount;
        private int _metadataTriangleCount;
        private SurvivorGroup[] _metadataGroups;
        private readonly int[] _parentIndices;
        private readonly Matrix4x4[] _bindLocalTransforms;
        private SurvivorAnimationBank? _animationBank;
        private byte[]? _replacementAnimationData;
        public int TriangleCount { get; private set; }
        public byte[] Data => _data;
        public SurvivorTexture Texture { get; private set; }
        public string AnimationPath { get; }
        public bool HasAnimationBank => _replacementAnimationData != null || File.Exists(AnimationPath);
        public bool HasAnimations => _animationBank != null && _animationBank.SelectorCount != 0;
        public int SelectorCount => _animationBank?.SelectorCount ?? 0;

        private SurvivorNab(byte[] data, Vector3[] vertices, Vector3[] normals, Vector2[] uv, SurvivorGroup[] groups, int[] parentIndices,
            Matrix4x4[] bindLocalTransforms, int triangleCount, SurvivorTexture texture, string animationPath,
            SurvivorAnimationBank? animationBank, int originalVertexEnd, int originalGroupCountOffset)
        {
            _data = data;
            _vertices = vertices;
            _normals = normals;
            _uv = uv;
            _groups = groups;
            _parentIndices = parentIndices;
            _bindLocalTransforms = bindLocalTransforms;
            _animationBank = animationBank;
            TriangleCount = triangleCount;
            Texture = texture;
            AnimationPath = animationPath;
            _originalVertexEnd = originalVertexEnd;
            _originalGroupCountOffset = originalGroupCountOffset;
            _suffix = data.AsSpan(originalVertexEnd).ToArray();
            _metadataVertexCount = vertices.Length;
            _metadataTriangleCount = triangleCount;
            _metadataGroups = (SurvivorGroup[])groups.Clone();
        }

        public static SurvivorNab FromFile(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 16) throw new InvalidDataException("NAB file is too small.");
            int vertexCount = BitConverter.ToInt32(data, 0);
            int triangleCount = BitConverter.ToInt32(data, 4);
            if (vertexCount <= 0 || triangleCount <= 0 || vertexCount != triangleCount * 3)
                throw new InvalidDataException("This is not a supported Survivor PC NAB triangle stream.");
            int vertexEnd = checked(8 + vertexCount * VertexStride);
            var surfaces = new List<(int Offset, int Width, int Height)>();
            int surfaceOffset = vertexEnd;
            int groupCountOffset;
            while (true)
            {
                int surfaceWidth, surfaceHeight, surfaceData;
                if (surfaces.Count == 0)
                {
                    if (surfaceOffset + SurfaceDataOffset > data.Length)
                        throw new InvalidDataException("NAB surface header is truncated.");
                    if (BitConverter.ToInt32(data, surfaceOffset + SurfaceFormatOffset) != 25)
                        throw new InvalidDataException("NAB vertex or embedded texture data is truncated.");
                    surfaceWidth = BitConverter.ToInt32(data, surfaceOffset + SurfaceWidthOffset);
                    surfaceHeight = BitConverter.ToInt32(data, surfaceOffset + SurfaceHeightOffset);
                    surfaceData = surfaceOffset + SurfaceDataOffset;
                }
                else if (TryReadContinuationSurface(data, surfaceOffset, out surfaceWidth, out surfaceHeight, out surfaceData))
                {
                    // Another embedded surface follows the previous pixel block.
                }
                else
                {
                    // No further surface: the mesh group table must begin here.
                    groupCountOffset = surfaceOffset;
                    break;
                }
                if (surfaceWidth <= 0 || surfaceWidth > 2048 || surfaceHeight <= 0 || surfaceHeight > 2048 ||
                    (long)surfaceData + surfaceWidth * surfaceHeight * 2 > data.Length)
                    throw new InvalidDataException("NAB vertex or embedded texture data is truncated.");
                surfaces.Add((surfaceData, surfaceWidth, surfaceHeight));
                groupCountOffset = checked(surfaceData + surfaceWidth * surfaceHeight * 2);
                if (IsGroupTable(data, groupCountOffset, triangleCount)) break;
                surfaceOffset = groupCountOffset;
            }
            int textureWidth = surfaces[0].Width;
            int textureHeight = surfaces[0].Height;

            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                int o = 8 + i * VertexStride;
                float x = BitConverter.ToSingle(data, o);
                float y = BitConverter.ToSingle(data, o + 4);
                float z = BitConverter.ToSingle(data, o + 8);
                float u = BitConverter.ToSingle(data, o + 28);
                float v = BitConverter.ToSingle(data, o + 32);
                if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z) || !float.IsFinite(u) || !float.IsFinite(v))
                    throw new InvalidDataException($"Invalid NAB vertex {i}.");
                vertices[i] = new Vector3(x, y, z);
                normals[i] = new Vector3(BitConverter.ToSingle(data, o + 12), BitConverter.ToSingle(data, o + 16), BitConverter.ToSingle(data, o + 20));
                uv[i] = new Vector2(u * (textureWidth - 1), v * (textureHeight - 1));
            }

            int groupCount = BitConverter.ToInt32(data, groupCountOffset);
            if (groupCount <= 0 || groupCount > 256)
                throw new InvalidDataException("NAB mesh group table was not found.");
            int groupTable = groupCountOffset + 4;
            if (groupTable + groupCount * 32 > data.Length)
                throw new InvalidDataException("NAB mesh group table is truncated.");

            uint minimumNodePointer = uint.MaxValue;
            var rawGroups = new (int Start, int Count, uint NodePointer, byte TexturePage)[groupCount];
            var surfacePages = new Dictionary<uint, byte>();
            int expectedTriangle = 0;
            int groupRecord = groupTable;
            for (int i = 0; i < groupCount; i++)
            {
                uint surfacePointer = BitConverter.ToUInt32(data, groupRecord);
                int start = BitConverter.ToInt32(data, groupRecord + 8);
                int count = BitConverter.ToInt32(data, groupRecord + 12);
                uint nodePointer = BitConverter.ToUInt32(data, groupRecord + 16);
                byte texturePage = 0;
                if (surfacePointer != 0)
                {
                    if (!surfacePages.TryGetValue(surfacePointer, out texturePage))
                    {
                        texturePage = checked((byte)surfacePages.Count);
                        surfacePages.Add(surfacePointer, texturePage);
                    }
                }
                if (start != expectedTriangle || count <= 0 || start + count > triangleCount)
                    throw new InvalidDataException($"Invalid NAB mesh group {i}.");
                if ((uint)texturePage >= (uint)surfaces.Count)
                    throw new InvalidDataException($"Invalid NAB texture page for mesh group {i}.");
                expectedTriangle += count;
                minimumNodePointer = Math.Min(minimumNodePointer, nodePointer);
                rawGroups[i] = (start, count, nodePointer, texturePage);
                // Textured group records are 32 bytes; untextured ones omit the four texture words.
                groupRecord += surfacePointer != 0 ? 32 : 28;
            }
            if (expectedTriangle != triangleCount)
                throw new InvalidDataException("NAB mesh groups do not cover every triangle.");

            int groupTableEnd = groupRecord;
            uint rootNodePointer = FindRootNodePointer(data, groupTableEnd, minimumNodePointer);
            uint maximumNodePointer = rawGroups.Max(x => x.NodePointer);
            int minimumNodeCount = checked((int)((maximumNodePointer - rootNodePointer) / 0x104) + 1);
            int nodeCount = FindNodeCount(data, rootNodePointer, minimumNodeCount, groupTableEnd);
            rootNodePointer = checked(rootNodePointer - (uint)((nodeCount - minimumNodeCount) * 0x104));
            int rootNodeOffset = checked(data.Length - nodeCount * 0x108);
            if (rootNodeOffset < 0) throw new InvalidDataException("NAB node table is truncated.");
            var nodeTransforms = new Matrix4x4[nodeCount];
            var bindLocalTransforms = new Matrix4x4[nodeCount];
            var parentIndices = new int[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                int nodeOffset = checked(rootNodeOffset + i * 0x108);
                Matrix4x4 rotation = ReadMatrix(data, nodeOffset + 0x40);
                Matrix4x4 translation = ReadMatrix(data, nodeOffset + 0xC0);
                bindLocalTransforms[i] = rotation * translation;
                nodeTransforms[i] = bindLocalTransforms[i];
                uint parentPointer = BitConverter.ToUInt32(data, nodeOffset + 0x100);
                parentIndices[i] = parentPointer >= rootNodePointer && (parentPointer - rootNodePointer) % 0x104 == 0
                    ? checked((int)((parentPointer - rootNodePointer) / 0x104))
                    : -1;
            }
            for (int i = 0; i < nodeCount; i++)
            {
                int parent = parentIndices[i];
                if (parent >= 0 && parent < i)
                    nodeTransforms[i] *= nodeTransforms[parent];
            }
            var groups = new SurvivorGroup[groupCount];
            for (int i = 0; i < groupCount; i++)
            {
                uint delta = rawGroups[i].NodePointer - rootNodePointer;
                if (delta % 0x104 != 0) throw new InvalidDataException($"Invalid NAB node pointer for group {i}.");
                int nodeIndex = checked((int)(delta / 0x104));
                groups[i] = new SurvivorGroup(rawGroups[i].Start, rawGroups[i].Count, rawGroups[i].TexturePage, nodeIndex, nodeTransforms[nodeIndex]);
            }
            string animationPath = Path.ChangeExtension(path, ".NAC");
            SurvivorAnimationBank? animationBank = File.Exists(animationPath)
                ? SurvivorAnimationBank.TryLoad(data, animationPath, rootNodeOffset, nodeCount, rootNodePointer)
                : null;
            return new SurvivorNab(data, vertices, normals, uv, groups, parentIndices, bindLocalTransforms, triangleCount,
                new SurvivorTexture(data, surfaces), animationPath, animationBank, vertexEnd, groupCountOffset);
        }

        // Reads the header of an embedded surface that follows the previous pixel block. Most dumps
        // place [size][format][width][height] straight after the pixels, but some reserve a gap in
        // which the format word sits at +356 (the historical continuation layout).
        private static bool TryReadContinuationSurface(byte[] data, int offset, out int width, out int height, out int dataOffset)
        {
            width = height = dataOffset = 0;
            if (offset <= data.Length - 16 && BitConverter.ToInt32(data, offset + 4) == 25)
            {
                int size = BitConverter.ToInt32(data, offset);
                int immediateWidth = BitConverter.ToInt32(data, offset + 8);
                int immediateHeight = BitConverter.ToInt32(data, offset + 12);
                if (immediateWidth > 0 && immediateWidth <= 2048 && immediateHeight > 0 && immediateHeight <= 2048 &&
                    size == immediateWidth * immediateHeight * 2 && (long)offset + 16 + size <= data.Length)
                {
                    width = immediateWidth;
                    height = immediateHeight;
                    dataOffset = offset + 16;
                    return true;
                }
            }
            const int continuationAdjustment = -4;
            if (offset > data.Length - (SurfaceDataOffset + continuationAdjustment)) return false;
            if (BitConverter.ToInt32(data, offset + SurfaceFormatOffset + continuationAdjustment) != 25) return false;
            width = BitConverter.ToInt32(data, offset + SurfaceWidthOffset + continuationAdjustment);
            height = BitConverter.ToInt32(data, offset + SurfaceHeightOffset + continuationAdjustment);
            dataOffset = offset + SurfaceDataOffset + continuationAdjustment;
            return true;
        }

        private static bool IsGroupTable(byte[] data, int offset, int triangleCount)
        {
            if (offset < 0 || offset + 4 > data.Length) return false;
            int count = BitConverter.ToInt32(data, offset);
            if (count <= 0 || count > 256 || offset + 4 + count * 32 > data.Length) return false;
            int expected = 0;
            int record = offset + 4;
            for (int i = 0; i < count; i++)
            {
                // Textured group records are 32 bytes; untextured ones omit the four texture words.
                uint surface = BitConverter.ToUInt32(data, record);
                int start = BitConverter.ToInt32(data, record + 8);
                int length = BitConverter.ToInt32(data, record + 12);
                if (start != expected || length <= 0 || start + length > triangleCount) return false;
                expected += length;
                record += surface != 0 ? 32 : 28;
            }
            return expected == triangleCount;
        }

        private static uint FindRootNodePointer(byte[] data, int searchStart, uint lowestReferencedPointer)
        {
            // Build the pointer set once instead of rescanning the whole tail
            // for every candidate. The naive rescan is O(candidates * fileSize),
            // which is fine in Release but looks like a hang under the VS
            // debugger (unoptimized BitConverter calls + debugger overhead).
            var present = new HashSet<uint>();
            for (int o = searchStart; o <= data.Length - 4; o += 4)
                present.Add(BitConverter.ToUInt32(data, o));
            uint pointer = lowestReferencedPointer;
            while (pointer >= 0x104)
            {
                uint candidate = pointer - 0x104;
                if (!present.Contains(candidate)) break;
                pointer = candidate;
            }
            return pointer;
        }

        private static int FindNodeCount(byte[] data, uint rootNodePointer, int minimum, int contentEnd)
        {
            for (int count = minimum; count <= 256; count++)
            {
                int rootOffset = data.Length - count * 0x108;
                int qCount = count - 1;
                int qStart = rootOffset - 0x1C - qCount * 0x1B0;
                if (qStart < contentEnd || qStart + 0x60 > data.Length) continue;
                int selectors = BitConverter.ToInt32(data, qStart + 0x0C);
                if (selectors <= 0 || selectors > 256) continue;
                bool valid = true;
                for (int q = 0; q < qCount; q++)
                {
                    int header = qStart + q * 0x1B0;
                    if (BitConverter.ToInt32(data, header) != 4 || BitConverter.ToInt32(data, header + 0x0C) != selectors)
                    {
                        valid = false;
                        break;
                    }
                }
                if (valid) return count;
            }
            return minimum;
        }

        private static Matrix4x4 ReadMatrix(byte[] data, int offset)
        {
            if (offset < 0 || offset + 64 > data.Length) throw new InvalidDataException("NAB node matrix is truncated.");
            float F(int i) => BitConverter.ToSingle(data, offset + i * 4);
            return new Matrix4x4(F(0), F(1), F(2), F(3), F(4), F(5), F(6), F(7),
                F(8), F(9), F(10), F(11), F(12), F(13), F(14), F(15));
        }

        public void Save(string path) => File.WriteAllBytes(path, _data);

        public void SaveAnimationBank(string path)
        {
            byte[] bytes = _replacementAnimationData ?? File.ReadAllBytes(AnimationPath);
            File.WriteAllBytes(path, bytes);
        }

        public void ValidateAnimationBankCompatibility(SurvivorNab source)
        {
            if (!HasAnimations || !source.HasAnimations || source.NodeCount != NodeCount || source.SelectorCount != SelectorCount ||
                !_parentIndices.SequenceEqual(source._parentIndices))
                throw new InvalidDataException("The source and target must have readable animation banks with matching node hierarchies and selector counts.");
            int qCount = NodeCount - 1;
            int targetQ = _data.Length - NodeCount * 0x108 - 0x1C - qCount * 0x1B0;
            int sourceQ = source._data.Length - source.NodeCount * 0x108 - 0x1C - qCount * 0x1B0;
            if (targetQ < 0 || sourceQ < 0) throw new InvalidDataException("The NAB selector table is truncated.");
            for (int node = 0; node < qCount; node++)
                for (int selector = 0; selector < SelectorCount; selector++)
                {
                    int offset = node * 0x1B0 + 0x60 + selector * 8 + 4;
                    if (0x60 + selector * 8 + 8 > 0x1B0 || BitConverter.ToInt32(_data, targetQ + offset) != BitConverter.ToInt32(source._data, sourceQ + offset))
                        throw new InvalidDataException($"The NAC command mapping differs for node {node + 1}, selector {selector}; this bank requires retargeting.");
                }
        }

        public void ReplaceAnimationBank(SurvivorNab source)
        {
            ValidateAnimationBankCompatibility(source);
            byte[] data = source._replacementAnimationData ?? File.ReadAllBytes(source.AnimationPath);
            _replacementAnimationData = (byte[])data.Clone();
            _animationBank = source._animationBank;
        }

        public void CommitTextureEdits()
        {
            int vertexEnd = checked(8 + _vertices.Length * VertexStride);
            _suffix = _data.AsSpan(vertexEnd).ToArray();
        }

        public void ExportObj(string path, int selectedGroup)
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine("# Survivor mesh: local bone-space coordinates, one object per group");
            int index = 1;
            IEnumerable<int> groups = selectedGroup >= 0 ? new[] { selectedGroup } : Enumerable.Range(0, _groups.Length);
            foreach (int groupIndex in groups)
            {
                SurvivorGroup group = _groups[groupIndex];
                writer.WriteLine($"o part_{groupIndex:D2}");
                int first = group.TriangleStart * 3, count = group.TriangleCount * 3;
                for (int i = first; i < first + count; i++)
                {
                    Vector3 p = _vertices[i];
                    writer.WriteLine(FormattableString.Invariant($"v {p.X / 1000f:R} {p.Y / 1000f:R} {p.Z / 1000f:R}"));
                }
                for (int i = first; i < first + count; i++)
                {
                    Vector3 n = _normals[i];
                    writer.WriteLine(FormattableString.Invariant($"vn {n.X:R} {n.Y:R} {n.Z:R}"));
                }
                for (int i = first; i < first + count; i++)
                {
                    Vector2 uv = _uv[i];
                    float u = (uv.X + group.TexturePage * 128f) / Texture.Width;
                    float v = 1f - uv.Y / Texture.Height;
                    writer.WriteLine(FormattableString.Invariant($"vt {u:R} {v:R}"));
                }
                for (int i = 0; i < count; i += 3)
                    writer.WriteLine($"f {index + i + 2}/{index + i + 2}/{index + i + 2} {index + i + 1}/{index + i + 1}/{index + i + 1} {index + i}/{index + i}/{index + i}");
                index += count;
            }
        }

        public Md2 ToMd2(int selectedGroup = -1)
        {
            var builder = new Md2.Builder();
            IEnumerable<int> indices = selectedGroup >= 0 ? new[] { selectedGroup } : Enumerable.Range(0, _groups.Length);
            foreach (int groupIndex in indices)
            {
                SurvivorGroup group = _groups[groupIndex];
                var part = new Md2.Builder.Part();
                int first = group.TriangleStart * 3;
                for (int triangle = 0; triangle < group.TriangleCount; triangle++)
                {
                    int source = first + triangle * 3;
                    if (part.Positions.Count > 252) throw new InvalidDataException($"Survivor part {groupIndex} exceeds the MD2 8-bit vertex limit.");
                    byte baseIndex = (byte)part.Positions.Count;
                    for (int corner = 0; corner < 3; corner++)
                    {
                        Vector3 p = _vertices[source + corner];
                        Vector3 n = _normals[source + corner];
                        part.Positions.Add(new Md2.Vector(ToShort(p.X), ToShort(p.Y), ToShort(p.Z)));
                        Vector3 unitNormal = n.LengthSquared() == 0 ? Vector3.UnitY : Vector3.Normalize(n);
                        part.Normals.Add(new Md2.Vector(ToShort(unitNormal.X * 4096), ToShort(unitNormal.Y * 4096), ToShort(unitNormal.Z * 4096)));
                    }
                    Vector2 a = _uv[source], b = _uv[source + 1], c = _uv[source + 2];
                    part.Triangles.Add(new Md2.Triangle
                    {
                        v0 = baseIndex, v1 = (byte)(baseIndex + 1), v2 = (byte)(baseIndex + 2),
                        tu0 = ToByte(a.X), tv0 = ToByte(a.Y), tu1 = ToByte(b.X), tv1 = ToByte(b.Y),
                        tu2 = ToByte(c.X), tv2 = ToByte(c.Y), page = group.TexturePage, visible = 120
                    });
                }
                builder.Parts.Add(part);
            }
            return builder.ToMesh();
        }

        public void ReplaceWith(SurvivorNab incoming, int selectedGroup)
        {
            if (selectedGroup < 0)
            {
                ApplyParts(incoming.GetParts().ToArray());
                return;
            }
            SurvivorPart[] parts = GetParts().ToArray();
            SurvivorPart replacement = incoming.GetParts().FirstOrDefault()
                ?? throw new InvalidDataException("The incoming NAB contains no mesh groups.");
            parts[selectedGroup] = replacement with { NodeIndex = parts[selectedGroup].NodeIndex, TexturePage = parts[selectedGroup].TexturePage };
            ApplyParts(parts);
        }

        public void ReplaceFromObj(string path, int selectedGroup)
        {
            var obj = new WavefrontObjFile(path);
            SurvivorPart[] target = GetParts().ToArray();
            if (selectedGroup >= 0 && obj.Objects.Count != 1)
                throw new InvalidDataException("A selected-part replacement must contain exactly one OBJ object.");
            if (selectedGroup < 0 && obj.Objects.Count != target.Length)
                throw new InvalidDataException($"Expected {target.Length} OBJ objects, one per Survivor mesh part; found {obj.Objects.Count}.");
            for (int objectIndex = 0; objectIndex < obj.Objects.Count; objectIndex++)
            {
                int targetIndex = selectedGroup >= 0 ? selectedGroup : objectIndex;
                SurvivorPart original = target[targetIndex];
                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var uv = new List<Vector2>();
                void Add(WavefrontObjFile.FaceVertex face)
                {
                    if ((uint)face.Vertex >= (uint)obj.Vertices.Count || (uint)face.Texture >= (uint)obj.TextureCoordinates.Count ||
                        (uint)face.Normal >= (uint)obj.Normals.Count)
                        throw new InvalidDataException($"OBJ part {objectIndex} has an invalid vertex, UV, or normal index.");
                    var p = obj.Vertices[face.Vertex];
                    var n = obj.Normals[face.Normal];
                    var t = obj.TextureCoordinates[face.Texture];
                    var position = new Vector3((float)p.x * 1000, (float)p.y * 1000, (float)p.z * 1000);
                    var normal = new Vector3((float)n.x, (float)n.y, (float)n.z);
                    var texture = new Vector2((float)t.u * Texture.Width - original.TexturePage * 128f, (float)(1 - t.v) * Texture.Height);
                    if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z) ||
                        !float.IsFinite(normal.X) || !float.IsFinite(normal.Y) || !float.IsFinite(normal.Z) ||
                        !float.IsFinite(texture.X) || !float.IsFinite(texture.Y))
                        throw new InvalidDataException($"OBJ part {objectIndex} contains non-finite data.");
                    vertices.Add(position); normals.Add(normal); uv.Add(texture);
                }
                foreach (var triangle in obj.Objects[objectIndex].Triangles)
                {
                    // OBJ export reverses the Survivor winding.
                    Add(triangle.c); Add(triangle.b); Add(triangle.a);
                }
                foreach (var quad in obj.Objects[objectIndex].Quads)
                {
                    Add(quad.c); Add(quad.b); Add(quad.a);
                    Add(quad.d); Add(quad.c); Add(quad.a);
                }
                target[targetIndex] = new SurvivorPart(vertices.ToArray(), normals.ToArray(), uv.ToArray(), original.TexturePage, original.NodeIndex);
            }
            ApplyParts(target);
        }

        public void ReplaceFromClassic(IModelMesh incoming, Emr? skeleton, int selectedGroup)
        {
            var converter = new MeshConverter();
            Md2 md2 = (Md2)converter.ConvertMesh(incoming, BioVersion.Biohazard3, remap: false);
            Matrix4x4[] sourceTransforms = BuildPartTransforms(skeleton, md2.NumParts, 0);
            SurvivorPart[] source = ReadMd2Parts(md2, sourceTransforms);
            // Classic enemy meshes can append detached/damaged variants after the
            // animated skeleton parts. They are not candidate limbs for retargeting.
            if (skeleton != null)
                source = source.Take(Math.Min(skeleton.NumParts, source.Length)).ToArray();
            source = source.Where(part => part.Vertices.Length != 0).ToArray();
            if (source.Length == 0) throw new InvalidDataException("The incoming mesh has no parts.");
            SurvivorPart[] target = GetParts().ToArray();
            if (selectedGroup >= 0)
            {
                target[selectedGroup] = FitPartToTarget(source[0], target[selectedGroup]);
                ApplyParts(target);
                return;
            }

            ApplyMappedClassicParts(source, target);
        }

        private SurvivorPart[] MapClassicParts(SurvivorPart[] source, SurvivorPart[] target)
        {
            // Some NABs contain placeholder groups consisting solely of degenerate
            // triangles. Keep those groups intact rather than assigning a body part.
            int[] activeIndices = Enumerable.Range(0, target.Length).Where(i => HasVisibleTriangles(target[i])).ToArray();
            SurvivorPart[] active = activeIndices.Select(i => target[i]).ToArray();
            if (active.Length == 0) throw new InvalidDataException("The Survivor target contains no non-degenerate mesh parts.");
            Vector3[] targetGlobalVertices = active.SelectMany(x =>
            {
                Matrix4x4 global = GetBindGlobal(x.NodeIndex);
                return x.Vertices.Select(v => Vector3.Transform(v, global));
            }).ToArray();

            // Classic and Survivor models do not share an axis convention or a mesh
            // part order.  Try every orthogonal axis/sign mapping, and for each one
            // solve a unique minimum-cost body-part assignment.  This preserves a
            // stable anatomical mapping instead of stacking unrelated numbered parts.
            (int[] map, int permutation, int signs) = FindPartMapping(source, active);
            Vector3[] orientedVertices = source.SelectMany(p => p.Vertices.Select(v => Orient(v, permutation, signs))).ToArray();
            Vector3 sourceCenter = Average(orientedVertices);
            Vector3 targetCenter = Average(targetGlobalVertices);
            float scale = GetHeight(targetGlobalVertices) / GetHeight(orientedVertices);
            for (int t = 0; t < active.Length; t++)
                target[activeIndices[t]] = FitPartToTarget(source[map[t]], active[t], sourceCenter, targetCenter, scale, permutation, signs);
            return target;
        }

        private static bool HasVisibleTriangles(SurvivorPart part)
        {
            for (int i = 0; i + 2 < part.Vertices.Length; i += 3)
                if (Vector3.Cross(part.Vertices[i + 1] - part.Vertices[i], part.Vertices[i + 2] - part.Vertices[i]).LengthSquared() > 0.000001f)
                    return true;
            return false;
        }

        private void ApplyMappedClassicParts(SurvivorPart[] source, SurvivorPart[] target)
        {
            ApplyParts(MapClassicParts(source, target));
        }

        public void ReplaceFromRe2WithTexture(Md1 mesh, Emr? skeleton, TimFile tim)
        {
            if (Texture.SurfaceCount != 1 || _groups.Any(g => g.TexturePage != 0))
                throw new InvalidDataException("Automatic RE2 texture conversion currently requires a single-surface Survivor model.");
            int partCount = Math.Min(mesh.NumParts, skeleton?.NumParts ?? mesh.NumParts);
            Matrix4x4[] transforms = BuildPartTransforms(skeleton, mesh.NumParts, 0);
            var tiles = new List<(int Page, int Clut)>();
            var parts = new List<SurvivorPart>();
            for (int partIndex = 0; partIndex < partCount; partIndex++)
            {
                Md1.ObjectDescriptor triObj = mesh.Objects[partIndex * 2];
                Md1.ObjectDescriptor quadObj = mesh.Objects[partIndex * 2 + 1];
                Md1.Vector[] positions = mesh.GetPositionData(triObj).ToArray();
                Md1.Vector[] normals = mesh.GetNormalData(triObj).ToArray();
                var vertices = new List<Vector3>(); var outNormals = new List<Vector3>(); var uv = new List<Vector2>();
                int Tile(ushort page, ushort clutId)
                {
                    int p = page & 15;
                    int c = ClutFromId(clutId, page);
                    if ((uint)c >= (uint)tim.ClutCount) throw new InvalidDataException($"RE2 texture references missing CLUT {c}.");
                    var key = (p, c);
                    int slot = tiles.IndexOf(key);
                    if (slot < 0) { slot = tiles.Count; tiles.Add(key); }
                    return slot;
                }
                void Add(ushort vertex, ushort normal, byte u, byte v, int tile)
                {
                    if (vertex >= positions.Length || normal >= normals.Length) throw new InvalidDataException("RE2 mesh has an invalid vertex or normal index.");
                    var p = positions[vertex]; var n = normals[normal];
                    vertices.Add(Vector3.Transform(new Vector3(p.x, p.y, p.z), transforms[partIndex]));
                    Vector3 normalValue = Vector3.TransformNormal(new Vector3(n.x, n.y, n.z), transforms[partIndex]);
                    outNormals.Add(normalValue.LengthSquared() == 0 ? Vector3.UnitY : Vector3.Normalize(normalValue));
                    uv.Add(new Vector2(tile * 256 + u, v));
                }
                Md1.Triangle[] tris = mesh.GetTriangles(triObj).ToArray();
                Md1.TriangleTexture[] triUv = mesh.GetTriangleTextures(triObj).ToArray();
                for (int i = 0; i < tris.Length; i++)
                {
                    var t = tris[i]; var tex = triUv[i]; int tile = Tile(tex.page, tex.clutId);
                    Add(t.v0, t.n0, tex.u0, tex.v0, tile); Add(t.v1, t.n1, tex.u1, tex.v1, tile); Add(t.v2, t.n2, tex.u2, tex.v2, tile);
                }
                Md1.Quad[] quads = mesh.GetQuads(quadObj).ToArray();
                Md1.QuadTexture[] quadUv = mesh.GetQuadTextures(quadObj).ToArray();
                for (int i = 0; i < quads.Length; i++)
                {
                    var q = quads[i]; var tex = quadUv[i]; int tile = Tile(tex.page, tex.clutId);
                    Add(q.v0, q.n0, tex.u0, tex.v0, tile); Add(q.v1, q.n1, tex.u1, tex.v1, tile); Add(q.v2, q.n2, tex.u2, tex.v2, tile);
                    Add(q.v3, q.n3, tex.u3, tex.v3, tile); Add(q.v2, q.n2, tex.u2, tex.v2, tile); Add(q.v1, q.n1, tex.u1, tex.v1, tile);
                }
                if (vertices.Count != 0) parts.Add(new SurvivorPart(vertices.ToArray(), outNormals.ToArray(), uv.ToArray(), 0, 0));
            }
            if (tiles.Count == 0 || tiles.Count > 8 || tim.Height != 256)
                throw new InvalidDataException("The source TIM needs 1–8 texture-page/CLUT combinations and a height of 256 pixels.");
            SurvivorPart[] targetParts = GetParts().ToArray();
            // The common 15-bone RE2 humanoid has separate feet (4 and 7),
            // whereas this 13-visible-group Survivor rig includes each foot in
            // its lower-leg group. Check the hierarchy before applying this map.
            if (skeleton != null && skeleton.NumParts == 15 && parts.Count == 15 &&
                targetParts.Count(HasVisibleTriangles) == 13 &&
                skeleton.GetArmatureParts(1).Contains((byte)2) && skeleton.GetArmatureParts(1).Contains((byte)5) &&
                skeleton.GetArmatureParts(2).SequenceEqual(new byte[] { 3 }) && skeleton.GetArmatureParts(3).SequenceEqual(new byte[] { 4 }) &&
                skeleton.GetArmatureParts(5).SequenceEqual(new byte[] { 6 }) && skeleton.GetArmatureParts(6).SequenceEqual(new byte[] { 7 }) &&
                skeleton.GetArmatureParts(4).Length == 0 && skeleton.GetArmatureParts(7).Length == 0)
            {
                parts[3] = MergeParts(parts[3], parts[4]);
                parts[6] = MergeParts(parts[6], parts[7]);
                parts.RemoveAt(7);
                parts.RemoveAt(4);
            }
            SurvivorPart[] mapped = MapClassicParts(parts.ToArray(), targetParts);
            int width = tiles.Count * 256;
            byte[] image = new byte[width * 256 * 2];
            for (int tile = 0; tile < tiles.Count; tile++)
                for (int y = 0; y < 256; y++)
                    for (int x = 0; x < 256; x++)
                    {
                        int sourceX = (tiles[tile].Page * 128 + x) % tim.Width;
                        uint pixel = tim.GetPixel(sourceX, y, tiles[tile].Clut);
                        int r = (int)((pixel >> 16) & 255), g = (int)((pixel >> 8) & 255), b = (int)(pixel & 255);
                        ushort value = (ushort)((((pixel >> 24) != 0) ? 0x8000 : 0) | ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3));
                        int offset = (y * width + tile * 256 + x) * 2;
                        image[offset] = (byte)value; image[offset + 1] = (byte)(value >> 8);
                    }
            ReplaceSingleSurface(image, width, 256);
            ApplyParts(mapped);
        }

        private static SurvivorPart MergeParts(SurvivorPart first, SurvivorPart second) => new SurvivorPart(
            first.Vertices.Concat(second.Vertices).ToArray(), first.Normals.Concat(second.Normals).ToArray(),
            first.UV.Concat(second.UV).ToArray(), first.TexturePage, first.NodeIndex);

        private void ReplaceSingleSurface(byte[] image, int width, int height)
        {
            int start = Texture.FirstSurfaceOffset;
            int oldSize = Texture.Width * Texture.Height * 2;
            int vertexEnd = 8 + _vertices.Length * VertexStride;
            int delta = image.Length - oldSize;
            var data = new byte[checked(_data.Length + delta)];
            Buffer.BlockCopy(_data, 0, data, 0, start);
            Buffer.BlockCopy(image, 0, data, start, image.Length);
            Buffer.BlockCopy(_data, start + oldSize, data, start + image.Length, _data.Length - start - oldSize);
            BitConverter.GetBytes(image.Length).CopyTo(data, start - 16);
            BitConverter.GetBytes(width).CopyTo(data, start - 8);
            BitConverter.GetBytes(height).CopyTo(data, start - 4);
            _data = data;
            _originalGroupCountOffset += delta;
            _suffix = data.AsSpan(vertexEnd).ToArray();
            Texture = new SurvivorTexture(data, new[] { (start, width, height) });
        }

        private SurvivorPart FitPartToTarget(SurvivorPart source, SurvivorPart target)
        {
            Vector3 sourceCenter = PartCenter(source);
            Vector3 targetCenter = PartCenterGlobal(target);
            float sourceRadius = Math.Max(1, source.Vertices.Max(v => Vector3.Distance(v, sourceCenter)));
            Matrix4x4 targetGlobal = GetBindGlobal(target.NodeIndex);
            Matrix4x4.Invert(targetGlobal, out Matrix4x4 inverseTarget);
            Vector3[] currentGlobal = target.Vertices.Select(v => Vector3.Transform(v, targetGlobal)).ToArray();
            float targetRadius = Math.Max(1, currentGlobal.Max(v => Vector3.Distance(v, targetCenter)));
            float scale = targetRadius / sourceRadius;
            Vector3[] vertices = source.Vertices.Select(v => Vector3.Transform(((v - sourceCenter) * scale) + targetCenter, inverseTarget)).ToArray();
            Vector3[] normals = source.Normals.Select(n => Vector3.Normalize(Vector3.TransformNormal(n, inverseTarget))).ToArray();
            return new SurvivorPart(vertices, normals, source.UV, target.TexturePage, target.NodeIndex);
        }

        private SurvivorPart FitPartToTarget(SurvivorPart source, SurvivorPart target,
            Vector3 sourceCenter, Vector3 targetCenter, float scale, int permutation, int signs)
        {
            Matrix4x4.Invert(GetBindGlobal(target.NodeIndex), out Matrix4x4 inverseTarget);
            Vector3[] vertices = source.Vertices
                .Select(v => Vector3.Transform(((Orient(v, permutation, signs) - sourceCenter) * scale) + targetCenter, inverseTarget))
                .ToArray();
            Vector3[] normals = source.Normals.Select(n =>
            {
                Vector3 value = Vector3.TransformNormal(Orient(n, permutation, signs), inverseTarget);
                return value.LengthSquared() == 0 ? Vector3.UnitY : Vector3.Normalize(value);
            }).ToArray();
            return new SurvivorPart(vertices, normals, source.UV, target.TexturePage, target.NodeIndex);
        }

        private (int[] Map, int Permutation, int Signs) FindPartMapping(SurvivorPart[] source, SurvivorPart[] target)
        {
            Vector3[] targetCenters = target.Select(PartCenterGlobal).ToArray();
            NormalizeCenters(targetCenters);
            float bestCost = float.PositiveInfinity;
            int[] bestMap = new int[target.Length];
            int bestPermutation = 0, bestSigns = 0;
            for (int permutation = 0; permutation < 6; permutation++)
            for (int signs = 0; signs < 8; signs++)
            {
                Vector3[] sourceCenters = source.Select(p => Orient(PartCenter(p), permutation, signs)).ToArray();
                NormalizeCenters(sourceCenters);
                (float cost, int[] map) = AssignParts(sourceCenters, targetCenters);
                if (cost >= bestCost) continue;
                bestCost = cost; bestMap = map; bestPermutation = permutation; bestSigns = signs;
            }
            return (bestMap, bestPermutation, bestSigns);
        }

        private static (float Cost, int[] Map) AssignParts(Vector3[] source, Vector3[] target)
        {
            if (source.Length < target.Length)
                throw new InvalidDataException($"The source has {source.Length} mesh parts, but Survivor requires {target.Length}.");
            if (source.Length > 20)
                throw new InvalidDataException("Automatic part matching supports at most 20 source parts; this model needs manual part mapping.");
            int limit = 1 << source.Length;
            var costs = Enumerable.Repeat(float.PositiveInfinity, limit).ToArray();
            var previous = new int[limit];
            var chosen = new int[limit];
            costs[0] = 0;
            for (int mask = 0; mask < limit; mask++)
            {
                int t = System.Numerics.BitOperations.PopCount((uint)mask);
                if (t >= target.Length || !float.IsFinite(costs[mask])) continue;
                for (int s = 0; s < source.Length; s++)
                {
                    int bit = 1 << s;
                    if ((mask & bit) != 0) continue;
                    int next = mask | bit;
                    float cost = costs[mask] + Vector3.DistanceSquared(source[s], target[t]);
                    if (cost >= costs[next]) continue;
                    costs[next] = cost; previous[next] = mask; chosen[next] = s;
                }
            }
            int finalMask = -1;
            float best = float.PositiveInfinity;
            for (int mask = 0; mask < limit; mask++)
                if (System.Numerics.BitOperations.PopCount((uint)mask) == target.Length && costs[mask] < best)
                { best = costs[mask]; finalMask = mask; }
            if (finalMask < 0) throw new InvalidDataException("Could not map the source body parts to the Survivor skeleton.");
            var map = new int[target.Length];
            for (int t = target.Length - 1; t >= 0; t--)
            { map[t] = chosen[finalMask]; finalMask = previous[finalMask]; }
            return (best, map);
        }

        private static Vector3 Orient(Vector3 value, int permutation, int signs)
        {
            float[] v = { value.X, value.Y, value.Z };
            int[,] p = { { 0, 1, 2 }, { 0, 2, 1 }, { 1, 0, 2 }, { 1, 2, 0 }, { 2, 0, 1 }, { 2, 1, 0 } };
            return new Vector3(v[p[permutation, 0]] * ((signs & 1) == 0 ? 1 : -1),
                v[p[permutation, 1]] * ((signs & 2) == 0 ? 1 : -1),
                v[p[permutation, 2]] * ((signs & 4) == 0 ? 1 : -1));
        }

        private static float GetHeight(IEnumerable<Vector3> vertices)
        {
            bool any = false;
            float min = float.MaxValue, max = float.MinValue;
            foreach (Vector3 vertex in vertices)
            {
                any = true;
                min = Math.Min(min, vertex.Y);
                max = Math.Max(max, vertex.Y);
            }
            return any ? Math.Max(1, max - min) : 1;
        }

        private Matrix4x4 GetBindGlobal(int node)
        {
            Matrix4x4 result = _bindLocalTransforms[node];
            int parent = _parentIndices[node];
            while (parent >= 0)
            {
                result *= _bindLocalTransforms[parent];
                parent = _parentIndices[parent];
            }
            return result;
        }

        private Vector3 PartCenterGlobal(SurvivorPart part)
        {
            Matrix4x4 global = GetBindGlobal(part.NodeIndex);
            return Average(part.Vertices.Select(v => Vector3.Transform(v, global)));
        }

        private static Vector3 PartCenter(SurvivorPart part) => Average(part.Vertices);
        private static Vector3 Average(IEnumerable<Vector3> values)
        {
            Vector3 total = Vector3.Zero; int count = 0;
            foreach (Vector3 value in values) { total += value; count++; }
            return count == 0 ? Vector3.Zero : total / count;
        }

        private static void NormalizeCenters(Vector3[] centers)
        {
            if (centers.Length == 0) return;
            Vector3 min = centers.Aggregate(Vector3.Min), max = centers.Aggregate(Vector3.Max);
            Vector3 center = (min + max) / 2;
            Vector3 size = max - min;
            for (int i = 0; i < centers.Length; i++)
                centers[i] = new Vector3((centers[i].X - center.X) / Math.Max(1, size.X), (centers[i].Y - center.Y) / Math.Max(1, size.Y), (centers[i].Z - center.Z) / Math.Max(1, size.Z));
        }

        private SurvivorPart[] ReadMd2Parts(Md2 mesh, Matrix4x4[] transforms)
        {
            var result = new SurvivorPart[mesh.NumParts];
            for (int partIndex = 0; partIndex < mesh.NumParts; partIndex++)
            {
                Md2.ObjectDescriptor obj = mesh.Objects[partIndex];
                Md2.Vector[] positions = mesh.GetPositionData(obj).ToArray();
                Md2.Vector[] normals = mesh.GetNormalData(obj).ToArray();
                var vertices = new List<Vector3>(); var outNormals = new List<Vector3>(); var uv = new List<Vector2>();
                void Add(byte index, byte u, byte v)
                {
                    Md2.Vector p = positions[index]; Md2.Vector n = normals[index];
                    vertices.Add(Vector3.Transform(new Vector3(p.x, p.y, p.z), transforms[Math.Min(partIndex, transforms.Length - 1)]));
                    Vector3 normal = Vector3.TransformNormal(new Vector3(n.x, n.y, n.z), transforms[Math.Min(partIndex, transforms.Length - 1)]);
                    outNormals.Add(normal.LengthSquared() == 0 ? Vector3.UnitY : Vector3.Normalize(normal));
                    uv.Add(new Vector2(u, v));
                }
                foreach (Md2.Triangle triangle in mesh.GetTriangles(obj))
                {
                    Add(triangle.v0, triangle.tu0, triangle.tv0); Add(triangle.v1, triangle.tu1, triangle.tv1); Add(triangle.v2, triangle.tu2, triangle.tv2);
                }
                foreach (Md2.Quad quad in mesh.GetQuads(obj))
                {
                    Add(quad.v0, quad.tu0, quad.tv0); Add(quad.v1, quad.tu1, quad.tv1); Add(quad.v2, quad.tu2, quad.tv2);
                    Add(quad.v3, quad.tu3, quad.tv3); Add(quad.v2, quad.tu2, quad.tv2); Add(quad.v1, quad.tu1, quad.tv1);
                }
                result[partIndex] = new SurvivorPart(vertices.ToArray(), outNormals.ToArray(), uv.ToArray(), 0, 0);
            }
            return result;
        }

        private IEnumerable<SurvivorPart> GetParts()
        {
            foreach (SurvivorGroup group in _groups)
            {
                int first = group.TriangleStart * 3, count = group.TriangleCount * 3;
                yield return new SurvivorPart(_vertices.AsSpan(first, count).ToArray(), _normals.AsSpan(first, count).ToArray(),
                    _uv.AsSpan(first, count).ToArray(), group.TexturePage, group.NodeIndex);
            }
        }

        private void ApplyParts(SurvivorPart[] parts)
        {
            if (parts.Length != _groups.Length) throw new InvalidDataException($"Expected {_groups.Length} Survivor mesh parts, found {parts.Length}.");
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>();
            var groups = new SurvivorGroup[parts.Length];
            int triangleStart = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                SurvivorPart part = parts[i];
                if (part.Vertices.Length == 0 || part.Vertices.Length % 3 != 0 || part.Normals.Length != part.Vertices.Length || part.UV.Length != part.Vertices.Length)
                    throw new InvalidDataException($"Replacement part {i} is not a valid triangle stream.");
                int triangles = part.Vertices.Length / 3;
                groups[i] = new SurvivorGroup(triangleStart, triangles, part.TexturePage, part.NodeIndex, GetBindGlobal(part.NodeIndex));
                triangleStart += triangles;
                vertices.AddRange(part.Vertices);
                normals.AddRange(part.Normals.Select(n => (n.LengthSquared() == 0 ? Vector3.UnitY : Vector3.Normalize(n)) * 4096f));
                uv.AddRange(part.UV);
            }
            _vertices = vertices.ToArray(); _normals = normals.ToArray(); _uv = uv.ToArray(); _groups = groups;
            TriangleCount = triangleStart;
            _data = SerializeMesh();
        }

        private byte[] SerializeMesh()
        {
            int groupOffset = checked(_originalGroupCountOffset - _originalVertexEnd + 4);
            int cursor = groupOffset;
            var groupOffsets = new int[_groups.Length];
            for (int i = 0; i < _groups.Length; i++)
            {
                if (cursor < 0 || cursor + 28 > _suffix.Length) throw new InvalidDataException("NAB mesh group data is truncated.");
                groupOffsets[i] = cursor;
                bool textured = BitConverter.ToUInt32(_suffix, cursor) != 0;
                cursor = checked(cursor + (textured ? 32 : 28));
            }
            int oldVertexAux = cursor;
            int oldTriangleAux = checked(oldVertexAux + _metadataVertexCount * 60);
            int oldTail = checked(oldTriangleAux + _metadataTriangleCount * 20);
            if (oldTail > _suffix.Length) throw new InvalidDataException("NAB auxiliary mesh records are truncated.");
            int newTriangleAux = checked(oldVertexAux + _vertices.Length * 60);
            int newTail = checked(newTriangleAux + TriangleCount * 20);
            byte[] suffix = new byte[checked(newTail + _suffix.Length - oldTail)];
            Buffer.BlockCopy(_suffix, 0, suffix, 0, oldVertexAux);
            Buffer.BlockCopy(_suffix, oldTail, suffix, newTail, _suffix.Length - oldTail);
            bool countsChanged = _metadataVertexCount != _vertices.Length || _metadataGroups.Where((g, i) =>
                g.TriangleStart != _groups[i].TriangleStart || g.TriangleCount != _groups[i].TriangleCount).Any();
            for (int group = 0; group < _groups.Length; group++)
            {
                SurvivorGroup previous = _metadataGroups[group], current = _groups[group];
                bool reindex = countsChanged && HasVisibleTrianglesInGroup(group);
                BitConverter.GetBytes(current.TriangleStart).CopyTo(suffix, groupOffsets[group] + 8);
                BitConverter.GetBytes(current.TriangleCount).CopyTo(suffix, groupOffsets[group] + 12);
                for (int triangle = 0; triangle < current.TriangleCount; triangle++)
                {
                    int sourceTriangle = previous.TriangleStart + Math.Min(triangle, previous.TriangleCount - 1);
                    int destinationTriangle = current.TriangleStart + triangle;
                    Buffer.BlockCopy(_suffix, oldTriangleAux + sourceTriangle * 20, suffix, newTriangleAux + destinationTriangle * 20, 20);
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int source = sourceTriangle * 3 + corner, destination = destinationTriangle * 3 + corner;
                        int aux = oldVertexAux + destination * 60;
                        Buffer.BlockCopy(_suffix, oldVertexAux + source * 60, suffix, aux, 60);
                        Vector3 p = _vertices[destination], n = _normals[destination];
                        WriteFloat(suffix, aux + 4, p.X); WriteFloat(suffix, aux + 8, p.Y); WriteFloat(suffix, aux + 12, p.Z);
                        WriteFloat(suffix, aux + 16, p.X); WriteFloat(suffix, aux + 20, p.Y); WriteFloat(suffix, aux + 24, p.Z);
                        WriteFloat(suffix, aux + 28, n.X); WriteFloat(suffix, aux + 32, n.Y); WriteFloat(suffix, aux + 36, n.Z);
                        WriteFloat(suffix, aux + 40, n.X); WriteFloat(suffix, aux + 44, n.Y); WriteFloat(suffix, aux + 48, n.Z);
                        if (reindex)
                        {
                            BitConverter.GetBytes(destination).CopyTo(suffix, aux + 52);
                            BitConverter.GetBytes(destination).CopyTo(suffix, aux + 56);
                        }
                    }
                }
            }
            int vertexEnd = checked(8 + _vertices.Length * VertexStride);
            byte[] result = new byte[checked(vertexEnd + suffix.Length)];
            BitConverter.GetBytes(_vertices.Length).CopyTo(result, 0);
            BitConverter.GetBytes(TriangleCount).CopyTo(result, 4);
            for (int i = 0; i < _vertices.Length; i++)
            {
                int o = 8 + i * VertexStride; Vector3 p = _vertices[i]; Vector3 n = _normals[i]; Vector2 t = _uv[i];
                WriteFloat(result, o, p.X); WriteFloat(result, o + 4, p.Y); WriteFloat(result, o + 8, p.Z);
                WriteFloat(result, o + 12, n.X); WriteFloat(result, o + 16, n.Y); WriteFloat(result, o + 20, n.Z);
                // NAB vertices carry a packed diffuse colour. Zero makes newly
                // serialized geometry black/transparent; use opaque white.
                BitConverter.GetBytes(0xFFFFFFFFu).CopyTo(result, o + 24);
                WriteFloat(result, o + 28, t.X / Math.Max(1, Texture.Width - 1)); WriteFloat(result, o + 32, t.Y / Math.Max(1, Texture.Height - 1));
            }
            suffix.CopyTo(result, vertexEnd);
            Texture = Texture.Rebase(result, vertexEnd - _originalVertexEnd);
            _originalGroupCountOffset = vertexEnd + groupOffset - 4;
            _originalVertexEnd = vertexEnd;
            _suffix = suffix;
            _metadataVertexCount = _vertices.Length;
            _metadataTriangleCount = TriangleCount;
            _metadataGroups = (SurvivorGroup[])_groups.Clone();
            return result;
        }

        private bool HasVisibleTrianglesInGroup(int group)
        {
            SurvivorGroup g = _groups[group];
            for (int triangle = 0; triangle < g.TriangleCount; triangle++)
            {
                int i = (g.TriangleStart + triangle) * 3;
                if (Vector3.Cross(_vertices[i + 1] - _vertices[i], _vertices[i + 2] - _vertices[i]).LengthSquared() > 0.000001f)
                    return true;
            }
            return false;
        }

        private static void WriteFloat(byte[] data, int offset, float value) => BitConverter.GetBytes(value).CopyTo(data, offset);
        private static short ToShort(float value) => (short)Math.Clamp(MathF.Round(value), short.MinValue, short.MaxValue);
        private static byte ToByte(float value) => (byte)Math.Clamp(MathF.Round(value), byte.MinValue, byte.MaxValue);

        public List<RenderPart> CreateRenderParts()
        {
            var result = new List<RenderPart>(_groups.Length);
            foreach (SurvivorGroup group in _groups)
            {
                int vertexStart = group.TriangleStart * 3;
                int vertexCount = group.TriangleCount * 3;
                var localVertices = new Vector3[vertexCount];
                Array.Copy(_vertices, vertexStart, localVertices, 0, vertexCount);
                var faces = new List<RenderFace>(group.TriangleCount);
                for (int i = 0; i < group.TriangleCount; i++)
                {
                    int source = vertexStart + i * 3;
                    int target = i * 3;
                    faces.Add(new RenderFace(new[] { target, target + 1, target + 2 },
                        new[] { _uv[source], _uv[source + 1], _uv[source + 2] }, group.TexturePage, 0));
                }
                var localNormals = new Vector3[vertexCount];
                Array.Copy(_normals, vertexStart, localNormals, 0, vertexCount);
                var part = new RenderPart(localVertices, faces, localNormals);
                part.ApplyTransform(group.BindTransform);
                result.Add(part);
            }
            return result;
        }

        public int GetAnimationDuration(int selector) => _animationBank?.GetDuration(selector) ?? 0;

        public int NodeCount => _bindLocalTransforms.Length;
        public int GetParentIndex(int node) => _parentIndices[node];
        public Matrix4x4 GetBindLocal(int node) => _bindLocalTransforms[node];

        public Matrix4x4[] GetLocalPose(int selector, int frame)
        {
            Matrix4x4[] local = (Matrix4x4[])_bindLocalTransforms.Clone();
            _animationBank?.ApplyLocalPose(selector, frame, local);
            return local;
        }

        public Matrix4x4[] GetGlobalPose(int selector, int frame)
        {
            Matrix4x4[] local = GetLocalPose(selector, frame);
            var global = new Matrix4x4[local.Length];
            for (int i = 0; i < local.Length; i++)
            {
                int parent = _parentIndices[i];
                global[i] = parent >= 0 && parent < i ? local[i] * global[parent] : local[i];
            }
            return global;
        }

        public void ApplyPose(List<RenderPart> parts, int selector, int frame)
        {
            if (_animationBank == null) return;
            Matrix4x4[] global = GetGlobalPose(selector, frame);
            for (int i = 0; i < parts.Count && i < _groups.Length; i++)
                parts[i].ApplyTransform(global[_groups[i].NodeIndex]);
        }

        private sealed class SurvivorAnimationBank
        {
            private const int QHeaderSize = 0x60;
            private const int SelectorPairSize = 8;
            private const int QDiskSize = 0x1B0;
            private readonly Track[][] _selectors;
            public int SelectorCount => _selectors.Length;

            private SurvivorAnimationBank(Track[][] selectors) => _selectors = selectors;

            public static SurvivorAnimationBank? TryLoad(byte[] nab, string nacPath, int rootNodeOffset, int nodeCount, uint rootNodePointer)
            {
                int qCount = nodeCount - 1;
                if (qCount <= 0) return null;
                int qStart = rootNodeOffset - 0x1C - qCount * QDiskSize;
                if (qStart < 0) return null;
                int selectorCount = BitConverter.ToInt32(nab, qStart + 0x0C);
                if (selectorCount <= 0 || selectorCount > 256) return null;

                byte[] nac = File.ReadAllBytes(nacPath);
                if (nac.Length < 8) return null;
                int recordCount = BitConverter.ToInt32(nac, 0);
                int commandCountOffset = checked(4 + recordCount * 0x70);
                if (recordCount <= 0 || commandCountOffset + 4 > nac.Length) return null;
                int commandCount = BitConverter.ToInt32(nac, commandCountOffset);
                int commandOffset = commandCountOffset + 4;
                if (commandCount <= 0 || commandOffset + commandCount * 8 != nac.Length) return null;

                var poses = new Pose[recordCount];
                for (int i = 0; i < recordCount; i++)
                {
                    int o = 4 + i * 0x70;
                    poses[i] = new Pose(BitConverter.ToSingle(nac, o + 0x24), BitConverter.ToSingle(nac, o + 0x28),
                        BitConverter.ToSingle(nac, o + 0x2C), BitConverter.ToSingle(nac, o + 0x14),
                        BitConverter.ToSingle(nac, o + 0x18), BitConverter.ToSingle(nac, o + 0x1C));
                }

                var selectors = new Track[selectorCount][];
                for (int selector = 0; selector < selectorCount; selector++)
                {
                    var tracks = new List<Track>();
                    for (int q = 0; q < qCount; q++)
                    {
                        int header = qStart + q * QDiskSize;
                        if (BitConverter.ToInt32(nab, header) != 4 || BitConverter.ToInt32(nab, header + 0x0C) != selectorCount)
                            return null;
                        int nodeIndex = q + 1;
                        int pair = header + QHeaderSize + selector * SelectorPairSize;
                        int cursor = BitConverter.ToInt32(nab, pair + 4) + 1;
                        Track? track = ReadTrack(nac, commandOffset, commandCount, poses, nodeIndex, cursor);
                        if (track != null) tracks.Add(track);
                    }
                    selectors[selector] = tracks.ToArray();
                }
                return new SurvivorAnimationBank(selectors);
            }

            private static Track? ReadTrack(byte[] nac, int commandOffset, int commandCount, Pose[] poses, int nodeIndex, int cursor)
            {
                var keys = new List<(Pose Pose, int Duration)>();
                var visited = new HashSet<int>();
                for (int budget = 0; budget < 4096 && (uint)cursor < (uint)commandCount && visited.Add(cursor); budget++)
                {
                    int o = commandOffset + cursor * 8;
                    uint command = BitConverter.ToUInt32(nac, o);
                    uint record = BitConverter.ToUInt32(nac, o + 4);
                    if ((command & 0x80000000u) != 0)
                    {
                        int jump = (int)(command & 0xFFFF);
                        if ((uint)jump >= (uint)commandCount || visited.Contains(jump)) break;
                        cursor = jump;
                        continue;
                    }
                    if (record >= poses.Length) break;
                    int duration = Math.Max(1, (int)(command >> 16)) * 2;
                    keys.Add((poses[record], duration));
                    cursor++;
                }
                return keys.Count == 0 ? null : new Track(nodeIndex, keys);
            }

            public int GetDuration(int selector) => (uint)selector < (uint)_selectors.Length
                ? Math.Max(1, _selectors[selector].Select(x => x.Duration).DefaultIfEmpty(1).Max()) : 0;

            public void ApplyLocalPose(int selector, int frame, Matrix4x4[] local)
            {
                if ((uint)selector >= (uint)_selectors.Length) return;
                foreach (Track track in _selectors[selector])
                    if ((uint)track.NodeIndex < (uint)local.Length) local[track.NodeIndex] = track.Sample(frame).ToMatrix();
            }

            private readonly record struct Pose(float X, float Y, float Z, float RX, float RY, float RZ)
            {
                public Matrix4x4 ToMatrix()
                {
                    const float unit = MathF.PI * 2 / 4096f;
                    return Matrix4x4.CreateRotationZ(RZ * unit) * Matrix4x4.CreateRotationY(-RY * unit)
                        * Matrix4x4.CreateRotationX(-RX * unit) * Matrix4x4.CreateTranslation(X, Y, -Z);
                }

                public static Pose Lerp(Pose a, Pose b, float t) => new(
                    a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t), a.Z + ((b.Z - a.Z) * t),
                    LerpAngle(a.RX, b.RX, t), LerpAngle(a.RY, b.RY, t), LerpAngle(a.RZ, b.RZ, t));

                private static float LerpAngle(float a, float b, float t)
                {
                    float delta = (b - a + 2048f) % 4096f - 2048f;
                    return a + delta * t;
                }
            }

            private sealed class Track
            {
                private readonly (Pose Pose, int Duration)[] _keys;
                public int NodeIndex { get; }
                public int Duration { get; }

                public Track(int nodeIndex, List<(Pose Pose, int Duration)> keys)
                {
                    NodeIndex = nodeIndex;
                    _keys = keys.ToArray();
                    Duration = Math.Max(1, _keys.Skip(1).Sum(x => x.Duration));
                }

                public Pose Sample(int frame)
                {
                    if (_keys.Length == 1) return _keys[0].Pose;
                    int remaining = Math.Clamp(frame, 0, Duration - 1);
                    for (int i = 0; i < _keys.Length - 1; i++)
                    {
                        int span = _keys[i + 1].Duration;
                        if (remaining < span) return Pose.Lerp(_keys[i].Pose, _keys[i + 1].Pose, remaining / (float)span);
                        remaining -= span;
                    }
                    return _keys[^1].Pose;
                }
            }
        }

        private sealed record SurvivorGroup(int TriangleStart, int TriangleCount, byte TexturePage, int NodeIndex, Matrix4x4 BindTransform);
        private sealed record SurvivorPart(Vector3[] Vertices, Vector3[] Normals, Vector2[] UV, byte TexturePage, int NodeIndex);
    }

    // Clut < 0 marks an untextured (flat/vertex-coloured) face. VtColors holds
    // one PSX vertex colour per corner (RGB 0-255), interpolated across the
    // triangle and multiplied with the texture (or used directly when flat).
    private sealed record RenderFace(int[] Indices, Vector2[] UV, byte Page, int Clut, Color[]? VtColors = null);

    // Meditor text sources that accompany many PSX_DATA HMDs:
    // .PLY = "tex v0 v1 v2 v3 n0 n1 n2 n3" quads + vertex/normal tables,
    // .MAT = one line per polygon: idx ? F H/D ? 4x(u,v) uv pairs, then
    // 4 RGB triples (H) or one RGB (D). A zero 4th UV marks a triangle.
    // Meditor text sources that accompany many PSX_DATA HMDs:
    // .PLY = "form v0 v1 v2 v3 n0 n1 n2 n3" (form 0 = triangle, 1 = quad)
    // plus vertex/normal tables; .MAT = one line per polygon:
    // "idx flag F|G H|D|T page 4x(u,v) [colours]" where the material type is
    // H (textured + per-vertex colour), T (textured) or D (flat colour).
    private static class PlyMatParts
    {
        private static string[] Section(string[] lines, string marker, int count)
        {
            int start = Array.FindIndex(lines, l => l.TrimStart().StartsWith('#') &&
                l.TrimStart().StartsWith(marker, StringComparison.OrdinalIgnoreCase));
            if (start < 0) throw new InvalidDataException($"PLY/MAT section '{marker}' not found.");
            return lines.Skip(start + 1).Take(count).ToArray();
        }

        private static int[] SectionCounts(string[] lines)
        {
            // "@XXX..." then "# Number of Items" then counts: PLY carries
            // "v n p" on one line, MAT carries a single count line.
            var found = new List<int>();
            for (int i = 0; i < Math.Min(6, lines.Length); i++)
            {
                int[] nums = lines[i].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s, out int n) ? n : -1).ToArray();
                if (nums.Length >= 3 && nums.All(n => n > 0)) return nums;
                if (nums.Length == 1 && nums[0] > 0) found.Add(nums[0]);
            }
            if (found.Count > 0) return found.ToArray();
            throw new InvalidDataException("PLY/MAT item counts not found.");
        }

        private static int[] Ints(string line) =>
            line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

        private static float[] Floats(string line) =>
            line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();

        public static List<RenderPart> Load(string plyPath, string matPath,
            IReadOnlyDictionary<string, int> pageMap)
        {
            string[] plyLines = File.ReadAllLines(plyPath);
            string[] matLines = File.ReadAllLines(matPath);
            int[] counts = SectionCounts(plyLines);
            int vCount = counts[0], nCount = counts[1], pCount = counts[2];
            Vector3[] verts = Section(plyLines, "# Vertex", vCount).Select(l =>
            {
                float[] f = Floats(l);
                return new Vector3(f[0], f[1], f[2]);
            }).ToArray();
            Vector3[] normals = Section(plyLines, "# Normal", nCount).Select(l =>
            {
                float[] f = Floats(l);
                var v = new Vector3(f[0], f[1], f[2]);
                return v.LengthSquared() == 0 ? Vector3.UnitY : Vector3.Normalize(v);
            }).ToArray();
            int[] matCounts = SectionCounts(matLines);
            string[] matRows = Section(matLines, "# Material", matCounts[0]);
            if (matRows.Length < pCount) throw new InvalidDataException("MAT has fewer rows than PLY polygons.");
            string[] polyRows = Section(plyLines, "# Polygon", pCount);

            var result = new List<RenderPart>(pCount);
            for (int i = 0; i < pCount; i++)
            {
                int[] poly = Ints(polyRows[i]);
                if (poly.Length != 9) throw new InvalidDataException($"PLY polygon {i} must have 9 fields.");
                // PLY field 0 is the polygon form: 0 = triangle, 1 = quad.
                bool triangle = poly[0] == 0;
                int useVerts = triangle ? 3 : 4;
                int[] vi = poly.Skip(1).Take(useVerts).ToArray();
                int[] ni = poly.Skip(5).Take(useVerts).ToArray();
                foreach (int v in vi)
                    if ((uint)v >= (uint)verts.Length) throw new InvalidDataException($"PLY polygon {i} has vertex {v} out of range.");
                foreach (int n in ni)
                    if ((uint)n >= (uint)normals.Length) throw new InvalidDataException($"PLY polygon {i} has normal {n} out of range.");
                // MAT row: "<index> <flag> <F|G> <H|D|T> <page/U0..>" where the
                // material token is H (textured + per-vertex colour), T
                // (textured, unmodulated) or D (flat colour). Tokens 5..12 are
                // four UV pairs; H rows append four RGB triples at 13..24 and
                // D rows append one RGB triple at 13..15.
                string[] mt = matRows[i].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (mt.Length < 5) throw new InvalidDataException($"MAT row {i} is too short.");
                string matType = mt[3];
                bool textured = matType is "H" or "T";
                bool hasVertexColors = matType == "H";
                bool hasFlatColor = matType == "D";
                if (!textured && !hasFlatColor)
                    throw new InvalidDataException($"MAT row {i} has unknown material type '{matType}'.");

                int Token(int index) => index < mt.Length
                    ? int.TryParse(mt[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0
                    : 0;

                // 4 UV pairs at tokens 5..12; triangles only use the first 3.
                Vector2[] uvs = Enumerable.Range(0, 4).Select(k =>
                    new Vector2(Token(5 + k * 2), Token(6 + k * 2))).ToArray();
                Vector3[] localVerts = vi.Select(v => verts[v]).ToArray();
                Vector3[] localNormals = ni.Select(n => normals[n]).ToArray();
                byte page = (byte)(pageMap.TryGetValue(string.Join(",", vi), out int slot) ? slot : 0);
                Color[] colors;
                if (hasVertexColors && mt.Length >= 25)
                    colors = Enumerable.Range(0, 4).Select(k => Color.FromArgb(255,
                        Math.Clamp(Token(13 + k * 3), 0, 255),
                        Math.Clamp(Token(14 + k * 3), 0, 255),
                        Math.Clamp(Token(15 + k * 3), 0, 255))).ToArray();
                else
                {
                    int r = 255, g = 255, b = 255;
                    if (hasFlatColor && mt.Length >= 16)
                    {
                        r = Math.Clamp(Token(13), 0, 255);
                        g = Math.Clamp(Token(14), 0, 255);
                        b = Math.Clamp(Token(15), 0, 255);
                    }
                    colors = Enumerable.Repeat(Color.FromArgb(255, r, g, b), 4).ToArray();
                }

                var faces = new List<RenderFace>();
                if (!textured)
                {
                    if (triangle)
                        faces.Add(new RenderFace(new[] { 0, 1, 2 }, Enumerable.Repeat(Vector2.Zero, 3).ToArray(),
                            page, -1, new[] { colors[0], colors[1], colors[2] }));
                    else
                    {
                        faces.Add(new RenderFace(new[] { 0, 1, 2 }, Enumerable.Repeat(Vector2.Zero, 3).ToArray(),
                            page, -1, new[] { colors[0], colors[1], colors[2] }));
                        faces.Add(new RenderFace(new[] { 3, 2, 1 }, Enumerable.Repeat(Vector2.Zero, 3).ToArray(),
                            page, -1, new[] { colors[3], colors[2], colors[1] }));
                    }
                }
                else if (triangle)
                    faces.Add(new RenderFace(new[] { 0, 1, 2 }, uvs.Take(3).ToArray(),
                        page, 0, colors.Take(3).ToArray()));
                else
                {
                    faces.Add(new RenderFace(new[] { 0, 1, 2 }, uvs.Take(3).ToArray(),
                        page, 0, colors.Take(3).ToArray()));
                    faces.Add(new RenderFace(new[] { 3, 2, 1 }, new[] { uvs[3], uvs[2], uvs[1] },
                        page, 0, new[] { colors[3], colors[2], colors[1] }));
                }
                var part = new RenderPart(localVerts, faces, localNormals);
                part.ApplyTransform(Matrix4x4.Identity);
                result.Add(part);
            }
            return result;
        }
    }

    private sealed class SurvivorHmd
    {
        private readonly byte[] _data;
        public int PrimitiveHeaderOffset { get; }
        public int BlockCount => BlockPointers.Length;
        public uint[] BlockPointers { get; }

        private SurvivorHmd(byte[] data, int primitiveHeaderOffset, uint[] blockPointers)
        {
            _data = data;
            PrimitiveHeaderOffset = primitiveHeaderOffset;
            BlockPointers = blockPointers;
        }

        public static SurvivorHmd FromFile(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 16 || BitConverter.ToUInt32(data, 0) != 0x50)
                throw new InvalidDataException("This is not a PlayStation HMD file (version ID 0x50). ");
            uint mapFlag = BitConverter.ToUInt32(data, 4);
            if (mapFlag > 1) throw new InvalidDataException("The HMD map flag is invalid.");
            uint primitiveHeaderWords = BitConverter.ToUInt32(data, 8);
            uint blockCount = BitConverter.ToUInt32(data, 12);
            if (blockCount == 0 || blockCount > 4096 || 16L + blockCount * 4L > data.Length)
                throw new InvalidDataException("The HMD block table is invalid or truncated.");
            long primitiveHeaderOffset = primitiveHeaderWords * 4L;
            if (primitiveHeaderOffset < 0 || primitiveHeaderOffset >= data.Length)
                throw new InvalidDataException("The HMD primitive-header pointer is outside the file.");
            var pointers = new uint[blockCount];
            for (int i = 0; i < pointers.Length; i++) pointers[i] = BitConverter.ToUInt32(data, 16 + i * 4);
            return new SurvivorHmd(data, (int)primitiveHeaderOffset, pointers);
        }

        private int WordPointer(uint value)
        {
            long offset = (value & 0x7FFFFFFFu) * 4L;
            if (offset < 0 || offset >= _data.Length) throw new InvalidDataException($"HMD pointer 0x{value:X8} is outside the file.");
            return (int)offset;
        }

        private ushort U16(int offset)
        {
            if ((uint)(offset + 2) > (uint)_data.Length) throw new InvalidDataException("The HMD polygon data is truncated.");
            return BitConverter.ToUInt16(_data, offset);
        }

        private uint U32(int offset)
        {
            if ((uint)(offset + 4) > (uint)_data.Length) throw new InvalidDataException("The HMD data is truncated.");
            return BitConverter.ToUInt32(_data, offset);
        }

        // Packet layout: Size, vertex-index offsets, packed-UV offsets
        // (uint16: low byte U, high byte V; null = flat untextured packet),
        // RGB-triple offsets (null = no colour data), and the TPAGE word
        // offset (-1 if none).
        // Verified against the Meditor .PLY/.MAT sources of IO301_0/A.HMD
        // (types 0x49/0x4D/0x51/0x55) and KENK206/A.HMD (types
        // 0x09/0x0D/0x11/0x15). The 0x40 bit marks packets that carry
        // colour data; the uncoloured 0x0X variants leave the texture
        // unmodulated.
        private static (int Size, int[] VertexOffsets, int[]? UvOffsets, int[]? ColorOffsets, int TpageOffset)? PacketLayout(byte type)
        {
            // Standard SCE HMD category-0 polygon packets. Driver flags occupy
            // the upper bits; the low byte describes the GPU packet layout.
            return type switch
            {
                0x08 => (12, new[] { 6, 8, 10 }, null, new[] { 0 }, -1),
                0x09 => (20, new[] { 14, 16, 18 }, new[] { 0, 4, 8 }, null, 6),
                0x0A => (20, new[] { 14, 16, 18 }, null, new[] { 0 }, -1),
                0x0C => (16, new[] { 6, 10, 14 }, null, new[] { 0 }, -1),
                0x0D => (24, new[] { 14, 18, 22 }, new[] { 0, 4, 8 }, null, 6),
                0x0E => (24, new[] { 14, 18, 22 }, null, new[] { 0 }, -1),
                0x10 => (16, new[] { 6, 8, 10, 12 }, null, new[] { 0 }, -1),
                0x11 => (24, new[] { 16, 18, 20, 22 }, new[] { 0, 4, 8, 12 }, null, 6),
                0x12 => (28, new[] { 18, 20, 22, 24 }, null, new[] { 0 }, -1),
                0x14 => (20, new[] { 6, 10, 14, 18 }, null, new[] { 0 }, -1),
                0x15 => (28, new[] { 14, 18, 22, 26 }, new[] { 0, 4, 8, 12 }, null, 6),
                0x16 => (32, new[] { 18, 22, 26, 30 }, null, new[] { 0 }, -1),
                0x49 => (20, new[] { 14, 16, 18 }, new[] { 4, 8, 12 }, new[] { 0 }, 10),
                0x4D => (28, new[] { 22, 24, 26 }, new[] { 12, 16, 20 }, new[] { 0, 4, 8 }, 18),
                0x50 => (12, new[] { 4, 6, 8, 10 }, null, new[] { 0 }, -1),
                0x51 => (24, new[] { 14, 18, 20, 22 }, new[] { 4, 8, 12, 16 }, new[] { 0 }, 10),
                0x54 => (24, new[] { 16, 18, 20, 22 }, null, new[] { 0 }, -1),
                0x55 => (36, new[] { 26, 30, 32, 34 }, new[] { 16, 20, 24, 28 }, new[] { 0, 4, 8, 12 }, 22),
                _ => null
            };
        }

        public List<RenderPart> CreateRenderParts(Func<int, int>? pageMapper = null)
        {
            var result = new List<RenderPart>();
            foreach (uint rawBlock in BlockPointers)
            {
                if (rawBlock == 0 || rawBlock == 0xFFFFFFFF) continue;
                int primitive = WordPointer(rawBlock);
                int guard = 0;
                while (primitive >= 0 && guard++ < 256)
                {
                    uint next = U32(primitive);
                    int header = WordPointer(U32(primitive + 4));
                    int typeCount = (int)(U32(primitive + 8) & 0x7FFFFFFF);
                    if (typeCount < 0 || typeCount > 256) throw new InvalidDataException("The HMD primitive type count is invalid.");
                    int headerSize = (int)U32(header);
                    if (headerSize < 2) throw new InvalidDataException("The HMD model primitive has no polygon/vertex header.");
                    int polygonTop = WordPointer(U32(header + 4));
                    int vertexTop = WordPointer(U32(header + 8));
                    var faces = new List<RenderFace>();
                    int maximumVertex = -1;
                    for (int t = 0; t < typeCount; t++)
                    {
                        int descriptor = primitive + 12 + t * 12;
                        uint typeWord = U32(descriptor);
                        uint countSize = U32(descriptor + 4);
                        int polygonCount = (int)((countSize >> 16) & 0x7FFF);
                        uint polygonIndex = U32(descriptor + 8) & 0x00FFFFFF;
                        var layoutNullable = PacketLayout((byte)typeWord);
                        if (layoutNullable == null || polygonCount == 0) continue;
                        var layout = layoutNullable.Value;
                        int packet = checked(polygonTop + (int)polygonIndex * 4);
                        // Non-polygon primitives (lights, items, skeletons) reuse the same
                        // descriptor shape; a packet range that runs past the file means this
                        // descriptor is not a polygon stream, so skip it instead of failing.
                        if (packet < 0 || (long)packet + (long)polygonCount * layout.Size > _data.Length) continue;
                        int numVerts = layout.VertexOffsets.Length;
                        Color[]? PacketColors(int pkt)
                        {
                            if (layout.ColorOffsets == null) return null;
                            if (layout.ColorOffsets.Length == 1)
                            {
                                int o = pkt + layout.ColorOffsets[0];
                                return Enumerable.Repeat(Color.FromArgb(255, _data[o], _data[o + 1], _data[o + 2]), numVerts).ToArray();
                            }
                            return layout.ColorOffsets
                                .Select(o => Color.FromArgb(255, _data[pkt + o], _data[pkt + o + 1], _data[pkt + o + 2]))
                                .ToArray();
                        }
                        for (int p = 0; p < polygonCount; p++, packet += layout.Size)
                        {
                            int[] indices = layout.VertexOffsets.Select(o => (int)U16(packet + o)).ToArray();
                            maximumVertex = Math.Max(maximumVertex, indices.Max());
                            Color[]? colors = PacketColors(packet);
                            if (layout.UvOffsets == null)
                            {
                                Color[]? flat = colors;
                                if (indices.Length == 3)
                                    faces.Add(new RenderFace(indices, Enumerable.Repeat(Vector2.Zero, 3).ToArray(), 0, -1, flat?.Take(3).ToArray()));
                                else
                                {
                                    faces.Add(new RenderFace(new[] { indices[0], indices[1], indices[2] },
                                        Enumerable.Repeat(Vector2.Zero, 3).ToArray(), 0, -1, flat == null ? null : new[] { colors![0], colors[1], colors[2] }));
                                    faces.Add(new RenderFace(new[] { indices[3], indices[2], indices[1] },
                                        Enumerable.Repeat(Vector2.Zero, 3).ToArray(), 0, -1, flat == null ? null : new[] { colors![3], colors[2], colors[1] }));
                                }
                                continue;
                            }
                            // Packed UV: low byte U, high byte V.
                            Vector2[] uvs = layout.UvOffsets.Select(o =>
                            {
                                ushort w = U16(packet + o);
                                return new Vector2((byte)(w & 0xFF), (byte)(w >> 8));
                            }).ToArray();
                            int tpageX = layout.TpageOffset >= 0 ? U16(packet + layout.TpageOffset) & 0xF : 0;
                            byte page = (byte)(pageMapper != null ? pageMapper(tpageX) : tpageX);
                            // Clut is unused by 16bpp TIMs; pass 0 (atlas slot selects the TIM).
                            if (indices.Length == 3)
                                faces.Add(new RenderFace(indices, uvs, page, 0, colors));
                            else
                            {
                                faces.Add(new RenderFace(new[] { indices[0], indices[1], indices[2] },
                                    new[] { uvs[0], uvs[1], uvs[2] }, page, 0, colors == null ? null : new[] { colors[0], colors[1], colors[2] }));
                                faces.Add(new RenderFace(new[] { indices[3], indices[2], indices[1] },
                                    new[] { uvs[3], uvs[2], uvs[1] }, page, 0, colors == null ? null : new[] { colors[3], colors[2], colors[1] }));
                            }
                        }
                    }
                    if (maximumVertex >= 0 && maximumVertex < 65536 && faces.Count > 0)
                    {
                        var vertices = new Vector3[maximumVertex + 1];
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            int v = vertexTop + i * 8;
                            if (v + 8 > _data.Length) throw new InvalidDataException("The HMD vertex section is truncated.");
                            vertices[i] = new Vector3(BitConverter.ToInt16(_data, v), BitConverter.ToInt16(_data, v + 2), BitConverter.ToInt16(_data, v + 4));
                        }
                        var renderFaces = faces;
                        var part = new RenderPart(vertices, renderFaces);
                        part.ApplyTransform(Matrix4x4.Identity);
                        result.Add(part);
                    }
                    if (next == 0xFFFFFFFF || next == 0) break;
                    primitive = WordPointer(next);
                }
            }
            if (result.Count == 0) throw new InvalidDataException("No supported standard polygon primitives were found in this HMD.");
            return result;
        }

        // Maps a polygon's (ordered) vertex-index key to the texture-page slot
        // its packet samples. Used by the PLY/MAT fallback, which has no TPAGE
        // word of its own. Never throws: malformed primitives are skipped.
        public Dictionary<string, int> BuildTexturePageMap(Func<int, int>? pageMapper = null)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            try
            {
                foreach (uint rawBlock in BlockPointers)
                {
                    if (rawBlock == 0 || rawBlock == 0xFFFFFFFF) continue;
                    int primitive = WordPointer(rawBlock);
                    int guard = 0;
                    while (primitive >= 0 && guard++ < 256)
                    {
                        uint next = U32(primitive);
                        int header = WordPointer(U32(primitive + 4));
                        int typeCount = (int)(U32(primitive + 8) & 0x7FFFFFFF);
                        if (typeCount < 0 || typeCount > 256) break;
                        int polygonTop = WordPointer(U32(header + 4));
                        for (int t = 0; t < typeCount; t++)
                        {
                            int descriptor = primitive + 12 + t * 12;
                            if (descriptor + 12 > _data.Length) break;
                            var layoutNullable = PacketLayout((byte)U32(descriptor));
                            if (layoutNullable == null) continue;
                            var layout = layoutNullable.Value;
                            if (layout.UvOffsets == null || layout.TpageOffset < 0) continue;
                            int polygonCount = (int)((U32(descriptor + 4) >> 16) & 0x7FFF);
                            uint polygonIndex = U32(descriptor + 8) & 0x00FFFFFF;
                            int packet = polygonTop + (int)polygonIndex * 4;
                            if (packet < 0 || (long)packet + (long)polygonCount * layout.Size > _data.Length) continue;
                            int numVerts = layout.VertexOffsets.Length;
                            for (int p = 0; p < polygonCount; p++, packet += layout.Size)
                            {
                                var key = new StringBuilder();
                                for (int i = 0; i < numVerts; i++)
                                {
                                    if (i != 0) key.Append(',');
                                    key.Append(U16(packet + layout.VertexOffsets[i]));
                                }
                                int tpageX = U16(packet + layout.TpageOffset) & 0xF;
                                map[key.ToString()] = pageMapper != null ? pageMapper(tpageX) : tpageX;
                            }
                        }
                        if (next == 0xFFFFFFFF || next == 0) break;
                        primitive = WordPointer(next);
                    }
                }
            }
            catch { /* best effort */ }
            return map;
        }
    }
    private sealed record AnimationChoice(IEdd Edd, Emr Skeleton, int Bank, int AnimationIndex);

    private sealed class RenderPart
    {
        public Vector3[] LocalVertices { get; }
        public Vector3[]? LocalNormals { get; }
        public Vector3[] Vertices { get; private set; }
        public Vector3[] Normals { get; private set; }
        public List<RenderFace> Faces { get; }
        public bool Visible { get; set; } = true;

        public RenderPart(Vector3[] localVertices, List<RenderFace> faces, Vector3[]? localNormals = null)
        {
            LocalVertices = localVertices;
            LocalNormals = localNormals;
            Vertices = new Vector3[localVertices.Length];
            Normals = new Vector3[localVertices.Length];
            Faces = faces;
        }

        public void ApplyTransform(Matrix4x4 transform)
        {
            for (int i = 0; i < LocalVertices.Length; i++)
            {
                Vector3 p = Vector3.Transform(LocalVertices[i], transform);
                Vertices[i] = p;
            }
            if (LocalNormals != null && LocalNormals.Length == Normals.Length)
            {
                for (int i = 0; i < Normals.Length; i++)
                {
                    Vector3 normal = Vector3.TransformNormal(LocalNormals[i], transform);
                    Normals[i] = normal.LengthSquared() == 0 ? Vector3.UnitY : Vector3.Normalize(normal);
                }
                return;
            }
            Array.Clear(Normals, 0, Normals.Length);
            foreach (RenderFace face in Faces)
            {
                if (face.Indices.Any(i => (uint)i >= (uint)Vertices.Length)) continue;
                Vector3 n = Vector3.Cross(Vertices[face.Indices[1]] - Vertices[face.Indices[0]], Vertices[face.Indices[2]] - Vertices[face.Indices[0]]);
                if (n.LengthSquared() == 0) continue;
                n = Vector3.Normalize(n);
                foreach (int index in face.Indices) Normals[index] += n;
            }
            for (int i = 0; i < Normals.Length; i++) if (Normals[i].LengthSquared() != 0) Normals[i] = Vector3.Normalize(Normals[i]);
        }
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
        private bool _smoothShading = true;
        private bool _texturesVisible = true;
        private IViewerTexture? _texture;

        public bool Wireframe { get => _wireframe; set { _wireframe = value; Invalidate(); } }
        public bool SmoothShading { get => _smoothShading; set { _smoothShading = value; Invalidate(); } }
        public bool TexturesVisible { get => _texturesVisible; set { _texturesVisible = value; Invalidate(); } }

        public ModelViewport()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(28, 30, 34);
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        public void SetModel(List<RenderPart> parts, IViewerTexture? texture)
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

        public void RefitModel()
        {
            Vector3[] all = _parts.Where(x => x.Visible).SelectMany(x => x.Vertices).ToArray();
            if (all.Length == 0) return;
            Vector3 min = all.Aggregate(Vector3.Min), max = all.Aggregate(Vector3.Max);
            _center = (min + max) / 2;
            _radius = Math.Max(1, Vector3.Distance(min, max) / 2);
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
                Vector3[] normals = part.Normals.Select(n => n.LengthSquared() == 0 ? Vector3.UnitZ : Vector3.Normalize(Vector3.TransformNormal(n, rotation))).ToArray();
                foreach (RenderFace face in part.Faces)
                {
                    if (face.Indices.Any(i => (uint)i >= (uint)transformed.Length)) continue;
                    Vector3 a = transformed[face.Indices[0]], b = transformed[face.Indices[1]], c = transformed[face.Indices[2]];
                    Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                    float light = Math.Clamp(0.25f + Math.Abs(Vector3.Dot(normal, Vector3.Normalize(new Vector3(-0.4f, -0.7f, -1)))) * 0.75f, 0, 1);
                    float[] lights = face.Indices.Select(i => SmoothShading
                        ? Math.Clamp(0.25f + Math.Abs(Vector3.Dot(normals[i], Vector3.Normalize(new Vector3(-0.4f, -0.7f, -1)))) * 0.75f, 0, 1)
                        : light).ToArray();
                    Vector3[] screen = face.Indices.Select(i => new Vector3(Width / 2f + transformed[i].X * scale,
                        Height / 2f + transformed[i].Y * scale, transformed[i].Z)).ToArray();
                    RasterTriangle(pixels, depth, screen, face, lights, ColorFromPart(partIndex));
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

        private void RasterTriangle(int[] pixels, float[] depth, Vector3[] p, RenderFace face, float[] lights, Color fallback)
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
                float light = w0 * lights[0] + w1 * lights[1] + w2 * lights[2];
                // Vertex colours (PSX RGB) modulate the texture or colour flat faces.
                float vr = 1f, vg = 1f, vb = 1f;
                bool hasVertexColor = face.VtColors is { Length: 3 };
                if (hasVertexColor)
                {
                    vr = (w0 * face.VtColors![0].R + w1 * face.VtColors[1].R + w2 * face.VtColors[2].R) / 255f;
                    vg = (w0 * face.VtColors[0].G + w1 * face.VtColors[1].G + w2 * face.VtColors[2].G) / 255f;
                    vb = (w0 * face.VtColors[0].B + w1 * face.VtColors[1].B + w2 * face.VtColors[2].B) / 255f;
                }
                Color flat = Color.FromArgb(255,
                    (int)(fallback.R * light * vr), (int)(fallback.G * light * vg), (int)(fallback.B * light * vb));

                Color color = flat;
                if (face.Clut < 0) { depth[index] = z; pixels[index] = flat.ToArgb(); continue; }
                if (_texture != null && TexturesVisible)
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
                        color = Color.FromArgb(color.A,
                            (int)(color.R * light * vr), (int)(color.G * light * vg), (int)(color.B * light * vb));
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

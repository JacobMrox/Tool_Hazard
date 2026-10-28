#nullable enable
using System.Numerics;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;
using System.Text.Json;
using IntelOrca.Biohazard.Model;
using Tool_Hazard.IntelOrca.Biohazard;
using Tool_Hazard.IntelOrca.Biohazard.Model;

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
        Text = "Biohazard Model Viewer - RE1 / RE2 / RE3 / Survivor PC";
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
            new ToolStripMenuItem("Export current native animation bank...", null, (_, _) => ExportNativeAnimationBank())
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
            Title = "Open classic Biohazard model",
            Filter = "Biohazard models (*.emd;*.pld;*.nab)|*.emd;*.pld;*.nab|Survivor PC model (*.nab)|*.nab|Enemy model (*.emd)|*.emd|Player model (*.pld)|*.pld|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) Open(dialog.FileName);
    }

    private void Open(string path)
    {
        try
        {
            if (Path.GetExtension(path).Equals(".nab", StringComparison.OrdinalIgnoreCase))
            {
                OpenSurvivor(path);
                return;
            }
            ModelFile model = ModelFile.FromFile(path);
            _model = model;
            _survivor = null;
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
            for (int bank = 0; ; bank++)
            {
                try
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
                catch { break; }
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
        int palettes = Math.Max(1, texture.PaletteCount);
        var bitmap = new Bitmap(texture.Width, checked(texture.Height * palettes), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (int palette = 0; palette < palettes; palette++)
            for (int y = 0; y < texture.Height; y++)
                for (int x = 0; x < texture.Width; x++)
                    bitmap.SetPixel(x, palette * texture.Height + y, Color.FromArgb(unchecked((int)texture.GetPixel(x, y, palette))));
        return bitmap;
    }

    private void ViewTexture()
    {
        using Bitmap? bitmap = CreateTextureBitmap();
        if (bitmap == null)
        {
            MessageBox.Show(this, "The current model has no texture loaded.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var preview = new Form { Text = $"Texture atlas - {CurrentTexture!.PaletteCount} CLUT(s), {CurrentTexture.Width}x{CurrentTexture.Height} each", StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(Math.Clamp(bitmap.Width * 2, 480, 1100), Math.Clamp(bitmap.Height * 2, 360, 800)) };
        var picture = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 32, 32), SizeMode = PictureBoxSizeMode.Zoom,
            Image = new Bitmap(bitmap) };
        preview.FormClosed += (_, _) => picture.Image?.Dispose();
        preview.Controls.Add(picture);
        preview.Show(this);
    }

    private void ExportTexture()
    {
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
            if (_survivor != null)
            {
                _survivor.Texture.Replace(bitmap, _survivor.Data);
                _survivor.CommitTextureEdits();
                LoadSurvivorMesh();
            }
            else
            {
                if (_texture == null || _model == null) throw new InvalidOperationException("Load a TIM texture before importing an image.");
                ImportTimAtlas(_texture, bitmap);
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
                File.Copy(_survivor.AnimationPath, nacDialog.FileName, true);
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
        File.WriteAllBytes(Path.ChangeExtension(dialog.FileName, ".emr"), choice.Skeleton.Data.ToArray());
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
        if (_survivor != null && _path != null)
        {
            using var survivorDialog = new SaveFileDialog { FileName = Path.GetFileName(_path), Filter = "Survivor model (*.nab)|*.nab" };
            if (survivorDialog.ShowDialog(this) == DialogResult.OK)
            {
                _survivor.Save(survivorDialog.FileName);
                if (_survivor.HasAnimationBank)
                    File.Copy(_survivor.AnimationPath, Path.ChangeExtension(survivorDialog.FileName, ".NAC"), true);
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
                        File.Copy(_survivor.AnimationPath, Path.ChangeExtension(nabDialog.FileName, ".NAC"), true);
                }
                return;
            }
            IModelMesh survivorMesh = _survivor.ToMd2(selected);
            string extension = native ? "md2" : "obj";
            using var survivorDialog = new SaveFileDialog
            {
                FileName = selectedPart ? $"survivor_part_{selected:D2}.{extension}" : $"{Path.GetFileNameWithoutExtension(_path)}.{extension}",
                Filter = native ? "RE3-compatible mesh (*.md2)|*.md2" : "Wavefront OBJ (*.obj)|*.obj"
            };
            if (survivorDialog.ShowDialog(this) != DialogResult.OK) return;
            if (native) File.WriteAllBytes(survivorDialog.FileName, survivorMesh.Data.ToArray());
            else new ObjExporter().Export(survivorMesh, survivorDialog.FileName, Math.Max(1, _survivor.Texture.Width / 128));
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
        if (_survivor != null)
        {
            ReplaceSurvivorMesh(selectedPart, native);
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
            else
            {
                IModelMesh incoming;
                Emr? incomingSkeleton = null;
                if (extension is ".emd" or ".pld")
                {
                    ModelFile model = ModelFile.FromFile(dialog.FileName);
                    incoming = model.GetMesh(0);
                    incomingSkeleton = TryGetSkeleton(model);
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
        try
        {
            try
            {
                _texture = model.GetTim(0);
                _texturePath = "Embedded TIM";
            }
            catch
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
        if (_survivor != null) { LoadSurvivorMesh(); return; }
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

    private static Emr? TryGetSkeleton(ModelFile model)
    {
        try { return model.GetEmr(0); }
        catch { return null; }
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

    private sealed class SurvivorTexture : IViewerTexture
    {
        private readonly ushort[] _pixels;
        private readonly (int Offset, int Width, int Height)[] _surfaces;
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
                    ushort value = (ushort)(((color.R * 31 / 255) << 10) | ((color.G * 31 / 255) << 5) | (color.B * 31 / 255));
                    int pixel = y * Width + sourceX + x;
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
        private readonly int _originalVertexEnd;
        private readonly int _originalGroupCountOffset;
        private byte[] _suffix;
        private readonly int[] _parentIndices;
        private readonly Matrix4x4[] _bindLocalTransforms;
        private readonly SurvivorAnimationBank? _animationBank;
        public int TriangleCount { get; private set; }
        public byte[] Data => _data;
        public SurvivorTexture Texture { get; }
        public string AnimationPath { get; }
        public bool HasAnimationBank => File.Exists(AnimationPath);
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
                int continuationAdjustment = surfaces.Count == 0 ? 0 : -4;
                if (surfaceOffset + SurfaceDataOffset + continuationAdjustment > data.Length)
                    throw new InvalidDataException("NAB surface header is truncated.");
                int format = BitConverter.ToInt32(data, surfaceOffset + SurfaceFormatOffset + continuationAdjustment);
                int surfaceWidth = BitConverter.ToInt32(data, surfaceOffset + SurfaceWidthOffset + continuationAdjustment);
                int surfaceHeight = BitConverter.ToInt32(data, surfaceOffset + SurfaceHeightOffset + continuationAdjustment);
                int surfaceData = checked(surfaceOffset + SurfaceDataOffset + continuationAdjustment);
                if (format != 25 || surfaceWidth <= 0 || surfaceWidth > 2048 || surfaceHeight <= 0 || surfaceHeight > 2048 ||
                    surfaceData + surfaceWidth * surfaceHeight * 2 > data.Length)
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
            for (int i = 0; i < groupCount; i++)
            {
                int o = groupTable + i * 32;
                uint surfacePointer = BitConverter.ToUInt32(data, o);
                int start = BitConverter.ToInt32(data, o + 8);
                int count = BitConverter.ToInt32(data, o + 12);
                uint nodePointer = BitConverter.ToUInt32(data, o + 16);
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
            }
            if (expectedTriangle != triangleCount)
                throw new InvalidDataException("NAB mesh groups do not cover every triangle.");

            uint rootNodePointer = FindRootNodePointer(data, groupTable + groupCount * 32, minimumNodePointer);
            uint maximumNodePointer = rawGroups.Max(x => x.NodePointer);
            int minimumNodeCount = checked((int)((maximumNodePointer - rootNodePointer) / 0x104) + 1);
            int nodeCount = FindNodeCount(data, rootNodePointer, minimumNodeCount, groupTable + groupCount * 32);
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

        private static bool IsGroupTable(byte[] data, int offset, int triangleCount)
        {
            if (offset < 0 || offset + 4 > data.Length) return false;
            int count = BitConverter.ToInt32(data, offset);
            if (count <= 0 || count > 256 || offset + 4 + count * 32 > data.Length) return false;
            int expected = 0;
            for (int i = 0; i < count; i++)
            {
                int record = offset + 4 + i * 32;
                int start = BitConverter.ToInt32(data, record + 8);
                int length = BitConverter.ToInt32(data, record + 12);
                if (start != expected || length <= 0 || start + length > triangleCount) return false;
                expected += length;
            }
            return expected == triangleCount;
        }

        private static uint FindRootNodePointer(byte[] data, int searchStart, uint lowestReferencedPointer)
        {
            uint pointer = lowestReferencedPointer;
            while (pointer >= 0x104)
            {
                uint candidate = pointer - 0x104;
                bool found = false;
                for (int o = searchStart; o <= data.Length - 4; o += 4)
                {
                    if (BitConverter.ToUInt32(data, o) == candidate) { found = true; break; }
                }
                if (!found) break;
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

        public void CommitTextureEdits()
        {
            int vertexEnd = checked(8 + _vertices.Length * VertexStride);
            _suffix = _data.AsSpan(vertexEnd).ToArray();
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

        public void ReplaceFromClassic(IModelMesh incoming, Emr? skeleton, int selectedGroup)
        {
            var converter = new MeshConverter();
            Md2 md2 = (Md2)converter.ConvertMesh(incoming, BioVersion.Biohazard3, remap: false);
            Matrix4x4[] sourceTransforms = BuildPartTransforms(skeleton, md2.NumParts, 0);
            SurvivorPart[] source = ReadMd2Parts(md2, sourceTransforms);
            if (source.Length == 0) throw new InvalidDataException("The incoming mesh has no parts.");
            SurvivorPart[] target = GetParts().ToArray();
            if (selectedGroup >= 0)
            {
                target[selectedGroup] = FitPartToTarget(source[0], target[selectedGroup]);
                ApplyParts(target);
                return;
            }

            Vector3[] targetGlobalVertices = target.SelectMany(x =>
            {
                Matrix4x4 global = GetBindGlobal(x.NodeIndex);
                return x.Vertices.Select(v => Vector3.Transform(v, global));
            }).ToArray();

            // Classic and Survivor models do not share an axis convention or a mesh
            // part order.  Try every orthogonal axis/sign mapping, and for each one
            // solve a unique minimum-cost body-part assignment.  This preserves a
            // stable anatomical mapping instead of stacking unrelated numbered parts.
            (int[] map, int permutation, int signs) = FindPartMapping(source, target);
            Vector3[] orientedVertices = source.SelectMany(p => p.Vertices.Select(v => Orient(v, permutation, signs))).ToArray();
            Vector3 sourceCenter = Average(orientedVertices);
            Vector3 targetCenter = Average(targetGlobalVertices);
            float scale = GetHeight(targetGlobalVertices) / GetHeight(orientedVertices);
            for (int t = 0; t < target.Length; t++)
                target[t] = FitPartToTarget(source[map[t]], target[t], sourceCenter, targetCenter, scale, permutation, signs);
            ApplyParts(target);
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
            int vertexEnd = 8 + _vertices.Length * VertexStride;
            byte[] result = new byte[vertexEnd + _suffix.Length];
            BitConverter.GetBytes(_vertices.Length).CopyTo(result, 0);
            BitConverter.GetBytes(TriangleCount).CopyTo(result, 4);
            for (int i = 0; i < _vertices.Length; i++)
            {
                int o = 8 + i * VertexStride; Vector3 p = _vertices[i]; Vector3 n = _normals[i]; Vector2 t = _uv[i];
                WriteFloat(result, o, p.X); WriteFloat(result, o + 4, p.Y); WriteFloat(result, o + 8, p.Z);
                WriteFloat(result, o + 12, n.X); WriteFloat(result, o + 16, n.Y); WriteFloat(result, o + 20, n.Z);
                WriteFloat(result, o + 28, t.X / Math.Max(1, Texture.Width - 1)); WriteFloat(result, o + 32, t.Y / Math.Max(1, Texture.Height - 1));
            }
            _suffix.CopyTo(result, vertexEnd);
            int groupTable = vertexEnd + (_originalGroupCountOffset - _originalVertexEnd) + 4;
            for (int i = 0; i < _groups.Length; i++)
            {
                BitConverter.GetBytes(_groups[i].TriangleStart).CopyTo(result, groupTable + i * 32 + 8);
                BitConverter.GetBytes(_groups[i].TriangleCount).CopyTo(result, groupTable + i * 32 + 12);
            }
            return result;
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

    private sealed record RenderFace(int[] Indices, Vector2[] UV, byte Page, int Clut);
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
                Color flat = Color.FromArgb(255, (int)(fallback.R * light), (int)(fallback.G * light), (int)(fallback.B * light));

                Color color = flat;
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

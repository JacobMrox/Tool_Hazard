using System.Globalization;
using System.Xml.Linq;

namespace Tool_Hazard.Biohazard
{
    public partial class RebirthBgmXmlEditorForm : Form
    {
        private XDocument? _doc;
        private string? _path;

        private readonly List<TracksBlock> _blocks = new();
        private readonly List<RowBinding> _rows = new();

        private sealed class TracksBlock
        {
            public int Id;
            public string SectionName = "";
            public XElement SectionElement = null!;
            public XElement TracksElement = null!;
            public int OrdinalInSection;
            public XComment? LabelComment;

            public string Label => LabelComment == null ? "" : (LabelComment.Value ?? "").Trim();

            public string Display
            {
                get
                {
                    var c = Label;
                    if (!string.IsNullOrEmpty(c)) return c;
                    return "<no comment>"; // instead of #xx
                }
            }

            public override string ToString() => Display;
        }

        private sealed class RowBinding
        {
            public XElement TrackElement = null!;
            public int BlockId;
        }

        public RebirthBgmXmlEditorForm()
        {
            InitializeComponent();

            // Allow typing a custom comment label into the combo (and also selecting predefined)
            cmbTracksBlock.DropDownStyle = ComboBoxStyle.DropDown;

            BuildGrid();
            WireEvents();
            WireFileButtons();
        }

        private int SampleRateHz => (int)nudSampleRate.Value;

        private void WireEvents()
        {
            btnAddTrackToGroup.Click += (_, __) => AddTrackSmart();
            btnAddRow.Click += (_, __) => AddTrackSmart();

            cmbSectionName.SelectedIndexChanged += (_, __) => RefreshTracksBlockCombo();

            gridTracks.CurrentCellDirtyStateChanged += (_, __) =>
            {
                if (gridTracks.IsCurrentCellDirty)
                    gridTracks.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            gridTracks.CellValueChanged += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                CommitGridRowToXml(gridTracks.Rows[e.RowIndex]);
            };
        }
        private void WireFileButtons()
        {
            btnLoadXml.Click += (_, __) => LoadXmlViaDialog();
            btnSaveXml.Click += (_, __) => SaveXmlViaDialog();
            btnReloadXml.Click += (_, __) => ReloadXml();
            btnDeleteRow.Click += (_, __) => DeleteSelectedRow();
        }
        // Load via dialog
        private void LoadXmlViaDialog()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Open bgm_attr.xml",
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                FileName = "bgm_attr.xml"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                LoadXml(ofd.FileName); // your existing method
                Text = $"Classic Rebirth BGM XML Editor - {Path.GetFileName(ofd.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to load XML:\n" + ex.Message, "BGM XML Editor",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // Save via dialog
        private void SaveXmlViaDialog()
        {
            if (_doc == null)
            {
                MessageBox.Show(this, "No XML loaded.", "BGM XML Editor",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Save bgm_attr.xml",
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                FileName = string.IsNullOrWhiteSpace(_path) ? "bgm_attr.xml" : Path.GetFileName(_path),
                InitialDirectory = string.IsNullOrWhiteSpace(_path) ? null : Path.GetDirectoryName(_path)
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                SaveXml(sfd.FileName); // your existing method
                _path = sfd.FileName;
                Text = $"Classic Rebirth BGM XML Editor - {Path.GetFileName(_path)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to save XML:\n" + ex.Message, "BGM XML Editor",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // Reload from disk
        private void ReloadXml()
        {
            if (string.IsNullOrWhiteSpace(_path) || !File.Exists(_path))
            {
                MessageBox.Show(this, "No file path to reload (load an XML first).", "BGM XML Editor",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                LoadXml(_path); // re-load from disk and rebuild UI
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to reload XML:\n" + ex.Message, "BGM XML Editor",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //Delete row from both grid and XML
        private void DeleteSelectedRow()
        {
            if (_doc == null) return;
            if (gridTracks.SelectedRows.Count == 0) return;

            var row = gridTracks.SelectedRows[0];
            var idStr = row.Cells["colRowId"].Value?.ToString();
            if (!int.TryParse(idStr, out var rowId)) return;
            if (rowId < 0 || rowId >= _rows.Count) return;

            // Remove from XML
            var trackEl = _rows[rowId].TrackElement;
            trackEl.Remove();

            // Remove from grid
            gridTracks.Rows.Remove(row);

            // Note: rowId mapping becomes stale after deletes. Easiest fix: rebuild UI.
            RebuildModelAndUi();
        }

        private void BuildGrid()
        {
            gridTracks.AutoGenerateColumns = false;
            gridTracks.AllowUserToAddRows = false;
            gridTracks.AllowUserToDeleteRows = false;
            gridTracks.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridTracks.MultiSelect = false;
            gridTracks.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            gridTracks.Columns.Clear();

            gridTracks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSection", HeaderText = "Section", ReadOnly = true });
            gridTracks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTracksBlock", HeaderText = "Tracks Block (Comment)", ReadOnly = true });

            gridTracks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Track Name" });
            gridTracks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colIndex", HeaderText = "Index" });

            gridTracks.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colLoop", HeaderText = "Loop?" });
            gridTracks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStartSec", HeaderText = "Loop Start (sec)" });
            gridTracks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colEndSec", HeaderText = "Loop End (sec)" });

            gridTracks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRowId", Visible = false });
        }

        // Call this from outside or add menu item later
        public void LoadXml(string path)
        {
            _path = path;
            _doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            RebuildModelAndUi();
        }

        public void SaveXml(string? path = null)
        {
            if (_doc == null) return;

            gridTracks.EndEdit();

            var outPath = path ?? _path;
            if (string.IsNullOrWhiteSpace(outPath))
                throw new InvalidOperationException("No output path set.");

            _doc.Save(outPath);
        }

        private void RebuildModelAndUi()
        {
            if (_doc?.Root == null) return;

            BuildTracksBlocks();
            PopulateSectionCombo();
            RefreshTracksBlockCombo();
            PopulateGrid();
        }

        private void BuildTracksBlocks()
        {
            _blocks.Clear();
            int id = 0;

            foreach (var section in _doc!.Root!.Elements())
            {
                var sectionName = section.Name.LocalName;
                var tracksList = section.Elements("Tracks").ToList();

                for (int i = 0; i < tracksList.Count; i++)
                {
                    var tracksEl = tracksList[i];
                    var comment = FindCommentImmediatelyBefore(tracksEl);

                    _blocks.Add(new TracksBlock
                    {
                        Id = id++,
                        SectionName = sectionName,
                        SectionElement = section,
                        TracksElement = tracksEl,
                        OrdinalInSection = i,
                        LabelComment = comment
                    });
                }
            }
        }

        private void PopulateSectionCombo()
        {
            var sections = _blocks.Select(b => b.SectionName).Distinct().ToList();

            cmbSectionName.Items.Clear();
            foreach (var s in sections)
                cmbSectionName.Items.Add(s);

            if (cmbSectionName.Items.Count > 0 && cmbSectionName.SelectedIndex < 0)
                cmbSectionName.SelectedIndex = 0;
        }

        private void RefreshTracksBlockCombo()
        {
            cmbTracksBlock.Items.Clear();

            var section = cmbSectionName.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(section)) return;

            // Only show blocks that actually have a comment label
            foreach (var b in _blocks.Where(b => b.SectionName == section)
                                     .Where(b => !string.IsNullOrWhiteSpace(b.Label))
                                     .OrderBy(b => b.Label))
            {
                cmbTracksBlock.Items.Add(b.Label);
            }

            if (cmbTracksBlock.Items.Count > 0)
                cmbTracksBlock.SelectedIndex = 0;
        }

        // Smart add: add track to selected block, or create new block if none selected
        private void AddTrackSmart()
        {
            if (_doc?.Root == null) return;

            var sectionName = cmbSectionName.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(sectionName))
            {
                MessageBox.Show(this, "Select a section.");
                return;
            }

            var label = (cmbTracksBlock.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                MessageBox.Show(this, "Type or select a Tracks Block label (comment).");
                return;
            }

            var trackName = (txtNewGroupLabel.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(trackName))
            {
                MessageBox.Show(this, "Enter a track name.");
                return;
            }

            var sectionEl = _doc.Root.Elements().FirstOrDefault(e => e.Name.LocalName == sectionName);
            if (sectionEl == null)
            {
                MessageBox.Show(this, $"Section '{sectionName}' not found.");
                return;
            }

            // Find existing <Tracks> whose immediate previous non-whitespace node is <!-- label -->
            var tracksEl = FindTracksByCommentLabel(sectionEl, label);

            // If not found, create <!-- label --> <Tracks/> at end of section
            if (tracksEl == null)
            {
                sectionEl.Add(new XComment(" " + label + " "));
                tracksEl = new XElement("Tracks");
                sectionEl.Add(tracksEl);
            }

            // Add the new Track under that Tracks
            var newTrack = new XElement("Track",
                new XAttribute("name", trackName),
                new XAttribute("index", "0"),
                new XAttribute("has_loop", "0")
            );
            tracksEl.Add(newTrack);

            // Rebuild UI so dropdown + grid reflect new block/track
            RebuildModelAndUi();

            // Set combo boxes to what user used
            cmbSectionName.SelectedItem = sectionName;
            cmbTracksBlock.Text = label;
        }

        // Helper to find <Tracks> by its preceding comment label
        private static XElement? FindTracksByCommentLabel(XElement sectionEl, string label)
        {
            foreach (var tracks in sectionEl.Elements("Tracks"))
            {
                var c = FindCommentImmediatelyBefore(tracks);
                if (c != null && string.Equals(c.Value.Trim(), label, StringComparison.Ordinal))
                    return tracks;
            }
            return null;
        }

        private static XComment? FindCommentImmediatelyBefore(XElement tracksEl)
        {
            XNode? prev = tracksEl.PreviousNode;

            while (prev is XText t && string.IsNullOrWhiteSpace(t.Value))
                prev = prev.PreviousNode;

            return prev as XComment;
        }


        private void PopulateGrid()
        {
            gridTracks.Rows.Clear();
            _rows.Clear();

            foreach (var block in _blocks)
            {
                foreach (var track in block.TracksElement.Elements("Track"))
                    AddGridRow(block, track);
            }
        }

        private void AddGridRow(TracksBlock block, XElement track)
        {
            var name = (string?)track.Attribute("name") ?? "";
            var index = (string?)track.Attribute("index") ?? "0";
            var loop = ((string?)track.Attribute("has_loop") ?? "0") == "1";

            var ls = ParseLong(track, "l_start");
            var le = ParseLong(track, "l_end");

            var startSec = ls.HasValue ? SamplesToSeconds(ls.Value).ToString("0.###", CultureInfo.InvariantCulture) : "";
            var endSec = le.HasValue ? SamplesToSeconds(le.Value).ToString("0.###", CultureInfo.InvariantCulture) : "";

            var rowId = _rows.Count;
            _rows.Add(new RowBinding { TrackElement = track, BlockId = block.Id });

            gridTracks.Rows.Add(block.SectionName, block.Display, name, index, loop, startSec, endSec, rowId.ToString(CultureInfo.InvariantCulture));
        }

        private static long? ParseLong(XElement el, string attr)
        {
            var s = (string?)el.Attribute(attr);
            if (string.IsNullOrWhiteSpace(s)) return null;

            if (long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;

            return null;
        }

        private double SamplesToSeconds(long samples) => samples / (double)SampleRateHz;
        private long SecondsToSamples(double sec) => (long)Math.Round(sec * SampleRateHz);

        private TracksBlock? GetSelectedBlock()
        {
            return cmbTracksBlock.SelectedItem as TracksBlock;
        }

        private void AddTrackToSelectedBlock()
        {
            if (_doc?.Root == null) return;

            var block = GetSelectedBlock();
            if (block == null)
            {
                MessageBox.Show(this, "Select a Tracks block first.");
                return;
            }

            // txtNewGroupLabel is used as NEW TRACK NAME (your request)
            var trackName = (txtNewGroupLabel.Text ?? "").Trim();
            if (string.IsNullOrEmpty(trackName))
                trackName = "NEW_TRACK";

            var newTrack = new XElement("Track",
                new XAttribute("name", trackName),
                new XAttribute("index", "0"),
                new XAttribute("has_loop", "0")
            );

            block.TracksElement.Add(newTrack);

            AddGridRow(block, newTrack);

            gridTracks.ClearSelection();
            gridTracks.Rows[gridTracks.Rows.Count - 1].Selected = true;
            gridTracks.FirstDisplayedScrollingRowIndex = Math.Max(0, gridTracks.Rows.Count - 1);
        }

        // Applies/updates the XML comment immediately above the selected <Tracks>
        private void ApplyLabelToSelectedBlock()
        {
            if (_doc?.Root == null) return;

            var block = GetSelectedBlock();
            if (block == null)
            {
                MessageBox.Show(this, "Select a Tracks block first.");
                return;
            }

            // IMPORTANT: use what the user typed in the combo box
            var raw = (cmbTracksBlock.Text ?? "").Trim();
            var label = ExtractLabel(raw);

            // if empty -> remove comment (optional behavior)
            if (string.IsNullOrWhiteSpace(label))
            {
                if (block.LabelComment != null)
                {
                    block.LabelComment.Remove();
                    block.LabelComment = null;
                }
                RebuildModelAndUi();
                return;
            }

            // update or insert the comment immediately above this <Tracks>
            XNode? prev = block.TracksElement.PreviousNode;
            while (prev is XText t && string.IsNullOrWhiteSpace(t.Value))
                prev = prev.PreviousNode;

            if (prev is XComment c)
            {
                c.Value = " " + label + " ";
                block.LabelComment = c;
            }
            else
            {
                var nc = new XComment(" " + label + " ");
                block.TracksElement.AddBeforeSelf(nc);
                block.LabelComment = nc;
            }

            // rebuild and keep selection on the same physical TracksElement
            var tracksRef = block.TracksElement;
            RebuildModelAndUi();

            // find the rebuilt block by reference (best effort: match by section+ordinal)
            var sectionName = block.SectionName;
            var ordinal = block.OrdinalInSection;
            var rebuilt = _blocks.FirstOrDefault(b => b.SectionName == sectionName && b.OrdinalInSection == ordinal);
            if (rebuilt != null)
                cmbTracksBlock.SelectedItem = rebuilt;
        }
        private static string ExtractLabel(string raw)
        {
            int lp = raw.IndexOf('(');
            int rp = raw.LastIndexOf(')');
            if (lp >= 0 && rp > lp)
                return raw.Substring(lp + 1, rp - lp - 1).Trim();

            // If user typed only "#12" treat as empty
            if (raw.StartsWith("#") && raw.All(ch => ch == '#' || char.IsDigit(ch) || char.IsWhiteSpace(ch)))
                return "";

            return raw.Trim();
        }

        private void NewTracksBlockInSelectedSection()
        {
            if (_doc?.Root == null) return;

            var sectionName = cmbSectionName.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(sectionName))
            {
                MessageBox.Show(this, "Select a section first.");
                return;
            }

            var section = _doc.Root.Elements().FirstOrDefault(e => e.Name.LocalName == sectionName);
            if (section == null)
            {
                MessageBox.Show(this, $"Section '{sectionName}' not found.");
                return;
            }

            // label comes from whatever user typed in the TracksBlock combo
            // (since you set DropDownStyle=DropDown)
            var label = ExtractLabel((cmbTracksBlock.Text ?? "").Trim());

            if (string.IsNullOrWhiteSpace(label))
            {
                // auto label if user didn't type one
                // e.g. CUSTOM_00, CUSTOM_01 ...
                var existing = _blocks.Where(b => b.SectionName == sectionName)
                                      .Select(b => b.Label)
                                      .Where(x => x.StartsWith("CUSTOM_", StringComparison.OrdinalIgnoreCase))
                                      .Count();
                label = $"CUSTOM_{existing:00}";
            }

            // Insert: <!-- label --> <Tracks/>
            section.Add(new XComment(" " + label + " "));
            section.Add(new XElement("Tracks"));

            RebuildModelAndUi();

            // select the new block in the combo (last block in that section)
            var last = _blocks.Where(b => b.SectionName == sectionName).OrderBy(b => b.OrdinalInSection).LastOrDefault();
            if (last != null)
                cmbTracksBlock.SelectedItem = last;
        }

        private void CommitGridRowToXml(DataGridViewRow row)
        {
            if (_doc?.Root == null) return;
            if (row.IsNewRow) return;

            var idStr = row.Cells["colRowId"].Value?.ToString();
            if (!int.TryParse(idStr, out var rowId)) return;
            if (rowId < 0 || rowId >= _rows.Count) return;

            var track = _rows[rowId].TrackElement;

            var name = (row.Cells["colName"].Value?.ToString() ?? "").Trim();
            var indexStr = (row.Cells["colIndex"].Value?.ToString() ?? "0").Trim();

            bool loop = row.Cells["colLoop"].Value is bool b && b;

            var startStr = (row.Cells["colStartSec"].Value?.ToString() ?? "").Trim().Replace(',', '.');
            var endStr = (row.Cells["colEndSec"].Value?.ToString() ?? "").Trim().Replace(',', '.');

            track.SetAttributeValue("name", name);
            track.SetAttributeValue("index", SafeInt(indexStr, 0).ToString(CultureInfo.InvariantCulture));
            track.SetAttributeValue("has_loop", loop ? "1" : "0");

            if (loop)
            {
                double.TryParse(startStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var startSec);
                double.TryParse(endStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var endSec);

                startSec = Math.Max(0, startSec);
                endSec = Math.Max(0, endSec);

                var ls = SecondsToSamples(startSec);
                var le = SecondsToSamples(endSec);

                if (le < ls) { var tmp = ls; ls = le; le = tmp; }

                track.SetAttributeValue("l_start", ls.ToString(CultureInfo.InvariantCulture));
                track.SetAttributeValue("l_end", le.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                track.Attribute("l_start")?.Remove();
                track.Attribute("l_end")?.Remove();
            }
        }

        private static int SafeInt(string s, int fallback)
        {
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;
            return fallback;
        }
    }
}

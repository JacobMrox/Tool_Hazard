using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Tool_Hazard.Biohazard
{
    public partial class RebirthBgmXmlEditorForm : Form
    {
        private string? _currentPath;
        private XDocument? _doc;

        // Track model in the grid (represents one <Track/> element + where it lives)
        private sealed class TrackRow
        {
            public XElement TrackElement { get; }
            public string GroupPath { get; } // e.g. "Main / MAIN00", "SBgm0 / SBGM0_0C"

            public TrackRow(XElement trackElement, string groupPath)
            {
                TrackElement = trackElement;
                GroupPath = groupPath;
            }
        }

        // We store per-row metadata here so we can write back to the exact XML element.
        private readonly List<TrackRow> _rows = new();
        public RebirthBgmXmlEditorForm()
        {
            InitializeComponent();
            BuildGrid();
            WireButtons();
        }

        private void WireButtons()
        {
            btnLoadXml.Click += (_, __) => LoadXml();
            btnSaveXml.Click += (_, __) => SaveXmlAs(_currentPath ?? "");
            btnAddRow.Click += (_, __) => AddNewRow();
            btnDeleteRow.Click += (_, __) => DeleteSelectedRow();
            if (btnReloadXml != null)
                btnReloadXml.Click += (_, __) => ReloadXml();
        }

        private void BuildGrid()
        {
            gridTracks.AutoGenerateColumns = false;
            gridTracks.AllowUserToAddRows = true;
            gridTracks.AllowUserToDeleteRows = true;
            gridTracks.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridTracks.MultiSelect = false;
            gridTracks.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            gridTracks.Columns.Clear();

            // GroupPath (read-only): helps user know which slot this belongs to
            gridTracks.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colGroup",
                HeaderText = "Slot",
                ReadOnly = true
            });

            gridTracks.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "Track Name"
            });

            gridTracks.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colIndex",
                HeaderText = "Index"
            });

            gridTracks.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "colLoop",
                HeaderText = "Loop?"
            });

            gridTracks.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colLoopStartSec",
                HeaderText = "Loop Start (sec)"
            });

            gridTracks.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colLoopEndSec",
                HeaderText = "Loop End (sec)"
            });

            // Hidden column to map grid row -> _rows index
            gridTracks.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colRowId",
                HeaderText = "RowId",
                Visible = false
            });

            gridTracks.CellEndEdit += (_, e) =>
            {
                // Optional: basic sanitization for numeric cells
                if (e.RowIndex < 0) return;
                NormalizeRow(gridTracks.Rows[e.RowIndex]);
            };
        }

        private void NormalizeRow(DataGridViewRow row)
        {
            // Index: keep it integer if possible
            var idxObj = row.Cells["colIndex"].Value;
            if (idxObj != null)
            {
                var s = idxObj.ToString()?.Trim();
                if (!string.IsNullOrEmpty(s))
                {
                    if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
                        row.Cells["colIndex"].Value = iv.ToString(CultureInfo.InvariantCulture);
                }
            }

            // Loop seconds: normalize decimal formatting
            NormalizeDecimalCell(row.Cells["colLoopStartSec"]);
            NormalizeDecimalCell(row.Cells["colLoopEndSec"]);
        }

        private static void NormalizeDecimalCell(DataGridViewCell cell)
        {
            var s = cell.Value?.ToString()?.Trim();
            if (string.IsNullOrEmpty(s)) return;

            // Accept both comma and dot from user input
            s = s.Replace(',', '.');

            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv))
                cell.Value = dv.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private int SampleRate => (int)nudSampleRate.Value;

        private void LoadXml()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Open bgm_attr.xml",
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                FileName = "bgm_attr.xml"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            _currentPath = ofd.FileName;
            LoadXmlFromPath(_currentPath);
        }

        private void ReloadXml()
        {
            if (string.IsNullOrWhiteSpace(_currentPath) || !File.Exists(_currentPath))
                return;

            LoadXmlFromPath(_currentPath);
        }

        private void LoadXmlFromPath(string path)
        {
            try
            {
                _doc = XDocument.Load(path, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to load XML:\n" + ex.Message, "BGM XML Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            PopulateGridFromDoc();
            Text = $"BGM XML Editor - {Path.GetFileName(path)}";
        }

        private void PopulateGridFromDoc()
        {
            gridTracks.Rows.Clear();
            _rows.Clear();

            if (_doc?.Root == null) return;

            // Expected root: <BgmAttributes>
            // Sections: <Main>, <SBgm0>, <SBgm1>, ...
            foreach (var section in _doc.Root.Elements())
            {
                var sectionName = section.Name.LocalName; // "Main", "SBgm0", ...

                // Each slot is represented by a <Tracks> element (many of them)
                // The slot name isn't an element; it's a comment above. We can't reliably bind to comments,
                // so we number them and show "Tracks #XX" unless you later add explicit ids.
                var tracksNodes = section.Elements("Tracks").ToList();

                for (int i = 0; i < tracksNodes.Count; i++)
                {
                    var tracksNode = tracksNodes[i];

                    // Try to find the nearest preceding comment to label slot (best-effort)
                    var slotLabel = TryGetPreviousCommentLabel(tracksNode) ?? $"Tracks_{i:00}";
                    var groupPath = $"{sectionName} / {slotLabel}";

                    // If <Tracks/> is empty, still show a placeholder row? Usually no.
                    foreach (var track in tracksNode.Elements("Track"))
                    {
                        AddGridRowForTrack(track, groupPath);
                    }
                }
            }
        }

        private static string? TryGetPreviousCommentLabel(XElement tracksNode)
        {
            // Best effort: scan siblings backwards for the closest XComment like " MAIN00 "
            var prev = tracksNode.PreviousNode;
            while (prev != null)
            {
                if (prev is XComment c)
                {
                    var t = (c.Value ?? "").Trim();
                    if (!string.IsNullOrEmpty(t)) return t;
                }
                // stop if we hit another Tracks node or element boundary
                if (prev is XElement) break;
                prev = prev.PreviousNode;
            }
            return null;
        }

        private void AddGridRowForTrack(XElement track, string groupPath)
        {
            var name = (string?)track.Attribute("name") ?? "";
            var index = (string?)track.Attribute("index") ?? "0";
            var hasLoop = ((string?)track.Attribute("has_loop") ?? "0") == "1";

            var lStart = ParseLongAttr(track, "l_start");
            var lEnd = ParseLongAttr(track, "l_end");

            // Convert to seconds (if not present, show blank)
            string startSec = lStart.HasValue ? SamplesToSeconds(lStart.Value).ToString("0.###", CultureInfo.InvariantCulture) : "";
            string endSec = lEnd.HasValue ? SamplesToSeconds(lEnd.Value).ToString("0.###", CultureInfo.InvariantCulture) : "";

            var rowId = _rows.Count;
            _rows.Add(new TrackRow(track, groupPath));

            var gridRowIndex = gridTracks.Rows.Add(groupPath, name, index, hasLoop, startSec, endSec, rowId.ToString(CultureInfo.InvariantCulture));
            NormalizeRow(gridTracks.Rows[gridRowIndex]);
        }

        private static long? ParseLongAttr(XElement el, string attrName)
        {
            var s = (string?)el.Attribute(attrName);
            if (string.IsNullOrWhiteSpace(s)) return null;

            if (long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;

            return null;
        }

        private double SamplesToSeconds(long samples)
        {
            if (SampleRate <= 0) return 0;
            return samples / (double)SampleRate;
        }

        private long SecondsToSamples(double seconds)
        {
            if (SampleRate <= 0) return 0;
            // Round to nearest sample frame
            return (long)Math.Round(seconds * SampleRate);
        }

        private void AddNewRow()
        {
            // Add a grid-only row; user will fill values.
            // This won't be written back until you decide where in XML to put it.
            // For now, we require the user edits an existing track row (safe), OR you can add logic to insert into a chosen Tracks node.
            MessageBox.Show(
                this,
                "Add Track creates a new grid row, but inserting brand-new <Track> nodes requires choosing a destination <Tracks> slot.\n\n" +
                "For safety, edit existing rows for now.\n\n" +
                "If you want full insert support, tell me how you want to pick the destination (e.g., dropdown of slots).",
                "BGM XML Editor",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void DeleteSelectedRow()
        {
            if (gridTracks.SelectedRows.Count == 0) return;

            var row = gridTracks.SelectedRows[0];
            var rowIdStr = row.Cells["colRowId"].Value?.ToString();
            if (!int.TryParse(rowIdStr, out var rowId)) return;
            if (rowId < 0 || rowId >= _rows.Count) return;

            var model = _rows[rowId];
            model.TrackElement.Remove(); // remove from XML

            gridTracks.Rows.Remove(row);
        }

        private void SaveXmlAs(string currentPath)
        {
            if (_doc == null)
            {
                MessageBox.Show(this, "No XML loaded.", "BGM XML Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Commit grid edits into XML elements
            if (!CommitGridToXml())
                return;

            string savePath = currentPath;
            if (string.IsNullOrWhiteSpace(savePath))
            {
                using var sfd = new SaveFileDialog
                {
                    Title = "Save bgm_attr.xml",
                    Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                    FileName = "bgm_attr.xml"
                };

                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return;

                savePath = sfd.FileName;
            }
            else
            {
                // Ask where to save
                using var sfd = new SaveFileDialog
                {
                    Title = "Save bgm_attr.xml",
                    Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                    FileName = Path.GetFileName(savePath),
                    InitialDirectory = Path.GetDirectoryName(savePath)
                };

                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return;

                savePath = sfd.FileName;
            }

            try
            {
                _doc.Save(savePath);
                MessageBox.Show(this, "Saved:\n" + savePath, "BGM XML Editor", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to save XML:\n" + ex.Message, "BGM XML Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool CommitGridToXml()
        {
            // Make sure edits are committed from grid UI
            gridTracks.EndEdit();

            foreach (DataGridViewRow gridRow in gridTracks.Rows)
            {
                if (gridRow.IsNewRow) continue;

                var rowIdStr = gridRow.Cells["colRowId"].Value?.ToString();
                if (!int.TryParse(rowIdStr, out var rowId)) continue;
                if (rowId < 0 || rowId >= _rows.Count) continue;

                var model = _rows[rowId];
                var trackEl = model.TrackElement;

                var name = (gridRow.Cells["colName"].Value?.ToString() ?? "").Trim();
                var indexStr = (gridRow.Cells["colIndex"].Value?.ToString() ?? "0").Trim();
                var loopObj = gridRow.Cells["colLoop"].Value;

                bool hasLoop = false;
                if (loopObj is bool b) hasLoop = b;
                else if (loopObj != null && bool.TryParse(loopObj.ToString(), out var pb)) hasLoop = pb;

                // Parse numeric fields (seconds)
                var startSecStr = (gridRow.Cells["colLoopStartSec"].Value?.ToString() ?? "").Trim().Replace(',', '.');
                var endSecStr = (gridRow.Cells["colLoopEndSec"].Value?.ToString() ?? "").Trim().Replace(',', '.');

                double? startSec = null;
                double? endSec = null;

                if (!string.IsNullOrWhiteSpace(startSecStr))
                {
                    if (!double.TryParse(startSecStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    {
                        MessageBox.Show(this, $"Invalid Loop Start seconds: '{startSecStr}'", "BGM XML Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                    if (v < 0) v = 0;
                    startSec = v;
                }

                if (!string.IsNullOrWhiteSpace(endSecStr))
                {
                    if (!double.TryParse(endSecStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    {
                        MessageBox.Show(this, $"Invalid Loop End seconds: '{endSecStr}'", "BGM XML Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                    if (v < 0) v = 0;
                    endSec = v;
                }

                // Write attributes back
                trackEl.SetAttributeValue("name", name);
                trackEl.SetAttributeValue("index", SafeInt(indexStr, 0).ToString(CultureInfo.InvariantCulture));
                trackEl.SetAttributeValue("has_loop", hasLoop ? "1" : "0");

                if (hasLoop)
                {
                    // If user leaves blanks, default to 0
                    var ls = SecondsToSamples(startSec ?? 0);
                    var le = SecondsToSamples(endSec ?? 0);

                    // Guard: if both provided and end < start, clamp or swap
                    if (endSec.HasValue && startSec.HasValue && le < ls)
                    {
                        // swap
                        var tmp = ls; ls = le; le = tmp;
                    }

                    trackEl.SetAttributeValue("l_start", ls.ToString(CultureInfo.InvariantCulture));
                    trackEl.SetAttributeValue("l_end", le.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    // Match the style in your XML: loop disabled tracks omit l_start/l_end
                    trackEl.Attribute("l_start")?.Remove();
                    trackEl.Attribute("l_end")?.Remove();
                }
            }

            return true;
        }

        private static int SafeInt(string s, int fallback)
        {
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;
            return fallback;
        }
    }
}
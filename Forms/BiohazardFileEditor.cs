// Tool_Hazard/Forms/BiohazardFileEditor.cs
#nullable enable
using Tool_Hazard.Biohazard.FileEditor;

namespace Tool_Hazard.Forms
{
    public partial class BiohazardFileEditor : Form
    {
        private FileEncoding? _enc;
        private FileFontRenderer? _renderer;
        private FileProject? _project;
        private string? _projectPath;
        private bool _suppressTextEvents;

        public BiohazardFileEditor()
        {
            InitializeComponent();

            // If you didn’t wire these in designer, this makes it “just work”.
            Load += BiohazardFileEditor_Load;
            FormClosed += BiohazardFileEditor_FormClosed;

            // Assumes you used these exact names:
            // gridPages (DataGridView), txtPage (RichTextBox), picPreview (PictureBox)
            // openProjectDialog, saveProjectDialog, exportDialog (dialogs)
            gridPages.SelectionChanged += gridPages_SelectionChanged;
            txtPage.TextChanged += txtPage_TextChanged;
        }

        private void BiohazardFileEditor_Load(object? sender, EventArgs e)
        {
            try
            {
                // You can change this to wherever you keep your resources.
                // By default, it expects encoding.xml + font.png next to the EXE.
                var baseDir = AppContext.BaseDirectory;

                var encodingPath = Path.Combine(baseDir, "Resources/Biohazard/encoding_file.xml");
                var fontPath = Path.Combine(baseDir, "Resources/Biohazard/font_file.png");

                _enc = FileEncoding.Load(encodingPath);
                _renderer = new FileFontRenderer(_enc);
                _renderer.LoadFont(fontPath);

                // Setup grid
                gridPages.AutoGenerateColumns = false;
                gridPages.AllowUserToAddRows = false;
                gridPages.AllowUserToDeleteRows = false;
                gridPages.MultiSelect = false;
                gridPages.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                gridPages.RowHeadersVisible = false;

                if (gridPages.Columns.Count == 0)
                {
                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "colText",
                        HeaderText = "Pages",
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                        ReadOnly = true
                    };
                    gridPages.Columns.Add(col);
                }

                // Good defaults
                picPreview.SizeMode = PictureBoxSizeMode.Normal;

                SetUiEnabled(false);
                SetStatus("Ready. Click New or Open.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Failed to initialize File Editor.\n\n" + ex.Message,
                    "File Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                SetUiEnabled(false);
            }
        }

        private void BiohazardFileEditor_FormClosed(object? sender, FormClosedEventArgs e)
        {
            DisposePreviewImage();
            _renderer?.Dispose();
            _renderer = null;
            _enc = null;
            _project = null;
        }

        // --------------------------------------------------------------------
        // Events
        // --------------------------------------------------------------------

        private void gridPages_SelectionChanged(object? sender, EventArgs e)
        {
            if (_project is null) return;
            if (_suppressTextEvents) return;

            var idx = GetSelectedIndex();
            if (idx < 0 || idx >= _project.Pages.Count)
                return;

            _suppressTextEvents = true;
            try
            {
                txtPage.Text = _project.Pages[idx];
                RenderPreviewForCurrentText();
            }
            finally
            {
                _suppressTextEvents = false;
            }
        }

        private void txtPage_TextChanged(object? sender, EventArgs e)
        {
            if (_project is null) return;
            if (_suppressTextEvents) return;

            var idx = GetSelectedIndex();
            if (idx < 0 || idx >= _project.Pages.Count)
                return;

            _project.Pages[idx] = txtPage.Text;

            // Update grid cell text
            if (idx < gridPages.Rows.Count)
                gridPages.Rows[idx].Cells[0].Value = MakeGridPreview(txtPage.Text);

            RenderPreviewForCurrentText();
        }

        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        private void RefreshGridFromProject(int selectIndex)
        {
            if (_project is null)
                return;

            _suppressTextEvents = true;
            try
            {
                gridPages.Rows.Clear();
                foreach (var p in _project.Pages)
                    gridPages.Rows.Add(MakeGridPreview(p));

                if (gridPages.Rows.Count > 0)
                {
                    selectIndex = Math.Clamp(selectIndex, 0, gridPages.Rows.Count - 1);
                    gridPages.ClearSelection();
                    gridPages.Rows[selectIndex].Selected = true;
                    gridPages.CurrentCell = gridPages.Rows[selectIndex].Cells[0];

                    txtPage.Text = _project.Pages[selectIndex];
                    RenderPreviewForCurrentText();
                }
                else
                {
                    txtPage.Text = "";
                    DisposePreviewImage();
                }
            }
            finally
            {
                _suppressTextEvents = false;
            }
        }

        private void RenderPreviewForCurrentText()
        {
            if (_renderer is null)
                return;

            DisposePreviewImage();
            picPreview.Image = _renderer.RenderPreviewFrame(txtPage.Text);
        }

        private void DisposePreviewImage()
        {
            if (picPreview.Image is Image old)
            {
                picPreview.Image = null;
                old.Dispose();
            }
        }

        private int GetSelectedIndex()
        {
            if (gridPages.CurrentRow != null)
                return gridPages.CurrentRow.Index;

            if (gridPages.SelectedRows.Count > 0)
                return gridPages.SelectedRows[0].Index;

            return -1;
        }

        private void SetUiEnabled(bool enabled)
        {
            // Required buttons: btnSave, btnSaveAs, btnExport, btnAddPage, btnDeletePage, btnMoveUp, btnMoveDown
            saveToolStripMenuItem.Enabled = enabled;
            saveAsToolStripMenuItem.Enabled = enabled;
            exportToolStripMenuItem.Enabled = enabled;

            addPageToolStripMenuItem.Enabled = enabled;
            deletePageToolStripMenuItem.Enabled = enabled;
            moveUpToolStripMenuItem.Enabled = enabled;
            moveDownToolStripMenuItem.Enabled = enabled;

            gridPages.Enabled = enabled;
            txtPage.Enabled = enabled;
        }

        private void SetStatus(string text)
        {
            Text = $"Biohazard File Editor - {text}";
            lblStatus.Text = $"Biohazard File Editor - {text}";
        }

        private static string MakeGridPreview(string s)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            // Make it one-line and not too long.
            var oneLine = s.Replace("\r", "").Replace("\n", " ⏎ ");
            if (oneLine.Length > 80) oneLine = oneLine.Substring(0, 80) + "…";
            return oneLine;
        }

        // --------------------------------------------------------------------
        // Buttons
        // --------------------------------------------------------------------

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Open code here
            using var dlg = new OpenFileDialog
            {
                Filter = "XML project (*.xml)|*.xml|All files (*.*)|*.*",
                Title = "Open File Editor Project",
                FileName = ""
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                _project = FileProject.Load(dlg.FileName);
                _projectPath = dlg.FileName;

                RefreshGridFromProject(selectIndex: 0);
                SetUiEnabled(true);
                SetStatus("Opened: " + Path.GetFileName(_projectPath));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Failed to open project.\n\n" + ex.Message,
                    "Open",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Save code here
            if (_project is null)
                return;

            if (string.IsNullOrWhiteSpace(_projectPath))
            {
                //btnSaveAs_Click(sender, e);
                return;
            }

            try
            {
                _project.Save(_projectPath);
                SetStatus("Saved: " + Path.GetFileName(_projectPath));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Failed to save project.\n\n" + ex.Message,
                    "Save",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Save As code here
            if (_project is null)
                return;

            using var dlg = new SaveFileDialog
            {
                Filter = "XML project (*.xml)|*.xml|All files (*.*)|*.*",
                Title = "Save File Editor Project As",
                FileName = string.IsNullOrWhiteSpace(_projectPath) ? "project.xml" : Path.GetFileName(_projectPath),
                OverwritePrompt = true
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                _project.Save(dlg.FileName);
                _projectPath = dlg.FileName;
                SetStatus("Saved: " + Path.GetFileName(_projectPath));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Failed to save project.\n\n" + ex.Message,
                    "Save As",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Export code here
            if (_project is null || _renderer is null)
                return;

            using var dlg = new SaveFileDialog
            {
                Filter = "PNG image (*.png)|*.png|All files (*.*)|*.*",
                Title = "Export Pages (Base Name)",
                FileName = "FILE.png",
                OverwritePrompt = true
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var chosen = dlg.FileName;

                // base path without extension, renderer appends 00/01 etc + .png
                var baseNoExt = Path.Combine(
                    Path.GetDirectoryName(chosen) ?? "",
                    Path.GetFileNameWithoutExtension(chosen));

                _renderer.ExportSequence(baseNoExt, _project.Pages.ToArray());
                SetStatus("Exported: " + Path.GetFileName(baseNoExt) + "00.png ...");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Failed to export.\n\n" + ex.Message,
                    "Export",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void addPageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Add Page code here
            if (_project is null)
                return;

            _project.Pages.Add("{center}New Page");
            RefreshGridFromProject(selectIndex: _project.Pages.Count - 1);
            SetStatus("Page added.");
        }

        private void deletePageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Delete Page code here
            if (_project is null)
                return;

            var idx = GetSelectedIndex();
            if (idx < 0)
                return;

            if (_project.Pages.Count <= 1)
            {
                MessageBox.Show(this,
                    "You must have at least one page.",
                    "Delete Page",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var res = MessageBox.Show(this,
                "Delete selected page?",
                "Delete Page",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res != DialogResult.Yes)
                return;

            _project.Pages.RemoveAt(idx);

            var newIdx = Math.Min(idx, _project.Pages.Count - 1);
            RefreshGridFromProject(selectIndex: newIdx);
            SetStatus("Page deleted.");
        }

        private void moveUpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Move up code here
            if (_project is null)
                return;

            var idx = GetSelectedIndex();
            if (idx <= 0)
                return;

            _project.MoveUp(idx);
            RefreshGridFromProject(selectIndex: idx - 1);
            SetStatus("Moved up.");
        }

        private void moveDownToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Move down code here
            if (_project is null)
                return;

            var idx = GetSelectedIndex();
            if (idx < 0 || idx >= _project.Pages.Count - 1)
                return;

            _project.MoveDown(idx);
            RefreshGridFromProject(selectIndex: idx + 1);
            SetStatus("Moved down.");
        }

        private void selectEncodingxmlToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Encoding XML (*.xml)|*.xml|All files (*.*)|*.*",
                Title = "Select encoding XML"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                _enc = FileEncoding.Load(dlg.FileName);

                // Recreate renderer because it holds the encoding reference
                _renderer?.Dispose();
                _renderer = new FileFontRenderer(_enc);

                // Reload current font from your default path OR ask user to pick it too.
                // If you want to keep the current font path, store it in a field.
                var baseDir = AppContext.BaseDirectory;
                var fontPath = Path.Combine(baseDir, "Resources/Biohazard/font_file.png");
                _renderer.LoadFont(fontPath);

                RenderPreviewForCurrentText();
                SetStatus("Encoding loaded: " + Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to load encoding.\n\n" + ex.Message,
                    "Encoding", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void selectFontpngToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_renderer is null)
                return;

            using var dlg = new OpenFileDialog
            {
                Filter = "Font PNG (*.png)|*.png|All files (*.*)|*.*",
                Title = "Select font PNG"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                _renderer.LoadFont(dlg.FileName);
                RenderPreviewForCurrentText();
                SetStatus("Font loaded: " + Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to load font.\n\n" + ex.Message,
                    "Font", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _project = FileProject.New();
            _projectPath = null;

            RefreshGridFromProject(selectIndex: 0);
            SetUiEnabled(true);
            SetStatus("New project created.");
        }
    }
}

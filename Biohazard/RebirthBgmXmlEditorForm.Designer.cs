namespace Tool_Hazard.Biohazard
{
    partial class RebirthBgmXmlEditorForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        public void InitializeComponent()
        {
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            btnLoadXml = new ToolStripMenuItem();
            btnSaveXml = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            btnAddRow = new ToolStripMenuItem();
            btnDeleteRow = new ToolStripMenuItem();
            btnReloadXml = new ToolStripMenuItem();
            lblSampleRate = new Label();
            nudSampleRate = new NumericUpDown();
            gridTracks = new DataGridView();
            cmbSectionName = new ComboBox();
            btnAddTrackToGroup = new Button();
            txtNewGroupLabel = new TextBox();
            cmbTracksBlock = new ComboBox();
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)nudSampleRate).BeginInit();
            ((System.ComponentModel.ISupportInitialize)gridTracks).BeginInit();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(800, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { btnLoadXml, btnSaveXml });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // btnLoadXml
            // 
            btnLoadXml.Name = "btnLoadXml";
            btnLoadXml.Size = new Size(127, 22);
            btnLoadXml.Text = "Load XML";
            // 
            // btnSaveXml
            // 
            btnSaveXml.Name = "btnSaveXml";
            btnSaveXml.Size = new Size(127, 22);
            btnSaveXml.Text = "Save XML";
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { btnAddRow, btnDeleteRow, btnReloadXml });
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            editToolStripMenuItem.Size = new Size(39, 20);
            editToolStripMenuItem.Text = "Edit";
            // 
            // btnAddRow
            // 
            btnAddRow.Name = "btnAddRow";
            btnAddRow.Size = new Size(180, 22);
            btnAddRow.Text = "Add Track";
            // 
            // btnDeleteRow
            // 
            btnDeleteRow.Name = "btnDeleteRow";
            btnDeleteRow.Size = new Size(180, 22);
            btnDeleteRow.Text = "Delete Selected";
            // 
            // btnReloadXml
            // 
            btnReloadXml.Name = "btnReloadXml";
            btnReloadXml.Size = new Size(180, 22);
            btnReloadXml.Text = "Reload";
            // 
            // lblSampleRate
            // 
            lblSampleRate.AutoSize = true;
            lblSampleRate.Location = new Point(668, 432);
            lblSampleRate.Name = "lblSampleRate";
            lblSampleRate.Size = new Size(97, 15);
            lblSampleRate.TabIndex = 1;
            lblSampleRate.Text = "Sample rate (Hz):";
            // 
            // nudSampleRate
            // 
            nudSampleRate.Increment = new decimal(new int[] { 100, 0, 0, 0 });
            nudSampleRate.Location = new Point(668, 455);
            nudSampleRate.Maximum = new decimal(new int[] { 192000, 0, 0, 0 });
            nudSampleRate.Minimum = new decimal(new int[] { 8000, 0, 0, 0 });
            nudSampleRate.Name = "nudSampleRate";
            nudSampleRate.Size = new Size(120, 23);
            nudSampleRate.TabIndex = 2;
            nudSampleRate.Value = new decimal(new int[] { 44100, 0, 0, 0 });
            // 
            // gridTracks
            // 
            gridTracks.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridTracks.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridTracks.Dock = DockStyle.Top;
            gridTracks.Location = new Point(0, 24);
            gridTracks.MultiSelect = false;
            gridTracks.Name = "gridTracks";
            gridTracks.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridTracks.Size = new Size(800, 394);
            gridTracks.TabIndex = 3;
            // 
            // cmbSectionName
            // 
            cmbSectionName.FormattingEnabled = true;
            cmbSectionName.Items.AddRange(new object[] { "Main", "SBgm0", "SBgm1" });
            cmbSectionName.Location = new Point(6, 455);
            cmbSectionName.Name = "cmbSectionName";
            cmbSectionName.Size = new Size(121, 23);
            cmbSectionName.TabIndex = 4;
            cmbSectionName.Text = "<Section Name>";
            // 
            // btnAddTrackToGroup
            // 
            btnAddTrackToGroup.Location = new Point(537, 432);
            btnAddTrackToGroup.Name = "btnAddTrackToGroup";
            btnAddTrackToGroup.Size = new Size(125, 46);
            btnAddTrackToGroup.TabIndex = 5;
            btnAddTrackToGroup.Text = "Add Track to Block";
            btnAddTrackToGroup.UseVisualStyleBackColor = true;
            // 
            // txtNewGroupLabel
            // 
            txtNewGroupLabel.Location = new Point(328, 455);
            txtNewGroupLabel.Name = "txtNewGroupLabel";
            txtNewGroupLabel.Size = new Size(203, 23);
            txtNewGroupLabel.TabIndex = 7;
            txtNewGroupLabel.Text = "e.g. MAIN40";
            // 
            // cmbTracksBlock
            // 
            cmbTracksBlock.FormattingEnabled = true;
            cmbTracksBlock.Location = new Point(133, 455);
            cmbTracksBlock.Name = "cmbTracksBlock";
            cmbTracksBlock.Size = new Size(189, 23);
            cmbTracksBlock.TabIndex = 8;
            cmbTracksBlock.Text = "<Tracks Block Label>";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(328, 432);
            label1.Name = "label1";
            label1.Size = new Size(71, 15);
            label1.TabIndex = 9;
            label1.Text = "Track name:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(133, 432);
            label2.Name = "label2";
            label2.Size = new Size(140, 15);
            label2.TabIndex = 10;
            label2.Text = "Tracks Block (Comment):";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(6, 432);
            label3.Name = "label3";
            label3.Size = new Size(84, 15);
            label3.TabIndex = 11;
            label3.Text = "Section Name:";
            // 
            // RebirthBgmXmlEditorForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 484);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(cmbTracksBlock);
            Controls.Add(txtNewGroupLabel);
            Controls.Add(btnAddTrackToGroup);
            Controls.Add(cmbSectionName);
            Controls.Add(gridTracks);
            Controls.Add(nudSampleRate);
            Controls.Add(lblSampleRate);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            MaximizeBox = false;
            Name = "RebirthBgmXmlEditorForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Classic Rebirth BGM XML Editor";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)nudSampleRate).EndInit();
            ((System.ComponentModel.ISupportInitialize)gridTracks).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem loadXMLToolStripMenuItem;
        //private ToolStripMenuItem btnSaveXml;
        private Label lblSampleRate;
        private NumericUpDown nudSampleRate;
        private DataGridView gridTracks;
        private ToolStripMenuItem editToolStripMenuItem;
        private ToolStripMenuItem btnAddRow;
        private ToolStripMenuItem btnDeleteRow;
        private ToolStripMenuItem btnReloadXml;
        private ToolStripMenuItem btnLoadXml;
        private ToolStripMenuItem btnSaveXml;
        private ComboBox cmbSectionName;
        private Button btnAddTrackToGroup;
        private TextBox txtNewGroupLabel;
        private ComboBox cmbTracksBlock;
        private Label label1;
        private Label label2;
        private Label label3;
    }
}
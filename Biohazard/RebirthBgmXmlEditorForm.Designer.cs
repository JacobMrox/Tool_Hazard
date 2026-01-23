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
        private void InitializeComponent()
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
            lblSampleRate.Location = new Point(12, 426);
            lblSampleRate.Name = "lblSampleRate";
            lblSampleRate.Size = new Size(97, 15);
            lblSampleRate.TabIndex = 1;
            lblSampleRate.Text = "Sample rate (Hz):";
            // 
            // nudSampleRate
            // 
            nudSampleRate.Increment = new decimal(new int[] { 100, 0, 0, 0 });
            nudSampleRate.Location = new Point(115, 424);
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
            // RebirthBgmXmlEditorForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(gridTracks);
            Controls.Add(nudSampleRate);
            Controls.Add(lblSampleRate);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "RebirthBgmXmlEditorForm";
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
    }
}
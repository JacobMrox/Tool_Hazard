namespace Tool_Hazard.Forms
{
    partial class BssViewerForm
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
            MenuStrip = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            openBSSToolStripMenuItem = new ToolStripMenuItem();
            saveBSSAsToolStripMenuItem = new ToolStripMenuItem();
            exportImagePNGBMPToolStripMenuItem = new ToolStripMenuItem();
            importImagePNGBMPToolStripMenuItem = new ToolStripMenuItem();
            exitToolStripMenuItem = new ToolStripMenuItem();
            pictureBoxPreview = new PictureBox();
            nudWidth = new NumericUpDown();
            nudHeight = new NumericUpDown();
            nudQuant = new NumericUpDown();
            chkTreatAsType2 = new CheckBox();
            toolStripStatusLabel1 = new StatusStrip();
            toolStripStatusLabel2 = new ToolStripStatusLabel();
            progressBar1 = new ToolStripProgressBar();
            btnExportPng = new Button();
            btnImportReplace = new Button();
            btnReencode = new Button();
            btnNext = new Button();
            btnPrev = new Button();
            MenuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBoxPreview).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudWidth).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudHeight).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudQuant).BeginInit();
            toolStripStatusLabel1.SuspendLayout();
            SuspendLayout();
            // 
            // MenuStrip
            // 
            MenuStrip.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem });
            MenuStrip.Location = new Point(0, 0);
            MenuStrip.Name = "MenuStrip";
            MenuStrip.Size = new Size(507, 24);
            MenuStrip.TabIndex = 0;
            MenuStrip.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openBSSToolStripMenuItem, saveBSSAsToolStripMenuItem, exportImagePNGBMPToolStripMenuItem, importImagePNGBMPToolStripMenuItem, exitToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // openBSSToolStripMenuItem
            // 
            openBSSToolStripMenuItem.Name = "openBSSToolStripMenuItem";
            openBSSToolStripMenuItem.Size = new Size(220, 22);
            openBSSToolStripMenuItem.Text = "Open BSS...";
            openBSSToolStripMenuItem.Click += openBSSToolStripMenuItem_Click;
            // 
            // saveBSSAsToolStripMenuItem
            // 
            saveBSSAsToolStripMenuItem.Name = "saveBSSAsToolStripMenuItem";
            saveBSSAsToolStripMenuItem.Size = new Size(220, 22);
            saveBSSAsToolStripMenuItem.Text = "Save BSS As...";
            saveBSSAsToolStripMenuItem.Click += saveBSSAsToolStripMenuItem_Click;
            // 
            // exportImagePNGBMPToolStripMenuItem
            // 
            exportImagePNGBMPToolStripMenuItem.Name = "exportImagePNGBMPToolStripMenuItem";
            exportImagePNGBMPToolStripMenuItem.Size = new Size(220, 22);
            exportImagePNGBMPToolStripMenuItem.Text = "Export Image (PNG/BMP)...";
            exportImagePNGBMPToolStripMenuItem.Click += exportImagePNGBMPToolStripMenuItem_Click;
            // 
            // importImagePNGBMPToolStripMenuItem
            // 
            importImagePNGBMPToolStripMenuItem.Name = "importImagePNGBMPToolStripMenuItem";
            importImagePNGBMPToolStripMenuItem.Size = new Size(220, 22);
            importImagePNGBMPToolStripMenuItem.Text = "Import Image (PNG/BMP)...";
            importImagePNGBMPToolStripMenuItem.Click += importImagePNGBMPToolStripMenuItem_Click;
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(220, 22);
            exitToolStripMenuItem.Text = "Exit";
            // 
            // pictureBoxPreview
            // 
            pictureBoxPreview.Location = new Point(87, 46);
            pictureBoxPreview.Name = "pictureBoxPreview";
            pictureBoxPreview.Size = new Size(320, 240);
            pictureBoxPreview.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxPreview.TabIndex = 1;
            pictureBoxPreview.TabStop = false;
            // 
            // nudWidth
            // 
            nudWidth.Location = new Point(8, 337);
            nudWidth.Maximum = new decimal(new int[] { 320, 0, 0, 0 });
            nudWidth.Name = "nudWidth";
            nudWidth.Size = new Size(120, 23);
            nudWidth.TabIndex = 2;
            nudWidth.Value = new decimal(new int[] { 320, 0, 0, 0 });
            // 
            // nudHeight
            // 
            nudHeight.Location = new Point(134, 337);
            nudHeight.Maximum = new decimal(new int[] { 240, 0, 0, 0 });
            nudHeight.Name = "nudHeight";
            nudHeight.Size = new Size(120, 23);
            nudHeight.TabIndex = 3;
            nudHeight.Value = new decimal(new int[] { 240, 0, 0, 0 });
            // 
            // nudQuant
            // 
            nudQuant.Location = new Point(260, 337);
            nudQuant.Maximum = new decimal(new int[] { 63, 0, 0, 0 });
            nudQuant.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            nudQuant.Name = "nudQuant";
            nudQuant.Size = new Size(120, 23);
            nudQuant.TabIndex = 4;
            nudQuant.Value = new decimal(new int[] { 8, 0, 0, 0 });
            // 
            // chkTreatAsType2
            // 
            chkTreatAsType2.AutoSize = true;
            chkTreatAsType2.Location = new Point(392, 338);
            chkTreatAsType2.Name = "chkTreatAsType2";
            chkTreatAsType2.Size = new Size(103, 19);
            chkTreatAsType2.TabIndex = 5;
            chkTreatAsType2.Text = "Treat as Type 2";
            chkTreatAsType2.UseVisualStyleBackColor = true;
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel2, progressBar1 });
            toolStripStatusLabel1.Location = new Point(0, 371);
            toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            toolStripStatusLabel1.Size = new Size(507, 22);
            toolStripStatusLabel1.TabIndex = 7;
            toolStripStatusLabel1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel2
            // 
            toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            toolStripStatusLabel2.Size = new Size(39, 17);
            toolStripStatusLabel2.Text = "Ready";
            // 
            // progressBar1
            // 
            progressBar1.Alignment = ToolStripItemAlignment.Right;
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(100, 16);
            // 
            // btnExportPng
            // 
            btnExportPng.Location = new Point(194, 308);
            btnExportPng.Name = "btnExportPng";
            btnExportPng.Size = new Size(96, 23);
            btnExportPng.TabIndex = 8;
            btnExportPng.Text = "Export as PNG";
            btnExportPng.UseVisualStyleBackColor = true;
            btnExportPng.Click += btnExportPng_Click;
            // 
            // btnImportReplace
            // 
            btnImportReplace.Location = new Point(296, 308);
            btnImportReplace.Name = "btnImportReplace";
            btnImportReplace.Size = new Size(75, 23);
            btnImportReplace.TabIndex = 9;
            btnImportReplace.Text = "Import";
            btnImportReplace.UseVisualStyleBackColor = true;
            btnImportReplace.Click += btnImportReplace_Click;
            // 
            // btnReencode
            // 
            btnReencode.Location = new Point(377, 308);
            btnReencode.Name = "btnReencode";
            btnReencode.Size = new Size(96, 23);
            btnReencode.TabIndex = 10;
            btnReencode.Text = "Re-encode";
            btnReencode.UseVisualStyleBackColor = true;
            btnReencode.Click += btnReencode_Click;
            // 
            // btnNext
            // 
            btnNext.Location = new Point(113, 308);
            btnNext.Name = "btnNext";
            btnNext.Size = new Size(75, 23);
            btnNext.TabIndex = 12;
            btnNext.Text = "Next >";
            btnNext.UseVisualStyleBackColor = true;
            btnNext.Click += btnNext_Click;
            // 
            // btnPrev
            // 
            btnPrev.Location = new Point(32, 308);
            btnPrev.Name = "btnPrev";
            btnPrev.Size = new Size(75, 23);
            btnPrev.TabIndex = 13;
            btnPrev.Text = "< Previous";
            btnPrev.UseVisualStyleBackColor = true;
            btnPrev.Click += btnPrev_Click;
            // 
            // BssViewerForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(507, 393);
            Controls.Add(btnPrev);
            Controls.Add(btnNext);
            Controls.Add(btnReencode);
            Controls.Add(btnImportReplace);
            Controls.Add(btnExportPng);
            Controls.Add(toolStripStatusLabel1);
            Controls.Add(chkTreatAsType2);
            Controls.Add(nudQuant);
            Controls.Add(nudHeight);
            Controls.Add(nudWidth);
            Controls.Add(pictureBoxPreview);
            Controls.Add(MenuStrip);
            MainMenuStrip = MenuStrip;
            Name = "BssViewerForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Biohazard BSS Viewer";
            MenuStrip.ResumeLayout(false);
            MenuStrip.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBoxPreview).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudWidth).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudHeight).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudQuant).EndInit();
            toolStripStatusLabel1.ResumeLayout(false);
            toolStripStatusLabel1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip MenuStrip;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem openBSSToolStripMenuItem;
        private ToolStripMenuItem saveBSSAsToolStripMenuItem;
        private ToolStripMenuItem exportImagePNGBMPToolStripMenuItem;
        private ToolStripMenuItem importImagePNGBMPToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
        private PictureBox pictureBoxPreview;
        private NumericUpDown nudWidth;
        private NumericUpDown nudHeight;
        private NumericUpDown nudQuant;
        private CheckBox chkTreatAsType2;
        private StatusStrip toolStripStatusLabel1;
        private Button btnExportPng;
        private Button btnImportReplace;
        private Button btnReencode;
        private ToolStripStatusLabel toolStripStatusLabel2;
        private ToolStripProgressBar progressBar1;
        private Button button1;
        private Button btnNext;
        private Button btnPrev;
    }
}
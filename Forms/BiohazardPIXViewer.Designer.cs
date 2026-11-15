namespace Tool_Hazard.Forms
{
    partial class Biohazard_PIX_Viewer
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
            openToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            saveToolStripMenuItem = new ToolStripMenuItem();
            saveAsToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            exportSelectedToolStripMenuItem = new ToolStripMenuItem();
            exportAllToolStripMenuItem = new ToolStripMenuItem();
            replaceSelectedToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator3 = new ToolStripSeparator();
            closeToolStripMenuItem = new ToolStripMenuItem();
            sheetToolStripMenuItem = new ToolStripMenuItem();
            openSheetToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator10 = new ToolStripSeparator();
            exportIconToolStripMenuItem = new ToolStripMenuItem();
            replaceIconToolStripMenuItem = new ToolStripMenuItem();
            saveSheetToolStripMenuItem = new ToolStripMenuItem();
            viewToolStripMenuItem = new ToolStripMenuItem();
            zoomFitToolStripMenuItem = new ToolStripMenuItem();
            zoom1xToolStripMenuItem = new ToolStripMenuItem();
            zoom2xToolStripMenuItem = new ToolStripMenuItem();
            zoom4xToolStripMenuItem = new ToolStripMenuItem();
            zoom8xToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator4 = new ToolStripSeparator();
            perPageClutToolStripMenuItem = new ToolStripMenuItem();
            transparencyGridToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator5 = new ToolStripSeparator();
            copyImageToolStripMenuItem = new ToolStripMenuItem();
            navigateToolStripMenuItem = new ToolStripMenuItem();
            prevItemToolStripMenuItem = new ToolStripMenuItem();
            nextItemToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator6 = new ToolStripSeparator();
            firstItemToolStripMenuItem = new ToolStripMenuItem();
            lastItemToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator7 = new ToolStripSeparator();
            prevClutToolStripMenuItem = new ToolStripMenuItem();
            nextClutToolStripMenuItem = new ToolStripMenuItem();
            toolStrip1 = new ToolStrip();
            btnPrev = new ToolStripButton();
            btnNext = new ToolStripButton();
            toolStripSeparator8 = new ToolStripSeparator();
            lblClut = new ToolStripLabel();
            cmbClut = new ToolStripComboBox();
            chkPerPageClut = new ToolStripButton();
            toolStripSeparator9 = new ToolStripSeparator();
            lblZoom = new ToolStripLabel();
            cmbZoom = new ToolStripComboBox();
            previewPanel = new Panel();
            pictureBox1 = new PictureBox();
            infoPanel = new Panel();
            infoLeftLabel = new Label();
            infoRightLabel = new Label();
            statusStrip1 = new StatusStrip();
            toolStripStatusLabel1 = new ToolStripStatusLabel();
            toolStripStatusLabel2 = new ToolStripStatusLabel();
            menuStrip1.SuspendLayout();
            toolStrip1.SuspendLayout();
            previewPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            infoPanel.SuspendLayout();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, sheetToolStripMenuItem, viewToolStripMenuItem, navigateToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(904, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openToolStripMenuItem, toolStripSeparator1, saveToolStripMenuItem, saveAsToolStripMenuItem, toolStripSeparator2, exportSelectedToolStripMenuItem, exportAllToolStripMenuItem, replaceSelectedToolStripMenuItem, toolStripSeparator3, closeToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "&File";
            // 
            // openToolStripMenuItem
            // 
            openToolStripMenuItem.Name = "openToolStripMenuItem";
            openToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            openToolStripMenuItem.Size = new Size(214, 22);
            openToolStripMenuItem.Text = "&Open...";
            openToolStripMenuItem.Click += openToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(211, 6);
            // 
            // saveToolStripMenuItem
            // 
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.S;
            saveToolStripMenuItem.Size = new Size(214, 22);
            saveToolStripMenuItem.Text = "&Save";
            saveToolStripMenuItem.Click += saveToolStripMenuItem_Click;
            // 
            // saveAsToolStripMenuItem
            // 
            saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            saveAsToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            saveAsToolStripMenuItem.Size = new Size(214, 22);
            saveAsToolStripMenuItem.Text = "Save &As...";
            saveAsToolStripMenuItem.Click += saveAsToolStripMenuItem_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(211, 6);
            // 
            // exportSelectedToolStripMenuItem
            // 
            exportSelectedToolStripMenuItem.Name = "exportSelectedToolStripMenuItem";
            exportSelectedToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.E;
            exportSelectedToolStripMenuItem.Size = new Size(214, 22);
            exportSelectedToolStripMenuItem.Text = "&Export Current...";
            exportSelectedToolStripMenuItem.Click += exportSelectedToolStripMenuItem_Click;
            // 
            // exportAllToolStripMenuItem
            // 
            exportAllToolStripMenuItem.Name = "exportAllToolStripMenuItem";
            exportAllToolStripMenuItem.Size = new Size(214, 22);
            exportAllToolStripMenuItem.Text = "Export A&ll...";
            exportAllToolStripMenuItem.Click += exportAllToolStripMenuItem_Click;
            // 
            // replaceSelectedToolStripMenuItem
            // 
            replaceSelectedToolStripMenuItem.Name = "replaceSelectedToolStripMenuItem";
            replaceSelectedToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.R;
            replaceSelectedToolStripMenuItem.Size = new Size(214, 22);
            replaceSelectedToolStripMenuItem.Text = "Re&place Current...";
            replaceSelectedToolStripMenuItem.Click += replaceSelectedToolStripMenuItem_Click;
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new Size(211, 6);
            // 
            // closeToolStripMenuItem
            // 
            closeToolStripMenuItem.Name = "closeToolStripMenuItem";
            closeToolStripMenuItem.Size = new Size(214, 22);
            closeToolStripMenuItem.Text = "&Close";
            closeToolStripMenuItem.Click += closeToolStripMenuItem_Click;
            // 
            // sheetToolStripMenuItem
            // 
            sheetToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openSheetToolStripMenuItem, toolStripSeparator10, exportIconToolStripMenuItem, replaceIconToolStripMenuItem, saveSheetToolStripMenuItem });
            sheetToolStripMenuItem.Name = "sheetToolStripMenuItem";
            sheetToolStripMenuItem.Size = new Size(71, 20);
            sheetToolStripMenuItem.Text = "PIX &Sheet";
            // 
            // openSheetToolStripMenuItem
            // 
            openSheetToolStripMenuItem.Name = "openSheetToolStripMenuItem";
            openSheetToolStripMenuItem.Size = new Size(162, 22);
            openSheetToolStripMenuItem.Text = "&Open Sheet...";
            openSheetToolStripMenuItem.Click += openSheetToolStripMenuItem_Click;
            // 
            // toolStripSeparator10
            // 
            toolStripSeparator10.Name = "toolStripSeparator10";
            toolStripSeparator10.Size = new Size(159, 6);
            // 
            // exportIconToolStripMenuItem
            // 
            exportIconToolStripMenuItem.Name = "exportIconToolStripMenuItem";
            exportIconToolStripMenuItem.Size = new Size(162, 22);
            exportIconToolStripMenuItem.Text = "&Export Icon...";
            exportIconToolStripMenuItem.Click += exportIconToolStripMenuItem_Click;
            // 
            // replaceIconToolStripMenuItem
            // 
            replaceIconToolStripMenuItem.Name = "replaceIconToolStripMenuItem";
            replaceIconToolStripMenuItem.Size = new Size(162, 22);
            replaceIconToolStripMenuItem.Text = "&Replace Icon...";
            replaceIconToolStripMenuItem.Click += replaceIconToolStripMenuItem_Click;
            // 
            // saveSheetToolStripMenuItem
            // 
            saveSheetToolStripMenuItem.Name = "saveSheetToolStripMenuItem";
            saveSheetToolStripMenuItem.Size = new Size(162, 22);
            saveSheetToolStripMenuItem.Text = "Save Sheet";
            saveSheetToolStripMenuItem.Click += saveSheetToolStripMenuItem_Click;
            // 
            // viewToolStripMenuItem
            // 
            viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { zoomFitToolStripMenuItem, zoom1xToolStripMenuItem, zoom2xToolStripMenuItem, zoom4xToolStripMenuItem, zoom8xToolStripMenuItem, toolStripSeparator4, perPageClutToolStripMenuItem, transparencyGridToolStripMenuItem, toolStripSeparator5, copyImageToolStripMenuItem });
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Size = new Size(44, 20);
            viewToolStripMenuItem.Text = "&View";
            // 
            // zoomFitToolStripMenuItem
            // 
            zoomFitToolStripMenuItem.Name = "zoomFitToolStripMenuItem";
            zoomFitToolStripMenuItem.Size = new Size(190, 22);
            zoomFitToolStripMenuItem.Tag = "Fit";
            zoomFitToolStripMenuItem.Text = "&Fit to Window";
            zoomFitToolStripMenuItem.Click += zoomToolStripMenuItem_Click;
            // 
            // zoom1xToolStripMenuItem
            // 
            zoom1xToolStripMenuItem.Name = "zoom1xToolStripMenuItem";
            zoom1xToolStripMenuItem.Size = new Size(190, 22);
            zoom1xToolStripMenuItem.Tag = "1";
            zoom1xToolStripMenuItem.Text = "1x";
            zoom1xToolStripMenuItem.Click += zoomToolStripMenuItem_Click;
            // 
            // zoom2xToolStripMenuItem
            // 
            zoom2xToolStripMenuItem.Name = "zoom2xToolStripMenuItem";
            zoom2xToolStripMenuItem.Size = new Size(190, 22);
            zoom2xToolStripMenuItem.Tag = "2";
            zoom2xToolStripMenuItem.Text = "2x";
            zoom2xToolStripMenuItem.Click += zoomToolStripMenuItem_Click;
            // 
            // zoom4xToolStripMenuItem
            // 
            zoom4xToolStripMenuItem.Name = "zoom4xToolStripMenuItem";
            zoom4xToolStripMenuItem.Size = new Size(190, 22);
            zoom4xToolStripMenuItem.Tag = "4";
            zoom4xToolStripMenuItem.Text = "4x";
            zoom4xToolStripMenuItem.Click += zoomToolStripMenuItem_Click;
            // 
            // zoom8xToolStripMenuItem
            // 
            zoom8xToolStripMenuItem.Name = "zoom8xToolStripMenuItem";
            zoom8xToolStripMenuItem.Size = new Size(190, 22);
            zoom8xToolStripMenuItem.Tag = "8";
            zoom8xToolStripMenuItem.Text = "8x";
            zoom8xToolStripMenuItem.Click += zoomToolStripMenuItem_Click;
            // 
            // toolStripSeparator4
            // 
            toolStripSeparator4.Name = "toolStripSeparator4";
            toolStripSeparator4.Size = new Size(187, 6);
            // 
            // perPageClutToolStripMenuItem
            // 
            perPageClutToolStripMenuItem.CheckOnClick = true;
            perPageClutToolStripMenuItem.Name = "perPageClutToolStripMenuItem";
            perPageClutToolStripMenuItem.Size = new Size(190, 22);
            perPageClutToolStripMenuItem.Text = "Per-page C&LUTs";
            perPageClutToolStripMenuItem.CheckedChanged += perPageClutToolStripMenuItem_CheckedChanged;
            // 
            // transparencyGridToolStripMenuItem
            // 
            transparencyGridToolStripMenuItem.Checked = true;
            transparencyGridToolStripMenuItem.CheckOnClick = true;
            transparencyGridToolStripMenuItem.CheckState = CheckState.Checked;
            transparencyGridToolStripMenuItem.Name = "transparencyGridToolStripMenuItem";
            transparencyGridToolStripMenuItem.Size = new Size(190, 22);
            transparencyGridToolStripMenuItem.Text = "Transparency &Grid";
            transparencyGridToolStripMenuItem.CheckedChanged += transparencyGridToolStripMenuItem_CheckedChanged;
            // 
            // toolStripSeparator5
            // 
            toolStripSeparator5.Name = "toolStripSeparator5";
            toolStripSeparator5.Size = new Size(187, 6);
            // 
            // copyImageToolStripMenuItem
            // 
            copyImageToolStripMenuItem.Name = "copyImageToolStripMenuItem";
            copyImageToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.C;
            copyImageToolStripMenuItem.Size = new Size(190, 22);
            copyImageToolStripMenuItem.Text = "&Copy Image";
            copyImageToolStripMenuItem.Click += copyImageToolStripMenuItem_Click;
            // 
            // navigateToolStripMenuItem
            // 
            navigateToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { prevItemToolStripMenuItem, nextItemToolStripMenuItem, toolStripSeparator6, firstItemToolStripMenuItem, lastItemToolStripMenuItem, toolStripSeparator7, prevClutToolStripMenuItem, nextClutToolStripMenuItem });
            navigateToolStripMenuItem.Name = "navigateToolStripMenuItem";
            navigateToolStripMenuItem.Size = new Size(66, 20);
            navigateToolStripMenuItem.Text = "&Navigate";
            // 
            // prevItemToolStripMenuItem
            // 
            prevItemToolStripMenuItem.Name = "prevItemToolStripMenuItem";
            prevItemToolStripMenuItem.ShortcutKeyDisplayString = "Left";
            prevItemToolStripMenuItem.Size = new Size(180, 22);
            prevItemToolStripMenuItem.Text = "&Previous Item";
            prevItemToolStripMenuItem.Click += prevItemToolStripMenuItem_Click;
            // 
            // nextItemToolStripMenuItem
            // 
            nextItemToolStripMenuItem.Name = "nextItemToolStripMenuItem";
            nextItemToolStripMenuItem.ShortcutKeyDisplayString = "Right";
            nextItemToolStripMenuItem.Size = new Size(180, 22);
            nextItemToolStripMenuItem.Text = "&Next Item";
            nextItemToolStripMenuItem.Click += nextItemToolStripMenuItem_Click;
            // 
            // toolStripSeparator6
            // 
            toolStripSeparator6.Name = "toolStripSeparator6";
            toolStripSeparator6.Size = new Size(177, 6);
            // 
            // firstItemToolStripMenuItem
            // 
            firstItemToolStripMenuItem.Name = "firstItemToolStripMenuItem";
            firstItemToolStripMenuItem.ShortcutKeyDisplayString = "Home";
            firstItemToolStripMenuItem.Size = new Size(180, 22);
            firstItemToolStripMenuItem.Text = "&First Item";
            firstItemToolStripMenuItem.Click += firstItemToolStripMenuItem_Click;
            // 
            // lastItemToolStripMenuItem
            // 
            lastItemToolStripMenuItem.Name = "lastItemToolStripMenuItem";
            lastItemToolStripMenuItem.ShortcutKeyDisplayString = "End";
            lastItemToolStripMenuItem.Size = new Size(180, 22);
            lastItemToolStripMenuItem.Text = "&Last Item";
            lastItemToolStripMenuItem.Click += lastItemToolStripMenuItem_Click;
            // 
            // toolStripSeparator7
            // 
            toolStripSeparator7.Name = "toolStripSeparator7";
            toolStripSeparator7.Size = new Size(177, 6);
            // 
            // prevClutToolStripMenuItem
            // 
            prevClutToolStripMenuItem.Name = "prevClutToolStripMenuItem";
            prevClutToolStripMenuItem.ShortcutKeyDisplayString = "Up";
            prevClutToolStripMenuItem.Size = new Size(180, 22);
            prevClutToolStripMenuItem.Text = "Previous &CLUT";
            prevClutToolStripMenuItem.Click += prevClutToolStripMenuItem_Click;
            // 
            // nextClutToolStripMenuItem
            // 
            nextClutToolStripMenuItem.Name = "nextClutToolStripMenuItem";
            nextClutToolStripMenuItem.ShortcutKeyDisplayString = "Down";
            nextClutToolStripMenuItem.Size = new Size(180, 22);
            nextClutToolStripMenuItem.Text = "Next C&LUT";
            nextClutToolStripMenuItem.Click += nextClutToolStripMenuItem_Click;
            // 
            // toolStrip1
            // 
            toolStrip1.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip1.Items.AddRange(new ToolStripItem[] { btnPrev, btnNext, toolStripSeparator8, lblClut, cmbClut, chkPerPageClut, toolStripSeparator9, lblZoom, cmbZoom });
            toolStrip1.Location = new Point(0, 24);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Padding = new Padding(6, 2, 6, 2);
            toolStrip1.Size = new Size(904, 27);
            toolStrip1.TabIndex = 1;
            toolStrip1.Text = "toolStrip1";
            // 
            // btnPrev
            // 
            btnPrev.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnPrev.Name = "btnPrev";
            btnPrev.Size = new Size(55, 20);
            btnPrev.Text = "<< Prev";
            btnPrev.ToolTipText = "Previous TIM / icon (Left arrow)";
            btnPrev.Click += btnPrev_Click;
            // 
            // btnNext
            // 
            btnNext.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnNext.Name = "btnNext";
            btnNext.Size = new Size(55, 20);
            btnNext.Text = "Next >>";
            btnNext.ToolTipText = "Next TIM / icon (Right arrow)";
            btnNext.Click += btnNext_Click;
            // 
            // toolStripSeparator8
            // 
            toolStripSeparator8.Name = "toolStripSeparator8";
            toolStripSeparator8.Size = new Size(6, 23);
            // 
            // lblClut
            // 
            lblClut.Name = "lblClut";
            lblClut.Size = new Size(34, 20);
            lblClut.Text = "CLUT";
            // 
            // cmbClut
            // 
            cmbClut.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbClut.Name = "cmbClut";
            cmbClut.Size = new Size(140, 23);
            cmbClut.SelectedIndexChanged += cmbClut_SelectedIndexChanged;
            // 
            // chkPerPageClut
            // 
            chkPerPageClut.DisplayStyle = ToolStripItemDisplayStyle.Text;
            chkPerPageClut.Name = "chkPerPageClut";
            chkPerPageClut.Size = new Size(105, 20);
            chkPerPageClut.Text = "Per-page CLUTs";
            chkPerPageClut.ToolTipText = "Draw each 128px page with its own CLUT (RE texture atlas layout)";
            chkPerPageClut.CheckedChanged += chkPerPageClut_CheckedChanged;
            // 
            // toolStripSeparator9
            // 
            toolStripSeparator9.Name = "toolStripSeparator9";
            toolStripSeparator9.Size = new Size(6, 23);
            // 
            // lblZoom
            // 
            lblZoom.Name = "lblZoom";
            lblZoom.Size = new Size(38, 20);
            lblZoom.Text = "Zoom";
            // 
            // cmbZoom
            // 
            cmbZoom.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbZoom.Name = "cmbZoom";
            cmbZoom.Size = new Size(70, 23);
            cmbZoom.SelectedIndexChanged += cmbZoom_SelectedIndexChanged;
            // 
            // previewPanel
            // 
            previewPanel.AutoScroll = true;
            previewPanel.Controls.Add(pictureBox1);
            previewPanel.Dock = DockStyle.Fill;
            previewPanel.Location = new Point(0, 51);
            previewPanel.Name = "previewPanel";
            previewPanel.Size = new Size(904, 602);
            previewPanel.TabIndex = 2;
            // 
            // pictureBox1
            // 
            pictureBox1.Location = new Point(0, 0);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(100, 100);
            pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            // 
            // infoPanel
            // 
            infoPanel.Controls.Add(infoLeftLabel);
            infoPanel.Controls.Add(infoRightLabel);
            infoPanel.Dock = DockStyle.Bottom;
            infoPanel.Location = new Point(0, 653);
            infoPanel.Name = "infoPanel";
            infoPanel.Padding = new Padding(10, 4, 10, 4);
            infoPanel.Size = new Size(904, 46);
            infoPanel.TabIndex = 3;
            // 
            // infoLeftLabel
            // 
            infoLeftLabel.Dock = DockStyle.Fill;
            infoLeftLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            infoLeftLabel.Location = new Point(10, 4);
            infoLeftLabel.Name = "infoLeftLabel";
            infoLeftLabel.Size = new Size(520, 38);
            infoLeftLabel.TabIndex = 0;
            infoLeftLabel.Text = "No file loaded";
            infoLeftLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // infoRightLabel
            // 
            infoRightLabel.Dock = DockStyle.Right;
            infoRightLabel.Location = new Point(530, 4);
            infoRightLabel.Name = "infoRightLabel";
            infoRightLabel.Size = new Size(364, 38);
            infoRightLabel.TabIndex = 1;
            infoRightLabel.TextAlign = ContentAlignment.MiddleRight;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel1, toolStripStatusLabel2 });
            statusStrip1.Location = new Point(0, 699);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(904, 22);
            statusStrip1.TabIndex = 4;
            statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            toolStripStatusLabel1.Size = new Size(39, 17);
            toolStripStatusLabel1.Text = "Ready";
            // 
            // toolStripStatusLabel2
            // 
            toolStripStatusLabel2.Alignment = ToolStripItemAlignment.Right;
            toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            toolStripStatusLabel2.Size = new Size(279, 17);
            toolStripStatusLabel2.Text = "Left/Right item     Up/Down CLUT     Ctrl+S save     Ctrl+E export";
            // 
            // Biohazard_PIX_Viewer
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AllowDrop = true;
            ClientSize = new Size(904, 721);
            Controls.Add(previewPanel);
            Controls.Add(infoPanel);
            Controls.Add(statusStrip1);
            Controls.Add(toolStrip1);
            Controls.Add(menuStrip1);
            Font = new Font("Segoe UI", 9F);
            KeyPreview = true;
            MainMenuStrip = menuStrip1;
            MinimumSize = new Size(640, 480);
            Name = "Biohazard_PIX_Viewer";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Biohazard PIX & PS TIM Viewer";
            FormClosing += Biohazard_PIX_Viewer_FormClosing;
            DragDrop += Biohazard_PIX_Viewer_DragDrop;
            DragEnter += Biohazard_PIX_Viewer_DragEnter;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            previewPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            infoPanel.ResumeLayout(false);
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem openToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem saveToolStripMenuItem;
        private ToolStripMenuItem saveAsToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem exportSelectedToolStripMenuItem;
        private ToolStripMenuItem exportAllToolStripMenuItem;
        private ToolStripMenuItem replaceSelectedToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripMenuItem closeToolStripMenuItem;
        private ToolStripMenuItem sheetToolStripMenuItem;
        private ToolStripMenuItem openSheetToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator10;
        private ToolStripMenuItem exportIconToolStripMenuItem;
        private ToolStripMenuItem replaceIconToolStripMenuItem;
        private ToolStripMenuItem saveSheetToolStripMenuItem;
        private ToolStripMenuItem viewToolStripMenuItem;
        private ToolStripMenuItem zoomFitToolStripMenuItem;
        private ToolStripMenuItem zoom1xToolStripMenuItem;
        private ToolStripMenuItem zoom2xToolStripMenuItem;
        private ToolStripMenuItem zoom4xToolStripMenuItem;
        private ToolStripMenuItem zoom8xToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripMenuItem perPageClutToolStripMenuItem;
        private ToolStripMenuItem transparencyGridToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator5;
        private ToolStripMenuItem copyImageToolStripMenuItem;
        private ToolStripMenuItem navigateToolStripMenuItem;
        private ToolStripMenuItem prevItemToolStripMenuItem;
        private ToolStripMenuItem nextItemToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator6;
        private ToolStripMenuItem firstItemToolStripMenuItem;
        private ToolStripMenuItem lastItemToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator7;
        private ToolStripMenuItem prevClutToolStripMenuItem;
        private ToolStripMenuItem nextClutToolStripMenuItem;
        private ToolStrip toolStrip1;
        private ToolStripButton btnPrev;
        private ToolStripButton btnNext;
        private ToolStripSeparator toolStripSeparator8;
        private ToolStripLabel lblClut;
        private ToolStripComboBox cmbClut;
        private ToolStripButton chkPerPageClut;
        private ToolStripSeparator toolStripSeparator9;
        private ToolStripLabel lblZoom;
        private ToolStripComboBox cmbZoom;
        private Panel previewPanel;
        private PictureBox pictureBox1;
        private Panel infoPanel;
        private Label infoLeftLabel;
        private Label infoRightLabel;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel toolStripStatusLabel1;
        private ToolStripStatusLabel toolStripStatusLabel2;
    }
}

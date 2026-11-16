namespace Tool_Hazard.Forms
{
    partial class ClassicREmsgTool
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            mnuLoadXmlDictionary = new ToolStripMenuItem();
            mnuOpenMsg = new ToolStripMenuItem();
            mnuSaveMsg = new ToolStripMenuItem();
            mnuSaveMsgAs = new ToolStripMenuItem();
            txtMsg = new TextBox();
            picPreview = new PictureBox();
            cboFont = new ComboBox();
            lblFont = new Label();
            cboEncoding = new ComboBox();
            lblEncoding = new Label();
            ((System.ComponentModel.ISupportInitialize)picPreview).BeginInit();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            //
            // menuStrip1
            //
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(800, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            //
            // fileToolStripMenuItem
            //
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { mnuLoadXmlDictionary, mnuOpenMsg, mnuSaveMsg, mnuSaveMsgAs });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            //
            // mnuLoadXmlDictionary
            //
            mnuLoadXmlDictionary.Name = "mnuLoadXmlDictionary";
            mnuLoadXmlDictionary.Size = new Size(184, 22);
            mnuLoadXmlDictionary.Text = "Load XML Dictionary";
            //
            // mnuOpenMsg
            //
            mnuOpenMsg.Name = "mnuOpenMsg";
            mnuOpenMsg.Size = new Size(184, 22);
            mnuOpenMsg.Text = "Open MSG";
            //
            // mnuSaveMsg
            //
            mnuSaveMsg.Name = "mnuSaveMsg";
            mnuSaveMsg.Size = new Size(184, 22);
            mnuSaveMsg.Text = "Save MSG";
            //
            // mnuSaveMsgAs
            //
            mnuSaveMsgAs.Name = "mnuSaveMsgAs";
            mnuSaveMsgAs.Size = new Size(184, 22);
            mnuSaveMsgAs.Text = "Save MSG As";
            //
            // lblFont
            //
            lblFont.AutoSize = true;
            lblFont.Location = new Point(410, 5);
            lblFont.Name = "lblFont";
            lblFont.Size = new Size(31, 15);
            lblFont.TabIndex = 3;
            lblFont.Text = "Font:";
            //
            // cboFont
            //
            cboFont.DropDownStyle = ComboBoxStyle.DropDown;
            cboFont.Location = new Point(447, 2);
            cboFont.Name = "cboFont";
            cboFont.Size = new Size(180, 23);
            cboFont.TabIndex = 4;
            //
            // lblEncoding
            //
            lblEncoding.AutoSize = true;
            lblEncoding.Location = new Point(640, 5);
            lblEncoding.Name = "lblEncoding";
            lblEncoding.Size = new Size(61, 15);
            lblEncoding.TabIndex = 5;
            lblEncoding.Text = "Encoding:";
            //
            // cboEncoding
            //
            cboEncoding.DropDownStyle = ComboBoxStyle.DropDown;
            cboEncoding.Location = new Point(707, 2);
            cboEncoding.Name = "cboEncoding";
            cboEncoding.Size = new Size(190, 23);
            cboEncoding.TabIndex = 6;
            //
            // txtMsg
            //
            txtMsg.Dock = DockStyle.Left;
            txtMsg.Location = new Point(0, 24);
            txtMsg.Multiline = true;
            txtMsg.Name = "txtMsg";
            txtMsg.ScrollBars = ScrollBars.Both;
            txtMsg.Size = new Size(400, 426);
            txtMsg.TabIndex = 1;
            //
            // picPreview
            //
            picPreview.Dock = DockStyle.Fill;
            picPreview.Location = new Point(400, 24);
            picPreview.Name = "picPreview";
            picPreview.Size = new Size(400, 426);
            picPreview.SizeMode = PictureBoxSizeMode.Zoom;
            picPreview.TabIndex = 2;
            picPreview.TabStop = false;
            picPreview.BackColor = Color.Black;
            //
            // ClassicREmsgTool
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(picPreview);
            Controls.Add(txtMsg);
            Controls.Add(cboEncoding);
            Controls.Add(lblEncoding);
            Controls.Add(cboFont);
            Controls.Add(lblFont);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "ClassicREmsgTool";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "MSG Tool";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)picPreview).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem mnuOpenMsg;
        private ToolStripMenuItem mnuSaveMsg;
        private ToolStripMenuItem mnuSaveMsgAs;
        private TextBox txtMsg;
        private PictureBox picPreview;
        private ComboBox cboFont;
        private Label lblFont;
        private ComboBox cboEncoding;
        private Label lblEncoding;
        private ToolStripMenuItem mnuLoadXmlDictionary;
    }
}

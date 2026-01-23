namespace Tool_Hazard.Forms
{
    partial class ClassicREmsgTool
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
            mnuLoadXmlDictionary = new ToolStripMenuItem();
            mnuOpenMsg = new ToolStripMenuItem();
            mnuSaveMsg = new ToolStripMenuItem();
            mnuSaveMsgAs = new ToolStripMenuItem();
            txtMsg = new TextBox();
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
            // txtMsg
            // 
            txtMsg.Dock = DockStyle.Fill;
            txtMsg.Location = new Point(0, 24);
            txtMsg.Multiline = true;
            txtMsg.Name = "txtMsg";
            txtMsg.Size = new Size(800, 426);
            txtMsg.TabIndex = 1;
            // 
            // ClassicREmsgTool
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(txtMsg);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "ClassicREmsgTool";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "MSG Tool";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
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
        private ToolStripMenuItem mnuLoadXmlDictionary;
    }
}
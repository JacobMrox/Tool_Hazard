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
            pictureBox1 = new PictureBox();
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            openToolStripMenuItem = new ToolStripMenuItem();
            saveToolStripMenuItem = new ToolStripMenuItem();
            sheetToolStripMenuItem = new ToolStripMenuItem();
            openToolStripMenuItem1 = new ToolStripMenuItem();
            exportToolStripMenuItem = new ToolStripMenuItem();
            replaceToolStripMenuItem = new ToolStripMenuItem();
            saveToolStripMenuItem1 = new ToolStripMenuItem();
            Next = new Button();
            Prev = new Button();
            label1 = new Label();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // pictureBox1
            // 
            pictureBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pictureBox1.Location = new Point(0, 24);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(323, 240);
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            pictureBox1.Click += pictureBox1_Click;
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, sheetToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(323, 24);
            menuStrip1.TabIndex = 1;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openToolStripMenuItem, saveToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(83, 20);
            fileToolStripMenuItem.Text = "Background";
            // 
            // openToolStripMenuItem
            // 
            openToolStripMenuItem.Name = "openToolStripMenuItem";
            openToolStripMenuItem.Size = new Size(103, 22);
            openToolStripMenuItem.Text = "Open";
            openToolStripMenuItem.Click += openToolStripMenuItem_Click;
            // 
            // saveToolStripMenuItem
            // 
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.Size = new Size(103, 22);
            saveToolStripMenuItem.Text = "Save";
            saveToolStripMenuItem.Click += saveToolStripMenuItem_Click;
            // 
            // sheetToolStripMenuItem
            // 
            sheetToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openToolStripMenuItem1, exportToolStripMenuItem, replaceToolStripMenuItem, saveToolStripMenuItem1 });
            sheetToolStripMenuItem.Name = "sheetToolStripMenuItem";
            sheetToolStripMenuItem.Size = new Size(48, 20);
            sheetToolStripMenuItem.Text = "Sheet";
            // 
            // openToolStripMenuItem1
            // 
            openToolStripMenuItem1.Name = "openToolStripMenuItem1";
            openToolStripMenuItem1.Size = new Size(162, 22);
            openToolStripMenuItem1.Text = "Open PIX";
            openToolStripMenuItem1.Click += openToolStripMenuItem1_Click;
            // 
            // exportToolStripMenuItem
            // 
            exportToolStripMenuItem.Name = "exportToolStripMenuItem";
            exportToolStripMenuItem.Size = new Size(162, 22);
            exportToolStripMenuItem.Text = "Export Selected";
            exportToolStripMenuItem.Click += exportToolStripMenuItem_Click;
            // 
            // replaceToolStripMenuItem
            // 
            replaceToolStripMenuItem.Name = "replaceToolStripMenuItem";
            replaceToolStripMenuItem.Size = new Size(162, 22);
            replaceToolStripMenuItem.Text = "Replace Selected";
            replaceToolStripMenuItem.Click += replaceToolStripMenuItem_Click;
            // 
            // saveToolStripMenuItem1
            // 
            saveToolStripMenuItem1.Name = "saveToolStripMenuItem1";
            saveToolStripMenuItem1.Size = new Size(162, 22);
            saveToolStripMenuItem1.Text = "Save PIX";
            saveToolStripMenuItem1.Click += saveToolStripMenuItem1_Click;
            // 
            // Next
            // 
            Next.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            Next.Location = new Point(236, 273);
            Next.Name = "Next";
            Next.Size = new Size(75, 23);
            Next.TabIndex = 2;
            Next.Text = "Next Index";
            Next.UseVisualStyleBackColor = true;
            Next.Click += Next_Click;
            // 
            // Prev
            // 
            Prev.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            Prev.Location = new Point(12, 273);
            Prev.Name = "Prev";
            Prev.Size = new Size(75, 23);
            Prev.TabIndex = 3;
            Prev.Text = "Previous Index";
            Prev.UseVisualStyleBackColor = true;
            Prev.Click += Prev_Click;
            // 
            // label1
            // 
            label1.Anchor = AnchorStyles.Bottom;
            label1.AutoSize = true;
            label1.Location = new Point(120, 277);
            label1.Name = "label1";
            label1.Size = new Size(78, 15);
            label1.TabIndex = 4;
            label1.Text = "Current Index";
            label1.TextAlign = ContentAlignment.BottomCenter;
            // 
            // Biohazard_PIX_Viewer
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(323, 301);
            Controls.Add(label1);
            Controls.Add(Prev);
            Controls.Add(Next);
            Controls.Add(pictureBox1);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "Biohazard_PIX_Viewer";
            Text = "Biohazard_PIX_Viewer";
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private PictureBox pictureBox1;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem openToolStripMenuItem;
        private ToolStripMenuItem saveToolStripMenuItem;
        private ToolStripMenuItem sheetToolStripMenuItem;
        private ToolStripMenuItem openToolStripMenuItem1;
        private ToolStripMenuItem exportToolStripMenuItem;
        private ToolStripMenuItem replaceToolStripMenuItem;
        private ToolStripMenuItem saveToolStripMenuItem1;
        private Button Next;
        private Button Prev;
        private Label label1;
    }
}
namespace Tool_Hazard.Forms
{
    partial class BiohazardFileEditor
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
            gridPages = new DataGridView();
            Column1 = new DataGridViewTextBoxColumn();
            txtPage = new RichTextBox();
            picPreview = new PictureBox();
            statusStrip1 = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            newToolStripMenuItem = new ToolStripMenuItem();
            openToolStripMenuItem = new ToolStripMenuItem();
            saveToolStripMenuItem = new ToolStripMenuItem();
            saveAsToolStripMenuItem = new ToolStripMenuItem();
            exportToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            addPageToolStripMenuItem = new ToolStripMenuItem();
            deletePageToolStripMenuItem = new ToolStripMenuItem();
            moveUpToolStripMenuItem = new ToolStripMenuItem();
            moveDownToolStripMenuItem = new ToolStripMenuItem();
            fontToolStripMenuItem = new ToolStripMenuItem();
            selectEncodingxmlToolStripMenuItem = new ToolStripMenuItem();
            selectFontpngToolStripMenuItem = new ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)gridPages).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picPreview).BeginInit();
            statusStrip1.SuspendLayout();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // gridPages
            // 
            gridPages.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridPages.Columns.AddRange(new DataGridViewColumn[] { Column1 });
            gridPages.Location = new Point(10, 32);
            gridPages.Name = "gridPages";
            gridPages.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
            gridPages.Size = new Size(240, 237);
            gridPages.TabIndex = 0;
            // 
            // Column1
            // 
            Column1.HeaderText = "Text Preview";
            Column1.Name = "Column1";
            // 
            // txtPage
            // 
            txtPage.Location = new Point(10, 275);
            txtPage.Name = "txtPage";
            txtPage.Size = new Size(566, 158);
            txtPage.TabIndex = 1;
            txtPage.Text = "";
            // 
            // picPreview
            // 
            picPreview.Location = new Point(256, 32);
            picPreview.Name = "picPreview";
            picPreview.Size = new Size(320, 240);
            picPreview.TabIndex = 2;
            picPreview.TabStop = false;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { lblStatus });
            statusStrip1.Location = new Point(0, 439);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(585, 22);
            statusStrip1.TabIndex = 12;
            statusStrip1.Text = "statusStrip1";
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(39, 17);
            lblStatus.Text = "Ready";
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem, fontToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(585, 24);
            menuStrip1.TabIndex = 13;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { newToolStripMenuItem, openToolStripMenuItem, saveToolStripMenuItem, saveAsToolStripMenuItem, exportToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // newToolStripMenuItem
            // 
            newToolStripMenuItem.Name = "newToolStripMenuItem";
            newToolStripMenuItem.Size = new Size(180, 22);
            newToolStripMenuItem.Text = "New";
            newToolStripMenuItem.Click += newToolStripMenuItem_Click;
            // 
            // openToolStripMenuItem
            // 
            openToolStripMenuItem.Name = "openToolStripMenuItem";
            openToolStripMenuItem.Size = new Size(180, 22);
            openToolStripMenuItem.Text = "Open";
            openToolStripMenuItem.Click += openToolStripMenuItem_Click;
            // 
            // saveToolStripMenuItem
            // 
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.Size = new Size(180, 22);
            saveToolStripMenuItem.Text = "Save";
            saveToolStripMenuItem.Click += saveToolStripMenuItem_Click;
            // 
            // saveAsToolStripMenuItem
            // 
            saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            saveAsToolStripMenuItem.Size = new Size(180, 22);
            saveAsToolStripMenuItem.Text = "Save As";
            saveAsToolStripMenuItem.Click += saveAsToolStripMenuItem_Click;
            // 
            // exportToolStripMenuItem
            // 
            exportToolStripMenuItem.Name = "exportToolStripMenuItem";
            exportToolStripMenuItem.Size = new Size(180, 22);
            exportToolStripMenuItem.Text = "Export";
            exportToolStripMenuItem.Click += exportToolStripMenuItem_Click;
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { addPageToolStripMenuItem, deletePageToolStripMenuItem, moveUpToolStripMenuItem, moveDownToolStripMenuItem });
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            editToolStripMenuItem.Size = new Size(39, 20);
            editToolStripMenuItem.Text = "Edit";
            // 
            // addPageToolStripMenuItem
            // 
            addPageToolStripMenuItem.Name = "addPageToolStripMenuItem";
            addPageToolStripMenuItem.Size = new Size(180, 22);
            addPageToolStripMenuItem.Text = "Add Page";
            addPageToolStripMenuItem.Click += addPageToolStripMenuItem_Click;
            // 
            // deletePageToolStripMenuItem
            // 
            deletePageToolStripMenuItem.Name = "deletePageToolStripMenuItem";
            deletePageToolStripMenuItem.Size = new Size(180, 22);
            deletePageToolStripMenuItem.Text = "Delete Page";
            deletePageToolStripMenuItem.Click += deletePageToolStripMenuItem_Click;
            // 
            // moveUpToolStripMenuItem
            // 
            moveUpToolStripMenuItem.Name = "moveUpToolStripMenuItem";
            moveUpToolStripMenuItem.Size = new Size(180, 22);
            moveUpToolStripMenuItem.Text = "Move Up";
            moveUpToolStripMenuItem.Click += moveUpToolStripMenuItem_Click;
            // 
            // moveDownToolStripMenuItem
            // 
            moveDownToolStripMenuItem.Name = "moveDownToolStripMenuItem";
            moveDownToolStripMenuItem.Size = new Size(180, 22);
            moveDownToolStripMenuItem.Text = "Move Down";
            moveDownToolStripMenuItem.Click += moveDownToolStripMenuItem_Click;
            // 
            // fontToolStripMenuItem
            // 
            fontToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { selectEncodingxmlToolStripMenuItem, selectFontpngToolStripMenuItem });
            fontToolStripMenuItem.Name = "fontToolStripMenuItem";
            fontToolStripMenuItem.Size = new Size(43, 20);
            fontToolStripMenuItem.Text = "Font";
            // 
            // selectEncodingxmlToolStripMenuItem
            // 
            selectEncodingxmlToolStripMenuItem.Name = "selectEncodingxmlToolStripMenuItem";
            selectEncodingxmlToolStripMenuItem.Size = new Size(180, 22);
            selectEncodingxmlToolStripMenuItem.Text = "Select Encoding.xml";
            selectEncodingxmlToolStripMenuItem.Click += selectEncodingxmlToolStripMenuItem_Click;
            // 
            // selectFontpngToolStripMenuItem
            // 
            selectFontpngToolStripMenuItem.Name = "selectFontpngToolStripMenuItem";
            selectFontpngToolStripMenuItem.Size = new Size(180, 22);
            selectFontpngToolStripMenuItem.Text = "Select Font.png";
            selectFontpngToolStripMenuItem.Click += selectFontpngToolStripMenuItem_Click;
            // 
            // BiohazardFileEditor
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(585, 461);
            Controls.Add(statusStrip1);
            Controls.Add(menuStrip1);
            Controls.Add(picPreview);
            Controls.Add(txtPage);
            Controls.Add(gridPages);
            FormBorderStyle = FormBorderStyle.Fixed3D;
            MainMenuStrip = menuStrip1;
            Name = "BiohazardFileEditor";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Biohazard File Editor";
            ((System.ComponentModel.ISupportInitialize)gridPages).EndInit();
            ((System.ComponentModel.ISupportInitialize)picPreview).EndInit();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DataGridView gridPages;
        private DataGridViewTextBoxColumn Column1;
        private RichTextBox txtPage;
        private PictureBox picPreview;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel lblStatus;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem openToolStripMenuItem;
        private ToolStripMenuItem saveToolStripMenuItem;
        private ToolStripMenuItem saveAsToolStripMenuItem;
        private ToolStripMenuItem exportToolStripMenuItem;
        private ToolStripMenuItem editToolStripMenuItem;
        private ToolStripMenuItem addPageToolStripMenuItem;
        private ToolStripMenuItem deletePageToolStripMenuItem;
        private ToolStripMenuItem moveUpToolStripMenuItem;
        private ToolStripMenuItem moveDownToolStripMenuItem;
        private ToolStripMenuItem fontToolStripMenuItem;
        private ToolStripMenuItem selectEncodingxmlToolStripMenuItem;
        private ToolStripMenuItem selectFontpngToolStripMenuItem;
        private ToolStripMenuItem newToolStripMenuItem;
    }
}
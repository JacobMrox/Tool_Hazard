namespace Tool_Hazard.Forms
{
    partial class BiohazardOpcodeEditorForm
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
            openScdToolStripMenuItem = new ToolStripMenuItem();
            saveToolStripMenuItem = new ToolStripMenuItem();
            saveSCDAsToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            addInstructionToolStripMenuItem = new ToolStripMenuItem();
            insertInstructionAboveToolStripMenuItem = new ToolStripMenuItem();
            insertInstructionBelowToolStripMenuItem = new ToolStripMenuItem();
            deleteInstructionToolStripMenuItem = new ToolStripMenuItem();
            viewToolStripMenuItem = new ToolStripMenuItem();
            decimalToolStripMenuItem = new ToolStripMenuItem();
            hexidecimalToolStripMenuItem = new ToolStripMenuItem();
            versionToolStripMenuItem = new ToolStripMenuItem();
            bIO1RE1ToolStripMenuItem = new ToolStripMenuItem();
            bIO15RE15ToolStripMenuItem = new ToolStripMenuItem();
            bIO2RE2ToolStripMenuItem = new ToolStripMenuItem();
            bIO3RE3ToolStripMenuItem = new ToolStripMenuItem();
            gridOpcodes = new DataGridView();
            statusStrip1 = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridOpcodes).BeginInit();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem, viewToolStripMenuItem, versionToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(800, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openScdToolStripMenuItem, saveToolStripMenuItem, saveSCDAsToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // openScdToolStripMenuItem
            // 
            openScdToolStripMenuItem.Name = "openScdToolStripMenuItem";
            openScdToolStripMenuItem.Size = new Size(114, 22);
            openScdToolStripMenuItem.Text = "Open";
            openScdToolStripMenuItem.Click += openScdToolStripMenuItem_Click;
            // 
            // saveToolStripMenuItem
            // 
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.Size = new Size(114, 22);
            saveToolStripMenuItem.Text = "Save";
            saveToolStripMenuItem.Click += saveToolStripMenuItem_Click;
            // 
            // saveSCDAsToolStripMenuItem
            // 
            saveSCDAsToolStripMenuItem.Name = "saveSCDAsToolStripMenuItem";
            saveSCDAsToolStripMenuItem.Size = new Size(114, 22);
            saveSCDAsToolStripMenuItem.Text = "Save As";
            saveSCDAsToolStripMenuItem.Click += saveSCDAsToolStripMenuItem_Click;
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { addInstructionToolStripMenuItem, insertInstructionAboveToolStripMenuItem, insertInstructionBelowToolStripMenuItem, deleteInstructionToolStripMenuItem });
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            editToolStripMenuItem.Size = new Size(39, 20);
            editToolStripMenuItem.Text = "Edit";
            // 
            // addInstructionToolStripMenuItem
            // 
            addInstructionToolStripMenuItem.Name = "addInstructionToolStripMenuItem";
            addInstructionToolStripMenuItem.Size = new Size(200, 22);
            addInstructionToolStripMenuItem.Text = "Add Instruction";
            addInstructionToolStripMenuItem.Click += addInstructionToolStripMenuItem_Click;
            // 
            // insertInstructionAboveToolStripMenuItem
            // 
            insertInstructionAboveToolStripMenuItem.Name = "insertInstructionAboveToolStripMenuItem";
            insertInstructionAboveToolStripMenuItem.Size = new Size(200, 22);
            insertInstructionAboveToolStripMenuItem.Text = "Insert Instruction Above";
            insertInstructionAboveToolStripMenuItem.Click += insertInstructionAboveToolStripMenuItem_Click;
            // 
            // insertInstructionBelowToolStripMenuItem
            // 
            insertInstructionBelowToolStripMenuItem.Name = "insertInstructionBelowToolStripMenuItem";
            insertInstructionBelowToolStripMenuItem.Size = new Size(200, 22);
            insertInstructionBelowToolStripMenuItem.Text = "Insert Instruction Below";
            insertInstructionBelowToolStripMenuItem.Click += insertInstructionBelowToolStripMenuItem_Click;
            // 
            // deleteInstructionToolStripMenuItem
            // 
            deleteInstructionToolStripMenuItem.Name = "deleteInstructionToolStripMenuItem";
            deleteInstructionToolStripMenuItem.Size = new Size(200, 22);
            deleteInstructionToolStripMenuItem.Text = "Delete Instruction";
            deleteInstructionToolStripMenuItem.Click += deleteInstructionToolStripMenuItem_Click;
            // 
            // viewToolStripMenuItem
            // 
            viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { decimalToolStripMenuItem, hexidecimalToolStripMenuItem });
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Size = new Size(44, 20);
            viewToolStripMenuItem.Text = "View";
            // 
            // decimalToolStripMenuItem
            // 
            decimalToolStripMenuItem.CheckOnClick = true;
            decimalToolStripMenuItem.Name = "decimalToolStripMenuItem";
            decimalToolStripMenuItem.Size = new Size(139, 22);
            decimalToolStripMenuItem.Text = "Decimal";
            decimalToolStripMenuItem.Click += decimalToolStripMenuItem_Click;
            // 
            // hexidecimalToolStripMenuItem
            // 
            hexidecimalToolStripMenuItem.CheckOnClick = true;
            hexidecimalToolStripMenuItem.Name = "hexidecimalToolStripMenuItem";
            hexidecimalToolStripMenuItem.Size = new Size(139, 22);
            hexidecimalToolStripMenuItem.Text = "Hexidecimal";
            hexidecimalToolStripMenuItem.Click += hexidecimalToolStripMenuItem_Click;
            // 
            // versionToolStripMenuItem
            // 
            versionToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { bIO1RE1ToolStripMenuItem, bIO15RE15ToolStripMenuItem, bIO2RE2ToolStripMenuItem, bIO3RE3ToolStripMenuItem });
            versionToolStripMenuItem.Name = "versionToolStripMenuItem";
            versionToolStripMenuItem.Size = new Size(57, 20);
            versionToolStripMenuItem.Text = "Version";
            // 
            // bIO1RE1ToolStripMenuItem
            // 
            bIO1RE1ToolStripMenuItem.CheckOnClick = true;
            bIO1RE1ToolStripMenuItem.Name = "bIO1RE1ToolStripMenuItem";
            bIO1RE1ToolStripMenuItem.Size = new Size(141, 22);
            bIO1RE1ToolStripMenuItem.Text = "BIO1/RE1";
            bIO1RE1ToolStripMenuItem.Click += bIO1RE1ToolStripMenuItem_Click;
            // 
            // bIO15RE15ToolStripMenuItem
            // 
            bIO15RE15ToolStripMenuItem.CheckOnClick = true;
            bIO15RE15ToolStripMenuItem.Name = "bIO15RE15ToolStripMenuItem";
            bIO15RE15ToolStripMenuItem.Size = new Size(141, 22);
            bIO15RE15ToolStripMenuItem.Text = "BIO1.5/RE1.5";
            bIO15RE15ToolStripMenuItem.Click += bIO15RE15ToolStripMenuItem_Click;
            // 
            // bIO2RE2ToolStripMenuItem
            // 
            bIO2RE2ToolStripMenuItem.CheckOnClick = true;
            bIO2RE2ToolStripMenuItem.Name = "bIO2RE2ToolStripMenuItem";
            bIO2RE2ToolStripMenuItem.Size = new Size(141, 22);
            bIO2RE2ToolStripMenuItem.Text = "BIO2/RE2";
            bIO2RE2ToolStripMenuItem.Click += bIO2RE2ToolStripMenuItem_Click;
            // 
            // bIO3RE3ToolStripMenuItem
            // 
            bIO3RE3ToolStripMenuItem.CheckOnClick = true;
            bIO3RE3ToolStripMenuItem.Name = "bIO3RE3ToolStripMenuItem";
            bIO3RE3ToolStripMenuItem.Size = new Size(141, 22);
            bIO3RE3ToolStripMenuItem.Text = "BIO3/RE3";
            bIO3RE3ToolStripMenuItem.Click += bIO3RE3ToolStripMenuItem_Click;
            // 
            // gridOpcodes
            // 
            gridOpcodes.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridOpcodes.Dock = DockStyle.Fill;
            gridOpcodes.Location = new Point(0, 24);
            gridOpcodes.MultiSelect = false;
            gridOpcodes.Name = "gridOpcodes";
            gridOpcodes.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridOpcodes.Size = new Size(800, 426);
            gridOpcodes.TabIndex = 1;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { lblStatus });
            statusStrip1.Location = new Point(0, 428);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(800, 22);
            statusStrip1.TabIndex = 2;
            statusStrip1.Text = "statusStrip1";
            // 
            // lblStatus
            // 
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(118, 17);
            lblStatus.Text = "toolStripStatusLabel1";
            // 
            // BiohazardOpcodeEditorForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(statusStrip1);
            Controls.Add(gridOpcodes);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "BiohazardOpcodeEditorForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Biohazard SCD OpCode Editor";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)gridOpcodes).EndInit();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem openScdToolStripMenuItem;
        private ToolStripMenuItem saveToolStripMenuItem;
        private ToolStripMenuItem versionToolStripMenuItem;
        private ToolStripMenuItem bIO1RE1ToolStripMenuItem;
        private ToolStripMenuItem bIO15RE15ToolStripMenuItem;
        private ToolStripMenuItem bIO2RE2ToolStripMenuItem;
        private ToolStripMenuItem bIO3RE3ToolStripMenuItem;
        private ToolStripMenuItem saveSCDAsToolStripMenuItem;
        private DataGridView gridOpcodes;
        private ToolStripMenuItem editToolStripMenuItem;
        private ToolStripMenuItem addInstructionToolStripMenuItem;
        private ToolStripMenuItem insertInstructionAboveToolStripMenuItem;
        private ToolStripMenuItem insertInstructionBelowToolStripMenuItem;
        private ToolStripMenuItem deleteInstructionToolStripMenuItem;
        private ToolStripMenuItem viewToolStripMenuItem;
        private ToolStripMenuItem decimalToolStripMenuItem;
        private ToolStripMenuItem hexidecimalToolStripMenuItem;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel lblStatus;
    }
}
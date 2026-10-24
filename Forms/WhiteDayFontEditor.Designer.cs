#nullable enable

namespace Tool_Hazard
{
    partial class WhiteDayFontEditor
    {
        private System.ComponentModel.IContainer? components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                ViewCharBox?.Image?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            Button_OpenFont = new Button();
            Button_SaveFont = new Button();
            fontInfoLabel = new Label();
            previewPanel = new Panel();
            ViewCharBox = new PictureBox();
            symbolLabel = new Label();
            CharSelect = new NumericUpDown();
            symbolNumberLabel = new Label();
            widthLabel = new Label();
            CharWidthSetting = new TrackBar();
            widthValueLabel = new Label();
            Button_ImportBMP = new Button();
            Button_ExportBMP = new Button();
            Button_SaveSymbol = new Button();
            statusLabel = new Label();
            shortcutLabel = new Label();
            previewPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)ViewCharBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)CharSelect).BeginInit();
            ((System.ComponentModel.ISupportInitialize)CharWidthSetting).BeginInit();
            SuspendLayout();
            //
            // Button_OpenFont
            //
            Button_OpenFont.Location = new Point(16, 15);
            Button_OpenFont.Name = "Button_OpenFont";
            Button_OpenFont.Size = new Size(130, 34);
            Button_OpenFont.TabIndex = 0;
            Button_OpenFont.Text = "Open Font";
            Button_OpenFont.UseVisualStyleBackColor = true;
            Button_OpenFont.Click += Button_OpenFont_Click;
            //
            // Button_SaveFont
            //
            Button_SaveFont.Enabled = false;
            Button_SaveFont.Location = new Point(152, 15);
            Button_SaveFont.Name = "Button_SaveFont";
            Button_SaveFont.Size = new Size(130, 34);
            Button_SaveFont.TabIndex = 1;
            Button_SaveFont.Text = "Export Font";
            Button_SaveFont.UseVisualStyleBackColor = true;
            Button_SaveFont.Click += Button_SaveFont_Click;
            //
            // fontInfoLabel
            //
            fontInfoLabel.AutoEllipsis = true;
            fontInfoLabel.ForeColor = SystemColors.GrayText;
            fontInfoLabel.Location = new Point(16, 58);
            fontInfoLabel.Name = "fontInfoLabel";
            fontInfoLabel.Size = new Size(482, 23);
            fontInfoLabel.TabIndex = 2;
            fontInfoLabel.Text = "Open an 18×18 FNT or 36×36 WFT font to begin";
            //
            // previewPanel
            //
            previewPanel.BackColor = Color.FromArgb(24, 24, 24);
            previewPanel.BorderStyle = BorderStyle.FixedSingle;
            previewPanel.Controls.Add(ViewCharBox);
            previewPanel.Location = new Point(16, 84);
            previewPanel.Name = "previewPanel";
            previewPanel.Size = new Size(300, 300);
            previewPanel.TabIndex = 3;
            //
            // ViewCharBox
            //
            ViewCharBox.Dock = DockStyle.Fill;
            ViewCharBox.Location = new Point(0, 0);
            ViewCharBox.Name = "ViewCharBox";
            ViewCharBox.Size = new Size(298, 298);
            ViewCharBox.SizeMode = PictureBoxSizeMode.CenterImage;
            ViewCharBox.TabIndex = 0;
            ViewCharBox.TabStop = false;
            //
            // symbolLabel
            //
            symbolLabel.AutoSize = true;
            symbolLabel.Location = new Point(338, 91);
            symbolLabel.Name = "symbolLabel";
            symbolLabel.Size = new Size(48, 15);
            symbolLabel.TabIndex = 4;
            symbolLabel.Text = "Symbol";
            //
            // CharSelect
            //
            CharSelect.Enabled = false;
            CharSelect.Location = new Point(338, 112);
            CharSelect.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
            CharSelect.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            CharSelect.Name = "CharSelect";
            CharSelect.Size = new Size(82, 23);
            CharSelect.TabIndex = 2;
            CharSelect.TextAlign = HorizontalAlignment.Center;
            CharSelect.Value = new decimal(new int[] { 1, 0, 0, 0 });
            CharSelect.ValueChanged += CharSelect_ValueChanged;
            //
            // symbolNumberLabel
            //
            symbolNumberLabel.AutoSize = true;
            symbolNumberLabel.ForeColor = SystemColors.GrayText;
            symbolNumberLabel.Location = new Point(426, 115);
            symbolNumberLabel.Name = "symbolNumberLabel";
            symbolNumberLabel.Size = new Size(24, 15);
            symbolNumberLabel.TabIndex = 6;
            symbolNumberLabel.Text = "of 0";
            //
            // widthLabel
            //
            widthLabel.AutoSize = true;
            widthLabel.Location = new Point(338, 159);
            widthLabel.Name = "widthLabel";
            widthLabel.Size = new Size(82, 15);
            widthLabel.TabIndex = 7;
            widthLabel.Text = "Symbol width";
            //
            // CharWidthSetting
            //
            CharWidthSetting.Enabled = false;
            CharWidthSetting.Location = new Point(329, 179);
            CharWidthSetting.Maximum = 35;
            CharWidthSetting.Name = "CharWidthSetting";
            CharWidthSetting.Size = new Size(146, 45);
            CharWidthSetting.TabIndex = 3;
            CharWidthSetting.TickStyle = TickStyle.None;
            CharWidthSetting.ValueChanged += CharWidthSetting_ValueChanged;
            //
            // widthValueLabel
            //
            widthValueLabel.BackColor = SystemColors.ControlLight;
            widthValueLabel.BorderStyle = BorderStyle.FixedSingle;
            widthValueLabel.Location = new Point(472, 179);
            widthValueLabel.Name = "widthValueLabel";
            widthValueLabel.Size = new Size(31, 24);
            widthValueLabel.TabIndex = 9;
            widthValueLabel.Text = "0";
            widthValueLabel.TextAlign = ContentAlignment.MiddleCenter;
            //
            // Button_ImportBMP
            //
            Button_ImportBMP.Enabled = false;
            Button_ImportBMP.Location = new Point(338, 233);
            Button_ImportBMP.Name = "Button_ImportBMP";
            Button_ImportBMP.Size = new Size(165, 34);
            Button_ImportBMP.TabIndex = 4;
            Button_ImportBMP.Text = "Import Symbol";
            Button_ImportBMP.UseVisualStyleBackColor = true;
            Button_ImportBMP.Click += Button_ImportBMP_Click;
            //
            // Button_ExportBMP
            //
            Button_ExportBMP.Enabled = false;
            Button_ExportBMP.Location = new Point(338, 273);
            Button_ExportBMP.Name = "Button_ExportBMP";
            Button_ExportBMP.Size = new Size(165, 34);
            Button_ExportBMP.TabIndex = 5;
            Button_ExportBMP.Text = "Export Symbol";
            Button_ExportBMP.UseVisualStyleBackColor = true;
            Button_ExportBMP.Click += Button_ExportBMP_Click;
            //
            // Button_SaveSymbol
            //
            Button_SaveSymbol.Enabled = false;
            Button_SaveSymbol.ForeColor = Color.DarkGreen;
            Button_SaveSymbol.Location = new Point(338, 313);
            Button_SaveSymbol.Name = "Button_SaveSymbol";
            Button_SaveSymbol.Size = new Size(165, 34);
            Button_SaveSymbol.TabIndex = 6;
            Button_SaveSymbol.Text = "Apply Symbol";
            Button_SaveSymbol.UseVisualStyleBackColor = true;
            Button_SaveSymbol.Click += Button_SaveSymbol_Click;
            //
            // statusLabel
            //
            statusLabel.AutoEllipsis = true;
            statusLabel.Cursor = Cursors.Hand;
            statusLabel.ForeColor = SystemColors.GrayText;
            statusLabel.Location = new Point(16, 400);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(487, 23);
            statusLabel.TabIndex = 13;
            statusLabel.Text = "Ready";
            statusLabel.Click += statusLabel_Click;
            //
            // shortcutLabel
            //
            shortcutLabel.AutoSize = true;
            shortcutLabel.ForeColor = SystemColors.GrayText;
            shortcutLabel.Location = new Point(338, 361);
            shortcutLabel.Name = "shortcutLabel";
            shortcutLabel.Size = new Size(162, 15);
            shortcutLabel.TabIndex = 14;
            shortcutLabel.Text = "W/S: symbol   A/D: width";
            //
            // WhiteDayFontEditor
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(520, 435);
            Controls.Add(shortcutLabel);
            Controls.Add(statusLabel);
            Controls.Add(Button_SaveSymbol);
            Controls.Add(Button_ExportBMP);
            Controls.Add(Button_ImportBMP);
            Controls.Add(widthValueLabel);
            Controls.Add(CharWidthSetting);
            Controls.Add(widthLabel);
            Controls.Add(symbolNumberLabel);
            Controls.Add(CharSelect);
            Controls.Add(symbolLabel);
            Controls.Add(previewPanel);
            Controls.Add(fontInfoLabel);
            Controls.Add(Button_SaveFont);
            Controls.Add(Button_OpenFont);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "WhiteDayFontEditor";
            StartPosition = FormStartPosition.CenterParent;
            Text = "White Day Font Editor";
            FormClosing += WhiteDayFontEditor_FormClosing;
            KeyDown += WhiteDayFontEditor_KeyDown;
            previewPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)ViewCharBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)CharSelect).EndInit();
            ((System.ComponentModel.ISupportInitialize)CharWidthSetting).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        private Button Button_OpenFont = null!;
        private Button Button_SaveFont = null!;
        private Label fontInfoLabel = null!;
        private Panel previewPanel = null!;
        private PictureBox ViewCharBox = null!;
        private Label symbolLabel = null!;
        private NumericUpDown CharSelect = null!;
        private Label symbolNumberLabel = null!;
        private Label widthLabel = null!;
        private TrackBar CharWidthSetting = null!;
        private Label widthValueLabel = null!;
        private Button Button_ImportBMP = null!;
        private Button Button_ExportBMP = null!;
        private Button Button_SaveSymbol = null!;
        private Label statusLabel = null!;
        private Label shortcutLabel = null!;
    }
}

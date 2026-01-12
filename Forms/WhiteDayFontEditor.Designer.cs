namespace Tool_Hazard
{
    partial class WhiteDayFontEditor
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WhiteDayFontEditor));
            Button_OpenFont = new Button();
            Button_SaveFont = new Button();
            NumericUpDown = new NumericUpDown();
            trackBar1 = new TrackBar();
            statusLabel = new Label();
            Button_ImportBMP = new Button();
            Button_ExportBMP = new Button();
            Button_SaveSymbol = new Button();
            button6 = new Button();
            ViewCharBox = new PictureBox();
            lstCharacters = new ListBox();
            numX = new NumericUpDown();
            numY = new NumericUpDown();
            numWidth = new NumericUpDown();
            numHeight = new NumericUpDown();
            txtCharCode = new TextBox();
            label1 = new Label();
            ((System.ComponentModel.ISupportInitialize)NumericUpDown).BeginInit();
            ((System.ComponentModel.ISupportInitialize)trackBar1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)ViewCharBox).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numX).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numY).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numWidth).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numHeight).BeginInit();
            SuspendLayout();
            // 
            // Button_OpenFont
            // 
            resources.ApplyResources(Button_OpenFont, "Button_OpenFont");
            Button_OpenFont.Name = "Button_OpenFont";
            Button_OpenFont.UseVisualStyleBackColor = true;
            Button_OpenFont.Click += Button_OpenFont_Click_1;
            // 
            // Button_SaveFont
            // 
            resources.ApplyResources(Button_SaveFont, "Button_SaveFont");
            Button_SaveFont.Name = "Button_SaveFont";
            Button_SaveFont.UseVisualStyleBackColor = true;
            Button_SaveFont.Click += Button_SaveFont_Click_1;
            // 
            // NumericUpDown
            // 
            resources.ApplyResources(NumericUpDown, "NumericUpDown");
            NumericUpDown.Name = "NumericUpDown";
            // 
            // trackBar1
            // 
            resources.ApplyResources(trackBar1, "trackBar1");
            trackBar1.Name = "trackBar1";
            // 
            // statusLabel
            // 
            resources.ApplyResources(statusLabel, "statusLabel");
            statusLabel.Name = "statusLabel";
            // 
            // Button_ImportBMP
            // 
            resources.ApplyResources(Button_ImportBMP, "Button_ImportBMP");
            Button_ImportBMP.Name = "Button_ImportBMP";
            Button_ImportBMP.UseVisualStyleBackColor = true;
            Button_ImportBMP.Click += Button_ImportBMP_Click;
            // 
            // Button_ExportBMP
            // 
            resources.ApplyResources(Button_ExportBMP, "Button_ExportBMP");
            Button_ExportBMP.Name = "Button_ExportBMP";
            Button_ExportBMP.UseVisualStyleBackColor = true;
            // 
            // Button_SaveSymbol
            // 
            resources.ApplyResources(Button_SaveSymbol, "Button_SaveSymbol");
            Button_SaveSymbol.Name = "Button_SaveSymbol";
            Button_SaveSymbol.UseVisualStyleBackColor = true;
            // 
            // button6
            // 
            resources.ApplyResources(button6, "button6");
            button6.Name = "button6";
            button6.UseVisualStyleBackColor = true;
            // 
            // ViewCharBox
            // 
            resources.ApplyResources(ViewCharBox, "ViewCharBox");
            ViewCharBox.Name = "ViewCharBox";
            ViewCharBox.TabStop = false;
            // 
            // lstCharacters
            // 
            lstCharacters.FormattingEnabled = true;
            resources.ApplyResources(lstCharacters, "lstCharacters");
            lstCharacters.Name = "lstCharacters";
            // 
            // numX
            // 
            resources.ApplyResources(numX, "numX");
            numX.Name = "numX";
            // 
            // numY
            // 
            resources.ApplyResources(numY, "numY");
            numY.Name = "numY";
            // 
            // numWidth
            // 
            resources.ApplyResources(numWidth, "numWidth");
            numWidth.Name = "numWidth";
            // 
            // numHeight
            // 
            resources.ApplyResources(numHeight, "numHeight");
            numHeight.Name = "numHeight";
            // 
            // txtCharCode
            // 
            resources.ApplyResources(txtCharCode, "txtCharCode");
            txtCharCode.Name = "txtCharCode";
            // 
            // label1
            // 
            resources.ApplyResources(label1, "label1");
            label1.Name = "label1";
            // 
            // FontEditor
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(label1);
            Controls.Add(txtCharCode);
            Controls.Add(numHeight);
            Controls.Add(numWidth);
            Controls.Add(numY);
            Controls.Add(numX);
            Controls.Add(lstCharacters);
            Controls.Add(ViewCharBox);
            Controls.Add(button6);
            Controls.Add(Button_SaveSymbol);
            Controls.Add(Button_ExportBMP);
            Controls.Add(Button_ImportBMP);
            Controls.Add(statusLabel);
            Controls.Add(trackBar1);
            Controls.Add(NumericUpDown);
            Controls.Add(Button_SaveFont);
            Controls.Add(Button_OpenFont);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "FontEditor";
            ((System.ComponentModel.ISupportInitialize)NumericUpDown).EndInit();
            ((System.ComponentModel.ISupportInitialize)trackBar1).EndInit();
            ((System.ComponentModel.ISupportInitialize)ViewCharBox).EndInit();
            ((System.ComponentModel.ISupportInitialize)numX).EndInit();
            ((System.ComponentModel.ISupportInitialize)numY).EndInit();
            ((System.ComponentModel.ISupportInitialize)numWidth).EndInit();
            ((System.ComponentModel.ISupportInitialize)numHeight).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button Button_OpenFont;
        private Button Button_SaveFont;
        private NumericUpDown NumericUpDown;
        private TrackBar trackBar1;
        private Label statusLabel;
        private Button Button_ImportBMP;
        private Button Button_ExportBMP;
        private Button Button_SaveSymbol;
        private Button button6;
        private PictureBox ViewCharBox;
        private ListBox lstCharacters;
        private NumericUpDown numX;
        private NumericUpDown numY;
        private NumericUpDown numWidth;
        private NumericUpDown numHeight;
        private TextBox txtCharCode;
        private Label label1;
    }
}
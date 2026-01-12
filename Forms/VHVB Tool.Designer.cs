namespace Tool_Hazard.Forms
{
    partial class VHVB_Tool
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
            button1 = new Button();
            button2 = new Button();
            button4 = new Button();
            button5 = new Button();
            listViewSamples = new ListView();
            SuspendLayout();
            // 
            // button1
            // 
            button1.Location = new Point(12, 417);
            button1.Name = "button1";
            button1.Size = new Size(120, 23);
            button1.TabIndex = 1;
            button1.Text = "Import VH/VB";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.Location = new Point(138, 417);
            button2.Name = "button2";
            button2.Size = new Size(120, 23);
            button2.TabIndex = 2;
            button2.Text = "Play Sample";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // button4
            // 
            button4.Location = new Point(264, 417);
            button4.Name = "button4";
            button4.Size = new Size(150, 23);
            button4.TabIndex = 4;
            button4.Text = "Extract Sample (Wav)";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click;
            // 
            // button5
            // 
            button5.Location = new Point(420, 417);
            button5.Name = "button5";
            button5.Size = new Size(150, 23);
            button5.TabIndex = 5;
            button5.Text = "Replace Sample";
            button5.UseVisualStyleBackColor = true;
            button5.Click += button5_Click;
            // 
            // listViewSamples
            // 
            listViewSamples.Dock = DockStyle.Top;
            listViewSamples.Location = new Point(0, 0);
            listViewSamples.Name = "listViewSamples";
            listViewSamples.Size = new Size(582, 411);
            listViewSamples.TabIndex = 6;
            listViewSamples.UseCompatibleStateImageBehavior = false;
            // 
            // VHVB_Tool
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(582, 448);
            Controls.Add(listViewSamples);
            Controls.Add(button5);
            Controls.Add(button4);
            Controls.Add(button2);
            Controls.Add(button1);
            Name = "VHVB_Tool";
            Text = "VHVB_Tool";
            ResumeLayout(false);
        }

        #endregion
        private Button button1;
        private Button button2;
        private Button button4;
        private Button button5;
        private ListView listViewSamples;
    }
}
using White_Day_Mod_Tool;

namespace Tool_Hazard
{
    public partial class WhiteDayFontEditor : Form
    {
        private FontParameters fontParams;
        private string loadedFontPath;

        public WhiteDayFontEditor()
        {
            InitializeComponent();
        }

        private void Button_OpenFont_Click_1(object sender, EventArgs e)
        {
            using (OpenFileDialog openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "Font Files (*.fnt)|*.fnt|All Files (*.*)|*.*";

                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    loadedFontPath = openDialog.FileName;
                    fontParams = new FontParameters();
                    fontParams.Load(loadedFontPath);
                    PopulateCharacterGrid();
                }
            }
        }

        private void Button_ImportBMP_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "Bitmap Files (*.bmp)|*.bmp|All Files (*.*)|*.*";

                if (openDialog.ShowDialog() == DialogResult.OK)
                {/*
                    loadedFontPath = openDialog.FileName;
                    fontParams = new FontParameters();
                    fontParams.Load(loadedFontPath);
                    PopulateCharacterGrid();*/
                }
            }
        }

        private void PopulateCharacterGrid()
        {
            lstCharacters.Items.Clear();

            foreach (var ch in fontParams.Characters)
            {
                lstCharacters.Items.Add($"Char: {ch.Character} | X:{ch.X} Y:{ch.Y} W:{ch.Width} H:{ch.Height}");
            }
        }

        private void lstCharacters_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstCharacters.SelectedIndex >= 0)
            {
                var selectedChar = fontParams.Characters[lstCharacters.SelectedIndex];
                txtCharCode.Text = selectedChar.Character.ToString();
                numX.Value = selectedChar.X;
                numY.Value = selectedChar.Y;
                numWidth.Value = selectedChar.Width;
                numHeight.Value = selectedChar.Height;
            }
        }

        private void Button_UpdateChar_Click_1(object sender, EventArgs e)
        {
            if (lstCharacters.SelectedIndex >= 0)
            {
                var selectedChar = fontParams.Characters[lstCharacters.SelectedIndex];
                selectedChar.X = (int)numX.Value;
                selectedChar.Y = (int)numY.Value;
                selectedChar.Width = (int)numWidth.Value;
                selectedChar.Height = (int)numHeight.Value;
                PopulateCharacterGrid();
            }
        }

        private void Button_SaveFont_Click_1(object sender, EventArgs e)
        {
            if (fontParams != null && !string.IsNullOrEmpty(loadedFontPath))
            {
                fontParams.Save(loadedFontPath);
                MessageBox.Show("Font saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}

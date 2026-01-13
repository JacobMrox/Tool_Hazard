using IntelOrca.Biohazard;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Compilation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Tool_Hazard.Biohazard.emd;
using Tool_Hazard.Biohazard.RDT;
using Tool_Hazard.Forms;
using static System.Net.Mime.MediaTypeNames;
//using Spectre.Console.Cli;
//using Tool_Hazard.FontEditor;

namespace Tool_Hazard
{
    public partial class Main : Form
    {
        public static Main Instance { get; private set; }
        //Need to move these to WhiteDay/Nop.cs
        private readonly ushort[] SonnoriLz77Key = { 0xFF21, 0x834F, 0x675F, 0x0034, 0xF237, 0x815F, 0x4765, 0x0233 };
        //private static readonly Encoding EucKr = Encoding.GetEncoding("EUC-KR");
        private readonly Encoding EucKr;
        //private NopCompression selectedCompression = NopCompression.None;

        //Other C# files to use functions from should be defined here, so we can use them in Form1/Main.cs/Main Window??
        //Rebirth Manager
        private readonly RebirthManager rebirth = new RebirthManager();
        private BioVersion CurrentBioVersion;

        public void UpdateStatus(string text)
        {
            //Just as a fail safe
            try
            {
                //toolStripStatusLabel1.Text = text;
                //statusStrip1.Refresh();
                if (InvokeRequired)
                {
                    Invoke(new Action(() => statusStrip1.Text = text));
                }
                else
                {
                    statusStrip1.Text = text;
                }
            }
            //Fail safe/exeption message triggered if a exception is caught
            catch (Exception ex)
            {
                MessageBox.Show($"Error Updating Status Bar to '{text}'.\n\nThe error is: {ex}", "UpdateStatus Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public Main()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                EucKr = Encoding.GetEncoding("EUC-KR");
            }
            catch
            {
                // fallback to UTF-8 if EUC-KR not available
                EucKr = Encoding.UTF8;
                MessageBox.Show("EUC-KR encoding not available. Using UTF-8 instead. Korean filenames may appear incorrect.",
                                "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            InitializeComponent();
            //UpdateStatus("Ready");
            //Bio3RDTunpackToolStripMenuItem.Click += Bio3RdtTool.OnUnpackClick;
            //Bio3RDTrepackToolStripMenuItem.Click += Bio3RdtTool.OnRepackClick;
        }

        private static byte[] CompressLz77(byte[] src)
        {
            // Greedy encoder. Window up to 4095 bytes, match length 2..17.
            var output = new List<byte>(src.Length);
            int i = 0;
            while (i < src.Length)
            {
                int flagIndex = output.Count;
                output.Add(0); // placeholder for flag byte
                int flagMask = 0;
                int bits = 0;

                while (bits < 8 && i < src.Length)
                {
                    int bestLen = 0;
                    int bestDist = 0;

                    int startWindow = Math.Max(0, i - 0x0FFF);
                    int maxLen = Math.Min(17, src.Length - i);

                    // Find best match
                    for (int j = i - 1; j >= startWindow; j--)
                    {
                        int dist = i - j;
                        int l = 0;
                        while (l < maxLen && src[j + l] == src[i + l]) l++;
                        if (l >= 2 && l > bestLen && dist >= 1 && dist <= 0x0FFF)
                        {
                            bestLen = l;
                            bestDist = dist;
                            if (bestLen == 17) break;
                        }
                    }

                    if (bestLen >= 2)
                    {
                        // flag bit = 1 => match token
                        flagMask |= (1 << bits);
                        ushort info = (ushort)(((bestLen - 2) << 12) | (bestDist & 0x0FFF));
                        output.Add((byte)(info & 0xFF));
                        output.Add((byte)(info >> 8));
                        i += bestLen;
                    }
                    else
                    {
                        // literal
                        output.Add(src[i++]);
                    }

                    bits++;
                }

                // write final flag byte
                output[flagIndex] = (byte)flagMask;
            }

            return output.ToArray();
        }

        private byte[] CompressSonnoriLz77(byte[] src)
        {
            // Same tokens as LZ77, but:
            //  - flag byte stored as bsrcmask (original), and decoder uses: bmask = bsrcmask ^ 0xC8
            //  - each match token's 16-bit info is XORed with custom key indexed by ((bsrcmask >> 3) & 7)
            var output = new List<byte>(src.Length);
            int i = 0;

            while (i < src.Length)
            {
                // We'll compose tokens for this block, first gather them, then compute flag bytes
                var block = new List<byte>();
                int flagMask = 0;
                int bits = 0;

                while (bits < 8 && i < src.Length)
                {
                    int bestLen = 0;
                    int bestDist = 0;

                    int startWindow = Math.Max(0, i - 0x0FFF);
                    int maxLen = Math.Min(17, src.Length - i);

                    for (int j = i - 1; j >= startWindow; j--)
                    {
                        int dist = i - j;
                        int l = 0;
                        while (l < maxLen && src[j + l] == src[i + l]) l++;
                        if (l >= 2 && l > bestLen && dist >= 1 && dist <= 0x0FFF)
                        {
                            bestLen = l;
                            bestDist = dist;
                            if (bestLen == 17) break;
                        }
                    }

                    if (bestLen >= 2)
                    {
                        flagMask |= (1 << bits); // match
                        ushort info = (ushort)(((bestLen - 2) << 12) | (bestDist & 0x0FFF));
                        // We'll XOR with key later after we know bsrcmask
                        block.Add((byte)(info & 0xFF));
                        block.Add((byte)(info >> 8));
                        i += bestLen;
                    }
                    else
                    {
                        // literal
                        block.Add(src[i++]);
                    }

                    bits++;
                }

                // We need bmask such that decoder does: bmask = bsrcmask ^ 0xC8
                // So choose bsrcmask = flagMask ^ 0xC8
                byte bsrcmask = (byte)(flagMask ^ 0xC8);
                output.Add(bsrcmask);

                // Key index for this block (must match decoder's rule)
                int keyIndex = (bsrcmask >> 3) & 0x07;
                ushort k = SonnoriLz77Key[keyIndex];

                // Now flush tokens, XORing match infos in this block with k.
                // We must walk through the same flag bits (LSB-first) to know which items are pairs (match) vs literals
                int cursor = 0;
                int bitNum = 0;
                while (bitNum < bits)
                {
                    bool isMatch = ((flagMask >> bitNum) & 1) != 0;
                    if (isMatch)
                    {
                        ushort info = (ushort)(block[cursor] | (block[cursor + 1] << 8));
                        info ^= k;
                        output.Add((byte)(info & 0xFF));
                        output.Add((byte)(info >> 8));
                        cursor += 2;
                    }
                    else
                    {
                        output.Add(block[cursor++]); // literal
                    }
                    bitNum++;
                }
            }

            return output.ToArray();
        }
        public enum NopCompression
        {
            None,
            Lz77,
            Sonnori
        }

        //public string SelectedCompMethod;
        //White Day (2001) NOP Unpack
        private async void unpackToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "NOP Files (*.nop)|*.nop";
                openDialog.Multiselect = true;

                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    ProgressBar progressBar = new ProgressBar
                    {
                        Dock = DockStyle.Bottom,
                        Minimum = 0,
                        Maximum = openDialog.FileNames.Length
                    };
                    Label statusLabel = new Label
                    {
                        Dock = DockStyle.Bottom,
                        Text = "Starting...",
                        AutoSize = true
                    };

                    this.Controls.Add(progressBar);
                    this.Controls.Add(statusLabel);

                    unpackToolStripMenuItem1.Enabled = false;

                    StringBuilder errorLog = new StringBuilder();

                    await Task.Run(() =>
                    {
                        int progress = 0;
                        foreach (string filePath in openDialog.FileNames)
                        {
                            try
                            {
                                string outputDir = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_unpacked");
                                Directory.CreateDirectory(outputDir);

                                // Update UI safely
                                this.Invoke(new Action(() =>
                                {
                                    statusLabel.Text = $"Unpacking: {Path.GetFileName(filePath)}";
                                    progressBar.Value = progress;
                                }));

                                UnpackNopFile(filePath, outputDir);
                            }
                            catch (Exception ex)
                            {
                                errorLog.AppendLine($"Error unpacking {Path.GetFileName(filePath)}: {ex.Message}");
                            }

                            progress++;
                        }
                    });

                    statusLabel.Text = "Completed";
                    progressBar.Value = progressBar.Maximum;

                    if (errorLog.Length > 0)
                    {
                        string logPath = Path.Combine(System.Windows.Forms.Application.StartupPath, "UnpackErrors.log");
                        File.WriteAllText(logPath, errorLog.ToString());
                        MessageBox.Show($"Unpacking finished with some errors.\nSee log: {logPath}", "Finished", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show("All files unpacked successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    this.Controls.Remove(progressBar);
                    this.Controls.Remove(statusLabel);
                    unpackToolStripMenuItem1.Enabled = true;
                }
            }
        }
        private void UnpackNopFile(string nopFilePath, string outputDir)
        {
            using (FileStream fs = new FileStream(nopFilePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                fs.Seek(-1, SeekOrigin.End);
                if (br.ReadByte() != 0x12)
                    throw new InvalidDataException("Invalid or corrupted NOP file.");

                fs.Seek(-9, SeekOrigin.End);
                int offset = br.ReadInt32();
                int numFiles = br.ReadInt32();

                byte key = 0;

                for (int i = 0; i < numFiles; i++)
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                    byte nameSize = br.ReadByte();
                    byte type = br.ReadByte();
                    int fileOffset = br.ReadInt32();
                    int encodeSize = br.ReadInt32();
                    int decodeSize = br.ReadInt32();
                    byte[] nameBytes = br.ReadBytes(nameSize + 1);
                    offset += nameSize + 15;

                    if (type == 0x02)
                        key = (byte)decodeSize;
                    else
                        decodeSize ^= key;

                    for (int j = 0; j < nameSize; j++)
                        nameBytes[j] ^= key;

                    string fileName = EucKr.GetString(nameBytes, 0, nameSize);
                    string fullPath = Path.Combine(outputDir, fileName);

                    switch (type)
                    {
                        case 0x00: // RAW
                            fs.Seek(fileOffset, SeekOrigin.Begin);
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                            File.WriteAllBytes(fullPath, br.ReadBytes(encodeSize));
                            break;

                        case 0x01: // LZ77
                            fs.Seek(fileOffset, SeekOrigin.Begin);
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                            File.WriteAllBytes(fullPath, DecompressLz77(br.ReadBytes(encodeSize), decodeSize));
                            break;

                        case 0x02: // Directory
                            Directory.CreateDirectory(fullPath);
                            break;

                        case 0x03: // SONNORI LZ77
                            fs.Seek(fileOffset, SeekOrigin.Begin);
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                            File.WriteAllBytes(fullPath, DecompressSonnoriLz77(br.ReadBytes(encodeSize), decodeSize));
                            break;

                        default:
                            throw new InvalidDataException($"Unknown data type: {type}");
                    }
                }
            }
        }

        private byte[] DecompressLz77(byte[] input, int expectedSize)
        {
            List<byte> output = new List<byte>(expectedSize);
            int i = 0, bcnt = 0, bmask = 0;

            while (i < input.Length)
            {
                if (bcnt == 0)
                {
                    bmask = input[i++];
                    bcnt = 8;
                }

                if ((bmask & 1) != 0)
                {
                    if (i + 1 >= input.Length) break;
                    ushort info = BitConverter.ToUInt16(input, i);
                    i += 2;
                    int off = info & 0x0FFF;
                    int len = (info >> 12) + 2;
                    for (int k = 0; k < len; k++)
                        output.Add(output[output.Count - off]);
                }
                else
                {
                    output.Add(input[i++]);
                }

                bmask >>= 1;
                bcnt--;
            }

            return output.ToArray();
        }

        private byte[] DecompressSonnoriLz77(byte[] input, int expectedSize)
        {
            List<byte> output = new List<byte>(expectedSize);
            int i = 0, bcnt = 0, bmask = 0, bsrcmask = 0;

            while (i < input.Length)
            {
                if (bcnt == 0)
                {
                    bmask = bsrcmask = input[i++];
                    bmask ^= 0xC8;
                    bcnt = 8;
                }

                if ((bmask & 1) != 0)
                {
                    if (i + 1 >= input.Length) break;
                    ushort info = BitConverter.ToUInt16(input, i);
                    i += 2;
                    info ^= SonnoriLz77Key[(bsrcmask >> 3) & 0x07];
                    int off = info & 0x0FFF;
                    int len = (info >> 12) + 2;
                    for (int k = 0; k < len; k++)
                        output.Add(output[output.Count - off]);
                }
                else
                {
                    output.Add(input[i++]);
                }

                bmask >>= 1;
                bcnt--;
            }

            return output.ToArray();
        }

        private void repackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Delete This
        }

        private void RepackNopFile(string sourceDir, string outputFilePath, NopCompression compression = NopCompression.None)
        //private void RepackNopFile(string sourceDir, string outputFilePath, selectedCompression)
        {
            byte pathKey = 0x5A;

            // Gather directories & files
            string[] dirs = Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories);
            string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

            // Sort directories so parents come before children
            Array.Sort(dirs, (a, b) => a.Length.CompareTo(b.Length));

            // Build file records
            var fileBlobs = new List<(string rel, byte type, byte[] encoded, int decodedSize)>();

            foreach (var file in files)
            {
                string rel = file.Substring(sourceDir.Length + 1).Replace("\\", "/");
                byte[] original = File.ReadAllBytes(file);
                byte[] encoded = original;
                byte type = 0x00;
                int decodedSize = original.Length;

                if (compression == NopCompression.Lz77)
                {
                    var comp = CompressLz77(original);
                    if (comp.Length < original.Length)
                    {
                        encoded = comp;
                        type = 0x01;
                    }
                }
                else if (compression == NopCompression.Sonnori)
                {
                    var comp = CompressSonnoriLz77(original);
                    if (comp.Length < original.Length)
                    {
                        encoded = comp;
                        type = 0x03;
                    }
                }

                fileBlobs.Add((rel, type, encoded, decodedSize));
            }

            // Prepare header
            using var headerMs = new MemoryStream();
            using var hw = new BinaryWriter(headerMs, EucKr);

            // Write directories first
            foreach (var dir in dirs)
            {
                string rel = dir.Substring(sourceDir.Length + 1).Replace("\\", "/");
                byte[] nameBytes = EucKr.GetBytes(rel);
                for (int i = 0; i < nameBytes.Length; i++) nameBytes[i] ^= pathKey;

                hw.Write((byte)nameBytes.Length);
                hw.Write((byte)0x02);
                hw.Write(0); // offset
                hw.Write(0); // encoded size
                hw.Write((int)pathKey); // store key
                hw.Write(nameBytes);
                hw.Write((byte)0);
            }

            // Keep track of data offset
            int dataOffset = 0;
            var fileHeaders = new List<(byte[] nameBytes, byte type, int offset, int encSize, int decSize)>();

            foreach (var (rel, type, encoded, decSize) in fileBlobs)
            {
                byte[] nameBytes = EucKr.GetBytes(rel);
                for (int i = 0; i < nameBytes.Length; i++) nameBytes[i] ^= pathKey;

                int encSize = encoded.Length;
                int xoredDecode = decSize ^ pathKey;

                fileHeaders.Add((nameBytes, type, dataOffset, encSize, xoredDecode));
                dataOffset += encSize;
            }

            // Write file headers after directories
            foreach (var h in fileHeaders)
            {
                hw.Write((byte)h.nameBytes.Length);
                hw.Write(h.type);
                hw.Write(h.offset);
                hw.Write(h.encSize);
                hw.Write(h.decSize);
                hw.Write(h.nameBytes);
                hw.Write((byte)0);
            }

            // Write final NOP file
            using var fs = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            // Write file data
            foreach (var (_, _, encoded, _) in fileBlobs)
                bw.Write(encoded);

            // Write header after data
            bw.Write(headerMs.ToArray());

            // Footer
            int headerOffset = dataOffset;
            int totalEntries = dirs.Length + fileBlobs.Count;
            bw.Write(headerOffset);
            bw.Write(totalEntries);
            bw.Write((byte)0x12);
        }

        private void fontEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Delete this
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void repackToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //NOP Repack Code Here
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder to repack into a .nop file";

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string sourceDir = folderDialog.SelectedPath;
                    string nopFilePath = Path.Combine(Path.GetDirectoryName(sourceDir), Path.GetFileName(sourceDir) + ".nop");

                    SaveFileDialog saveDialog = new SaveFileDialog
                    {
                        Filter = "NOP Files (*.nop)|*.nop",
                        FileName = Path.GetFileName(nopFilePath)
                    };

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        nopFilePath = saveDialog.FileName;

                        // Ask user for compression
                        var result = MessageBox.Show("Choose compression:\nYes = LZ77\nNo = Sonnori\nCancel = None",
                                                     "Compression Method",
                                                     MessageBoxButtons.YesNoCancel,
                                                     MessageBoxIcon.Question);

                        NopCompression method = NopCompression.None;
                        if (result == DialogResult.Yes)
                            method = NopCompression.Lz77;
                        else if (result == DialogResult.No)
                            method = NopCompression.Sonnori;

                        RepackNopFile(sourceDir, nopFilePath, method);
                        MessageBox.Show($"Repacking complete: {nopFilePath}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }

        }

        private void fontEditorToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            // Open Font Editor form
            using (var fontEditor = new WhiteDayFontEditor())
            {
                fontEditor.ShowDialog();
            }
        }

        //Clasic Rebirth Installers Menu Hooks
        private async void menuInstallRE1CR_Click_1(object sender, EventArgs e)
        {
            System.Media.SystemSounds.Exclamation.Play();//Play sound to grab attention
            DialogResult ask_setup = MessageBox.Show(
                    $"Classic Rebirth is a fan-patch made by Gemini-Loboto3.\n\nThis patch for the Mediakite version of Resident Evil/Biohazard fixes common compatibility issues on modern systems, and includes several other enhancements such as raw and native xinput controller support, higher resolution display options, and forcefeedback (vibration), among other things.\n\nWould you like to proceed with install?",
                    "Classic Rebirth Installer",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

            if (ask_setup == DialogResult.Yes)
            {
                using var dlg = new FolderBrowserDialog { Description = "Select Resident Evil 1 directory" };
                if (dlg.ShowDialog() == DialogResult.OK)
                    await rebirth.Install(RebirthGame.RE1, dlg.SelectedPath);
            }
        }

        private async void menuInstallRE2CR_Click(object sender, EventArgs e)
        {
            System.Media.SystemSounds.Exclamation.Play();//Play sound to grab attention
            DialogResult ask_setup = MessageBox.Show(
                    $"Classic Rebirth is a fan-patch made by Gemini-Loboto3.\n\nThis patch for Resident Evil/Biohazard 2 Sourcenext fix common compatibility issues on modern systems, and includes several other enhancements such as raw and native xinput controller support, higher resolution display options, and forcefeedback (vibration), among other things.\n\nWould you like to proceed with install?",
                    "Classic Rebirth Installer",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

            if (ask_setup == DialogResult.Yes)
            {
                using var dlg = new FolderBrowserDialog { Description = "Select Resident Evil 2 directory" };
                if (dlg.ShowDialog() == DialogResult.OK)
                    await rebirth.Install(RebirthGame.RE2, dlg.SelectedPath);
            }
        }

        private async void menuInstallRE3CR_Click(object sender, EventArgs e)
        {
            System.Media.SystemSounds.Exclamation.Play();//Play sound to grab attention
            DialogResult ask_setup = MessageBox.Show(
                    $"Classic Rebirth is a fan-patch made by Gemini-Loboto3.\n\nThis patch for Resident Evil/Biohazard 3 Sourcenext ver 1.1.0 fixes common compatibility issues on modern systems, and includes several other enhancements and features.\n\nThese features include fixing wobbly polygons, crash issues, controller support, a PC friendly version of the PS1's options menu restored and upgraded, Mercenaries launch-able through the main executable, among other things.\n\nWould you like to proceed with install?",
                    "Classic Rebirth Installer",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

            if (ask_setup == DialogResult.Yes)
            {
                using var dlg = new FolderBrowserDialog { Description = "Select Resident Evil 3 directory" };
                if (dlg.ShowDialog() == DialogResult.OK)
                    await rebirth.Install(RebirthGame.RE3, dlg.SelectedPath);
            }
        }

        private void vHVBToWavToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /*
            // Call the conversion action methods sequentially.
            // Note: Each one will trigger its own dialog, and then the conversion runs.

            // Step 1: Open VH File Dialog
            psSnd.OpenVhFile_Click(sender, e);

            // Step 2: Open VB File Dialog
            psSnd.OpenVbFile_Click(sender, e);

            // Step 3: Select Output Directory Dialog
            psSnd.SelectOutputDirectory_Click(sender, e);

            // Step 4: Start Conversion
            // This method internally checks if all paths are set before proceeding.
            psSnd.Convert_Click(sender, e);
            */

            // 1. Create the new manager form, passing the paths

        }

        private void vBVHToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                // Just create and show the form. 
                // The SampleManagerForm will handle prompting for files in its Shown event.
                VHVB_Tool managerForm = new VHVB_Tool();
                managerForm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Sample Manager: {ex.Message}", "Form Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void vHVBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                // Just create and show the form. 
                // The SampleManagerForm will handle prompting for files in its Shown event.
                VHVB_Tool managerForm = new VHVB_Tool();
                managerForm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Sample Manager: {ex.Message}", "Form Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //Bio 1, 2, 3 RDT Tool Menu Hooks

        private void unpackToolStripMenuItem4_Click(object sender, EventArgs e)
        {
            //Bio1 RDT Unpack (RdtUnpacker.cs)
            using var ofd = new OpenFileDialog
            {
                Filter = "RDT Files (*.rdt)|*.rdt",
                Title = "Select Bio1 .rdt to Unpack"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            using var fbd = new FolderBrowserDialog { Description = "Select Output Folder" };
            if (fbd.ShowDialog() != DialogResult.OK) return;

            string rdtName = Path.GetFileNameWithoutExtension(ofd.FileName);
            string outputDir = Path.Combine(fbd.SelectedPath, rdtName);
            Directory.CreateDirectory(outputDir);

            var unpacker = new RdtUnpacker(CurrentBioVersion, ofd.FileName, outputDir);
            unpacker.Unpack();
            UpdateStatus("Bio1 RDT unpacked successfully!");

        }
        private void repackToolStripMenuItem5_Click(object sender, EventArgs e)
        {
            //Bio1 RDT Repack (RdtPacker.cs)
            using var ofd = new OpenFileDialog
            {
                Filter = "HDR Files (*.hdr)|*.hdr",
                Title = "Select Bio1 .hdr to Repack"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            var packer = new RdtPacker(CurrentBioVersion, ofd.FileName);
            packer.Pack();
            UpdateStatus("Bio1 RDT repacked successfully!");
        }
        private void Bio2RDTunpackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Bio2 RDT Unpack (RdtUnpacker.cs)
            using var ofd = new OpenFileDialog
            {
                Filter = "RDT Files (*.rdt)|*.rdt",
                Title = "Select Bio2 .rdt to Unpack"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            using var fbd = new FolderBrowserDialog { Description = "Select Output Folder" };
            if (fbd.ShowDialog() != DialogResult.OK) return;

            string rdtName = Path.GetFileNameWithoutExtension(ofd.FileName);
            string outputDir = Path.Combine(fbd.SelectedPath, rdtName);

            Directory.CreateDirectory(outputDir);

            var unpacker = new RdtUnpacker(CurrentBioVersion, ofd.FileName, outputDir);
            unpacker.Unpack();
            UpdateStatus("Bio2 RDT unpacked successfully!");
        }

        private void Bio2RDTrepackToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            //Bio2 RDT Repack (RdtPacker.cs)
            using var ofd = new OpenFileDialog
            {
                Filter = "HDR Files (*.hdr)|*.hdr",
                Title = "Select Bio2 .hdr to Repack"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            var packer = new RdtPacker(CurrentBioVersion, ofd.FileName);
            packer.Pack();
            //MessageBox.Show("Bio2 RDT repacked successfully!", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            UpdateStatus("Bio2 RDT repacked successfully!");
        }

        //Bio3 RDT Unpack (RdtUnpacker.cs)
        private void Bio3RDTunpackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "RDT Files (*.rdt)|*.rdt",
                Title = "Select Bio3 .rdt to Unpack"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            using var fbd = new FolderBrowserDialog { Description = "Select Output Folder" };
            if (fbd.ShowDialog() != DialogResult.OK) return;

            string rdtName = Path.GetFileNameWithoutExtension(ofd.FileName);
            string outputDir = Path.Combine(fbd.SelectedPath, rdtName);

            Directory.CreateDirectory(outputDir);

            var unpacker = new RdtUnpacker(CurrentBioVersion, ofd.FileName, outputDir);
            unpacker.Unpack();
            UpdateStatus("Bio3 RDT unpacked successfully!");
        }
        private void Bio3RDTrepackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Bio3 RDT Repack (RdtPacker.cs)
            using var ofd = new OpenFileDialog
            {
                Filter = "HDR Files (*.hdr)|*.hdr",
                Title = "Select Bio3 .hdr to Repack"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            var packer = new RdtPacker(CurrentBioVersion, ofd.FileName);
            packer.Pack();
            //MessageBox.Show("Bio3 RDT repacked successfully!", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            UpdateStatus("Bio3 RDT repacked successfully!");
        }


        //RDT Conversion menu hooks

        private void convertToBIO3ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("This is an experimental functionality and as such it may not work correctly.", "Experimental Code", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            //Bio2 to Bio3 RDT Conversion (basically unpack a ofd selected rdt to a specific selecter out folder path and then usag the .hdr file got unpacked, e.g. if we extracted x.rdt to a specific folder, its likely we got outpath/x/x.hdr so that is used to repack as the usage is explained in the other menu hooks... the different is this will be automatic conversion without having to do it manually
            using var ofd = new OpenFileDialog
            {
                Filter = "RDT Files (*.rdt)|*.rdt",
                Title = "Select Bio2 RDT File"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            using var fbd = new FolderBrowserDialog { Description = "Select Temp Extraction Folder" };
            if (fbd.ShowDialog() != DialogResult.OK) return;

            // 1) Unpack Bio2
            string rdtName = Path.GetFileNameWithoutExtension(ofd.FileName);
            MessageBox.Show("RDT Name:\n\n" + rdtName, "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            string outputDir = Path.Combine(fbd.SelectedPath, rdtName);
            MessageBox.Show("directory set to:\n\n" + outputDir, "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            Directory.CreateDirectory(outputDir);
            var unpacker = new RdtUnpacker(CurrentBioVersion, ofd.FileName, outputDir);
            unpacker.Unpack();

            // HDR path inside temp
            var hdrPath = Path.Combine(outputDir,
                Path.GetFileNameWithoutExtension(ofd.FileName) + ".hdr");
            var newRDTpath = Path.Combine(outputDir,
    Path.GetFileNameWithoutExtension(ofd.FileName) + ".RDT");
            MessageBox.Show("HDR File Path:\n\n" + hdrPath, "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

            // 2) Repack as Bio3
            var packer = new RdtPacker(CurrentBioVersion, hdrPath);
            packer.Pack();

            MessageBox.Show("Converted Bio2 → Bio3 successfully!\n\n" + newRDTpath, "Success!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
        private void convertToBIO2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //same as above but for Bio3/RE3 to Bio2/RE2
            using var ofd = new OpenFileDialog
            {
                Filter = "RDT Files (*.rdt)|*.rdt",
                Title = "Select Bio3 RDT File"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            using var tempFolder = new FolderBrowserDialog { Description = "Select Temp Extraction Folder" };
            if (tempFolder.ShowDialog() != DialogResult.OK) return;

            // 1) Unpack Bio3
            var unpacker = new RdtUnpacker(CurrentBioVersion, ofd.FileName, tempFolder.SelectedPath + Path.GetFileNameWithoutExtension(ofd.FileName));
            unpacker.Unpack();

            // HDR path inside temp
            var hdrPath = Path.Combine(tempFolder.SelectedPath,
                Path.GetFileNameWithoutExtension(ofd.FileName) + ".hdr");

            // 2) Repack as Bio2
            var packer = new RdtPacker(CurrentBioVersion, hdrPath);
            packer.Pack();

            MessageBox.Show("Converted Bio3 → Bio2 successfully!");

            //statusStrip1.Update("Converted Bio3 → Bio2 successfully!");
            //statusStrip1.Text = "Importing data file"; //see next line

        }

        //Biohazard RDT Script Data (SCD) Menu hooks

        private void decompiletoSToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //Dcompile Bio2 RDT Script Data to .s
        }

        private void recompiletoRDTToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //Recompile Decompiled (.s) Script Data to Bio2 RDT
        }

        private void decompiletoSToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Bio3 RDT Script Data to .S
        }

        private void recompiletoRDTToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Bio2 Recompile Decompiled (.s) Script Data to .RDT
        }

        //RE3 ROFS Tool Menu Hooks

        private void ROFSunpackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "RE3 ROFS (*.dat)|*.dat|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            string inputPath = ofd.FileName;
            string outputDir = Path.Combine(
                Path.GetDirectoryName(inputPath)!,
                Path.GetFileNameWithoutExtension(inputPath)
            );

            try
            {
                using var archive = new RE3Archive(inputPath);
                archive.Extract(outputDir);

                MessageBox.Show(
                    $"Extracted to:\n{outputDir}",
                    "ROFS Unpack",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "ROFS Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        //Biohazard EMD/PLD Exporter/Repacker Menu Hooks
        private void originalMD2TIMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //RE3 EMD Original [MD2/BIN and TIM] Unpack
        }
        private void editableOBJPNGToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //RE3 EMD Editable [OBJ and PNG] Unpack
        }
        private void originalToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //RE3 EMD Original [MD2/BIN and TIM] Repack
        }
        private void editableToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //RE3 EMD Editable [OBJ and PNG] Repack
        }

        private void editableOBJPNGToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //RE2 EMD Editable [OBJ and PNG]  Unpack
        }

        private void originalMD2TIMToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //RE2 EMD Original [MD2/BIN and TIM] Unpack
        }

        private void editableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //RE2 Editable EMD Repack
        }

        private void originalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //RE2 Original EMD Repack
        }

        //Biohazard 1-3 SCD decompiler/compiler/extractor menu hooks
        private void decompiletoSToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            //Decompile SCD from a Bio3 RDT to .S editable script
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "RDT files (*.rdt)|*.rdt|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string rdtPath = openFileDialog.FileName;
                    string outputPath = Path.ChangeExtension(rdtPath, ".s");
                    //Finish this off / To be added
                }
            }
        }

        private void decompileSCDToSToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Diassemble/Decompile Bio3 SCD to .S/.LST
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Script Data files (*.scd)|*.scd|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string scdPath = openFileDialog.FileName;
                    string outputPath = Path.ChangeExtension(scdPath, ".s");
                    //Execute our SCD to .S/.LST Decompile function from ScdService.cs
                    ScdService.DecompileScd(scdPath, CurrentBioVersion);
                }
            }
        }
        private void extractSCDFromRDTToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //Extract SCD/Script Data from a Bio2/RE2 RDT
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "RDT files (*.rdt)|*.rdt|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string rdtPath = openFileDialog.FileName;
                    string outputPath = Path.ChangeExtension(rdtPath, ".scd");
                    //Execute our ScdService.cs Extract SCD function and set path, outputpath and global version
                    ScdService.ExtractScdFromRdt(rdtPath, outputPath, CurrentBioVersion);
                }
            }
        }

        private void extractSCDFromRDTToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Extract SCD from a Bio3/RE3 RDT
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "RDT files (*.rdt)|*.rdt|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string rdtPath = openFileDialog.FileName;
                    string outputPath = Path.ChangeExtension(rdtPath, ".scd");
                    //Execute our ScdService.cs Extract SCD function and set path, outputpath and global version
                    ScdService.ExtractScdFromRdt(rdtPath, outputPath, CurrentBioVersion);
                }
            }
        }

        private void decompileSCDToSToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //Diassemble/Decompile Bio2 SCD to .S/.LST
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Script Data files (*.scd)|*.scd|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string scdPath = openFileDialog.FileName;
                    string outputPath = Path.ChangeExtension(scdPath, ".s");
                    //Execute our SCD to .S/.LST Decompile function from ScdService.cs
                    ScdService.DecompileScd(scdPath, CurrentBioVersion);
                }
            }
        }

        //Load SCD Editor with selected game version
        private void sCDOpCodeEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var editor = new OpcodeEditorForm(CurrentBioVersion);
            editor.Show(this);
        }
        private void sCDOpCodeEditorToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            var editor = new OpcodeEditorForm(CurrentBioVersion);
            editor.Show(this);
        }
        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            var editor = new OpcodeEditorForm(CurrentBioVersion);
            editor.Show(this);
        }

        //Version set based on selected menu strip item

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            CurrentBioVersion = BioVersion.Biohazard1;
            UpdateStatus("Version set to Biohazard 1");
        }

        private void bIO2RE21998ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentBioVersion = BioVersion.Biohazard2;
            UpdateStatus("Version set to Biohazard 2");
        }

        private void bIORE31999ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentBioVersion = BioVersion.Biohazard3;
            UpdateStatus("Version set to Biohazard 3");
        }

        private void bIO151997ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentBioVersion = BioVersion.Biohazard1_5;
            UpdateStatus("Version set to Biohazard 1.5");
        }

        // --- Resident Evil EMD/PLD Unpack/Repack Menu Hooks ---

        private void unpackOriginalToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            //RE3 EMD [MD2 + TIM ONLY] Unpack
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Resident Evil Enemy Data (*.emd)|*.EMD|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFile = openFileDialog.FileName;

                    //try catch errors
                    try
                    {
                        //Call EMD tool and pass our paths and version to unpack as Original format
                        EmdTool.Unpack(selectedFile, CurrentBioVersion, EmdTool.Format.Original);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void repackOriginalToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            //RE3 EMD [MD2 + TIM ONLY] Repack
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder containing the unpacked EMD files (MD2 + TIM)";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string sourceDir = folderDialog.SelectedPath;
                    //try catch errors
                    try
                    {
                        EmdTool.RepackFromFolder(sourceDir, CurrentBioVersion, EmdTool.Format.Original, "emd");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void unpackEditableToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            //RE3 EMD [OBJ, MTL + PNG ONLY] Unpack
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Resident Evil Enemy Data (*.emd)|*.EMD|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFile = openFileDialog.FileName;
                    //try catch errors
                    try
                    {
                        EmdTool.Unpack(selectedFile, CurrentBioVersion, EmdTool.Format.Editable);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void repackEditableToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            //RE3 EMD [OBJ, MTL + PNG ONLY] Repack
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder containing the unpacked EMD files (MD2 + TIM)";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string sourceDir = folderDialog.SelectedPath;
                    //try catch errors
                    try
                    {
                        EmdTool.RepackFromFolder(sourceDir, CurrentBioVersion, EmdTool.Format.Editable, "emd");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void unpackOriginalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //RE3 PLD Original [MD2 + TIM ONLY] Unpack
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Resident Evil Player Data (*.pld)|*.PLD|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFile = openFileDialog.FileName;

                    //try catch errors
                    try
                    {
                        //Call EMD tool and pass our paths and version to unpack as original format
                        EmdTool.Unpack(selectedFile, CurrentBioVersion, EmdTool.Format.Original);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        private void repackOriginalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //RE3 PLD Original [MD2 + TIM ONLY] Repack
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder containing the unpacked PLD files (MD2 + TIM)";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string sourceDir = folderDialog.SelectedPath;
                    //try catch errors
                    try
                    {
                        EmdTool.RepackFromFolder(sourceDir, CurrentBioVersion, EmdTool.Format.Original, "pld");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        private void unpackEditableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //RE3 PLD Editable [OBJ + PNG ONLY] Unpack
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Resident Evil Player Data (*.pld)|*.PLD|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFile = openFileDialog.FileName;

                    //try catch errors
                    try
                    {
                        EmdTool.Unpack(selectedFile, CurrentBioVersion, EmdTool.Format.Editable);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        private void repackEditableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //RE3 PLD Editable Repack
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder containing the unpacked PLD files (OBJ + PNG)";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string sourceDir = folderDialog.SelectedPath;
                    //try catch errors
                    try
                    {
                        EmdTool.RepackFromFolder(sourceDir, CurrentBioVersion, EmdTool.Format.Editable, "pld");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void unpackToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            //RE2 EMD [MD1 + TIM] Unpack 
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Resident Evil Enemy Data (*.emd)|*.EMD|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFile = openFileDialog.FileName;

                    //try catch errors
                    try
                    {
                        //Call EMD tool and pass our paths and version to unpack as Original format
                        EmdTool.Unpack(selectedFile, CurrentBioVersion, EmdTool.Format.Original);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void repackToolStripMenuItem4_Click(object sender, EventArgs e)
        {
            //RE2 EMD [MD1 + TIM] Repack
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder containing the unpacked EMD files (MD1 + TIM)";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string sourceDir = folderDialog.SelectedPath;
                    //try catch errors
                    try
                    {
                        EmdTool.RepackFromFolder(sourceDir, CurrentBioVersion, EmdTool.Format.Original, "emd");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void unpackEditableToolStripMenuItem4_Click(object sender, EventArgs e)
        {
            //RE2 EMD [OBJ + PNG] Unpack
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Resident Evil Enemy Data (*.emd)|*.EMD|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFile = openFileDialog.FileName;

                    //try catch errors
                    try
                    {
                        //Call EMD tool and pass our paths and version to unpack as Original format
                        EmdTool.Unpack(selectedFile, CurrentBioVersion, EmdTool.Format.Editable);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void repackEditableToolStripMenuItem4_Click(object sender, EventArgs e)
        {
            //RE2 EMD [OBJ + PNG] Repack
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder containing the unpacked EMD files (OBJ + PNG)";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string sourceDir = folderDialog.SelectedPath;
                    //try catch errors
                    try
                    {
                        EmdTool.RepackFromFolder(sourceDir, CurrentBioVersion, EmdTool.Format.Editable, "emd");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void unpackOriginalToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            //RE2 PLD [MD1 + TIM]  Unpack
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Resident Evil Player Data (*.pld)|*.PLD|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFile = openFileDialog.FileName;
                    //try catch errors
                    try
                    {
                        //Call EMD tool and pass our paths and version to unpack as original format
                        EmdTool.Unpack(selectedFile, CurrentBioVersion, EmdTool.Format.Original);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void repackOriginalToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            //RE2 PLD [MD1 + TIM] Repack
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder containing the unpacked PLD files (MD1 + TIM)";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string sourceDir = folderDialog.SelectedPath;
                    //try catch errors
                    try
                    {
                        EmdTool.RepackFromFolder(sourceDir, CurrentBioVersion, EmdTool.Format.Original, "pld");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void unpackEditableToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            //RE2 PLD [OBJ + PNG] Unpack
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Resident Evil Player Data (*.pld)|*.PLD|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFile = openFileDialog.FileName;
                    //try catch errors
                    try
                    {
                        EmdTool.Unpack(selectedFile, CurrentBioVersion, EmdTool.Format.Editable);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void repackEditableToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            //RE2 PLD [OBJ + PNG] Repack
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder containing the unpacked PLD files (OBJ + PNG)";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string sourceDir = folderDialog.SelectedPath;
                    //try catch errors
                    try
                    {
                        EmdTool.RepackFromFolder(sourceDir, CurrentBioVersion, EmdTool.Format.Editable, "pld");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void unpackOriginalToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //RE1 EMD [MD1 + TIM] Unpack
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Resident Evil Enemy Data (*.emd)|*.EMD|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFile = openFileDialog.FileName;

                    //try catch errors
                    try
                    {
                        //Call EMD tool and pass our paths and version to unpack as Original format
                        EmdTool.Unpack(selectedFile, CurrentBioVersion, EmdTool.Format.Original);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void repackOriginalToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //RE1 EMD [MD1 + TIM] Repack
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder containing the unpacked EMD files (MD1 + TIM)";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string sourceDir = folderDialog.SelectedPath;
                    //try catch errors
                    try
                    {
                        EmdTool.RepackFromFolder(sourceDir, CurrentBioVersion, EmdTool.Format.Original, "emd");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        private void unpackEditableToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //RE1 EMD [OBJ + PNG] Unpack
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Resident Evil Enemy Data (*.emd)|*.EMD|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFile = openFileDialog.FileName;

                    //try catch errors
                    try
                    {
                        //Call EMD tool and pass our paths and version to unpack as Original format
                        EmdTool.Unpack(selectedFile, CurrentBioVersion, EmdTool.Format.Editable);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void repackEditableToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //RE1 EMD [OBJ + PNG] Repack
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder containing the unpacked EMD files (OBJ + PNG)";
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    string sourceDir = folderDialog.SelectedPath;
                    //try catch errors
                    try
                    {
                        EmdTool.RepackFromFolder(sourceDir, CurrentBioVersion, EmdTool.Format.Editable, "emd");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}

using System;
using System.IO;
using System.Text;
using System.Media;
using System.Reflection;
using IntelOrca.Biohazard;
using System.Windows.Forms;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Collections.Generic;
using Tool_Hazard.Biohazard;
using Tool_Hazard.Biohazard.emd;
using Tool_Hazard.Biohazard.RDT;
using Tool_Hazard.Forms;
using Tool_Hazard.Nintendo;
using Tool_Hazard.Sony_PS1;
using Tool_Hazard.White_Day;
using static System.Net.Mime.MediaTypeNames;

namespace Tool_Hazard
{
    public partial class Main : Form
    {
        private readonly ushort[] SonnoriLz77Key = { 0xFF21, 0x834F, 0x675F, 0x0034, 0xF237, 0x815F, 0x4765, 0x0233 };
        private readonly Encoding EucKr;
        public static Main Instance { get; private set; }
        //private NopCompression selectedCompression = NopCompression.None;

        //Other C# files to use functions from should be defined here, so we can use them in Form1/Main.cs/Main Window??
        //Rebirth Manager
        private readonly RebirthManager rebirth = new RebirthManager();
        private BioVersion CurrentBioVersion;
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
            UpdateStatus("Ready");

            // Sync menu check state from saved setting
            playStartupSoundToolStripMenuItem.Checked = Properties.Settings.Default.PlayStartupSound;

            playStartupSoundToolStripMenuItem.CheckedChanged += (_, __) =>
            {
                Properties.Settings.Default.PlayStartupSound = playStartupSoundToolStripMenuItem.Checked;
                Properties.Settings.Default.Save();
            };
        }
        // --------------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------------

        // Play the embedded WAV at startupPlays a .wav sound embedded as a resource in the assembly.
        // The resource name is the full namespace path to the .wav file.
        public static void PlayEmbeddedWav(string resourceName)
        {
            var asm = Assembly.GetExecutingAssembly();
            using Stream? s = asm.GetManifestResourceStream(resourceName);
            if (s == null) return;

            using var player = new SoundPlayer(s);
            player.Play();
        }

        //Update Status Bar Text
        public void UpdateStatus(string text)
        {
            //Just as a fail safe
            try
            {
                //toolStripStatusLabel1.Text = text;
                //statusStrip1.Refresh();
                if (InvokeRequired)
                {
                    Invoke(new Action(() => toolStripStatusLabel1.Text = text));
                }
                else
                {
                    toolStripStatusLabel1.Text = text;
                }
            }
            //Fail safe/exeption message triggered if a exception is caught
            catch (Exception ex)
            {
                MessageBox.Show($"Error Updating Status Bar to '{text}'.\n\nThe error is: {ex}", "UpdateStatus Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --------------------------------------------------------------------
        // Menu Hooks
        // --------------------------------------------------------------------

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

                                var archive = new SonnoriArchive();
                                archive.UnpackNopFile(filePath, outputDir);
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

        //File -> Exit
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

                        SonnoriArchive.NopCompression method = SonnoriArchive.NopCompression.None;
                        if (result == DialogResult.Yes)
                            method = SonnoriArchive.NopCompression.Lz77;
                        else if (result == DialogResult.No)
                            method = SonnoriArchive.NopCompression.Sonnori;

                        var archive = new SonnoriArchive();
                        archive.RepackNopFile(sourceDir, nopFilePath, method);
                        MessageBox.Show($"Repacking complete: {nopFilePath}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }

        }

        //White Day Font Editor
        private void fontEditorToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            // Open Font Editor form
            using (var fontEditor = new WhiteDayFontEditor())
            {
                fontEditor.ShowDialog();
            }
        }

        //Clasic Rebirth Installers Menu Hooks

        //RE1 Classic Rebirth Installer Menu Hook
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
                    await rebirth.Install(BioVersion.Biohazard1, dlg.SelectedPath);
            }
        }

        //RE2 Classic Rebirth Installer Menu Hook
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
                    await rebirth.Install(BioVersion.Biohazard2, dlg.SelectedPath);
            }
        }

        //RE3 Classic Rebirth Installer Menu Hook
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
                    await rebirth.Install(BioVersion.Biohazard3, dlg.SelectedPath);
            }
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

            var unpacker = new RdtUnpacker(BioVersion.Biohazard1, ofd.FileName, outputDir);
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

            var packer = new RdtPacker(BioVersion.Biohazard1, ofd.FileName);
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

            var unpacker = new RdtUnpacker(BioVersion.Biohazard2, ofd.FileName, outputDir);
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

            var packer = new RdtPacker(BioVersion.Biohazard2, ofd.FileName);
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

            var unpacker = new RdtUnpacker(BioVersion.Biohazard3, ofd.FileName, outputDir);
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

            var packer = new RdtPacker(BioVersion.Biohazard3, ofd.FileName);
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
            var unpacker = new RdtUnpacker(BioVersion.Biohazard2, ofd.FileName, outputDir);
            unpacker.Unpack();

            // HDR path inside temp
            var hdrPath = Path.Combine(outputDir,
                Path.GetFileNameWithoutExtension(ofd.FileName) + ".hdr");
            var newRDTpath = Path.Combine(outputDir,
    Path.GetFileNameWithoutExtension(ofd.FileName) + ".RDT");
            MessageBox.Show("HDR File Path:\n\n" + hdrPath, "DEBUG", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

            // 2) Repack as Bio3
            var packer = new RdtPacker(BioVersion.Biohazard3, hdrPath);
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
            var unpacker = new RdtUnpacker(BioVersion.Biohazard3, ofd.FileName, tempFolder.SelectedPath + Path.GetFileNameWithoutExtension(ofd.FileName));
            unpacker.Unpack();

            // HDR path inside temp
            var hdrPath = Path.Combine(tempFolder.SelectedPath,
                Path.GetFileNameWithoutExtension(ofd.FileName) + ".hdr");

            // 2) Repack as Bio2
            var packer = new RdtPacker(BioVersion.Biohazard2, hdrPath);
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
                    ScdService.DecompileScd(scdPath, BioVersion.Biohazard3);
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
                    ScdService.ExtractScdFromRdt(rdtPath, outputPath, BioVersion.Biohazard2);
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
                    ScdService.ExtractScdFromRdt(rdtPath, outputPath, BioVersion.Biohazard3);
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
                    ScdService.DecompileScd(scdPath, BioVersion.Biohazard2);
                }
            }
        }

        //Load SCD Editor with selected game version
        private void sCDOpCodeEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var editor = new OpcodeEditorForm(BioVersion.Biohazard3);
            editor.Show(this);
        }
        private void sCDOpCodeEditorToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            var editor = new OpcodeEditorForm(BioVersion.Biohazard2);
            editor.Show(this);
        }
        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            var editor = new OpcodeEditorForm(BioVersion.Biohazard1);
            editor.Show(this);
        }

        // --- Version select/set based on selected menu strip item --- 

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            CurrentBioVersion = BioVersion.Biohazard1;
            UpdateStatus("Selected Resident Evil 1/Biohazard 1");
        }
        private void bIO151997ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentBioVersion = BioVersion.Biohazard1_5;
            UpdateStatus("Selected Resident Evil 1.5/Biohazard 1.5");
        }
        private void bIO2RE21998ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentBioVersion = BioVersion.Biohazard2;
            UpdateStatus("Selected Resident Evil 2/Biohazard 2");
        }

        private void bIORE31999ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentBioVersion = BioVersion.Biohazard3;
            UpdateStatus("Selected Resident Evil 3/Biohazard 3");
        }
        private void rESURVVBIOGUNSURVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentBioVersion = BioVersion.BiohazardSurvivor;
            UpdateStatus("Selected Resident Evil Survivor/Biohazard Gun Survivor");
        }

        private void rECVBIOCV2000ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentBioVersion = BioVersion.BiohazardCv;
            UpdateStatus("Selected Resident Evil CODE: Veronica/Biohazard CODE: Veronica");
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
                        EmdTool.Unpack(selectedFile, BioVersion.Biohazard3, EmdTool.Format.Original);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.RepackFromFolder(sourceDir, BioVersion.Biohazard3, EmdTool.Format.Original, "emd");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.Unpack(selectedFile, BioVersion.Biohazard3, EmdTool.Format.Editable);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.RepackFromFolder(sourceDir, BioVersion.Biohazard3, EmdTool.Format.Editable, "emd");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        private void repackEditableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //RE3 PLD Editable [OBJ + PNG ONLY] Repack
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
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.Unpack(selectedFile, BioVersion.Biohazard2, EmdTool.Format.Original);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.RepackFromFolder(sourceDir, BioVersion.Biohazard2, EmdTool.Format.Original, "emd");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.Unpack(selectedFile, BioVersion.Biohazard2, EmdTool.Format.Editable);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.RepackFromFolder(sourceDir, BioVersion.Biohazard2, EmdTool.Format.Editable, "emd");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.Unpack(selectedFile, BioVersion.Biohazard2, EmdTool.Format.Original);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.RepackFromFolder(sourceDir, BioVersion.Biohazard2, EmdTool.Format.Original, "pld");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.Unpack(selectedFile, BioVersion.Biohazard2, EmdTool.Format.Editable);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.RepackFromFolder(sourceDir, BioVersion.Biohazard2, EmdTool.Format.Editable, "pld");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.Unpack(selectedFile, BioVersion.Biohazard1, EmdTool.Format.Original);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.RepackFromFolder(sourceDir, BioVersion.Biohazard1, EmdTool.Format.Original, "emd");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.Unpack(selectedFile, BioVersion.Biohazard1, EmdTool.Format.Editable);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
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
                        EmdTool.RepackFromFolder(sourceDir, BioVersion.Biohazard1, EmdTool.Format.Editable, "emd");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error:\n{ex.Message}", "EMD Tool",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // --- Nintendo LZ77 Decompressor Tool Menu Hook ---
        private void decompressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select a Nintendo LZ77-compressed file (0x10/0x11)",
                Filter = "All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            byte[] input;
            try { input = File.ReadAllBytes(ofd.FileName); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Read failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Try-decompress with validation
            if (!NdsLz77Wii.TryDecompress(input, out var output) || output.Length == 0)
            {
                MessageBox.Show(this,
                    "This file is not a standalone Nintendo LZ77 stream.\n\n" +
                    "It likely starts with 0x10 by coincidence (e.g., container header like FILES=0x10).\n" +
                    "Use: Nintendo DS -> Container Scanner.",
                    "Not a raw LZ stream",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Save decompressed output as...",
                Filter = "All files (*.*)|*.*",
                FileName = Path.GetFileNameWithoutExtension(ofd.FileName) + ".dec"
            };
            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                File.WriteAllBytes(sfd.FileName, output);
                MessageBox.Show(this,
                    $"Decompressed OK.\n\nInput:  {input.Length:N0} bytes\nOutput: {output.Length:N0} bytes",
                    "Done",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Write failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --- Nintendo DS Container Scanner/Extractor Tool Menu Hook ---
        private void containerScannerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select NDS container (Deadly Silence style)",
                Filter = "All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            using var fbd = new FolderBrowserDialog
            {
                Description = "Select output folder",
                UseDescriptionForTitle = true
            };

            if (fbd.ShowDialog(this) != DialogResult.OK)
                return;

            string outputRoot = fbd.SelectedPath;

            try
            {
                using var fs = File.OpenRead(ofd.FileName);

                // 1) Scan container
                var entries = ResidentEvilDeadlySilenceExtractor.Scan(fs);

                if (entries.Count == 0)
                {
                    MessageBox.Show(this, "No entries found.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 2) Extract with proper extension detection
                foreach (var entry in entries)
                {
                    byte[] bytes = ResidentEvilDeadlySilenceExtractor.ExtractEntry(fs, entry);

                    var parts = entry.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);

                    string relPath;
                    if (parts.Length >= 3)
                    {
                        relPath = Path.Combine(parts[0], parts[1], parts[2].TrimEnd('.'));
                    }
                    else
                    {
                        relPath = entry.Name.Replace('/', Path.DirectorySeparatorChar).TrimEnd('.');
                    }

                    string basePath = Path.Combine(outputRoot, relPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(basePath)!);

                    string ext = NintendoMagic.GuessExtension(bytes);
                    if (string.IsNullOrEmpty(ext))
                        ext = entry.IsCompressed ? ".dec" : ".bin";

                    File.WriteAllBytes(basePath + ext, bytes);
                }

                MessageBox.Show(this,
                    $"Extraction complete.\n\nEntries: {entries.Count}",
                    "Done",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(),
                    "Extraction failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // PlayStation TIM Tool - Export TIM to PNG 
        private void tIMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Open TIM",
                Filter = "PlayStation TIM (*.tim)|*.tim|All files (*.*)|*.*"
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            using var sfd = new SaveFileDialog
            {
                Title = "Save PNG",
                Filter = "PNG Image (*.png)|*.png",
                FileName = Path.ChangeExtension(Path.GetFileName(ofd.FileName), ".png")
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                TimPng.ExportTimToPng(ofd.FileName, sfd.FileName);
                MessageBox.Show(this, "Exported PNG successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "TIM → PNG failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //PNG to Sony PS1 TIM Tool - Import PNG to TIM
        private void pNG2TIMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Open PNG",
                Filter = "PNG Image (*.png)|*.png|All files (*.*)|*.*"
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            using var sfd = new SaveFileDialog
            {
                Title = "Save TIM",
                Filter = "PlayStation TIM (*.tim)|*.tim",
                FileName = Path.ChangeExtension(Path.GetFileName(ofd.FileName), ".tim")
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                TimPng.ImportPngToTimFile(ofd.FileName, sfd.FileName);
                MessageBox.Show(this, "Imported TIM successfully (8bpp).");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "PNG → TIM failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //Classic Resident Evil MSG Tool (Bio1/Bio2/Bio3)
        private void mSGToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var editor = new ClassicREmsgTool();
            editor.Show(this);
        }

        private void mSGToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            var editor = new ClassicREmsgTool();
            editor.Show(this);
        }

        private void mSGToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            var editor = new ClassicREmsgTool();
            editor.Show(this);
        }

        //Classic Rebirth BGM XML Editor Menu Hooks
        private void bGMXMLEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var editor = new RebirthBgmXmlEditorForm();
            editor.Show(this);
        }

        private void bGMXMLEditorToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            var editor = new RebirthBgmXmlEditorForm();
            editor.Show(this);
        }

        private void bGMXMLEditorToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            var editor = new RebirthBgmXmlEditorForm();
            editor.Show(this);
        }
        private void bGMXMLEditorToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            var editor = new RebirthBgmXmlEditorForm();
            editor.Show(this);
        }

        //PIX/TIM viewer menu hooks
        private void pIXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var editor = new Biohazard_PIX_Viewer();
            editor.Show(this);
        }

        private void pIXToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            var editor = new Biohazard_PIX_Viewer();
            editor.Show(this);
        }

        //BSS Manager/Viewer Hookers
        private void bSSToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var editor = new BssViewerForm();
            editor.Show(this);
        }

        private void bSSToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            var editor = new BssViewerForm();
            editor.Show(this);
        }

        private void bSSToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            var editor = new BssViewerForm();
            editor.Show(this);
        }

        //File Editor Menu Hooks
        private void fileEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //BiohazardFileEditor
            var editor = new BiohazardFileEditor();
            editor.Show(this);
        }

        private void fileEditorToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            var editor = new BiohazardFileEditor();
            editor.Show(this);
        }

        private void fileEditorToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            var editor = new BiohazardFileEditor();
            editor.Show(this);
        }

        private void fileEditorToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            var editor = new BiohazardFileEditor();
            editor.Show(this);
        }
    }
}

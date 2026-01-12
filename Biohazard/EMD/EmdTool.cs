using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard;
using IntelOrca.Biohazard.BioRand;
using IntelOrca.Biohazard.Model;

namespace Tool_Hazard.Biohazard.emd
{
    public static class EmdTool
    {
        public enum Format { Original, Editable }

        /// <summary>
        /// Unpack an .emd or .pld into a folder (Original = MD1/MD2+TIM, Editable = OBJ+PNG).
        /// Output folder is <inputdir>\<filename-without-ext>\
        /// </summary>
        public static void Unpack(string inputFile, BioVersion version, Format format)
        {
            if (string.IsNullOrWhiteSpace(inputFile))
                throw new ArgumentException("Input file path is empty.");

            if (!File.Exists(inputFile))
                throw new FileNotFoundException("File not found.", inputFile);

            var outputDir = Path.Combine(
                Path.GetDirectoryName(inputFile)!,
                Path.GetFileNameWithoutExtension(inputFile)
            );
            Directory.CreateDirectory(outputDir);

            var ext = Path.GetExtension(inputFile).ToLowerInvariant();
            if (ext != ".emd" && ext != ".pld")
                throw new InvalidOperationException("Input must be .emd or .pld");

            // Load model + optional tim (matches CLI behavior)
            ModelFile modelFile;
            TimFile? timFile = null;

            if (ext == ".emd")
            {
                modelFile = new EmdFile(version, inputFile);

                // CLI expects external TIM next to the EMD: EMxx.TIM
                var timPath = Path.ChangeExtension(inputFile, ".tim");
                if (File.Exists(timPath))
                    timFile = new TimFile(timPath);
            }
            else
            {
                // PLD carries its own TIM in the file
                var pldFile = new PldFile(version, inputFile);
                modelFile = pldFile;
                timFile = pldFile.Tim;
            }

            // Output paths (same naming strategy as CLI, just folder-based)
            var baseName = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputFile));
            var outObj = baseName + ".obj";
            var outPng = baseName + ".png";
            var outMd1 = baseName + ".md1";
            var outMd2 = baseName + ".md2";
            var outTim = baseName + ".tim";

            var objExporter = new ObjExporter();

            if (format == Format.Original)
            {
                // Original = dump MD + TIM (and ALSO dump OBJ if you want later)
                if (version == BioVersion.Biohazard2)
                {
                    // RE2: MD1 primary, CLI also writes converted MD2
                    File.WriteAllBytes(outMd1, modelFile.Md1.Data.ToArray());

                    var meshConverter = new MeshConverter();
                    var md2 = meshConverter.ConvertMesh(modelFile.Md1, BioVersion.Biohazard3);
                    File.WriteAllBytes(outMd2, md2.Data.ToArray());
                }
                else
                {
                    // RE1/RE3: MD2
                    File.WriteAllBytes(outMd2, modelFile.Md2.Data.ToArray());
                }

                if (timFile != null)
                {
                    // Save TIM (editable PNG is separate mode)
                    timFile.Save(outTim);
                }
            }
            else
            {
                // Editable = OBJ + PNG
                if (version == BioVersion.Biohazard2)
                {
                    objExporter.Export(modelFile.Md1, outObj, 3);
                }
                else
                {
                    objExporter.Export(modelFile.Md2, outObj, 3);
                }

                // CLI creates a PNG preview by tiling pages (x/128)
                timFile?.ToBitmap((x, y) => x / 128).Save(outPng);
            }
        }

        /// <summary>
        /// Repack an EMD/PLD based on files inside a folder.
        /// - If Original: expects .md2 (and .md1 for RE2) and optional .tim
        /// - If Editable: expects .obj and/or .png
        /// Writes <folder>.emd or <folder>.pld depending on targetKind
        /// </summary>
        public static void RepackFromFolder(
            string inputDir,
            BioVersion version,
            Format format,
            string targetKind // "emd" or "pld"
        )
        {
            if (string.IsNullOrWhiteSpace(inputDir))
                throw new ArgumentException("Input directory is empty.");

            if (!Directory.Exists(inputDir))
                throw new DirectoryNotFoundException(inputDir);

            targetKind = (targetKind ?? "").Trim().ToLowerInvariant();
            if (targetKind != "emd" && targetKind != "pld")
                throw new ArgumentException("targetKind must be \"emd\" or \"pld\"");

            var outputBase = inputDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var outEmdPath = outputBase + ".emd";
            var outPldPath = outputBase + ".pld";
            var outTimPath = outputBase + ".tim"; // only used for EMD case like CLI

            // Find inputs (same scanning logic as CLI)
            var files = Directory.GetFiles(inputDir);
            string? md1Path = files.FirstOrDefault(x => x.EndsWith(".md1", StringComparison.OrdinalIgnoreCase));
            string? md2Path = files.FirstOrDefault(x => x.EndsWith(".md2", StringComparison.OrdinalIgnoreCase));
            string? objPath = files.FirstOrDefault(x => x.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
            string? pngPath = files.FirstOrDefault(x => x.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
            string? timPath = files.FirstOrDefault(x => x.EndsWith(".tim", StringComparison.OrdinalIgnoreCase));

            bool importing = (format == Format.Editable)
                ? (objPath != null || pngPath != null)
                : (md2Path != null || md1Path != null || timPath != null);

            if (!importing)
                throw new InvalidOperationException("No suitable input files found in folder for the selected mode.");

            // Create target container
            ModelFile modelFile;
            TimFile? timFile = null;

            if (targetKind == "emd")
            {
                modelFile = new EmdFile(version, new MemoryStream()); // load empty via stream
            }
            else
            {
                // PLD is RE2/RE3 only in this tool
                modelFile = new PldFile(version, new MemoryStream());
            }

            // Import model data
            if (format == Format.Original)
            {
                if (version == BioVersion.Biohazard2)
                {
                    if (md1Path == null)
                        throw new InvalidOperationException("RE2 original repack requires a .md1 file in the folder.");

                    modelFile.Md1 = new Md1(File.ReadAllBytes(md1Path));
                }
                else
                {
                    if (md2Path == null)
                        throw new InvalidOperationException("Original repack requires a .md2 file in the folder.");

                    modelFile.Md2 = new Md2(File.ReadAllBytes(md2Path));
                }

                if (timPath != null)
                    timFile = new TimFile(timPath);
            }
            else
            {
                if (objPath != null)
                {
                    var objImporter = new ObjImporter();
                    objImporter.Import(modelFile.Version, objPath, 3);
                }

                if (pngPath != null)
                    timFile = ImportTimFile(pngPath);
            }

            // Apply TIM if present (matches CLI behavior)
            if (timFile != null)
            {
                if (modelFile is PldFile pld)
                {
                    pld.Tim = timFile;
                }
                else
                {
                    // EMD keeps tim as external file like CLI
                    timFile.Save(outTimPath);
                }
            }

            // Save final container
            if (modelFile is PldFile pldFile)
                pldFile.Save(outPldPath);
            else if (modelFile is EmdFile emdFile)
                emdFile.Save(outEmdPath);
        }

        // ====== Copied straight from CLI logic ======

        private static TimFile ImportTimFile(string path)
        {
            using (var bitmap = (Bitmap)Bitmap.FromFile(path))
            {
                var timFile = new TimFile(bitmap.Width, bitmap.Height, 8);
                var clutIndex = 0;

                for (int x = 0; x < bitmap.Width; x += 128)
                {
                    var srcBounds = new Rectangle(x, 0, Math.Min(bitmap.Width - x, 128), bitmap.Height);
                    var colours = GetColours(bitmap, srcBounds);
                    timFile.SetPalette(clutIndex, colours);
                    timFile.ImportBitmap(bitmap, srcBounds, x, 0, clutIndex);
                    clutIndex++;
                }

                return timFile;
            }
        }

        private static ushort[] GetColours(Bitmap bitmap, Rectangle area)
        {
            var coloursList = new ushort[256];
            var coloursIndex = 1;
            var colours = new HashSet<ushort>();

            for (int y = area.Top; y < area.Bottom; y++)
            {
                for (int x = area.Left; x < area.Right; x++)
                {
                    var c32 = bitmap.GetPixel(x, y);
                    var c16 = TimFile.Convert32to16((uint)c32.ToArgb());
                    if (colours.Add(c16))
                    {
                        coloursList[coloursIndex++] = c16;
                        if (coloursIndex == 256)
                            return coloursList;
                    }
                }
            }
            return coloursList;
        }
    }
}

// Ensure you reference the biohazard-utils library
//using Biohazard.RE1;
//using Biohazard.RE2;
//using Biohazard.RE3;
using IntelOrca.Biohazard.Model;
using System;
using System.IO;
// You might need other namespaces depending on the specific library version (e.g. Biohazard.Common)

public class EmdTool
{
    public enum GameVersion { RE1, RE2, RE3 }
    public enum Format { Original, Editable }

    public void Unpack(string inputFile, GameVersion version, Format format)
    {
        string outputDir = Path.Combine(Path.GetDirectoryName(inputFile), Path.GetFileNameWithoutExtension(inputFile));
        Directory.CreateDirectory(outputDir);

        switch (version)
        {
            case GameVersion.RE1:
                UnpackRe1(inputFile, outputDir, format);
                break;
            case GameVersion.RE2:
                UnpackRe2(inputFile, outputDir, format);
                break;
            case GameVersion.RE3:
                UnpackRe3(inputFile, outputDir, format);
                break;
        }
    }

    public void Repack(string inputDir, GameVersion version, Format format)
    {
        // Logic assumes inputDir contains the unpacked files (OBJ/PNG or MD2/TIM)
        // Output usually goes to a new file next to the folder
        string outputFile = inputDir + (version == GameVersion.RE3 ? ".emd" : ".emd"); // Extension might vary

        switch (version)
        {
            case GameVersion.RE1:
                RepackRe1(inputDir, outputFile, format);
                break;
            case GameVersion.RE2:
                RepackRe2(inputDir, outputFile, format);
                break;
            case GameVersion.RE3:
                RepackRe3(inputDir, outputFile, format);
                break;
        }
    }

    // --- Specific Implementations (Adapting logic from the original Program.cs) ---

    private void UnpackRe3(string file, string outDir, Format format)
    {
        // Example implementation based on library usage
        //var emd = new EmdFile(file);
        if (format == Format.Editable)
        {
            // Convert to Obj/Png
            // emd.ToObj().Save(Path.Combine(outDir, "model.obj"));
            // emd.GetTim().ToBitmap().Save(Path.Combine(outDir, "texture.png"));
        }
        else
        {
            // Extract raw MD2/TIM
            // File.WriteAllBytes(Path.Combine(outDir, "model.md2"), emd.Md2Data);
            // emd.GetTim().Save(Path.Combine(outDir, "texture.tim"));
        }
    }

    private void RepackRe3(string inDir, string outFile, Format format)
    {
        if (format == Format.Editable)
        {
            // Load OBJ/PNG and create EMD
        }
        else
        {
            // Load MD2/TIM and create EMD
        }
    }

    private void UnpackRe2(string file, string outDir, Format format)
    {
        // RE2 Logic
    }

    private void RepackRe2(string inDir, string outFile, Format format)
    {
        // RE2 Logic
    }

    private void UnpackRe1(string file, string outDir, Format format)
    {
        // RE1 Logic
    }

    private void RepackRe1(string inDir, string outFile, Format format)
    {
        // RE1 Logic
    }
}
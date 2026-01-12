//Adapted from https://raw.githubusercontent.com/biorand/biohazard-utils/refs/heads/master/src/scd/Program.cs
//Using Intel Orca's Biohazard Net libraries
using IntelOrca.Biohazard;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Compilation;
using System.IO;
using System.Reflection;
using Tool_Hazard;
//using Biohazard.Script;

public static class ScdService
{
    //Diassemble function; Ported as is from original
    public static string Diassemble(BioVersion version, BioScriptKind kind, ScdProcedureList scd, bool listing = false)
    {
        var scdReader = new ScdReader();
        return scdReader.Diassemble(scd, version, kind, listing);
    }

    // RDT → SCD; Extract SCD/Script Data from RDT
    public static void ExtractScdFromRdt(
            string rdtPath,
            string outputScdPath,
            BioVersion version)
    {
        try
        {
            var rdtFile = Rdt.FromFile(version, rdtPath);
            var outputDir = Path.GetDirectoryName(outputScdPath)!;
            Directory.CreateDirectory(outputDir);

            if (version == BioVersion.Biohazard1)
            {
                var rdt1 = (Rdt1)rdtFile;
                var eventScd = rdt1.EventSCD;

                for (int i = 0; i < eventScd.Count; i++)
                {
                    var outPath = Path.Combine(
                        outputDir,
                        $"event_{i:X2}.scd");

                    eventScd[i].Data.WriteToFile(outPath);
                }
            }
            else if (version == BioVersion.BiohazardCv)
            {
                var rdtCv = (RdtCv)rdtFile;
                var outPath = Path.Combine(outputDir, "main.scd");
                rdtCv.Script.Data.WriteToFile(outPath);
            }
            else
            {
                var rdt2 = (Rdt2)rdtFile;

                rdt2.SCDINIT.Data.WriteToFile(
                    Path.Combine(outputDir, "init.scd"));

                if (version != BioVersion.Biohazard3)
                {
                    rdt2.SCDMAIN.Data.WriteToFile(
                        Path.Combine(outputDir, "main.scd"));
                }
            }

            MessageBox.Show($"Successfully extracted to:\n{outputDir}", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"SCD Service Error: {ex}", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }


    // SCD → RDT; replace script inside RDT
    public static void RecompileScdToRdt(
        string originalRdtPath,
        string scdPath,
        string outputRdtPath,
        BioVersion version)
    {
        /*
        var rdt = new RdtFile(
            File.ReadAllBytes(originalRdtPath),
            version);

        rdt.Script = new ScdFile(
            File.ReadAllBytes(scdPath),
            version);

        File.WriteAllBytes(outputRdtPath, rdt.ToBytes());
        */
    }

    // SCD → .S/.LST; Decompile .SCD to readable Biohazard Script Op Code instructions
    public static void DecompileScd(
        string scdPath,
        BioVersion version)
    {
        BioScriptKind kind;

        // Choose script kind based on game
        if (version == BioVersion.Biohazard1)
        {
            // BIO1 uses event scripts
            kind = BioScriptKind.Event;
        }
        else if (version == BioVersion.Biohazard2)
        {
            // BIO2 uses MAIN
            kind = BioScriptKind.Main;
        }
        else if (version == BioVersion.Biohazard3)
        {
            // BIO3 INIT script
            kind = BioScriptKind.Init;
        }
        else if (version == BioVersion.BiohazardCv)
        {
            // CV MAIN script
            kind = BioScriptKind.Main;
        }
        else
        {
            throw new NotSupportedException(
                $"Unsupported BioVersion: {version}");
        }

        try
        {
            // Adapted from original CLI
            var data = File.ReadAllBytes(scdPath);
            var procedureList = new ScdProcedureList(version, data);

            var sText = Diassemble(version, kind, procedureList);
            var sPath = Path.ChangeExtension(scdPath, ".s");
            File.WriteAllText(sPath, sText);

            var lstText = Diassemble(version, kind, procedureList, listing: true);
            var lstPath = Path.ChangeExtension(scdPath, ".lst");
            File.WriteAllText(lstPath, lstText);

            //Show success dialog box
            var SCDSuccessMSG = $"Successfully decompiled to:\n\n{sPath}\n\n{lstPath}";
            MessageBox.Show(
                SCDSuccessMSG, "Success!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            
            //call update status from Main.cs?
            //Main.Instance?.UpdateStatus(SCDSuccessMSG);

        }
        catch (Exception ex)
        {
            //Show error diaogue box
            var errorMSG = $"SCD Service Error {ex}";
            MessageBox.Show(errorMSG, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            
            //call update status from Main.cs?
            //Main.Instance?.UpdateStatus(errorMSG);

        }
    }


    // .S → SCD; convert decompile code back to compiled SCD
    public static void CompileScd(
        string asmPath,
        string scdPath,
        BioVersion version)
    {
        /*
        var scd = ScdCompiler.Compile(
            File.ReadAllText(asmPath),
            version);

        File.WriteAllBytes(scdPath, scd.ToBytes());
        */
    }
}

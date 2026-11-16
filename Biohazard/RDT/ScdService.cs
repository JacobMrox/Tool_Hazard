//Adapted from https://raw.githubusercontent.com/biorand/biohazard-utils/refs/heads/master/src/scd/Program.cs
//Using Intel Orca's Biohazard Net libraries
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script;
using IntelOrca.Biohazard.Script.Compilation;
using Tool_Hazard.IntelOrca.Biohazard;
using Tool_Hazard.IntelOrca.Biohazard.Extensions;
using Tool_Hazard.IntelOrca.Biohazard.Room;
using Tool_Hazard.IntelOrca.Biohazard.Script;
using Tool_Hazard.IntelOrca.Biohazard.Script.Compilation;
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

                rdt1.InitSCD.Data.WriteToFile(
                    Path.Combine(outputDir, "init.scd"));
                rdt1.MainSCD.Data.WriteToFile(
                    Path.Combine(outputDir, "main.scd"));

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
        if (!File.Exists(originalRdtPath))
            throw new FileNotFoundException("Original RDT not found.", originalRdtPath);
        if (!File.Exists(scdPath))
            throw new FileNotFoundException("Compiled SCD not found.", scdPath);

        var scriptName = Path.GetFileNameWithoutExtension(scdPath).ToLowerInvariant();
        var scriptData = File.ReadAllBytes(scdPath);
        byte[] output;

        if (version == BioVersion.Biohazard1)
        {
            var builder = new Rdt1(originalRdtPath).ToBuilder();
            if (scriptName == "init")
            {
                builder.InitSCD = new ScdProcedureContainer(scriptData);
            }
            else if (scriptName == "main")
            {
                builder.MainSCD = new ScdProcedureContainer(scriptData);
            }
            else if (scriptName == "event")
            {
                builder.EventSCD = new ScdEventList(scriptData);
            }
            else if (scriptName.StartsWith("event_", StringComparison.Ordinal) &&
                     int.TryParse(scriptName.AsSpan(6), System.Globalization.NumberStyles.HexNumber,
                         provider: null, out var eventIndex))
            {
                var events = builder.EventSCD.ToBuilder();
                if ((uint)eventIndex >= (uint)events.Events.Count)
                    throw new ArgumentOutOfRangeException(nameof(scdPath),
                        $"BIO1 event index {eventIndex:X2} is outside this room's event table.");
                events.Events[eventIndex] = new ScdEvent(scriptData);
                builder.EventSCD = events.ToEventList();
            }
            else
            {
                throw new ArgumentException(
                    "BIO1 SCD filename must be init.scd, main.scd, event.scd, or event_XX.scd.",
                    nameof(scdPath));
            }

            output = builder.ToRdt().Data.ToArray();
            _ = new Rdt1(output);
        }
        else if (version == BioVersion.Biohazard2 || version == BioVersion.Biohazard3)
        {
            var builder = new Rdt2(version, originalRdtPath).ToBuilder();
            var script = new ScdProcedureList(version, scriptData);
            if (scriptName == "init")
            {
                builder.SCDINIT = script;
            }
            else if (scriptName == "main" && version == BioVersion.Biohazard2)
            {
                builder.SCDMAIN = script;
            }
            else
            {
                var expected = version == BioVersion.Biohazard2
                    ? "init.scd or main.scd"
                    : "init.scd";
                throw new ArgumentException($"{version} SCD filename must be {expected}.", nameof(scdPath));
            }

            output = builder.ToRdt().Data.ToArray();
            _ = new Rdt2(version, output);
        }
        else if (version == BioVersion.BiohazardCv)
        {
            if (scriptName != "main")
                throw new ArgumentException("Code Veronica SCD filename must be main.scd.", nameof(scdPath));

            var builder = new RdtCv(originalRdtPath).ToBuilder();
            builder.Script = new ScdProcedureList(version, scriptData);
            output = builder.ToRdt().Data.ToArray();
            _ = new RdtCv(output);
        }
        else
        {
            throw new NotSupportedException($"Unsupported BioVersion: {version}");
        }

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputRdtPath));
        if (!string.IsNullOrEmpty(outputDirectory))
            Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(outputRdtPath, output);
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


    // .S → SCD; compile a decompiled Biohazard script back into SCD procedure data.
    // The script's own "#version" directive selects the target game version.
    // Returns the paths of the written .scd files (init.scd and/or main.scd).
    public static string[] CompileScd(string asmPath, string outputDirectory)
    {
        if (!File.Exists(asmPath))
            throw new FileNotFoundException("Script not found.", asmPath);

        Directory.CreateDirectory(outputDirectory);

        var compiler = new IntelOrca.Biohazard.Script.Compilation.ScdCompiler();
        var includer = new SimpleFileIncluder(Path.GetDirectoryName(asmPath)!);
        var result = compiler.Generate(includer, asmPath);

        if (result != 0 || compiler.Errors.ErrorCount != 0)
        {
            var messages = string.Join(
                Environment.NewLine,
                compiler.Errors.Errors
                    .Where(e => e.Kind == ErrorKind.Error)
                    .Select(e => e.ToString()));

            throw new InvalidOperationException(
                $"Script compilation failed:{Environment.NewLine}{Environment.NewLine}{messages}");
        }

        var written = new List<string>();
        foreach (var operation in compiler.Operations)
        {
            if (operation is not ScdRdtEditOperation scdOperation)
                continue;

            var fileName = scdOperation.Kind switch
            {
                BioScriptKind.Init => "init.scd",
                BioScriptKind.Main => "main.scd",
                BioScriptKind.Event => "event.scd",
                _ => null
            };

            // Operations carrying raw Rdt2 SCD bytes always have a non-empty
            // Data payload; container/event (Rdt1) operations don't.
            if (fileName is null || scdOperation.Data.Data.Length == 0)
                continue;

            var outPath = Path.Combine(outputDirectory, fileName);
            File.WriteAllBytes(outPath, scdOperation.Data.Data.ToArray());
            written.Add(outPath);
        }

        if (written.Count == 0)
            throw new InvalidOperationException("The compiled script produced no SCD data.");

        return written.ToArray();
    }
}

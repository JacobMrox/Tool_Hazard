// Alias the form type so it doesn't conflict with Program.Main()
using System.Collections;
using System.Globalization;
using MainForm = Tool_Hazard.Main;
using Tool_Hazard.Localization;
using Tool_Hazard.UI;

namespace Tool_Hazard
{
    /// <summary>The main entry point for the application.</summary>
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            int nabMotion = Array.FindIndex(args,
                a => string.Equals(a, "--probe-nab-motion", StringComparison.OrdinalIgnoreCase));
            if (nabMotion >= 0)
            {
                if (nabMotion + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Usage: Tool Hazard --probe-nab-motion source.nab");
                    Environment.ExitCode = 2;
                    return;
                }
                RunNabMotionProbe(args[nabMotion + 1]);
                return;
            }

            int nabRoundTrip = Array.FindIndex(args,
                a => string.Equals(a, "--probe-nab-part-roundtrip", StringComparison.OrdinalIgnoreCase));
            if (nabRoundTrip >= 0)
            {
                if (nabRoundTrip + 4 >= args.Length || !int.TryParse(args[nabRoundTrip + 2], out int part))
                {
                    Console.Error.WriteLine("Usage: Tool Hazard --probe-nab-part-roundtrip source.nab part exported.obj rebuilt.nab");
                    Environment.ExitCode = 2;
                    return;
                }
                RunNabPartRoundTripProbe(args[nabRoundTrip + 1], part, args[nabRoundTrip + 3], args[nabRoundTrip + 4]);
                return;
            }

            int hmdRoundTrip = Array.FindIndex(args,
                a => string.Equals(a, "--probe-hmd-roundtrip", StringComparison.OrdinalIgnoreCase));
            if (hmdRoundTrip >= 0)
            {
                if (hmdRoundTrip + 3 >= args.Length)
                {
                    Console.Error.WriteLine("Usage: Tool Hazard --probe-hmd-roundtrip source.hmd exported.obj rebuilt.hmd");
                    Environment.ExitCode = 2;
                    return;
                }
                RunHmdRoundTripProbe(args[hmdRoundTrip + 1], args[hmdRoundTrip + 2], args[hmdRoundTrip + 3]);
                return;
            }

            // Developer probe harness (Survivor HMD texture diagnostics). Run with --probe.
            if (args.Any(a => string.Equals(a, "--probe", StringComparison.OrdinalIgnoreCase)))
            {
                RunProbe();
                return;
            }

            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            // Initialize the application configuration (e.g. play startup sound if enabled)
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            // Load translation dictionaries and apply the persisted language before any
            // form (including the main window) is constructed.
            LocalizationManager.Initialize();
            FluentTheme.Initialize();

            // Launched elevated via "Game Boost" (UAC relaunch) - open just that panel.
            if (args.Any(a => string.Equals(a, "--gameboost", StringComparison.OrdinalIgnoreCase)))
            {
                var boost = new Forms.GameBoostForm();
                FluentTheme.Apply(boost);
                Application.Run(boost);
                return;
            }

            var main = new MainForm();
            FluentTheme.Apply(main);

            main.Shown += (_, __) =>
            {
                if (Properties.Settings.Default.PlayStartupSound)
                {
                    // This calls the *form type* method (static), not Program.Main()
                    MainForm.PlayEmbeddedWav("Tool_Hazard.Resources.POWER-ON.WAV");
                }
            };

            Application.Run(main);
        }

        /// <summary>
        /// Dumps Survivor HMD texture statistics for the paths listed in probe_paths.txt.
        /// Diagnostic only; never runs during a normal start.
        /// </summary>
        static void RunProbe()
        {
            string outPath = Path.Combine(Path.GetTempPath(), "opencode", "hmdresult.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

            static void Log(string outPath, string s) => File.AppendAllText(outPath, "\n" + s);

            File.WriteAllText(outPath, "START");
            var watchdog = new Thread(() =>
            {
                Thread.Sleep(600000);
                Log(outPath, "WATCHDOG TIMEOUT");
                Environment.Exit(2);
            });
            watchdog.IsBackground = true;
            watchdog.Start();

            string pathsFile = Path.Combine(Path.GetTempPath(), "opencode", "probe_paths.txt");
            if (!File.Exists(pathsFile))
            {
                Log(outPath, $"MISSING {pathsFile}");
                return;
            }

            var formType = typeof(Forms.BiohazardModelViewerForm);
            var hmdType = formType.GetNestedType("SurvivorHmd", System.Reflection.BindingFlags.NonPublic)!;
            var fromFile = hmdType.GetMethod("FromFile", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
            var loader = formType.GetMethod("LoadHmdTexturedParts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

            foreach (string path in File.ReadAllLines(pathsFile))
            {
                Log(outPath, $"=== {path} ===");
                try
                {
                    var hmd = fromFile.Invoke(null, new object[] { path })!;
                    var result = loader.Invoke(null, new object[] { hmd, path })!;
                    var rt = result.GetType();
                    var parts = (IList)rt.GetField("Item1")!.GetValue(result)!;
                    var texture = rt.GetField("Item2")!.GetValue(result);
                    var desc = (string)rt.GetField("Item3")!.GetValue(result)!;
                    int faces = 0, textured = 0, opaque = 0, sampled = 0;
                    var partType = parts[0]!.GetType();
                    var facesProp = partType.GetProperty("Faces")!;
                    var faceType = facesProp.PropertyType.GetGenericArguments()[0];
                    var clutProp = faceType.GetProperty("Clut")!;
                    var pageProp = faceType.GetProperty("Page")!;
                    var uvProp = faceType.GetProperty("UV")!;
                    var getPixel = texture?.GetType().GetMethod("GetPixel");
                    int texW = texture == null ? 0 : (int)texture.GetType().GetProperty("Width")!.GetValue(texture)!;
                    int texH = texture == null ? 0 : (int)texture.GetType().GetProperty("Height")!.GetValue(texture)!;
                    var pages = new HashSet<int>();
                    foreach (var part in parts)
                    {
                        var list = (IList)facesProp.GetValue(part)!;
                        foreach (var face in list)
                        {
                            faces++;
                            int clut = (int)clutProp.GetValue(face)!;
                            int page = Convert.ToInt32(pageProp.GetValue(face));
                            pages.Add(page);
                            if (clut < 0) continue;
                            textured++;
                            if (getPixel != null)
                            {
                                var uvs = (System.Numerics.Vector2[])uvProp.GetValue(face)!;
                                int u = (int)uvs[0].X + page * 128;
                                int v = (int)uvs[0].Y;
                                if (u >= 0 && u < texW && v >= 0 && v < texH)
                                {
                                    sampled++;
                                    uint argb = (uint)getPixel.Invoke(texture, new object[] { u, v, clut })!;
                                    if ((argb >> 24) != 0) opaque++;
                                }
                            }
                        }
                    }
                    Log(outPath, $" parts={parts.Count} faces={faces} textured={textured} atlas={texW}x{texH} pages={string.Join(",", pages)} sampled={sampled} opaque={opaque}");
                    Log(outPath, $" desc={desc}");
                }
                catch (Exception ex)
                {
                    var inner = ex is System.Reflection.TargetInvocationException tie ? tie.InnerException ?? ex : ex;
                    Log(outPath, $" ERROR {inner.GetType().Name}: {inner.Message}");
                }
            }
            Log(outPath, "DONE");
        }

        /// <summary>
        /// Exercises the exact HMD OBJ export/replacement path without opening the UI. A no-edit
        /// round trip must reproduce every byte, proving that packet topology and non-mesh blocks
        /// are preserved while the signed 16-bit vertex positions are written back.
        /// </summary>
        static void RunHmdRoundTripProbe(string sourcePath, string objPath, string rebuiltPath)
        {
            try
            {
                var formType = typeof(Forms.BiohazardModelViewerForm);
                var hmdType = formType.GetNestedType("SurvivorHmd", System.Reflection.BindingFlags.NonPublic)!;
                var fromFile = hmdType.GetMethod("FromFile", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
                object hmd = fromFile.Invoke(null, new object[] { sourcePath })!;
                hmdType.GetMethod("ExportObj", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
                    .Invoke(hmd, new object[] { objPath, -1 });
                hmdType.GetMethod("ReplaceFromObj", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
                    .Invoke(hmd, new object[] { objPath, -1 });
                byte[] rebuilt = (byte[])hmdType.GetProperty("Data", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
                    .GetValue(hmd)!;
                File.WriteAllBytes(rebuiltPath, rebuilt);
                byte[] source = File.ReadAllBytes(sourcePath);
                if (!source.AsSpan().SequenceEqual(rebuilt))
                    throw new InvalidDataException("The no-edit HMD OBJ round trip changed file bytes.");
                Console.WriteLine($"PASS: {source.Length} HMD bytes preserved through multipart OBJ export/replacement.");
            }
            catch (Exception ex)
            {
                var inner = ex is System.Reflection.TargetInvocationException tie ? tie.InnerException ?? ex : ex;
                Console.Error.WriteLine($"FAIL: {inner.GetType().Name}: {inner.Message}");
                Environment.ExitCode = 1;
            }
        }

        static void RunNabPartRoundTripProbe(string sourcePath, int part, string objPath, string rebuiltPath)
        {
            try
            {
                var formType = typeof(Forms.BiohazardModelViewerForm);
                var nabType = formType.GetNestedType("SurvivorNab", System.Reflection.BindingFlags.NonPublic)!;
                var fromFile = nabType.GetMethod("FromFile", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
                object nab = fromFile.Invoke(null, new object[] { sourcePath })!;
                nabType.GetMethod("ExportObj", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
                    .Invoke(nab, new object[] { objPath, part });
                nabType.GetMethod("ReplaceFromObj", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
                    .Invoke(nab, new object[] { objPath, part, 0, 0 });
                nabType.GetMethod("Save", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
                    .Invoke(nab, new object[] { rebuiltPath });
                fromFile.Invoke(null, new object[] { rebuiltPath });
                Console.WriteLine($"PASS: NAB part {part} exported, reimported, saved and parsed.");
            }
            catch (Exception ex)
            {
                var inner = ex is System.Reflection.TargetInvocationException tie ? tie.InnerException ?? ex : ex;
                Console.Error.WriteLine($"FAIL: {inner.GetType().Name}: {inner.Message}");
                Environment.ExitCode = 1;
            }
        }

        static void RunNabMotionProbe(string sourcePath)
        {
            try
            {
                var formType = typeof(Forms.BiohazardModelViewerForm);
                var nabType = formType.GetNestedType("SurvivorNab", System.Reflection.BindingFlags.NonPublic)!;
                var fromFile = nabType.GetMethod("FromFile", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
                object nab = fromFile.Invoke(null, new object[] { sourcePath })!;
                int nodes = (int)nabType.GetProperty("NodeCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!.GetValue(nab)!;
                int selectors = (int)nabType.GetProperty("SelectorCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!.GetValue(nab)!;
                var bind = (System.Numerics.Matrix4x4[])nabType.GetMethod("GetBindGlobalPose", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!.Invoke(nab, null)!;
                var getPose = nabType.GetMethod("GetGlobalPose", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!;
                for (int node = 0; node < nodes; node++)
                {
                    float maximum = 0;
                    for (int selector = 0; selector < selectors; selector++)
                    for (int frame = 0; frame <= 120; frame += 10)
                    {
                        var pose = (System.Numerics.Matrix4x4[])getPose.Invoke(nab, new object[] { selector, frame })!;
                        float delta = Math.Abs(pose[node].M11 - bind[node].M11) + Math.Abs(pose[node].M12 - bind[node].M12)
                            + Math.Abs(pose[node].M21 - bind[node].M21) + Math.Abs(pose[node].M22 - bind[node].M22)
                            + Math.Abs(pose[node].M41 - bind[node].M41) + Math.Abs(pose[node].M42 - bind[node].M42)
                            + Math.Abs(pose[node].M43 - bind[node].M43);
                        maximum = Math.Max(maximum, delta);
                    }
                    Console.WriteLine($"node {node}: max global pose delta {maximum:R}");
                }
            }
            catch (Exception ex)
            {
                var inner = ex is System.Reflection.TargetInvocationException tie ? tie.InnerException ?? ex : ex;
                Console.Error.WriteLine($"FAIL: {inner.GetType().Name}: {inner.Message}");
                Environment.ExitCode = 1;
            }
        }
    }
}

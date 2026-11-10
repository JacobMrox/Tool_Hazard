// Alias the form type so it doesn't conflict with Program.Main()
using System.Collections;
using System.Globalization;
using MainForm = Tool_Hazard.Main;
using Tool_Hazard.UI;

namespace Tool_Hazard
{
    /// <summary>The main entry point for the application.</summary>
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
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
    }
}

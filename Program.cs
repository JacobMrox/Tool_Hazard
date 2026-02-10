// Alias the form type so it doesn't conflict with Program.Main()
using System.Globalization;
using MainForm = Tool_Hazard.Main;

namespace Tool_Hazard
{
    /// <summary>The main entry point for the application.</summary>
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            // Initialize the application configuration (e.g. play startup sound if enabled)
            // To customize application configuration such as set high DPI settings or default font, 
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            var main = new MainForm();

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
    }
}
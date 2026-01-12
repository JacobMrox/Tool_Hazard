using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using SevenZipExtractor;

public enum RebirthGame
{
    RE1,
    RE2,
    RE3
}

public class RebirthManager
{
    private readonly HttpClient _http = new HttpClient();

    // Direct 7z files (the actual targets behind the “Download” buttons)
    private const string RE1_URL = "https://classicrebirth.com/index.php/download/resident-evil-dll-fix-for-classic-edition/?wpdmdl=381&refresh=691b62859375e1763402373";
    private const string RE2_URL = "https://classicrebirth.com/index.php/download/resident-evil-2-classic-rebirth/?wpdmdl=390&refresh=691b6273e6eeb1763402355";
    private const string RE3_URL = "https://classicrebirth.com/index.php/download/resident-evil-3-classic-rebirth/?wpdmdl=1327&refresh=691b622263d691763402274";

    // Classic Rebirth always uses this file
    private const string CR_DLL = "ddraw.dll";

    private string GetDownloadUrl(RebirthGame game)
    {
        return game switch
        {
            RebirthGame.RE1 => RE1_URL,
            RebirthGame.RE2 => RE2_URL,
            RebirthGame.RE3 => RE3_URL,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    // ----- Detection -----

    public bool IsInstalled(string gameDir)
    {
        return File.Exists(Path.Combine(gameDir, CR_DLL));
    }

    public string GetInstalledVersion(string gameDir)
    {
        string dll = Path.Combine(gameDir, CR_DLL);

        if (!File.Exists(dll))
            return null;

        try
        {
            var info = FileVersionInfo.GetVersionInfo(dll);
            return info.FileVersion;
        }
        catch
        {
            return null;
        }
    }

    // ----- Installation -----

    public async Task Install(RebirthGame game, string gameDir)
    {
        if (!Directory.Exists(gameDir))
        {
            MessageBox.Show("Selected game directory does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // If already installed → confirm update
        if (IsInstalled(gameDir))
        {
            string version = GetInstalledVersion(gameDir);
            System.Media.SystemSounds.Exclamation.Play();//Play sound to grab attention
            DialogResult ask = MessageBox.Show(
                $"Classic Rebirth detected.\nInstalled version: {version}\n\nUpdate to latest?",
                "Classic Rebirth Installer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (ask == DialogResult.No)
                return;
        }

        string url = GetDownloadUrl(game);
        string temp7z = Path.Combine(Path.GetTempPath(), $"{game}_CR.7z");

        try
        {
            // Download the archive
            using (var resp = await _http.GetAsync(url))
            {
                resp.EnsureSuccessStatusCode();
                await using var fs = new FileStream(temp7z, FileMode.Create);
                await resp.Content.CopyToAsync(fs);
            }

            // Extract archive
            if (!Extract7z(temp7z, gameDir))
            {
                MessageBox.Show("Extraction failed. Make sure 7z.exe exists in /7zip/ folder.", "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
                return;
            }

            MessageBox.Show($"{game} Classic Rebirth installed/updated successfully.", "Done!");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Installation error:\n{ex.Message}", "Error");
        }
        finally
        {
            if (File.Exists(temp7z))
                File.Delete(temp7z);
        }
    }
    // ----- Extract using 7z.exe -----

    private bool Extract7z(string archive, string outputDir)
    {
        try
        {
            using (var archiveFile = new ArchiveFile(archive))
            {
                foreach (var entry in archiveFile.Entries)
                {
                    if (!entry.IsFolder)
                        entry.Extract(Path.Combine(outputDir, entry.FileName));
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Extraction failed:\n{ex.Message}");
            return false;
        }
    }
}

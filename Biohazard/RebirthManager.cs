using IntelOrca.Biohazard;
using SevenZipExtractor;
using System.Diagnostics;

public class RebirthManager
{
    private readonly HttpClient _http = new HttpClient();

    // Direct 7z files (the actual targets behind the “Download” buttons)
    private const string RE1_URL = "https://classicrebirth.com/index.php/download/resident-evil-dll-fix-for-classic-edition/?wpdmdl=381&refresh=691b62859375e1763402373";
    private const string RE2_URL = "https://classicrebirth.com/index.php/download/resident-evil-2-classic-rebirth/?wpdmdl=390&refresh=691b6273e6eeb1763402355";
    private const string RE3_URL = "https://classicrebirth.com/index.php/download/resident-evil-3-classic-rebirth/?wpdmdl=1327&refresh=691b622263d691763402274";
    private const string RE_SUR = "https://classicrebirth.com/index.php/download/resident-evil-3-classic-rebirth/?wpdmdl=1327&refresh=691b622263d691763402274";

    // Classic Rebirth always uses this file
    private const string CR_DLL = "ddraw.dll";

    private string GetDownloadUrl(BioVersion version)
    {
        return version switch
        {
            BioVersion.Biohazard1 => RE1_URL,
            BioVersion.Biohazard2 => RE2_URL,
            BioVersion.Biohazard3 => RE3_URL,
            BioVersion.BiohazardSurvivor => RE_SUR,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    // ----- Detection & Version Check -----

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

    // ----- Extracts the DLL from the archive to a temp location and checks its version ----- 
    private string GetArchiveDllVersion(string archivePath)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CR_VersionCheck_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            using (var archive = new ArchiveFile(archivePath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsFolder &&
                        entry.FileName.Equals(CR_DLL, StringComparison.OrdinalIgnoreCase))
                    {
                        string dllPath = Path.Combine(tempDir, CR_DLL);
                        entry.Extract(dllPath);

                        var info = FileVersionInfo.GetVersionInfo(dllPath);
                        return info.FileVersion;
                    }
                }
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }

        return null;
    }


    // ----- Installation -----

    public async Task Install(BioVersion version, string gameDir)
    {
        if (!Directory.Exists(gameDir))
        {
            MessageBox.Show("Selected game directory does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string url = GetDownloadUrl(version);
        string temp7z = Path.Combine(Path.GetTempPath(), $"{version}_CR.7z");

        try
        {
            // Download the archive
            using (var resp = await _http.GetAsync(url))
            {
                resp.EnsureSuccessStatusCode();
                await using var fs = new FileStream(temp7z, FileMode.Create);
                await resp.Content.CopyToAsync(fs);
            }

            // If already installed -> compare versions FIRST
            if (IsInstalled(gameDir))
            {
                string install_ver = GetInstalledVersion(gameDir);
                string archive_ver = GetArchiveDllVersion(temp7z);

                // If we could read both versions and they match -> no need to update
                if (!string.IsNullOrEmpty(install_ver) &&
                    !string.IsNullOrEmpty(archive_ver) &&
                    string.Equals(install_ver, archive_ver, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        $"Classic Rebirth is already up to date.\n\nInstalled version: {install_ver}\nLatest version: {archive_ver ?? "Unknown"}",
                        "No Update Needed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    return;
                }

                // Otherwise ask to update (only when it actually differs / unknown)
                System.Media.SystemSounds.Exclamation.Play(); // grab attention
                DialogResult ask = MessageBox.Show(
                    $"Classic Rebirth detected.\nInstalled version: {install_ver ?? "Unknown"}\nAvailable version: {archive_ver ?? "Unknown"}\n\nUpdate to latest?",
                    "Classic Rebirth Installer",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (ask == DialogResult.No)
                    return;
            }

            // Extract archive
            if (!Extract7z(temp7z, gameDir))
            {
                MessageBox.Show(
                    "Extraction failed. Make sure 7z.exe exists in /7zip/ folder.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            MessageBox.Show($"{version} Classic Rebirth installed/updated successfully.", "Done!");
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

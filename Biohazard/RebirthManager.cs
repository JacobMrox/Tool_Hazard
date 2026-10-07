using System;
using IntelOrca.Biohazard;
using SevenZipExtractor;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;

public class RebirthManager
{
    private readonly HttpClient _http;

    // Optional callback used by main.cs to update the main form status bar.
    private readonly Action<string> _statusCallback;

    private void SetStatus(string status)
    {
        try
        {
            _statusCallback?.Invoke(status);
        }
        catch
        {
            // Status reporting must never interrupt installation.
        }
    }

    // -------------------------------------------------------------------------
    // Download URLs
    // -------------------------------------------------------------------------

    private const string RE1_URL =
        "https://classicrebirth.com/index.php/download/resident-evil-dll-fix-for-classic-edition/?wpdmdl=381&refresh=691b62859375e1763402373";

    private const string RE2_URL =
        "https://classicrebirth.com/index.php/download/resident-evil-2-classic-rebirth/?wpdmdl=390&refresh=691b6273e6eeb1763402355";

    private const string RE3_URL =
        "https://classicrebirth.com/index.php/download/resident-evil-3-classic-rebirth/?wpdmdl=1327&refresh=691b622263d691763402274";

    private const string RE_SUR =
        "https://classicrebirth.com/index.php/download/resident-evil-3-classic-rebirth/?wpdmdl=1327&refresh=691b622263d691763402274";

    // Classic Rebirth DLL
    private const string CR_DLL = "ddraw.dll";


    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public RebirthManager(Action<string> statusCallback = null)
    {
        _statusCallback = statusCallback;

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,

            AutomaticDecompression =
                DecompressionMethods.GZip |
                DecompressionMethods.Deflate |
                DecompressionMethods.Brotli
        };

        _http = new HttpClient(handler);

        // Make the request look like a normal browser request.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/151.0.0.0 Safari/537.36"
        );

        _http.Timeout = TimeSpan.FromMinutes(10);
    }


    // -------------------------------------------------------------------------
    // Get download URL
    // -------------------------------------------------------------------------

    private string GetDownloadUrl(BioVersion version)
    {
        return version switch
        {
            BioVersion.Biohazard1 => RE1_URL,
            BioVersion.Biohazard2 => RE2_URL,
            BioVersion.Biohazard3 => RE3_URL,
            BioVersion.BiohazardSurvivor => RE_SUR,

            _ => throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                "Unsupported BioVersion."
            )
        };
    }


    // -------------------------------------------------------------------------
    // Installation detection
    // -------------------------------------------------------------------------

    public bool IsInstalled(string gameDir)
    {
        return File.Exists(
            Path.Combine(gameDir, CR_DLL)
        );
    }


    // -------------------------------------------------------------------------
    // Get installed DLL version
    // -------------------------------------------------------------------------

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


    // -------------------------------------------------------------------------
    // Detect archive type
    //
    // Returns:
    // "7z"
    // "zip"
    // null = unknown/invalid
    // -------------------------------------------------------------------------

    private string GetArchiveType(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return null;

            byte[] header = new byte[8];

            using (var fs = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                int bytesRead = fs.Read(
                    header,
                    0,
                    header.Length
                );

                if (bytesRead < 4)
                    return null;
            }

            // -------------------------------------------------------------
            // 7z signature:
            //
            // 37 7A BC AF 27 1C
            // -------------------------------------------------------------

            if (header[0] == 0x37 &&
                header[1] == 0x7A &&
                header[2] == 0xBC &&
                header[3] == 0xAF &&
                header[4] == 0x27 &&
                header[5] == 0x1C)
            {
                return "7z";
            }

            // -------------------------------------------------------------
            // ZIP signatures:
            //
            // 50 4B 03 04 = normal ZIP
            // 50 4B 05 06 = empty ZIP
            // 50 4B 07 08 = spanned ZIP
            // -------------------------------------------------------------

            if (header[0] == 0x50 &&
                header[1] == 0x4B &&
                (
                    (header[2] == 0x03 && header[3] == 0x04) ||
                    (header[2] == 0x05 && header[3] == 0x06) ||
                    (header[2] == 0x07 && header[3] == 0x08)
                ))
            {
                return "zip";
            }

            return null;
        }
        catch
        {
            return null;
        }
    }


    // -------------------------------------------------------------------------
    // Get DLL version from archive
    //
    // Supports both ZIP and 7z.
    // -------------------------------------------------------------------------

    private string GetArchiveDllVersion(string archivePath)
    {
        string archiveType = GetArchiveType(archivePath);

        if (archiveType == null)
            return null;

        string tempDir = Path.Combine(
            Path.GetTempPath(),
            "CR_VersionCheck_" +
            Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(tempDir);

        try
        {
            // =============================================================
            // ZIP
            // =============================================================

            if (archiveType == "zip")
            {
                using (var zip = ZipFile.OpenRead(archivePath))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        if (!entry.Name.Equals(
                                CR_DLL,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string dllPath = Path.Combine(
                            tempDir,
                            CR_DLL
                        );

                        entry.ExtractToFile(
                            dllPath,
                            true
                        );

                        if (!File.Exists(dllPath))
                            return null;

                        var info =
                            FileVersionInfo.GetVersionInfo(dllPath);

                        return info.FileVersion;
                    }
                }
            }

            // =============================================================
            // 7Z
            // =============================================================

            else if (archiveType == "7z")
            {
                using (var archive =
                    new ArchiveFile(archivePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.IsFolder)
                            continue;

                        string fileName =
                            Path.GetFileName(entry.FileName);

                        if (!fileName.Equals(
                                CR_DLL,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string dllPath = Path.Combine(
                            tempDir,
                            CR_DLL
                        );

                        entry.Extract(dllPath);

                        if (!File.Exists(dllPath))
                            return null;

                        var info =
                            FileVersionInfo.GetVersionInfo(dllPath);

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
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }

        return null;
    }


    // -------------------------------------------------------------------------
    // Download archive
    // -------------------------------------------------------------------------

    private async Task<bool> DownloadArchive(
        string url,
        string destination)
    {
        try
        {
            SetStatus("Connecting to Classic Rebirth...");

            using (var response = await _http.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                Uri finalUri =
                    response.RequestMessage?.RequestUri;

                string contentType =
                    response.Content.Headers.ContentType?.MediaType
                    ?? "Unknown";

                SetStatus("Downloading Classic Rebirth...");

                await using (var input =
                    await response.Content.ReadAsStreamAsync())

                await using (var output =
                    new FileStream(
                        destination,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None))
                {
                    await input.CopyToAsync(output);
                }

                SetStatus("Checking downloaded archive...");

                string archiveType =
                    GetArchiveType(destination);

                // ---------------------------------------------------------
                // We successfully downloaded an archive.
                // ---------------------------------------------------------

                if (archiveType != null)
                    return true;


                // ---------------------------------------------------------
                // Not a recognised archive.
                // ---------------------------------------------------------

                long size = 0;

                try
                {
                    size =
                        new FileInfo(destination).Length;
                }
                catch
                {
                    // Ignore.
                }

                SetStatus("Downloaded file is not a valid Classic Rebirth archive.");

                MessageBox.Show(
                    "The downloaded file is not a recognised " +
                    "ZIP or 7z archive.\n\n" +

                    $"Content-Type: {contentType}\n" +
                    $"Downloaded size: {size:N0} bytes\n\n" +

                    $"Final URL:\n{finalUri}\n\n" +

                    "The website may have returned an HTML page " +
                    "or another unexpected response.",
                    "Invalid Download",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            SetStatus("Classic Rebirth download failed.");

            MessageBox.Show(
                $"Could not download Classic Rebirth:\n\n{ex.Message}",
                "Download Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            return false;
        }
        catch (TaskCanceledException)
        {
            SetStatus("Classic Rebirth download cancelled or timed out.");

            MessageBox.Show(
                "The download timed out or was cancelled.",
                "Download Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            return false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Download error:\n\n{ex.Message}",
                "Download Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            return false;
        }
    }


    // -------------------------------------------------------------------------
    // Installation
    // -------------------------------------------------------------------------

    public async Task Install(
        BioVersion version,
        string gameDir)
    {
        SetStatus($"Preparing {version} Classic Rebirth...");

        if (!Directory.Exists(gameDir))
        {
            MessageBox.Show(
                "Selected game directory does not exist.",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            return;
        }

        string url =
            GetDownloadUrl(version);

        string tempArchive = Path.Combine(
            Path.GetTempPath(),
            $"{version}_CR_{Guid.NewGuid():N}.archive"
        );

        try
        {
            // -------------------------------------------------------------
            // Download
            // -------------------------------------------------------------

            SetStatus($"Downloading {version} Classic Rebirth...");

            bool downloaded =
                await DownloadArchive(
                    url,
                    tempArchive
                );

            if (!downloaded)
                return;


            // -------------------------------------------------------------
            // Determine archive type
            // -------------------------------------------------------------

            string archiveType =
                GetArchiveType(tempArchive);

            if (archiveType == null)
            {
                MessageBox.Show(
                    "Unable to determine the downloaded archive type.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                return;
            }


            // -------------------------------------------------------------
            // Existing installation
            // -------------------------------------------------------------

            SetStatus("Checking existing Classic Rebirth installation...");

            if (IsInstalled(gameDir))
            {
                string installVer =
                    GetInstalledVersion(gameDir);

                string archiveVer =
                    GetArchiveDllVersion(tempArchive);


                // Same version = nothing to do.
                if (!string.IsNullOrEmpty(installVer) &&
                    !string.IsNullOrEmpty(archiveVer) &&
                    string.Equals(
                        installVer,
                        archiveVer,
                        StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus("Classic Rebirth is already up to date.");

                    MessageBox.Show(
                        "Classic Rebirth is already up to date.\n\n" +
                        $"Installed version: {installVer}\n" +
                        $"Latest version: {archiveVer}",
                        "No Update Needed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );

                    return;
                }


                // Ask before replacing the installation.
                System.Media.SystemSounds.Exclamation.Play();

                DialogResult ask =
                    MessageBox.Show(
                        "Classic Rebirth is already installed.\n\n" +
                        $"Installed version: " +
                        $"{installVer ?? "Unknown"}\n" +
                        $"Available version: " +
                        $"{archiveVer ?? "Unknown"}\n\n" +
                        "Update to the latest version?",
                        "Classic Rebirth Installer",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );

                if (ask == DialogResult.No)
                {
                    SetStatus("Classic Rebirth update cancelled.");
                    return;
                }
            }


            // -------------------------------------------------------------
            // Extract
            // -------------------------------------------------------------

            SetStatus($"Installing {version} Classic Rebirth...");

            bool extracted =
                ExtractArchive(
                    tempArchive,
                    gameDir
                );

            if (!extracted)
                return;


            // -------------------------------------------------------------
            // Success
            // -------------------------------------------------------------

            SetStatus($"{version} Classic Rebirth installed successfully.");

            MessageBox.Show(
                $"{version} Classic Rebirth " +
                "installed/updated successfully.",
                "Done",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            SetStatus("Classic Rebirth installation failed.");

            MessageBox.Show(
                $"Installation error:\n\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
        finally
        {
            try
            {
                if (File.Exists(tempArchive))
                    File.Delete(tempArchive);
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }
    }


    // -------------------------------------------------------------------------
    // Extract ZIP or 7z
    // -------------------------------------------------------------------------

    private bool ExtractArchive(
        string archivePath,
        string outputDir)
    {
        try
        {
            SetStatus("Extracting Classic Rebirth files...");

            string archiveType =
                GetArchiveType(archivePath);

            if (archiveType == null)
            {
                MessageBox.Show(
                    "The downloaded file is not a valid ZIP or 7z archive.",
                    "Extraction Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                return false;
            }

            Directory.CreateDirectory(outputDir);


            // =============================================================
            // ZIP extraction
            // =============================================================

            if (archiveType == "zip")
            {
                using (var zip =
                    ZipFile.OpenRead(archivePath))
                {
                    string fullOutputDir =
                        Path.GetFullPath(outputDir);

                    foreach (var entry in zip.Entries)
                    {
                        // Directory entry
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        string relativePath =
                            entry.FullName
                                .Replace(
                                    '/',
                                    Path.DirectorySeparatorChar
                                )
                                .Replace(
                                    '\\',
                                    Path.DirectorySeparatorChar
                                )
                                .TrimStart(
                                    Path.DirectorySeparatorChar
                                );

                        string destination =
                            Path.GetFullPath(
                                Path.Combine(
                                    outputDir,
                                    relativePath
                                )
                            );

                        // Security check: don't allow an archive
                        // to write outside the selected game directory.
                        if (!destination.StartsWith(
                                fullOutputDir +
                                Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(
                                $"Unsafe archive path detected: " +
                                $"{entry.FullName}"
                            );
                        }

                        string parentDirectory =
                            Path.GetDirectoryName(destination);

                        if (!string.IsNullOrEmpty(parentDirectory))
                        {
                            Directory.CreateDirectory(
                                parentDirectory
                            );
                        }

                        entry.ExtractToFile(
                            destination,
                            true
                        );
                    }
                }

                return true;
            }


            // =============================================================
            // 7z extraction
            // =============================================================

            if (archiveType == "7z")
            {
                using (var archive =
                    new ArchiveFile(archivePath))
                {
                    string fullOutputDir =
                        Path.GetFullPath(outputDir);

                    foreach (var entry in archive.Entries)
                    {
                        if (entry.IsFolder)
                            continue;

                        string relativePath =
                            entry.FileName
                                .Replace(
                                    '/',
                                    Path.DirectorySeparatorChar
                                )
                                .Replace(
                                    '\\',
                                    Path.DirectorySeparatorChar
                                )
                                .TrimStart(
                                    Path.DirectorySeparatorChar
                                );

                        string destination =
                            Path.GetFullPath(
                                Path.Combine(
                                    outputDir,
                                    relativePath
                                )
                            );

                        // Security check.
                        if (!destination.StartsWith(
                                fullOutputDir +
                                Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(
                                $"Unsafe archive path detected: " +
                                $"{entry.FileName}"
                            );
                        }

                        string parentDirectory =
                            Path.GetDirectoryName(destination);

                        if (!string.IsNullOrEmpty(parentDirectory))
                        {
                            Directory.CreateDirectory(
                                parentDirectory
                            );
                        }

                        entry.Extract(destination);
                    }
                }

                return true;
            }


            return false;
        }
        catch (Exception ex)
        {
            SetStatus("Classic Rebirth extraction failed.");

            MessageBox.Show(
                "Extraction failed:\n\n" +
                $"{ex.Message}\n\n" +
                "Archive type: " +
                $"{GetArchiveType(archivePath) ?? "Unknown"}",
                "Extraction Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );

            return false;
        }
    }
}
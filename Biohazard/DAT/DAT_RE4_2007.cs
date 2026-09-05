using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tool_Hazard.Biohazard.DAT
{
    public static class GCAHandler
    {
        public struct ArchiveEntry
        {
            public string FileName;
            public uint Offset;
            public uint Size;
        }

        /// <summary>
        /// Extracts a GCA/DAT archive file into a specified output folder.
        /// </summary>
        /// <param name="archivePath">Path to the .dat / .gca file.</param>
        /// <param name="outputDirectory">Directory to extract files into.</param>
        public static void Extract(string archivePath, string outputDirectory)
        {
            if (!File.Exists(archivePath))
                throw new FileNotFoundException("Specified archive was not found.", archivePath);

            Directory.CreateDirectory(outputDirectory);

            using (FileStream fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                uint entryCount = br.ReadUInt32();
                ArchiveEntry[] entries = new ArchiveEntry[entryCount];

                // Read File Index Table
                for (int i = 0; i < entryCount; i++)
                {
                    byte[] nameBuffer = br.ReadBytes(32);
                    string name = Encoding.ASCII.GetString(nameBuffer).TrimEnd('\0');
                    uint offset = br.ReadUInt32();
                    uint size = br.ReadUInt32();

                    entries[i] = new ArchiveEntry
                    {
                        FileName = name,
                        Offset = offset,
                        Size = size
                    };
                }

                // Extract Payload Data
                for (int i = 0; i < entryCount; i++)
                {
                    fs.Seek(entries[i].Offset, SeekOrigin.Begin);
                    byte[] fileData = br.ReadBytes((int)entries[i].Size);

                    string destPath = Path.Combine(outputDirectory, entries[i].FileName);
                    string destFolder = Path.GetDirectoryName(destPath);

                    if (!string.IsNullOrEmpty(destFolder))
                    {
                        Directory.CreateDirectory(destFolder);
                    }

                    File.WriteAllBytes(destPath, fileData);
                }
            }
        }

        /// <summary>
        /// Packs a directory of files into a GCA/DAT archive.
        /// </summary>
        /// <param name="sourceDirectory">Directory containing files to pack.</param>
        /// <param name="outputArchivePath">Destination archive file path.</param>
        public static void Pack(string sourceDirectory, string outputArchivePath)
        {
            if (!Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException("Source directory does not exist.");

            string[] filePaths = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
            uint entryCount = (uint)filePaths.Length;

            // Calculate initial data offset (4-byte count + entries of 40 bytes each)
            uint headerSize = 4 + (entryCount * 40);
            uint currentOffset = headerSize;

            using (FileStream fs = new FileStream(outputArchivePath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                // Write Header Count
                bw.Write(entryCount);

                // Write Index Entries
                for (int i = 0; i < filePaths.Length; i++)
                {
                    string filePath = filePaths[i];
                    string relativePath = filePath.Substring(sourceDirectory.TrimEnd('\\', '/').Length + 1);

                    byte[] nameBytes = new byte[32];
                    byte[] rawNameBytes = Encoding.ASCII.GetBytes(relativePath);
                    Array.Copy(rawNameBytes, nameBytes, Math.Min(rawNameBytes.Length, 32));

                    FileInfo info = new FileInfo(filePath);
                    uint fileSize = (uint)info.Length;

                    bw.Write(nameBytes);
                    bw.Write(currentOffset);
                    bw.Write(fileSize);

                    currentOffset += fileSize;
                }

                // Write Data Payloads
                for (int i = 0; i < filePaths.Length; i++)
                {
                    byte[] fileData = File.ReadAllBytes(filePaths[i]);
                    bw.Write(fileData);
                }
            }
        }
    }
}
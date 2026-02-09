using System.Text;

namespace ToolHazard.Main
{
    public static class Rofs
    {
        public static void Unpack(string rofsPath, string outputDir)
        {
            if (!File.Exists(rofsPath))
                throw new FileNotFoundException("ROFS file not found.", rofsPath);

            Directory.CreateDirectory(outputDir);

            using var fs = File.OpenRead(rofsPath);
            using var br = new BinaryReader(fs);

            // ---- Header ----
            var magic = Encoding.ASCII.GetString(br.ReadBytes(4));
            if (magic != "ROFS")
                throw new InvalidDataException("Invalid ROFS archive.");

            int fileCount = br.ReadInt32();

            // ---- File table ----
            for (int i = 0; i < fileCount; i++)
            {
                int nameLen = br.ReadInt32();
                string name = Encoding.ASCII.GetString(br.ReadBytes(nameLen));

                int offset = br.ReadInt32();
                int size = br.ReadInt32();

                long curPos = fs.Position;

                fs.Seek(offset, SeekOrigin.Begin);
                byte[] data = br.ReadBytes(size);

                string outPath = Path.Combine(outputDir, name);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.WriteAllBytes(outPath, data);

                fs.Seek(curPos, SeekOrigin.Begin);
            }
        }
    }
}

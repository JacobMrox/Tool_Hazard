using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Tool_Hazard.Main;

namespace Tool_Hazard.White_Day
{
    internal class SonnoriArchive
    {
        private readonly ushort[] SonnoriLz77Key = { 0xFF21, 0x834F, 0x675F, 0x0034, 0xF237, 0x815F, 0x4765, 0x0233 };
        private readonly Encoding EucKr;
        public enum NopCompression
        {
            None,
            Lz77,
            Sonnori
        }
        public void UnpackNopFile(string nopFilePath, string outputDir)
        {
            using (FileStream fs = new FileStream(nopFilePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                fs.Seek(-1, SeekOrigin.End);
                if (br.ReadByte() != 0x12)
                    throw new InvalidDataException("Invalid or corrupted NOP file.");

                fs.Seek(-9, SeekOrigin.End);
                int offset = br.ReadInt32();
                int numFiles = br.ReadInt32();

                byte key = 0;

                for (int i = 0; i < numFiles; i++)
                {
                    fs.Seek(offset, SeekOrigin.Begin);
                    byte nameSize = br.ReadByte();
                    byte type = br.ReadByte();
                    int fileOffset = br.ReadInt32();
                    int encodeSize = br.ReadInt32();
                    int decodeSize = br.ReadInt32();
                    byte[] nameBytes = br.ReadBytes(nameSize + 1);
                    offset += nameSize + 15;

                    if (type == 0x02)
                        key = (byte)decodeSize;
                    else
                        decodeSize ^= key;

                    for (int j = 0; j < nameSize; j++)
                        nameBytes[j] ^= key;

                    string fileName = EucKr.GetString(nameBytes, 0, nameSize);
                    string fullPath = Path.Combine(outputDir, fileName);

                    switch (type)
                    {
                        case 0x00: // RAW
                            fs.Seek(fileOffset, SeekOrigin.Begin);
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                            File.WriteAllBytes(fullPath, br.ReadBytes(encodeSize));
                            break;

                        case 0x01: // LZ77
                            fs.Seek(fileOffset, SeekOrigin.Begin);
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                            File.WriteAllBytes(fullPath, DecompressLz77(br.ReadBytes(encodeSize), decodeSize));
                            break;

                        case 0x02: // Directory
                            Directory.CreateDirectory(fullPath);
                            break;

                        case 0x03: // SONNORI LZ77
                            fs.Seek(fileOffset, SeekOrigin.Begin);
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                            File.WriteAllBytes(fullPath, DecompressSonnoriLz77(br.ReadBytes(encodeSize), decodeSize));
                            break;

                        default:
                            throw new InvalidDataException($"Unknown data type: {type}");
                    }
                }
            }
        }

        public void RepackNopFile(string sourceDir, string outputFilePath, NopCompression compression = NopCompression.None)
        //private void RepackNopFile(string sourceDir, string outputFilePath, selectedCompression)
        {
            byte pathKey = 0x5A;

            // Gather directories & files
            string[] dirs = Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories);
            string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

            // Sort directories so parents come before children
            Array.Sort(dirs, (a, b) => a.Length.CompareTo(b.Length));

            // Build file records
            var fileBlobs = new List<(string rel, byte type, byte[] encoded, int decodedSize)>();

            foreach (var file in files)
            {
                string rel = file.Substring(sourceDir.Length + 1).Replace("\\", "/");
                byte[] original = File.ReadAllBytes(file);
                byte[] encoded = original;
                byte type = 0x00;
                int decodedSize = original.Length;

                if (compression == NopCompression.Lz77)
                {
                    var comp = CompressLz77(original);
                    if (comp.Length < original.Length)
                    {
                        encoded = comp;
                        type = 0x01;
                    }
                }
                else if (compression == NopCompression.Sonnori)
                {
                    var comp = CompressSonnoriLz77(original);
                    if (comp.Length < original.Length)
                    {
                        encoded = comp;
                        type = 0x03;
                    }
                }

                fileBlobs.Add((rel, type, encoded, decodedSize));
            }

            // Prepare header
            using var headerMs = new MemoryStream();
            using var hw = new BinaryWriter(headerMs, EucKr);

            // Write directories first
            foreach (var dir in dirs)
            {
                string rel = dir.Substring(sourceDir.Length + 1).Replace("\\", "/");
                byte[] nameBytes = EucKr.GetBytes(rel);
                for (int i = 0; i < nameBytes.Length; i++) nameBytes[i] ^= pathKey;

                hw.Write((byte)nameBytes.Length);
                hw.Write((byte)0x02);
                hw.Write(0); // offset
                hw.Write(0); // encoded size
                hw.Write((int)pathKey); // store key
                hw.Write(nameBytes);
                hw.Write((byte)0);
            }

            // Keep track of data offset
            int dataOffset = 0;
            var fileHeaders = new List<(byte[] nameBytes, byte type, int offset, int encSize, int decSize)>();

            foreach (var (rel, type, encoded, decSize) in fileBlobs)
            {
                byte[] nameBytes = EucKr.GetBytes(rel);
                for (int i = 0; i < nameBytes.Length; i++) nameBytes[i] ^= pathKey;

                int encSize = encoded.Length;
                int xoredDecode = decSize ^ pathKey;

                fileHeaders.Add((nameBytes, type, dataOffset, encSize, xoredDecode));
                dataOffset += encSize;
            }

            // Write file headers after directories
            foreach (var h in fileHeaders)
            {
                hw.Write((byte)h.nameBytes.Length);
                hw.Write(h.type);
                hw.Write(h.offset);
                hw.Write(h.encSize);
                hw.Write(h.decSize);
                hw.Write(h.nameBytes);
                hw.Write((byte)0);
            }

            // Write final NOP file
            using var fs = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            // Write file data
            foreach (var (_, _, encoded, _) in fileBlobs)
                bw.Write(encoded);

            // Write header after data
            bw.Write(headerMs.ToArray());

            // Footer
            int headerOffset = dataOffset;
            int totalEntries = dirs.Length + fileBlobs.Count;
            bw.Write(headerOffset);
            bw.Write(totalEntries);
            bw.Write((byte)0x12);
        }

        public byte[] CompressSonnoriLz77(byte[] src)
        {
            // Same tokens as LZ77, but:
            //  - flag byte stored as bsrcmask (original), and decoder uses: bmask = bsrcmask ^ 0xC8
            //  - each match token's 16-bit info is XORed with custom key indexed by ((bsrcmask >> 3) & 7)
            var output = new List<byte>(src.Length);
            int i = 0;

            while (i < src.Length)
            {
                // We'll compose tokens for this block, first gather them, then compute flag bytes
                var block = new List<byte>();
                int flagMask = 0;
                int bits = 0;

                while (bits < 8 && i < src.Length)
                {
                    int bestLen = 0;
                    int bestDist = 0;

                    int startWindow = Math.Max(0, i - 0x0FFF);
                    int maxLen = Math.Min(17, src.Length - i);

                    for (int j = i - 1; j >= startWindow; j--)
                    {
                        int dist = i - j;
                        int l = 0;
                        while (l < maxLen && src[j + l] == src[i + l]) l++;
                        if (l >= 2 && l > bestLen && dist >= 1 && dist <= 0x0FFF)
                        {
                            bestLen = l;
                            bestDist = dist;
                            if (bestLen == 17) break;
                        }
                    }

                    if (bestLen >= 2)
                    {
                        flagMask |= (1 << bits); // match
                        ushort info = (ushort)(((bestLen - 2) << 12) | (bestDist & 0x0FFF));
                        // We'll XOR with key later after we know bsrcmask
                        block.Add((byte)(info & 0xFF));
                        block.Add((byte)(info >> 8));
                        i += bestLen;
                    }
                    else
                    {
                        // literal
                        block.Add(src[i++]);
                    }

                    bits++;
                }

                // We need bmask such that decoder does: bmask = bsrcmask ^ 0xC8
                // So choose bsrcmask = flagMask ^ 0xC8
                byte bsrcmask = (byte)(flagMask ^ 0xC8);
                output.Add(bsrcmask);

                // Key index for this block (must match decoder's rule)
                int keyIndex = (bsrcmask >> 3) & 0x07;
                ushort k = SonnoriLz77Key[keyIndex];

                // Now flush tokens, XORing match infos in this block with k.
                // We must walk through the same flag bits (LSB-first) to know which items are pairs (match) vs literals
                int cursor = 0;
                int bitNum = 0;
                while (bitNum < bits)
                {
                    bool isMatch = ((flagMask >> bitNum) & 1) != 0;
                    if (isMatch)
                    {
                        ushort info = (ushort)(block[cursor] | (block[cursor + 1] << 8));
                        info ^= k;
                        output.Add((byte)(info & 0xFF));
                        output.Add((byte)(info >> 8));
                        cursor += 2;
                    }
                    else
                    {
                        output.Add(block[cursor++]); // literal
                    }
                    bitNum++;
                }
            }

            return output.ToArray();
        }
        private byte[] DecompressLz77(byte[] input, int expectedSize)
        {
            List<byte> output = new List<byte>(expectedSize);
            int i = 0, bcnt = 0, bmask = 0;

            while (i < input.Length)
            {
                if (bcnt == 0)
                {
                    bmask = input[i++];
                    bcnt = 8;
                }

                if ((bmask & 1) != 0)
                {
                    if (i + 1 >= input.Length) break;
                    ushort info = BitConverter.ToUInt16(input, i);
                    i += 2;
                    int off = info & 0x0FFF;
                    int len = (info >> 12) + 2;
                    for (int k = 0; k < len; k++)
                        output.Add(output[output.Count - off]);
                }
                else
                {
                    output.Add(input[i++]);
                }

                bmask >>= 1;
                bcnt--;
            }

            return output.ToArray();
        }

        private static byte[] CompressLz77(byte[] src)
        {
            // Greedy encoder. Window up to 4095 bytes, match length 2..17.
            var output = new List<byte>(src.Length);
            int i = 0;
            while (i < src.Length)
            {
                int flagIndex = output.Count;
                output.Add(0); // placeholder for flag byte
                int flagMask = 0;
                int bits = 0;

                while (bits < 8 && i < src.Length)
                {
                    int bestLen = 0;
                    int bestDist = 0;

                    int startWindow = Math.Max(0, i - 0x0FFF);
                    int maxLen = Math.Min(17, src.Length - i);

                    // Find best match
                    for (int j = i - 1; j >= startWindow; j--)
                    {
                        int dist = i - j;
                        int l = 0;
                        while (l < maxLen && src[j + l] == src[i + l]) l++;
                        if (l >= 2 && l > bestLen && dist >= 1 && dist <= 0x0FFF)
                        {
                            bestLen = l;
                            bestDist = dist;
                            if (bestLen == 17) break;
                        }
                    }

                    if (bestLen >= 2)
                    {
                        // flag bit = 1 => match token
                        flagMask |= (1 << bits);
                        ushort info = (ushort)(((bestLen - 2) << 12) | (bestDist & 0x0FFF));
                        output.Add((byte)(info & 0xFF));
                        output.Add((byte)(info >> 8));
                        i += bestLen;
                    }
                    else
                    {
                        // literal
                        output.Add(src[i++]);
                    }

                    bits++;
                }

                // write final flag byte
                output[flagIndex] = (byte)flagMask;
            }

            return output.ToArray();
        }
        private byte[] DecompressSonnoriLz77(byte[] input, int expectedSize)
        {
            List<byte> output = new List<byte>(expectedSize);
            int i = 0, bcnt = 0, bmask = 0, bsrcmask = 0;

            while (i < input.Length)
            {
                if (bcnt == 0)
                {
                    bmask = bsrcmask = input[i++];
                    bmask ^= 0xC8;
                    bcnt = 8;
                }

                if ((bmask & 1) != 0)
                {
                    if (i + 1 >= input.Length) break;
                    ushort info = BitConverter.ToUInt16(input, i);
                    i += 2;
                    info ^= SonnoriLz77Key[(bsrcmask >> 3) & 0x07];
                    int off = info & 0x0FFF;
                    int len = (info >> 12) + 2;
                    for (int k = 0; k < len; k++)
                        output.Add(output[output.Count - off]);
                }
                else
                {
                    output.Add(input[i++]);
                }

                bmask >>= 1;
                bcnt--;
            }

            return output.ToArray();
        }
    }
}

using System.Text;
using SampleHeader = Tool_Hazard.Forms.SampleHeader;

namespace Tool_Hazard
{
    public class PS1AudioService
    {
        // --- ADPCM CONSTANTS ---
        private static readonly double[,] Predictors = new double[,]
        {
            { 0.0, 0.0 },
            { 60.0 / 64.0, 0.0 },
            { 115.0 / 64.0, -52.0 / 64.0 },
            { 98.0 / 64.0, -55.0 / 64.0 },
            { 122.0 / 64.0, -60.0 / 64.0 }
        };

        /// <summary>
        /// Reads the VH file using a Heuristic Strategy focused on finding the VAG size table.
        /// </summary>
        public List<Tool_Hazard.Forms.SampleHeader> ReadVhFile(string vhPath, string vbPath)
        {
            if (!File.Exists(vhPath)) throw new FileNotFoundException("VH file not found", vhPath);
            if (!File.Exists(vbPath)) throw new FileNotFoundException("VB file not found", vbPath);

            long vbLength = new FileInfo(vbPath).Length;
            byte[] vh = File.ReadAllBytes(vhPath);

            var candidates = new List<List<Tool_Hazard.Forms.SampleHeader>>();

            // Strategy A: RE3-like explicit address table (validated; does NOT throw on small VH)
            var re3 = TryReadExplicitAddrTable(vh, vbLength, 0x2B4);
            if (re3.Count > 0) candidates.Add(re3);

            // Strategy B: Your original heuristic size-table scan (supports other banks)
            candidates.Add(ScanVabTable(vhPath, vbLength, 0x20));
            candidates.Add(ScanVabTable(vhPath, vbLength, 0x820));
            candidates.Add(ScanVabTable(vhPath, vbLength, 0x40));
            candidates.Add(ScanVabTable(vhPath, vbLength, 0x0));

            // Strategy C: VB-driven ADPCM stream scan (RE1/RE2 banks)
            var vbScan = ScanAdpcmStreams(vbPath);
            if (vbScan.Count > 0)
                candidates.Add(vbScan);

            // Pick best candidate
            return PickBestCandidate(candidates);
        }

        /// <summary>
        /// Scans for VAB-style Implicit Offsets (Size/8 table).
        /// This assumes tightly packed data in the VB file starting at 0x0.
        /// </summary>
        private List<SampleHeader> ScanVabTable(string vhPath, long vbLength, int explicitOffset)
        {
            var headers = new List<SampleHeader>();
            try
            {
                using (var fs = new FileStream(vhPath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    long tableOffset = explicitOffset;

                    if (fs.Length <= tableOffset) return headers;

                    fs.Seek(tableOffset, SeekOrigin.Begin);
                    uint currentOffset = 0; // The sample data always starts at 0x0 in the VB file

                    // Read up to 1024 entries (max possible samples for a single bank)
                    for (int index = 0; index < 1024 && fs.Position + 2 <= fs.Length; index++)
                    {
                        ushort rawSize = br.ReadUInt16();
                        // Real VAG size is stored in 8-byte blocks (2 VAG blocks = 32 bytes, 16 bytes per block).
                        // The PSX VAG Size in the table is blocks/2, so the multiplier is 8.
                        uint realSize = (uint)rawSize * 8;

                        if (realSize > 0)
                        {
                            // The size must be a multiple of 16 (two ADPCM blocks)
                            if (realSize % 16 != 0)
                            {
                                // If the size isn't 16-byte aligned, it's highly suspicious. Stop this scan.
                                // return new List<SampleHeader>(); // Or just break if we allow continuing after bad data
                                break;
                            }

                            if (currentOffset + realSize <= vbLength)
                            {
                                headers.Add(new SampleHeader
                                {
                                    Index = headers.Count, // Use current count for index
                                    Offset = (int)currentOffset,
                                    Size = (int)realSize
                                });
                                currentOffset += realSize;
                            }
                            else
                            {
                                // Size goes out of bounds. This strategy is either wrong or we've reached the end.
                                break;
                            }
                        }
                        // If realSize is 0, we found a gap (empty slot), and we continue to read the next 2 bytes.
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ScanVabTable (Offset 0x{explicitOffset:X}): {ex.Message}");
            }
            return headers;
        }

        // --- EXTRACTION / REPLACEMENT ---

        public void ExtractSampleToWav(string vbPath, SampleHeader header, string outputPath)
        {
            byte[] vagData;
            using (var fs = new FileStream(vbPath, FileMode.Open, FileAccess.Read))
            {
                if (header.Offset + header.Size > fs.Length)
                    throw new Exception("Sample data extends beyond the end of the VB file.");

                fs.Seek(header.Offset, SeekOrigin.Begin);
                vagData = new byte[header.Size];
                fs.Read(vagData, 0, header.Size);
            }

            // NOTE: Using a fixed sample rate of 22050 Hz.
            short[] pcmData = DecodeAdpcm(vagData);
            WriteWavFile(pcmData, outputPath, 22050);
        }

        public void ReplaceSampleWithWav(string vhPath, string vbPath, SampleHeader header, string inputWavPath)
        {
            byte[] vh = File.ReadAllBytes(vhPath);
            byte[] vb = File.ReadAllBytes(vbPath);

            // 1) Read current address table
            const int addrTableOffset = 0x2B4;
            var addrs = ReadAddrTable(vh, addrTableOffset);
            ushort baseAddr = addrs[0];

            // 2) Convert address table -> sample ranges
            var ranges = AddrTableToRanges(addrs);
            if (header.Index < 0 || header.Index >= ranges.Count)
                throw new ArgumentOutOfRangeException(nameof(header.Index));

            // 3) Read WAV -> PCM16 mono
            var wav = ReadWavPcm16Mono(inputWavPath);

            // Optional: resample here if you want (later)
            // wav = Resample(wav, targetHz);

            // 4) Encode PCM -> real PSX ADPCM frames
            byte[] newAdpcm = EncodePsxAdpcm(wav);

            if (newAdpcm.Length % 16 != 0)
                throw new InvalidDataException("Encoded ADPCM not 16-byte aligned.");

            // 5) Rebuild VB by replacing that range
            var (start, size) = ranges[header.Index];
            using var ms = new MemoryStream(vb.Length - size + newAdpcm.Length);
            ms.Write(vb, 0, start);
            ms.Write(newAdpcm, 0, newAdpcm.Length);
            ms.Write(vb, start + size, vb.Length - (start + size));
            byte[] newVb = ms.ToArray();

            // 6) Build new address table based on new sizes, preserving base address
            var sizes = ranges.Select(r => r.size).ToArray();
            sizes[header.Index] = newAdpcm.Length;

            var newAddrs = BuildAddrTable(baseAddr, sizes);

            // 7) Patch VH: VB size field + address table
            WriteUInt32LE(vh, 0x80, (uint)newVb.Length);
            WriteAddrTable(vh, addrTableOffset, newAddrs);

            // 8) Save
            File.WriteAllBytes(vbPath, newVb);
            File.WriteAllBytes(vhPath, vh);
        }
        // --- CORE ALGORITHMS (Decoders) ---

        private short[] DecodeAdpcm(byte[] vagData)
        {
            List<short> pcmBuffer = new List<short>();
            double s1 = 0;
            double s2 = 0;

            for (int i = 0; i < vagData.Length; i += 16)
            {
                int predictIndex = (vagData[i] >> 4) & 0xF;
                int shiftFactor = vagData[i] & 0xF;
                int flags = vagData[i + 1];
                if (predictIndex > 4) predictIndex = 0;

                for (int j = 2; j < 16; j++)
                {
                    byte b = vagData[i + j];
                    int[] nibbles = { b & 0xF, (b >> 4) & 0xF };

                    foreach (int nibble in nibbles)
                    {
                        int sample = nibble;
                        if ((nibble & 8) != 0) sample |= unchecked((int)0xFFFFFFF0); // Sign extend 4-bit to 32-bit (then cast to double)

                        double sampleD = (double)(sample << shiftFactor);
                        double processed = sampleD + s1 * Predictors[predictIndex, 0] + s2 * Predictors[predictIndex, 1];
                        s2 = s1;
                        s1 = processed;
                        int val = (int)Math.Round(processed);
                        val = Math.Max(-32768, Math.Min(32767, val));
                        pcmBuffer.Add((short)val);
                    }
                }
                // Check for END flag (bit 0)
                if ((flags & 0x01) != 0) break;
            }
            return pcmBuffer.ToArray();
        }

        // --- CORE ALGORITHMS (Encoder) ---
        // Real PSX ADPCM encoder: chooses predictor + shift per 28-sample frame and packs 16-byte frames.
        // Output is raw PSX ADPCM (no VAG header), aligned to 16 bytes. Last frame has END flag set.

        private static readonly int[,] PsxCoef = new int[,]
        {
    {   0,   0 },
    {  60,   0 },
    { 115, -52 },
    {  98, -55 },
    { 122, -60 },
        };

        private byte[] EncodePsxAdpcm(short[] pcm)
        {
            if (pcm == null) throw new ArgumentNullException(nameof(pcm));

            // Pad to multiple of 28 samples (one ADPCM frame)
            int frames = (pcm.Length + 27) / 28;
            int paddedLen = frames * 28;

            short[] padded = new short[paddedLen];
            Array.Copy(pcm, padded, pcm.Length);

            byte[] output = new byte[frames * 16];

            int hist1 = 0; // s1
            int hist2 = 0; // s2

            int outPos = 0;

            for (int f = 0; f < frames; f++)
            {
                // Frame input samples
                int baseIndex = f * 28;

                // Find best predictor + shift
                int bestPred = 0;
                int bestShift = 0;
                long bestErr = long.MaxValue;

                sbyte[] bestNibbles = new sbyte[28];

                for (int pred = 0; pred < 5; pred++)
                {
                    int a = PsxCoef[pred, 0];
                    int b = PsxCoef[pred, 1];

                    for (int shift = 0; shift <= 12; shift++)
                    {
                        int s1 = hist1;
                        int s2 = hist2;

                        long err = 0;
                        sbyte[] nibbles = new sbyte[28];

                        for (int i = 0; i < 28; i++)
                        {
                            int sample = padded[baseIndex + i];

                            // predicted = (a*s1 + b*s2) >> 6 (with rounding)
                            int predicted = ((a * s1) + (b * s2) + 32) >> 6;

                            int diff = sample - predicted;

                            int q = diff >> shift; // quantize
                            if (q > 7) q = 7;
                            if (q < -8) q = -8;

                            nibbles[i] = (sbyte)q;

                            int recon = predicted + (q << shift);

                            // clamp
                            if (recon > 32767) recon = 32767;
                            if (recon < -32768) recon = -32768;

                            int e = sample - recon;
                            err += (long)e * e;

                            // update history
                            s2 = s1;
                            s1 = recon;
                        }

                        if (err < bestErr)
                        {
                            bestErr = err;
                            bestPred = pred;
                            bestShift = shift;
                            Array.Copy(nibbles, bestNibbles, 28);
                        }
                    }
                }

                // Write frame header
                // Byte0: (shift << 4) | predictor
                output[outPos + 0] = (byte)((bestShift << 4) | (bestPred & 0x0F));

                // Byte1: flags
                // We'll set END flag on the last frame after writing everything.
                output[outPos + 1] = 0x00;

                // Pack 28 4-bit signed nibbles into 14 bytes
                int p = outPos + 2;
                for (int i = 0; i < 28; i += 2)
                {
                    int n0 = bestNibbles[i + 0] & 0x0F;
                    int n1 = bestNibbles[i + 1] & 0x0F;
                    output[p++] = (byte)(n0 | (n1 << 4));
                }

                // Reconstruct with best settings to update history for next frame
                {
                    int a = PsxCoef[bestPred, 0];
                    int b = PsxCoef[bestPred, 1];

                    for (int i = 0; i < 28; i++)
                    {
                        int predicted = ((a * hist1) + (b * hist2) + 32) >> 6;
                        int recon = predicted + (bestNibbles[i] << bestShift);

                        if (recon > 32767) recon = 32767;
                        if (recon < -32768) recon = -32768;

                        hist2 = hist1;
                        hist1 = recon;
                    }
                }

                outPos += 16;
            }

            // Set END flag on last frame (bit 0)
            // Byte1 of last frame:
            output[output.Length - 15] = 0x01;

            return output;
        }

        // --- HELPER: WAV IO ---

        private void WriteWavFile(short[] data, string path, int sampleRate)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                int subChunk2Size = data.Length * 2;
                int chunkSize = 36 + subChunk2Size;
                bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(chunkSize);
                bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1);
                bw.Write((short)1);
                bw.Write(sampleRate);
                bw.Write(sampleRate * 2);
                bw.Write((short)2);
                bw.Write((short)16);
                bw.Write(Encoding.ASCII.GetBytes("data"));
                bw.Write(subChunk2Size);
                foreach (short sample in data) bw.Write(sample);
            }
        }

        //Read Wav old way (assumes PCM 16-bit mono)
        private short[] ReadWavFile(string path)
        {
            byte[] fileBytes = File.ReadAllBytes(path);
            int headerSize = 44;
            int sampleCount = (fileBytes.Length - headerSize) / 2;
            short[] data = new short[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                data[i] = BitConverter.ToInt16(fileBytes, headerSize + i * 2);
            }
            return data;
        }

        //Read WAV PCM 16-bit mono or stereo (downmix to mono)
        private static short[] ReadWavPcm16Mono(string path)
        {
            using var br = new BinaryReader(File.OpenRead(path));

            string riff = new string(br.ReadChars(4));
            int riffSize = br.ReadInt32();
            string wave = new string(br.ReadChars(4));
            if (riff != "RIFF" || wave != "WAVE") throw new InvalidDataException("Not a WAV.");

            int channels = 0;
            int sampleRate = 0;
            int bits = 0;
            byte[]? data = null;

            while (br.BaseStream.Position + 8 <= br.BaseStream.Length)
            {
                string id = new string(br.ReadChars(4));
                int size = br.ReadInt32();

                if (id == "fmt ")
                {
                    short fmt = br.ReadInt16();
                    channels = br.ReadInt16();
                    sampleRate = br.ReadInt32();
                    br.ReadInt32(); // byteRate
                    br.ReadInt16(); // blockAlign
                    bits = br.ReadInt16();

                    // skip any extra fmt bytes
                    int remaining = size - 16;
                    if (remaining > 0) br.ReadBytes(remaining);

                    if (fmt != 1 || bits != 16) throw new InvalidDataException("Only PCM 16-bit WAV supported.");
                    if (channels != 1 && channels != 2) throw new InvalidDataException("Only mono/stereo WAV supported.");
                }
                else if (id == "data")
                {
                    data = br.ReadBytes(size);
                }
                else
                {
                    br.BaseStream.Position += size;
                }

                if (data != null && sampleRate != 0) break;
            }

            if (data == null) throw new InvalidDataException("WAV has no data chunk.");

            short[] pcm = new short[data.Length / 2];
            Buffer.BlockCopy(data, 0, pcm, 0, data.Length);

            if (channels == 1) return pcm;

            // Downmix stereo -> mono
            int frames = pcm.Length / 2;
            var mono = new short[frames];
            for (int i = 0; i < frames; i++)
            {
                int l = pcm[i * 2 + 0];
                int r = pcm[i * 2 + 1];
                mono[i] = (short)((l + r) / 2);
            }
            return mono;
        }

        //Scan for ADPCM streams using heuristic (VAB style)
        private List<Tool_Hazard.Forms.SampleHeader> ScanAdpcmStreams(string vbPath)
        {
            var headers = new List<Tool_Hazard.Forms.SampleHeader>();

            byte[] vb = File.ReadAllBytes(vbPath);
            int offset = 0;
            int index = 0;

            while (offset + 16 <= vb.Length)
            {
                int start = offset;
                int size = 0;
                bool foundEnd = false;

                while (offset + 16 <= vb.Length)
                {
                    // ADPCM frame header
                    byte flags = vb[offset + 1];
                    offset += 16;
                    size += 16;

                    // END flag
                    if ((flags & 0x01) != 0)
                    {
                        foundEnd = true;
                        break;
                    }
                }

                // sanity checks
                if (!foundEnd) break;
                if (size < 32) continue; // discard tiny junk

                headers.Add(new Tool_Hazard.Forms.SampleHeader
                {
                    Index = index++,
                    Offset = start,
                    Size = size
                });
            }

            return headers;
        }

        //Helpers

        private List<Tool_Hazard.Forms.SampleHeader> TryReadExplicitAddrTable(byte[] vh, long vbLength, int tableOffset)
        {
            var headers = new List<Tool_Hazard.Forms.SampleHeader>();

            // VH too small? Just return empty (no exception)
            if (vh.Length < tableOffset + 4) return headers;

            // Read all u16 entries from tableOffset
            int maxEntries = (vh.Length - tableOffset) / 2;
            var addrs = new List<ushort>(maxEntries);

            for (int i = 0; i < maxEntries; i++)
                addrs.Add(BitConverter.ToUInt16(vh, tableOffset + i * 2));

            // Trim trailing zeros
            int lastNonZero = addrs.FindLastIndex(x => x != 0);
            if (lastNonZero < 1) return headers;
            addrs = addrs.Take(lastNonZero + 1).ToList();

            // Validate: must be non-decreasing and last * 8 should be <= vbLength and "not absurd"
            int lastBytes = addrs[^1] * 8;
            if (lastBytes <= 0 || lastBytes > vbLength) return headers;

            for (int i = 0; i < addrs.Count - 1; i++)
                if (addrs[i + 1] < addrs[i]) return new List<Tool_Hazard.Forms.SampleHeader>();

            // Convert to ranges
            int idx = 0;
            for (int i = 0; i < addrs.Count - 1; i++)
            {
                int start = addrs[i] * 8;
                int end = addrs[i + 1] * 8;
                int size = end - start;

                if (size <= 0) continue;
                if (start < 0 || end > vbLength) break;
                if ((size % 16) != 0) continue; // PSX ADPCM frame alignment is a strong signal

                headers.Add(new Tool_Hazard.Forms.SampleHeader
                {
                    Index = idx++,
                    Offset = start,
                    Size = size
                });
            }

            // If it found "too few", treat as not-a-match
            if (headers.Count < 2) return new List<Tool_Hazard.Forms.SampleHeader>();

            return headers;
        }

        private List<Tool_Hazard.Forms.SampleHeader> PickBestCandidate(List<List<Tool_Hazard.Forms.SampleHeader>> candidates)
        {
            List<Tool_Hazard.Forms.SampleHeader> best = new();

            long bestCoverage = -1;

            foreach (var c in candidates)
            {
                if (c == null || c.Count == 0) continue;

                // coverage = sum of sample sizes (bigger = better, usually)
                long coverage = 0;
                foreach (var s in c) coverage += s.Size;

                // prefer more samples; tie-break by coverage
                if (c.Count > best.Count || (c.Count == best.Count && coverage > bestCoverage))
                {
                    best = c;
                    bestCoverage = coverage;
                }
            }

            if (best.Count == 0)
                throw new InvalidDataException("Could not find a valid sample table in this VH. (Tried explicit + heuristic scans)");

            // Ensure indices are sequential
            for (int i = 0; i < best.Count; i++) best[i].Index = i;

            return best;
        }
        // --- Extraction ---
        public void ExtractSampleToAdpcm(string vbPath, Tool_Hazard.Forms.SampleHeader header, string outputPath)
        {
            byte[] data;
            using (var fs = new FileStream(vbPath, FileMode.Open, FileAccess.Read))
            {
                if (header.Offset + header.Size > fs.Length)
                    throw new Exception("Sample data extends beyond the end of the VB file.");

                fs.Seek(header.Offset, SeekOrigin.Begin);
                data = new byte[header.Size];
                fs.Read(data, 0, data.Length);
            }
            File.WriteAllBytes(outputPath, data);
        }
        // --- Replace ---
        public void ReplaceSampleWithAdpcmSameLength(string vbPath, Tool_Hazard.Forms.SampleHeader header, string inputAdpcmPath)
        {
            byte[] newData = File.ReadAllBytes(inputAdpcmPath);

            if (newData.Length != header.Size)
                throw new Exception($"ADPCM must be EXACTLY the same length for in-place replace.\nExpected: {header.Size} bytes, got: {newData.Length} bytes.");

            using (var fs = new FileStream(vbPath, FileMode.Open, FileAccess.Write))
            {
                fs.Seek(header.Offset, SeekOrigin.Begin);
                fs.Write(newData, 0, newData.Length);
            }
        }
        public short[] DecodeSampleToPcm(string vbPath, Tool_Hazard.Forms.SampleHeader header)
        {
            byte[] vagData;
            using (var fs = new FileStream(vbPath, FileMode.Open, FileAccess.Read))
            {
                fs.Seek(header.Offset, SeekOrigin.Begin);
                vagData = new byte[header.Size];
                fs.Read(vagData, 0, vagData.Length);
            }
            return DecodeAdpcm(vagData);
        }

        //Read address table from VH
        private static List<ushort> ReadAddrTable(byte[] vh, int offset)
        {
            int max = (vh.Length - offset) / 2;
            var temp = new List<ushort>(max);
            for (int i = 0; i < max; i++)
                temp.Add(BitConverter.ToUInt16(vh, offset + i * 2));

            int lastNonZero = temp.FindLastIndex(x => x != 0);
            if (lastNonZero < 1) throw new InvalidDataException("Invalid address table.");
            return temp.Take(lastNonZero + 1).ToList();
        }

        private static List<(int start, int size)> AddrTableToRanges(List<ushort> addrs)
        {
            var ranges = new List<(int start, int size)>(addrs.Count - 1);
            for (int i = 0; i < addrs.Count - 1; i++)
            {
                int start = addrs[i] * 8;
                int end = addrs[i + 1] * 8;
                int size = end - start;
                if (size <= 0) size = 0;
                ranges.Add((start, size));
            }
            return ranges;
        }

        private static List<ushort> BuildAddrTable(ushort baseAddr, int[] sizes)
        {
            var list = new List<ushort>(sizes.Length + 1);
            int offsetBytes = baseAddr * 8;
            list.Add(baseAddr);

            for (int i = 0; i < sizes.Length; i++)
            {
                offsetBytes += sizes[i];
                if ((offsetBytes % 8) != 0) throw new InvalidDataException("Not 8-byte aligned.");
                int a = offsetBytes / 8;
                if (a > ushort.MaxValue) throw new InvalidDataException("Address overflow.");
                list.Add((ushort)a);
            }
            return list;
        }

        private static void WriteAddrTable(byte[] vh, int offset, List<ushort> addrs)
        {
            int max = (vh.Length - offset) / 2;
            if (addrs.Count > max) throw new InvalidDataException("Address table too large for VH.");

            for (int i = 0; i < addrs.Count; i++)
            {
                byte[] b = BitConverter.GetBytes(addrs[i]);
                vh[offset + i * 2 + 0] = b[0];
                vh[offset + i * 2 + 1] = b[1];
            }
            // zero remainder
            for (int i = addrs.Count; i < max; i++)
            {
                vh[offset + i * 2 + 0] = 0;
                vh[offset + i * 2 + 1] = 0;
            }
        }

        private static void WriteUInt32LE(byte[] buf, int offset, uint value)
        {
            byte[] b = BitConverter.GetBytes(value);
            buf[offset + 0] = b[0];
            buf[offset + 1] = b[1];
            buf[offset + 2] = b[2];
            buf[offset + 3] = b[3];
        }
    }
}
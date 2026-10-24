using System.Text;
using SampleHeader = Tool_Hazard.Forms.SampleHeader;

namespace Tool_Hazard
{
    /// <summary>
    /// Reads, previews, extracts, and replaces PlayStation ADPCM samples stored in
    /// Sony VAB VH/VB pairs and the compact VH/VB variant used by BIOHAZARD 3 PC.
    /// </summary>
    public sealed class PS1AudioService
    {
        private const int SpuOutputRate = 44100;
        private const int AdpcmFrameSize = 16;
        private const int SamplesPerFrame = 28;

        private static readonly int[,] PsxCoefficients =
        {
            { 0, 0 },
            { 60, 0 },
            { 115, -52 },
            { 98, -55 },
            { 122, -60 }
        };

        private enum VhFormat
        {
            SonyVab,
            Biohazard3Pc
        }

        private sealed class VhLayout
        {
            public required VhFormat Format { get; init; }
            public required int HeaderBase { get; init; }
            public required int SizeTableOffset { get; init; }
            public required int SampleCount { get; init; }
            public required List<SampleHeader> Samples { get; init; }
        }

        public List<SampleHeader> ReadVhFile(string vhPath, string vbPath)
        {
            if (!File.Exists(vhPath))
                throw new FileNotFoundException("VH file not found.", vhPath);
            if (!File.Exists(vbPath))
                throw new FileNotFoundException("VB file not found.", vbPath);

            byte[] vh = File.ReadAllBytes(vhPath);
            byte[] vb = File.ReadAllBytes(vbPath);
            VhLayout layout = ParseVh(vh, vb.LongLength);

            ApplyToneMetadata(vh, layout);
            foreach (SampleHeader sample in layout.Samples)
                ApplyAdpcmMetadata(vb, sample);

            return layout.Samples;
        }

        public byte[] CreateSampleWav(string vbPath, SampleHeader header)
        {
            short[] pcm = DecodeSampleToPcm(vbPath, header);
            using var output = new MemoryStream(44 + pcm.Length * sizeof(short));
            WriteWav(output, pcm, header.SampleRate > 0 ? header.SampleRate : SpuOutputRate);
            return output.ToArray();
        }

        public void ExtractSampleToWav(string vbPath, SampleHeader header, string outputPath)
        {
            File.WriteAllBytes(outputPath, CreateSampleWav(vbPath, header));
        }

        public void ExtractSampleToAdpcm(string vbPath, SampleHeader header, string outputPath)
        {
            File.WriteAllBytes(outputPath, ReadSampleBytes(vbPath, header));
        }

        public short[] DecodeSampleToPcm(string vbPath, SampleHeader header)
        {
            return DecodeAdpcm(ReadSampleBytes(vbPath, header));
        }

        public void ReplaceSampleWithAdpcmSameLength(string vbPath, SampleHeader header, string inputAdpcmPath)
        {
            byte[] replacement = File.ReadAllBytes(inputAdpcmPath);
            if (replacement.Length != header.Size)
            {
                throw new InvalidDataException(
                    $"Replacement ADPCM must be exactly {header.Size:N0} bytes; the selected file is {replacement.Length:N0} bytes.");
            }
            if (replacement.Length == 0 || replacement.Length % AdpcmFrameSize != 0)
                throw new InvalidDataException("PS1 ADPCM data must contain complete 16-byte frames.");

            using var stream = new FileStream(vbPath, FileMode.Open, FileAccess.Write, FileShare.None);
            ValidateSampleRange(stream.Length, header);
            stream.Position = header.Offset;
            stream.Write(replacement);
        }

        public void ReplaceSampleWithWav(string vhPath, string vbPath, SampleHeader header, string inputWavPath)
        {
            byte[] vh = File.ReadAllBytes(vhPath);
            byte[] vb = File.ReadAllBytes(vbPath);
            VhLayout layout = ParseVh(vh, vb.LongLength);
            ApplyToneMetadata(vh, layout);

            SampleHeader original = layout.Samples.FirstOrDefault(x => x.VabNumber == header.VabNumber)
                ?? throw new InvalidDataException("The selected sample is not present in this VH file.");

            (short[] inputPcm, int inputRate) = ReadWavPcm16Mono(inputWavPath);
            int targetRate = original.SampleRate > 0 ? original.SampleRate : SpuOutputRate;
            short[] resampled = inputRate == targetRate
                ? inputPcm
                : ResampleLinear(inputPcm, inputRate, targetRate);
            byte[] replacement = EncodePsxAdpcm(resampled);

            using var rebuilt = new MemoryStream(vb.Length - original.Size + replacement.Length);
            rebuilt.Write(vb, 0, original.Offset);
            rebuilt.Write(replacement);
            rebuilt.Write(vb, original.Offset + original.Size, vb.Length - original.Offset - original.Size);
            byte[] newVb = rebuilt.ToArray();

            int delta = replacement.Length - original.Size;
            if (layout.Format == VhFormat.Biohazard3Pc)
            {
                WriteUInt32LittleEndian(vh, layout.HeaderBase, checked((uint)newVb.Length));

                for (int i = original.VabNumber; i <= layout.SampleCount; i++)
                {
                    int entryOffset = layout.SizeTableOffset + i * sizeof(ushort);
                    int oldEnd = ReadUInt16LittleEndian(vh, entryOffset) * 8;
                    int newEnd = checked(oldEnd + delta);
                    if (newEnd < 0 || newEnd % 8 != 0 || newEnd / 8 > ushort.MaxValue)
                        throw new InvalidDataException("The replacement makes the RE3 VB offset table overflow.");
                    WriteUInt16LittleEndian(vh, entryOffset, (ushort)(newEnd / 8));
                }
            }
            else
            {
                int sizeUnits = replacement.Length / 8;
                if (sizeUnits > ushort.MaxValue)
                    throw new InvalidDataException("The replacement is too large for a Sony VAB size-table entry.");
                WriteUInt16LittleEndian(
                    vh,
                    layout.SizeTableOffset + original.VabNumber * sizeof(ushort),
                    (ushort)sizeUnits);

                uint declaredSize = ReadUInt32LittleEndian(vh, 0x0C);
                if (declaredSize != 0)
                    WriteUInt32LittleEndian(vh, 0x0C, checked((uint)(declaredSize + delta)));
            }

            File.WriteAllBytes(vbPath, newVb);
            File.WriteAllBytes(vhPath, vh);
        }

        private static VhLayout ParseVh(byte[] vh, long vbLength)
        {
            if (TryParseBiohazard3Pc(vh, vbLength, out VhLayout? re3) && re3 != null)
                return re3;
            if (TryParseSonyVab(vh, vbLength, out VhLayout? sony) && sony != null)
                return sony;

            throw new InvalidDataException(
                "Unsupported or damaged VH file. Expected a Sony pBAV header or a BIOHAZARD 3 PC compact VH header matching the selected VB.");
        }

        private static bool TryParseBiohazard3Pc(byte[] vh, long vbLength, out VhLayout? result)
        {
            result = null;
            if (vbLength < 0 || vbLength > uint.MaxValue)
                return false;

            for (int headerBase = 0; headerBase + 0x20 <= vh.Length; headerBase += 0x10)
            {
                if (ReadUInt32LittleEndian(vh, headerBase) != (uint)vbLength)
                    continue;
                if (ReadUInt16LittleEndian(vh, headerBase + 0x10) != 0xEEEE)
                    continue;

                uint headerSize = ReadUInt32LittleEndian(vh, headerBase + 0x04);
                uint tableRelative = ReadUInt32LittleEndian(vh, headerBase + 0x08);
                int programCount = ReadUInt16LittleEndian(vh, headerBase + 0x12);
                int sampleCount = ReadUInt16LittleEndian(vh, headerBase + 0x16);

                if (headerSize != vh.Length - headerBase || sampleCount <= 0)
                    continue;
                if (tableRelative > int.MaxValue)
                    continue;

                int sizeTableOffset = headerBase + (int)tableRelative;
                int toneTableOffset = headerBase + 0x20 + programCount * 0x10;
                if (sizeTableOffset < toneTableOffset || (sizeTableOffset - toneTableOffset) % 0x20 != 0)
                    continue;
                if (sizeTableOffset < 0 || sizeTableOffset + (sampleCount + 1) * 2 > vh.Length)
                    continue;

                var samples = new List<SampleHeader>(sampleCount);
                int previous = ReadUInt16LittleEndian(vh, sizeTableOffset) * 8;
                if (previous != 0)
                    continue;

                bool valid = true;
                for (int i = 1; i <= sampleCount; i++)
                {
                    int end = ReadUInt16LittleEndian(vh, sizeTableOffset + i * 2) * 8;
                    int size = end - previous;
                    if (end < previous || end > vbLength || size <= 0 || size % AdpcmFrameSize != 0)
                    {
                        valid = false;
                        break;
                    }

                    samples.Add(new SampleHeader
                    {
                        Index = i - 1,
                        VabNumber = i,
                        Offset = previous,
                        Size = size
                    });
                    previous = end;
                }

                if (!valid || previous != vbLength)
                    continue;

                result = new VhLayout
                {
                    Format = VhFormat.Biohazard3Pc,
                    HeaderBase = headerBase,
                    SizeTableOffset = sizeTableOffset,
                    SampleCount = sampleCount,
                    Samples = samples
                };
                return true;
            }

            return false;
        }

        private static bool TryParseSonyVab(byte[] vh, long vbLength, out VhLayout? result)
        {
            result = null;
            if (vh.Length < 0x820)
                return false;

            string magic = Encoding.ASCII.GetString(vh, 0, 4);
            if (magic != "pBAV" && magic != "VABp")
                return false;

            int declaredPrograms = ReadUInt16LittleEndian(vh, 0x12);
            int sampleCount = ReadUInt16LittleEndian(vh, 0x16);
            if (sampleCount <= 0 || sampleCount > 255)
                return false;

            int activeProgramBlocks = 0;
            for (int program = 0; program < 128; program++)
            {
                if (vh[0x20 + program * 0x10] != 0)
                    activeProgramBlocks++;
            }
            if (activeProgramBlocks == 0)
                activeProgramBlocks = declaredPrograms;

            int sizeTableOffset = 0x820 + activeProgramBlocks * 16 * 0x20;
            if (sizeTableOffset < 0 || sizeTableOffset + (sampleCount + 1) * 2 > vh.Length)
                return false;

            var samples = new List<SampleHeader>(sampleCount);
            int currentOffset = 0;
            for (int i = 1; i <= sampleCount; i++)
            {
                int size = ReadUInt16LittleEndian(vh, sizeTableOffset + i * 2) * 8;
                if (size <= 0 || size % AdpcmFrameSize != 0 || currentOffset + size > vbLength)
                    return false;

                samples.Add(new SampleHeader
                {
                    Index = i - 1,
                    VabNumber = i,
                    Offset = currentOffset,
                    Size = size
                });
                currentOffset += size;
            }

            if (currentOffset > vbLength)
                return false;

            result = new VhLayout
            {
                Format = VhFormat.SonyVab,
                HeaderBase = 0,
                SizeTableOffset = sizeTableOffset,
                SampleCount = sampleCount,
                Samples = samples
            };
            return true;
        }

        private static void ApplyToneMetadata(byte[] vh, VhLayout layout)
        {
            int toneTableOffset;
            int toneSlots;

            if (layout.Format == VhFormat.Biohazard3Pc)
            {
                int programCount = ReadUInt16LittleEndian(vh, layout.HeaderBase + 0x12);
                toneTableOffset = layout.HeaderBase + 0x20 + programCount * 0x10;
                toneSlots = (layout.SizeTableOffset - toneTableOffset) / 0x20;
            }
            else
            {
                toneTableOffset = 0x820;
                toneSlots = (layout.SizeTableOffset - toneTableOffset) / 0x20;
            }

            bool[] hasRate = new bool[layout.SampleCount];
            for (int tone = 0; tone < toneSlots; tone++)
            {
                int offset = toneTableOffset + tone * 0x20;
                int vagNumber = ReadUInt16LittleEndian(vh, offset + 0x16);
                if (vagNumber < 1 || vagNumber > layout.SampleCount)
                    continue;

                SampleHeader sample = layout.Samples[vagNumber - 1];
                sample.ToneReferenceCount++;
                if (hasRate[vagNumber - 1])
                    continue;

                int centerNote = vh[offset + 0x04];
                int centerFine = vh[offset + 0x05];
                int minimumNote = vh[offset + 0x06];
                int maximumNote = vh[offset + 0x07];
                int previewNote = minimumNote == maximumNote ? minimumNote : centerNote;
                double semitones = previewNote - centerNote - centerFine / 128.0;
                int rate = (int)Math.Round(SpuOutputRate * Math.Pow(2.0, semitones / 12.0));
                sample.SampleRate = Math.Clamp(rate, 1000, 192000);
                hasRate[vagNumber - 1] = true;
            }
        }

        private static void ApplyAdpcmMetadata(byte[] vb, SampleHeader sample)
        {
            if (sample.Offset < 0 || sample.Size <= 0 || sample.Offset + sample.Size > vb.Length)
                throw new InvalidDataException($"Sample {sample.Index} lies outside the selected VB file.");

            int decodedFrames = 0;
            int loopStart = -1;
            int loopEnd = -1;
            for (int offset = sample.Offset; offset < sample.Offset + sample.Size; offset += AdpcmFrameSize)
            {
                byte flags = vb[offset + 1];
                if ((flags & 0x04) != 0 && loopStart < 0)
                    loopStart = decodedFrames * SamplesPerFrame;

                decodedFrames++;
                if ((flags & 0x01) != 0)
                {
                    if ((flags & 0x02) != 0 && loopStart >= 0)
                        loopEnd = decodedFrames * SamplesPerFrame;
                    break;
                }
            }

            sample.PcmSampleCount = decodedFrames * SamplesPerFrame;
            sample.LoopStartSample = loopStart;
            sample.LoopEndSample = loopEnd;
        }

        private static byte[] ReadSampleBytes(string vbPath, SampleHeader header)
        {
            using var stream = new FileStream(vbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ValidateSampleRange(stream.Length, header);
            stream.Position = header.Offset;
            byte[] data = new byte[header.Size];
            stream.ReadExactly(data);
            return data;
        }

        private static void ValidateSampleRange(long fileLength, SampleHeader header)
        {
            if (header.Offset < 0 || header.Size <= 0 || header.Size % AdpcmFrameSize != 0 ||
                (long)header.Offset + header.Size > fileLength)
            {
                throw new InvalidDataException("The selected sample has an invalid range in the VB file.");
            }
        }

        private static short[] DecodeAdpcm(byte[] adpcm)
        {
            if (adpcm.Length == 0 || adpcm.Length % AdpcmFrameSize != 0)
                throw new InvalidDataException("PS1 ADPCM data must contain complete 16-byte frames.");

            var pcm = new List<short>((adpcm.Length / AdpcmFrameSize) * SamplesPerFrame);
            int history1 = 0;
            int history2 = 0;

            for (int frame = 0; frame < adpcm.Length; frame += AdpcmFrameSize)
            {
                int predictor = (adpcm[frame] >> 4) & 0x0F;
                int shift = adpcm[frame] & 0x0F;
                if (predictor > 4)
                    predictor = 0;
                if (shift > 12)
                    shift = 12;

                for (int i = 0; i < 14; i++)
                {
                    byte packed = adpcm[frame + 2 + i];
                    DecodeNibble(packed & 0x0F, predictor, shift, ref history1, ref history2, pcm);
                    DecodeNibble((packed >> 4) & 0x0F, predictor, shift, ref history1, ref history2, pcm);
                }

                if ((adpcm[frame + 1] & 0x01) != 0)
                    break;
            }

            return pcm.ToArray();
        }

        private static void DecodeNibble(
            int nibble,
            int predictor,
            int shift,
            ref int history1,
            ref int history2,
            List<short> output)
        {
            if ((nibble & 0x08) != 0)
                nibble -= 16;

            int residual = (nibble << 12) >> shift;
            int predicted = (history1 * PsxCoefficients[predictor, 0] +
                             history2 * PsxCoefficients[predictor, 1] + 32) >> 6;
            int decoded = Math.Clamp(residual + predicted, short.MinValue, short.MaxValue);
            output.Add((short)decoded);
            history2 = history1;
            history1 = decoded;
        }

        private static byte[] EncodePsxAdpcm(short[] pcm)
        {
            if (pcm.Length == 0)
                throw new InvalidDataException("The replacement WAV contains no audio samples.");

            int frameCount = (pcm.Length + SamplesPerFrame - 1) / SamplesPerFrame;
            byte[] output = new byte[frameCount * AdpcmFrameSize];
            int history1 = 0;
            int history2 = 0;

            for (int frame = 0; frame < frameCount; frame++)
            {
                int bestPredictor = 0;
                int bestShift = 0;
                long bestError = long.MaxValue;
                sbyte[] bestNibbles = new sbyte[SamplesPerFrame];

                for (int predictor = 0; predictor < 5; predictor++)
                {
                    for (int shift = 0; shift <= 12; shift++)
                    {
                        int scale = 1 << (12 - shift);
                        int trialHistory1 = history1;
                        int trialHistory2 = history2;
                        long error = 0;
                        var trialNibbles = new sbyte[SamplesPerFrame];

                        for (int i = 0; i < SamplesPerFrame; i++)
                        {
                            int sourceIndex = frame * SamplesPerFrame + i;
                            int source = sourceIndex < pcm.Length ? pcm[sourceIndex] : 0;
                            int predicted = (trialHistory1 * PsxCoefficients[predictor, 0] +
                                             trialHistory2 * PsxCoefficients[predictor, 1] + 32) >> 6;
                            int quantized = (int)Math.Round((source - predicted) / (double)scale, MidpointRounding.AwayFromZero);
                            quantized = Math.Clamp(quantized, -8, 7);
                            trialNibbles[i] = (sbyte)quantized;

                            int reconstructed = Math.Clamp(predicted + quantized * scale, short.MinValue, short.MaxValue);
                            long difference = source - reconstructed;
                            error += difference * difference;
                            trialHistory2 = trialHistory1;
                            trialHistory1 = reconstructed;
                        }

                        if (error < bestError)
                        {
                            bestError = error;
                            bestPredictor = predictor;
                            bestShift = shift;
                            Array.Copy(trialNibbles, bestNibbles, SamplesPerFrame);
                        }
                    }
                }

                int outputOffset = frame * AdpcmFrameSize;
                output[outputOffset] = (byte)((bestPredictor << 4) | bestShift);
                output[outputOffset + 1] = frame == frameCount - 1 ? (byte)0x01 : (byte)0x00;
                for (int i = 0; i < SamplesPerFrame; i += 2)
                {
                    output[outputOffset + 2 + i / 2] =
                        (byte)((bestNibbles[i] & 0x0F) | ((bestNibbles[i + 1] & 0x0F) << 4));
                }

                int scaleForBest = 1 << (12 - bestShift);
                for (int i = 0; i < SamplesPerFrame; i++)
                {
                    int predicted = (history1 * PsxCoefficients[bestPredictor, 0] +
                                     history2 * PsxCoefficients[bestPredictor, 1] + 32) >> 6;
                    int reconstructed = Math.Clamp(predicted + bestNibbles[i] * scaleForBest, short.MinValue, short.MaxValue);
                    history2 = history1;
                    history1 = reconstructed;
                }
            }

            return output;
        }

        private static void WriteWav(Stream output, short[] pcm, int sampleRate)
        {
            using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
            int dataSize = checked(pcm.Length * sizeof(short));
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(checked(36 + dataSize));
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(checked(sampleRate * sizeof(short)));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            foreach (short sample in pcm)
                writer.Write(sample);
        }

        private static (short[] Pcm, int SampleRate) ReadWavPcm16Mono(string path)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (new string(reader.ReadChars(4)) != "RIFF")
                throw new InvalidDataException("The replacement file is not a RIFF WAV.");
            reader.ReadUInt32();
            if (new string(reader.ReadChars(4)) != "WAVE")
                throw new InvalidDataException("The replacement file is not a WAV.");

            int channels = 0;
            int sampleRate = 0;
            int bitsPerSample = 0;
            ushort format = 0;
            byte[]? data = null;

            while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
            {
                string chunkId = new string(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();
                if (chunkSize < 0 || reader.BaseStream.Position + chunkSize > reader.BaseStream.Length)
                    throw new InvalidDataException("The WAV contains a damaged chunk.");

                long chunkEnd = reader.BaseStream.Position + chunkSize;
                if (chunkId == "fmt ")
                {
                    if (chunkSize < 16)
                        throw new InvalidDataException("The WAV format chunk is too small.");
                    format = reader.ReadUInt16();
                    channels = reader.ReadUInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadUInt32();
                    reader.ReadUInt16();
                    bitsPerSample = reader.ReadUInt16();
                }
                else if (chunkId == "data")
                {
                    data = reader.ReadBytes(chunkSize);
                }

                reader.BaseStream.Position = chunkEnd + (chunkSize & 1);
            }

            if (format != 1 || bitsPerSample != 16 || (channels != 1 && channels != 2) || sampleRate <= 0)
                throw new InvalidDataException("Only mono or stereo 16-bit PCM WAV files are supported.");
            if (data == null || data.Length == 0 || data.Length % (channels * sizeof(short)) != 0)
                throw new InvalidDataException("The WAV contains no valid PCM audio data.");

            short[] interleaved = new short[data.Length / sizeof(short)];
            Buffer.BlockCopy(data, 0, interleaved, 0, data.Length);
            if (channels == 1)
                return (interleaved, sampleRate);

            short[] mono = new short[interleaved.Length / 2];
            for (int i = 0; i < mono.Length; i++)
                mono[i] = (short)((interleaved[i * 2] + interleaved[i * 2 + 1]) / 2);
            return (mono, sampleRate);
        }

        private static short[] ResampleLinear(short[] source, int sourceRate, int targetRate)
        {
            if (sourceRate <= 0 || targetRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceRate));
            if (source.Length == 0 || sourceRate == targetRate)
                return source;

            int outputLength = Math.Max(1, checked((int)Math.Round(source.Length * targetRate / (double)sourceRate)));
            var output = new short[outputLength];
            double step = sourceRate / (double)targetRate;
            for (int i = 0; i < output.Length; i++)
            {
                double sourcePosition = i * step;
                int left = Math.Min((int)sourcePosition, source.Length - 1);
                int right = Math.Min(left + 1, source.Length - 1);
                double fraction = sourcePosition - left;
                output[i] = (short)Math.Clamp(
                    (int)Math.Round(source[left] + (source[right] - source[left]) * fraction),
                    short.MinValue,
                    short.MaxValue);
            }
            return output;
        }

        private static ushort ReadUInt16LittleEndian(byte[] data, int offset)
        {
            if (offset < 0 || offset + 2 > data.Length)
                return 0;
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static uint ReadUInt32LittleEndian(byte[] data, int offset)
        {
            if (offset < 0 || offset + 4 > data.Length)
                return 0;
            return (uint)(data[offset] |
                         (data[offset + 1] << 8) |
                         (data[offset + 2] << 16) |
                         (data[offset + 3] << 24));
        }

        private static void WriteUInt16LittleEndian(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32LittleEndian(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }
    }
}

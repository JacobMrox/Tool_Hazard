#nullable enable
using System.Text;

namespace Tool_Hazard.Biohazard.SAP
{
    public enum SapPayloadKind
    {
        WavBank,
        OggSingle,
        Unknown
    }

    public enum SapEntryKind
    {
        Wav,
        Ogg
    }

    public sealed class SapEntry
    {
        public int Index { get; set; }
        public SapEntryKind Kind { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();

        public override string ToString()
        {
            var ext = Kind == SapEntryKind.Wav ? "WAV" : "OGG";
            return $"{Index:00} - {ext} ({Data.Length:N0} bytes)";
        }
    }

    public sealed class SapBank
    {
        public string? SourcePath { get; set; }
        public ulong Header { get; set; }
        public SapPayloadKind PayloadKind { get; set; }
        public List<SapEntry> Entries { get; } = new();

        public bool IsDirty { get; set; }

        public string DisplayName =>
            !string.IsNullOrWhiteSpace(SourcePath) ? Path.GetFileName(SourcePath) : "Untitled.sap";
    }

    public static class SapService
    {
        public static SapBank Load(string path)
        {
            var data = File.ReadAllBytes(path);
            if (data.Length < 12)
                throw new InvalidDataException("SAP file is too small.");

            var bank = Parse(data);
            bank.SourcePath = path;
            bank.IsDirty = false;
            return bank;
        }

        public static SapBank Parse(byte[] data)
        {
            if (data.Length < 12)
                throw new InvalidDataException("SAP data is too small.");

            var header = BitConverter.ToUInt64(data, 0);
            var kind = DetectKind(data);

            var bank = new SapBank
            {
                Header = header,
                PayloadKind = kind
            };

            var payload = data.AsSpan(8);

            switch (kind)
            {
                case SapPayloadKind.WavBank:
                    {
                        var wavs = SplitWavBank(payload);
                        for (int i = 0; i < wavs.Count; i++)
                        {
                            bank.Entries.Add(new SapEntry
                            {
                                Index = i,
                                Kind = SapEntryKind.Wav,
                                Data = wavs[i]
                            });
                        }
                        break;
                    }
                case SapPayloadKind.OggSingle:
                    {
                        bank.Entries.Add(new SapEntry
                        {
                            Index = 0,
                            Kind = SapEntryKind.Ogg,
                            Data = payload.ToArray()
                        });
                        break;
                    }
                default:
                    throw new InvalidDataException("Unknown SAP payload type (expected RIFF or OggS after 8-byte header).");
            }

            return bank;
        }

        public static SapPayloadKind DetectKind(ReadOnlySpan<byte> data)
        {
            if (data.Length < 12) return SapPayloadKind.Unknown;
            var magic = Encoding.ASCII.GetString(data.Slice(8, 4));
            return magic switch
            {
                "RIFF" => SapPayloadKind.WavBank,
                "OggS" => SapPayloadKind.OggSingle,
                _ => SapPayloadKind.Unknown
            };
        }

        public static byte[] BuildSapBytes(SapBank bank)
        {
            using var ms = new MemoryStream();
            Span<byte> hdr = stackalloc byte[8];
            BitConverter.TryWriteBytes(hdr, bank.Header);
            ms.Write(hdr);

            if (bank.PayloadKind == SapPayloadKind.OggSingle)
            {
                if (bank.Entries.Count < 1)
                    throw new InvalidOperationException("OGG SAP requires exactly 1 entry.");
                if (!IsOgg(bank.Entries[0].Data))
                    throw new InvalidDataException("OGG SAP entry is not a valid OGG stream (missing OggS).");

                ms.Write(bank.Entries[0].Data, 0, bank.Entries[0].Data.Length);
                return ms.ToArray();
            }

            if (bank.PayloadKind == SapPayloadKind.WavBank)
            {
                foreach (var e in bank.Entries.OrderBy(e => e.Index))
                {
                    if (!IsRiffWav(e.Data))
                        throw new InvalidDataException($"Entry {e.Index:00} is not a valid RIFF WAV (missing RIFF).");
                    ms.Write(e.Data, 0, e.Data.Length);
                }
                return ms.ToArray();
            }

            throw new InvalidOperationException("Unsupported SAP payload kind.");
        }

        public static void Save(SapBank bank, string path)
        {
            var bytes = BuildSapBytes(bank);
            File.WriteAllBytes(path, bytes);
            bank.SourcePath = path;
            bank.IsDirty = false;
        }

        public static bool IsRiffWav(ReadOnlySpan<byte> data) =>
            data.Length >= 12 &&
            data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F';

        public static bool IsOgg(ReadOnlySpan<byte> data) =>
            data.Length >= 4 &&
            data[0] == (byte)'O' && data[1] == (byte)'g' && data[2] == (byte)'g' && data[3] == (byte)'S';

        private static List<byte[]> SplitWavBank(ReadOnlySpan<byte> payload)
        {
            var results = new List<byte[]>();
            int offset = 0;

            while (offset < payload.Length)
            {
                if (offset + 12 > payload.Length)
                    throw new InvalidDataException("WAV bank payload is truncated.");

                if (!(payload[offset + 0] == (byte)'R' &&
                      payload[offset + 1] == (byte)'I' &&
                      payload[offset + 2] == (byte)'F' &&
                      payload[offset + 3] == (byte)'F'))
                {
                    throw new InvalidDataException($"Expected RIFF at payload offset {offset}.");
                }

                // chunk size at offset+4 (little endian), total size = 8 + chunkSize
                int chunkSize = payload[offset + 4]
                              | (payload[offset + 5] << 8)
                              | (payload[offset + 6] << 16)
                              | (payload[offset + 7] << 24);

                int wavSize = checked(8 + chunkSize);
                if (wavSize <= 0)
                    throw new InvalidDataException("Invalid RIFF chunk size.");

                if (offset + wavSize > payload.Length)
                    throw new InvalidDataException("WAV entry exceeds payload bounds (truncated bank).");

                results.Add(payload.Slice(offset, wavSize).ToArray());
                offset += wavSize;
            }

            return results;
        }
    }
}

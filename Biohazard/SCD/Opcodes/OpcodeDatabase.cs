using System.Text.Json;
using Tool_Hazard.IntelOrca.Biohazard;
using Tool_Hazard.IntelOrca.Biohazard.Script;

namespace Tool_Hazard.Biohazard.SCD.Opcodes
{
    public static class OpcodeDatabase
    {
        public static Dictionary<string, OpcodeDefinition> Opcodes { get; private set; }
            = new();

        public static BioVersion LoadedVersion { get; private set; }

        public static void LoadForVersion(BioVersion version)
        {
            string basePath = Path.Combine(
                AppContext.BaseDirectory,
                "Resources",
                "Biohazard",
                "Opcodes"
            );

            string fileName = version switch
            {
                BioVersion.Biohazard1 => "re1_opcodes.json",
                BioVersion.Biohazard1_5 => "re15_opcodes.json",
                BioVersion.Biohazard2 => "re2_opcodes.json",
                BioVersion.Biohazard3 => "re3_opcodes.json",
                _ => throw new NotSupportedException()
            };

            string fullPath = Path.Combine(basePath, fileName);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException(
                    $"Opcode definition not found: {fullPath}");

            var json = File.ReadAllText(fullPath);
            Opcodes = JsonSerializer.Deserialize<
                Dictionary<string, OpcodeDefinition>>(json)
                ?? new();

            LoadedVersion = version;
            NormalizeFixedLayouts(version);
        }

        public static OpcodeDefinition? Get(byte opcode)
        {
            var key = opcode.ToString("X2");
            return Opcodes.TryGetValue(key, out var def) ? def : null;
        }

        public static int GetInstructionSize(BioVersion version, byte opcode, BinaryReader? reader = null)
        {
            IConstantTable table = version switch
            {
                BioVersion.Biohazard1 => new Bio1ConstantTable(),
                BioVersion.Biohazard2 => new Bio2ConstantTable(),
                BioVersion.Biohazard3 => new Bio3ConstantTable(),
                _ => throw new NotSupportedException($"Opcode layout is not supported for {version}.")
            };
            return table.GetInstructionSize(opcode, reader);
        }

        private static void NormalizeFixedLayouts(BioVersion version)
        {
            if (version != BioVersion.Biohazard2 && version != BioVersion.Biohazard3)
                return;

            foreach (var pair in Opcodes.ToArray())
            {
                var opcode = Convert.ToByte(pair.Key, 16);
                var expectedLength = GetInstructionSize(version, opcode);
                if (expectedLength <= 0)
                {
                    Opcodes.Remove(pair.Key);
                    continue;
                }

                var definition = pair.Value;
                var schemaLength = GetSchemaLength(definition);
                definition.BinaryLength = expectedLength;
                if (schemaLength != expectedLength)
                    Opcodes[pair.Key] = CreateRawDefinition(opcode, expectedLength, definition);
            }
        }

        public static OpcodeDefinition CreateRawDefinition(
            byte opcode,
            int length,
            OpcodeDefinition? original = null)
        {
            var fields = new Dictionary<string, OpcodeField>
            {
                ["Opcode"] = new OpcodeField
                {
                    Type = "UCHAR",
                    Description = $"0x{opcode:X2}"
                }
            };
            for (var i = 1; i < length; i++)
            {
                fields[$"Byte{i:00}"] = new OpcodeField
                {
                    Type = "UCHAR",
                    Description = "Raw instruction byte"
                };
            }

            return new OpcodeDefinition
            {
                OpcodeName = original?.OpcodeName ?? $"Unknown_{opcode:X2}",
                OpcodeNumber = opcode.ToString("X2"),
                OpcodeLength = $"{length} Bytes",
                OpcodeDescription = original is null
                    ? "Unknown opcode layout preserved as raw bytes."
                    : $"{original.OpcodeDescription} (Raw byte layout used because the old field schema had the wrong length.)",
                Bytes = fields,
                BinaryLength = length,
                IsRawLayout = true
            };
        }

        private static int GetSchemaLength(OpcodeDefinition definition)
        {
            if (definition.Bytes is null)
                return 0;

            var length = 0;
            foreach (var field in definition.Bytes.Values)
            {
                length += field.Type switch
                {
                    "UCHAR" or "CHAR" => 1,
                    "USHORT" or "SHORT" => 2,
                    "UINT" or "ULONG" or "INT" or "LONG" => 4,
                    _ => 0
                };
            }
            return length;
        }
    }
}

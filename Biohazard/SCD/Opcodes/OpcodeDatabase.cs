using IntelOrca.Biohazard;
using System.Text.Json;

namespace Tool_Hazard.Biohazard.SCD.Opcodes
{
    public static class OpcodeDatabase
    {
        public static Dictionary<string, OpcodeDefinition> Opcodes { get; private set; }
            = new();

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
        }

        public static OpcodeDefinition Get(byte opcode)
        {
            var key = opcode.ToString("X2");
            return Opcodes.TryGetValue(key, out var def) ? def : null;
        }
    }
}

using System.Text.Json.Serialization;

namespace Tool_Hazard.Biohazard.SCD.Opcodes
{
    public class OpcodeDefinition
    {
        [JsonPropertyName("Opcode Name")]
        public string? OpcodeName { get; set; }

        [JsonPropertyName("Opcode Number")]
        public string? OpcodeNumber { get; set; }

        [JsonPropertyName("Opcode Length")]
        public string? OpcodeLength { get; set; }

        [JsonPropertyName("Opcode Description")]
        public string? OpcodeDescription { get; set; }

        [JsonPropertyName("Bytes")]
        public Dictionary<string, OpcodeField>? Bytes { get; set; }
    }

    public class OpcodeField
    {
        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Description")]
        public string? Description { get; set; }
    }
}

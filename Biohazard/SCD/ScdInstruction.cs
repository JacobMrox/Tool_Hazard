using Tool_Hazard.Biohazard.SCD.Opcodes;

namespace Tool_Hazard.Biohazard.SCD
{
    public class ScdInstruction
    {
        public byte Opcode { get; set; }
        public OpcodeDefinition Definition { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}

namespace Tool_Hazard.Biohazard.SCD
{
    public class ScdInstructionRow
    {
        public int Offset { get; set; }
        public string Opcode { get; set; } = "";
        public string Name { get; set; } = "";
        public string Params { get; set; } = "";
        public string Description { get; set; } = "";

        // Keep the original instruction attached so we can save later
        public object? Tag { get; set; }
    }
}

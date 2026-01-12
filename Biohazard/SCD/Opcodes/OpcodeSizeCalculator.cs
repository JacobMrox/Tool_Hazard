namespace Tool_Hazard.Biohazard.SCD.Opcodes
{
    public static class OpcodeSizeCalculator
    {
        public static int Calculate(OpcodeDefinition def)
        {
            int size = 1; // opcode byte itself

            foreach (var field in def.Bytes)
            {
                if (field.Key == "Opcode")
                    continue;

                size += field.Value.Type switch
                {
                    "UCHAR" => 1,
                    "CHAR" => 1,
                    "USHORT" => 2,
                    "SHORT" => 2,
                    "UINT" => 4,
                    "ULONG" => 4,
                    "INT" => 4,
                    _ => throw new NotSupportedException(
                        $"Unknown size for {field.Value.Type}")
                };
            }

            return size;
        }
    }
}

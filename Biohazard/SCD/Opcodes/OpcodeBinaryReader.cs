namespace Tool_Hazard.Biohazard.SCD.Opcodes
{
    public static class OpcodeBinaryReader
    {
        public static object Read(BinaryReader br, string type)
        {
            return type switch
            {
                "UCHAR" => br.ReadByte(),
                "CHAR" => (sbyte)br.ReadByte(),

                "USHORT" => br.ReadUInt16(),
                "SHORT" => br.ReadInt16(),

                "UINT" => br.ReadUInt32(),
                "ULONG" => br.ReadUInt32(), // ← FIX
                "INT" => br.ReadInt32(),

                _ => throw new NotSupportedException($"Unknown field type: {type}")
            };
        }
    }
}

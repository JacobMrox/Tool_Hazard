namespace Tool_Hazard.Biohazard.SCD.Opcodes
{
    public static class OpcodeBinaryWriter
    {
        public static void Write(BinaryWriter bw, string type, object value)
        {
            switch (type)
            {
                case "UCHAR": bw.Write(Convert.ToByte(value)); break;
                case "CHAR": bw.Write(unchecked((byte)Convert.ToSByte(value))); break;

                case "USHORT": bw.Write(Convert.ToUInt16(value)); break;
                case "SHORT": bw.Write(Convert.ToInt16(value)); break;

                case "UINT":
                case "ULONG": bw.Write(Convert.ToUInt32(value)); break;

                case "INT":
                case "LONG": bw.Write(Convert.ToInt32(value)); break;

                default:
                    throw new NotSupportedException($"Unknown field type: {type}");
            }
        }
    }
}

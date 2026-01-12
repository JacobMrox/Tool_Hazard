namespace IntelOrca.Biohazard.Script.Opcodes
{
    public interface IAotSetOpcode : IAot
    {
        int Offset { get; }
        byte Opcode { get; }

        ushort Data0 { get; set; }
        ushort Data1 { get; set; }
        ushort Data2 { get; set; }
    }
}

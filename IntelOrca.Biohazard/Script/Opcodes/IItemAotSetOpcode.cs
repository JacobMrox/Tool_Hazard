namespace IntelOrca.Biohazard.Script.Opcodes
{
    public interface IItemAotSetOpcode : IAot
    {
        int Offset { get; }
        byte Opcode { get; }

        ushort Type { get; set; }
        ushort Amount { get; set; }
        ushort GlobalId { get; set; }
        byte MD1 { get; set; }
        byte Action { get; set; }
    }
}

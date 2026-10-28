using System.Diagnostics;

namespace Tool_Hazard.IntelOrca.Biohazard.Script.Opcodes
{
    [DebuggerDisplay("gosub")]
    internal class GosubOpcode : OpcodeBase
    {
        public byte Index { get; set; }

        public static GosubOpcode Read(BinaryReader br, int offset)
        {
            return new GosubOpcode()
            {
                Offset = offset,
                Length = 2,

                Opcode = br.ReadByte(),
                Index = br.ReadByte()
            };
        }

        public override void Write(BinaryWriter bw)
        {
            bw.Write(Opcode);
            bw.Write(Index);
        }
    }
}

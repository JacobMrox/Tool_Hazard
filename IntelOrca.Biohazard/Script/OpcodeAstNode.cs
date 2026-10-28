using System.Diagnostics;
using Tool_Hazard.IntelOrca.Biohazard.Script.Opcodes;

namespace Tool_Hazard.IntelOrca.Biohazard.Script
{
    [DebuggerDisplay("{Opcode}")]
    public class OpcodeAstNode : IScriptAstNode
    {
        public OpcodeBase Opcode { get; set; }

        public OpcodeAstNode(OpcodeBase opcode)
        {
            Opcode = opcode;
        }

        public void Visit(ScriptAstVisitor visitor)
        {
            visitor.VisitNode(this);
        }
    }
}

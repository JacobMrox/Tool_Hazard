using IntelOrca.Biohazard.Script.Opcodes;
using System.Diagnostics;

namespace IntelOrca.Biohazard.Script
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

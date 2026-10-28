namespace Tool_Hazard.IntelOrca.Biohazard.Script
{
    public interface IScriptAstNode
    {
        void Visit(ScriptAstVisitor visitor);
    }
}

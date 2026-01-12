using System;

namespace IntelOrca.Biohazard.Script
{
    public sealed class NullBioScriptVisitor : IBioScriptVisitor
    {
        public void VisitVersion(BioVersion version) { }
        public void VisitBeginScript(BioScriptKind kind) { }
        public void VisitBeginSubroutine(int index) { }
        public void VisitBeginEventOpcode(int offset, ReadOnlySpan<byte> opcodeBytes) { }
        public void VisitEndEventOpcode() { }
        public void VisitOpcode(int offset, Span<byte> opcodeBytes) { }
        public void VisitEndSubroutine(int index) { }
        public void VisitEndScript(BioScriptKind kind) { }
        public void VisitTrailingData(int offset, Span<byte> data) { }
    }
}

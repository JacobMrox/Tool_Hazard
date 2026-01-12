using System;

namespace IntelOrca.Biohazard.Script
{
    public interface IBioScriptVisitor
    {
        void VisitVersion(BioVersion version);
        void VisitBeginScript(BioScriptKind kind);
        void VisitBeginSubroutine(int index);
        void VisitBeginEventOpcode(int offset, ReadOnlySpan<byte> opcodeBytes);
        void VisitOpcode(int offset, Span<byte> opcodeBytes);
        void VisitEndEventOpcode();
        void VisitEndSubroutine(int index);
        void VisitEndScript(BioScriptKind kind);
        void VisitTrailingData(int offset, Span<byte> data);
    }
}
using System;
using IntelOrca.Biohazard.Room;
using IntelOrca.Biohazard.Script;

namespace IntelOrca.Biohazard.Extensions
{
    public static class RdtExtensions
    {
        public static IRdt WithScd(this IRdt rdt, BioScriptKind kind, ScdProcedureList scd)
        {
            var builder = rdt.ToBuilder();
            if (builder is Rdt1.Builder builder1)
            {
                throw new NotSupportedException();
            }
            else if (builder is Rdt2.Builder builder2)
            {
                if (kind == BioScriptKind.Init)
                {
                    builder2.SCDINIT = scd;
                }
                else if (kind == BioScriptKind.Main)
                {
                    builder2.SCDMAIN = scd;
                }
            }
            return builder.ToRdt();
        }

        public static string DisassembleScd(this IRdt rdt, bool listing = false)
        {
            var scriptDecompiler = new ScriptDecompiler(true, listing);
            ReadScript(rdt, scriptDecompiler);
            return scriptDecompiler.GetScript();
        }

        public static string DecompileScd(this IRdt rdt)
        {
            var scriptDecompiler = new ScriptDecompiler(false, false);
            ReadScript(rdt, scriptDecompiler);
            return scriptDecompiler.GetScript();
        }

        public static void ReadScript(this IRdt rdt, BioScriptVisitor visitor)
        {
            visitor.VisitVersion(rdt.Version);
            if (rdt.Version == BioVersion.BiohazardCv)
            {
                ReadScript(rdt, BioScriptKind.Main, visitor);
            }
            else
            {
                ReadScript(rdt, BioScriptKind.Init, visitor);
                if (rdt.Version != BioVersion.Biohazard3)
                    ReadScript(rdt, BioScriptKind.Main, visitor);
                if (rdt.Version == BioVersion.Biohazard1)
                    ReadScript(rdt, BioScriptKind.Event, visitor);
            }
        }

        private static void ReadScript(this IRdt rdt, BioScriptKind kind, BioScriptVisitor visitor)
        {
            if (rdt is Rdt1 rdt1)
            {
                if (kind == BioScriptKind.Init)
                {
                    var scd = rdt1.InitSCD;
                    var scdReader = new ScdReader();
                    scdReader.BaseOffset = rdt1.Offsets[6];
                    scdReader.ReadScript(scd.Data, rdt.Version, kind, visitor);
                }
                else if (kind == BioScriptKind.Main)
                {
                    var scd = rdt1.MainSCD;
                    var scdReader = new ScdReader();
                    scdReader.BaseOffset = rdt1.Offsets[7];
                    scdReader.ReadScript(scd.Data, rdt.Version, kind, visitor);
                }
                else if (kind == BioScriptKind.Event)
                {
                    var scd = rdt1.EventSCD;
                    var baseOffset = rdt1.Offsets[8];
                    for (int i = 0; i < scd.Count; i++)
                    {
                        var evt = scd[i];
                        var scdReader = new ScdReader();
                        scdReader.BaseOffset = baseOffset + scd.GetOffset(i);
                        scdReader.ReadEventScript(evt.Data, visitor, i);
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            else if (rdt is Rdt2 rdt2)
            {
                if (kind == BioScriptKind.Init)
                {
                    var scd = rdt2.SCDINIT;
                    var scdReader = new ScdReader();
                    scdReader.BaseOffset = rdt2.Offsets[16];
                    scdReader.ReadScript(scd.Data, rdt.Version, kind, visitor);
                }
                else if (kind == BioScriptKind.Main)
                {
                    var scd = rdt2.SCDMAIN;
                    var scdReader = new ScdReader();
                    scdReader.BaseOffset = rdt2.Offsets[17];
                    scdReader.ReadScript(scd.Data, rdt.Version, kind, visitor);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            else if (rdt is RdtCv rdtCv)
            {
                var scd = rdtCv.Script;
                var scdReader = new ScdReader();
                scdReader.BaseOffset = rdtCv.ScriptOffset;
                scdReader.ReadScript(scd.Data, rdt.Version, kind, visitor);
            }
        }
    }
}

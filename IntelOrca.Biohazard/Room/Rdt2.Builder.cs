using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.Model;

namespace IntelOrca.Biohazard.Room
{
    public sealed partial class Rdt2
    {
        public class Builder : IRdtBuilder
        {
            public BioVersion Version { get; }
            public Rdt2Header Header { get; set; }
            public Rid RID { get; set; }
            public byte[] RVD { get; set; } = new byte[0];
            public byte[] LIT { get; set; } = new byte[0];
            public byte[] PRI { get; set; } = new byte[0];
            public byte[] SCA { get; set; } = new byte[0];
            public byte[] BLK { get; set; } = new byte[0];
            public byte[] FLR { get; set; } = new byte[0];
            public ushort? FLRTerminator { get; set; }
            public ScdProcedureList SCDINIT { get; set; }
            public ScdProcedureList SCDMAIN { get; set; }
            public Etd ETD { get; set; }
            public MsgList MSGJA { get; set; }
            public MsgList MSGEN { get; set; }
            public Tim TIMSCROLL { get; set; }
            public EspTable EspTable { get; set; }
            public Rbj RBJ { get; set; }
            public byte[] EDT { get; set; } = new byte[0];
            public byte[] VH { get; set; } = new byte[0];
            public byte[] VB { get; set; } = new byte[0];
            public int? VBOFFSET { get; set; }

            public List<ModelTextureIndex> EmbeddedObjectModelTable { get; set; } = new List<ModelTextureIndex>();
            public List<Md1> EmbeddedObjectMd1 { get; set; } = new List<Md1>();
            public List<Tim> EmbeddedObjectTim { get; set; } = new List<Tim>();
            public EmbeddedEffectList EmbeddedEffects { get; set; }

            IRdt IRdtBuilder.ToRdt() => ToRdt();

            public Builder(BioVersion version)
            {
                Version = version;
            }

            public Rdt2 ToRdt()
            {
                // Validate
                if (EmbeddedObjectModelTable.Count != Header.nOmodel)
                    throw new InvalidOperationException("Number of embedded objects does not equal header.");

                var offsetTable = new int[Version == BioVersion.Biohazard3 ? 22 : 23];

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                bw.Write(Header);

                // Offsets placeholder
                var offsetTableOffset = ms.Position;
                for (var i = 0; i < offsetTable.Length; i++)
                {
                    bw.Write(0);
                }

                offsetTable[7] = (int)ms.Position;
                // Place holder until we write the PRI block
                bw.Write(0, RID.Data.Length);

                offsetTable[10] = (int)ms.Position;
                for (var i = 0; i < EmbeddedObjectModelTable.Count; i++)
                {
                    bw.Write(0);
                    bw.Write(0);
                }

                offsetTable[8] = (int)ms.Position;
                bw.Write(RVD);
                bw.Write(-1);

                offsetTable[9] = (int)ms.Position;
                bw.Write(LIT);

                var priOffset = (int)ms.Position;

                // Re-write RID now that we know the PRI offset
                ms.Position = offsetTable[7];
                bw.Write(RID.WithPriOffset(priOffset).Data);

                ms.Position = priOffset;
                bw.Write(PRI);

                offsetTable[6] = (int)ms.Position;
                bw.Write(SCA);

                if (BLK.Length != 0)
                {
                    offsetTable[12] = (int)ms.Position;
                    bw.Write(BLK);
                }

                offsetTable[11] = (int)ms.Position;
                bw.Write(FLR);
                bw.Write(FLRTerminator ?? 0);

                offsetTable[16] = (int)ms.Position;
                bw.Write(SCDINIT.Data);

                if (Version != BioVersion.Biohazard3)
                {
                    offsetTable[17] = (int)ms.Position;
                    bw.Write(SCDMAIN.Data);
                }

                if (!MSGJA.Data.IsEmpty)
                {
                    offsetTable[13] = (int)ms.Position;
                    bw.Write(MSGJA.Data);
                }

                if (!MSGEN.Data.IsEmpty)
                {
                    offsetTable[14] = (int)ms.Position;
                    bw.Write(MSGEN.Data);
                }

                if (!TIMSCROLL.Data.IsEmpty)
                {
                    offsetTable[15] = (int)ms.Position;
                    bw.Write(TIMSCROLL.Data);
                }

                var objectMd1Table = new int[EmbeddedObjectMd1.Count];
                for (var i = 0; i < EmbeddedObjectMd1.Count; i++)
                {
                    objectMd1Table[i] = (int)ms.Position;
                    bw.Write(EmbeddedObjectMd1[i].Data.ToArray());
                }

                if (!EspTable.Data.IsEmpty)
                {
                    var tableIndex = Version == BioVersion.Biohazard2 ? 18 : 17;
                    offsetTable[tableIndex] = (int)ms.Position;
                    bw.Write(EspTable.Data);
                    offsetTable[tableIndex + 1] = (int)ms.Position - 4;
                }
                else
                {
                    var numEsps = EmbeddedEffects.Count;
                    if (numEsps == 0)
                    {
                        // Most RE 3 RDTs just 80 0xFF bytes for some reason
                        // R100 however doesn't
                        // if (Version == BioVersion.Biohazard3)
                        // {
                        //     offsetTable[17] = (int)ms.Position;
                        //     for (var i = 0; i < 80; i++)
                        //         bw.Write((byte)0xFF);
                        //     offsetTable[18] = (int)ms.Position - 4;
                        // }
                    }
                    else
                    {
                        var maxEsps = Version == BioVersion.Biohazard2 ? 8 : 16;
                        var tableIndex = Version == BioVersion.Biohazard2 ? 18 : 17;
                        offsetTable[tableIndex] = (int)ms.Position;
                        bw.Write(EmbeddedEffects.ESPID);

                        // EFFs
                        var espTable = new int[EmbeddedEffects.Count];
                        var relativeOffset = maxEsps;
                        for (var i = 0; i < EmbeddedEffects.Count; i++)
                        {
                            var eff = EmbeddedEffects[i].Eff;
                            espTable[i] = relativeOffset;
                            bw.Write(eff.Data);
                            relativeOffset += eff.Data.Length;
                        }

                        // EFF relative offsets
                        for (var i = 0; i < maxEsps - espTable.Length; i++)
                            bw.Write(-1);
                        foreach (var o in espTable.Reverse())
                            bw.Write(o);

                        offsetTable[tableIndex + 1] = (int)ms.Position - 4;
                    }
                }

                if (Version != BioVersion.Biohazard3 && !RBJ.Data.IsEmpty)
                {
                    offsetTable[22] = (int)ms.Position;
                    bw.Write(RBJ.Data);
                }

                if (Version == BioVersion.Biohazard2)
                {
                    offsetTable[5] = (int)ms.Position;
                }

                if (EDT.Length != 0)
                {
                    offsetTable[0] = (int)ms.Position;
                    bw.Write(EDT);
                }

                if (Version == BioVersion.Biohazard2)
                {
                    if (VH.Length != 0)
                    {
                        offsetTable[1] = (int)ms.Position;
                        bw.Write(VH);
                    }
                    if (VB.Length != 0)
                    {
                        offsetTable[2] = (int)ms.Position;
                        bw.Write(VB);
                    }
                }
                else
                {
                    if (VH.Length != 0)
                    {
                        offsetTable[1] = (int)ms.Position;
                        bw.Write(VH);
                    }
                    offsetTable[2] = VBOFFSET ?? offsetTable[1] + 1;
                }

                if (Version == BioVersion.Biohazard3 && !ETD.Data.IsEmpty)
                {
                    offsetTable[19] = (int)ms.Position;
                    bw.Write(ETD.Data);
                    offsetTable[20] = (int)ms.Position;
                }

                if (Version == BioVersion.Biohazard2)
                {
                    var numEsps = EmbeddedEffects.Count;
                    if (numEsps != 0)
                    {
                        offsetTable[20] = (int)ms.Position;
                        var relativeOffsets = new int[numEsps];
                        var relativeOffset = 0;
                        for (var i = 0; i < numEsps; i++)
                        {
                            var tim = EmbeddedEffects[i].Tim;
                            relativeOffsets[i] = relativeOffset;
                            bw.Write(tim.Data);
                            relativeOffset += tim.Data.Length;
                        }
                        for (var i = 0; i < 8; i++)
                        {
                            var index = 7 - i;
                            var offset = index >= numEsps ? -1 : relativeOffsets[index];
                            bw.Write(offset);
                        }
                        offsetTable[21] = (int)ms.Position;
                    }
                }
                else
                {
                    // offsetTable[20] = (int)ms.Position;
                    // offsetTable[21] = (int)ms.Position;
                }

                var objectTimTable = new int[EmbeddedObjectTim.Count];
                for (var i = 0; i < EmbeddedObjectTim.Count; i++)
                {
                    objectTimTable[i] = (int)ms.Position;
                    bw.Write(EmbeddedObjectTim[i].Data);
                }

                // Write object TIM table
                ms.Position = offsetTable[10];
                for (var i = 0; i < EmbeddedObjectModelTable.Count; i++)
                {
                    var obj = EmbeddedObjectModelTable[i];
                    if (obj.Texture != -1)
                        bw.Write(objectTimTable[obj.Texture]);
                    else
                        bw.Write(0);
                    if (obj.Model != -1)
                        bw.Write(objectMd1Table[obj.Model]);
                    else
                        bw.Write(0);
                }

                // Write offsets
                ms.Position = offsetTableOffset;
                for (var i = 0; i < offsetTable.Length; i++)
                {
                    bw.Write(offsetTable[i]);
                }

                var bytes = ms.ToArray();
                return new Rdt2(Version, bytes);
            }
        }
    }
}

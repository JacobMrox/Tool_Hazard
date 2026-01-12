using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.Model;

namespace IntelOrca.Biohazard.Room
{
    public unsafe partial class Rdt1
    {
        public class Builder : IRdtBuilder
        {
            public Rdt1Header Header { get; set; }
            public byte[] LIT { get; set; } = new byte[0];
            public byte[] RID { get; set; } = new byte[0];
            public List<ModelTextureIndex> EmbeddedObjectModelTable { get; set; } = new List<ModelTextureIndex>();
            public List<ModelTextureIndex> EmbeddedItemModelTable { get; set; } = new List<ModelTextureIndex>();
            public byte[] RVD { get; set; } = new byte[0];
            public byte[] PRI { get; set; } = new byte[0];
            public byte[] SCA { get; set; } = new byte[0];
            public int? SCATerminator { get; set; }
            public byte[] BLK { get; set; } = new byte[0];
            public byte[] FLR { get; set; } = new byte[0];
            public ScdProcedureContainer InitSCD { get; set; }
            public ScdProcedureContainer MainSCD { get; set; }
            public ScdEventList EventSCD { get; set; }
            public Emr? EMR { get; set; }
            public Edd1? EDD { get; set; }
            public byte[] MSG { get; set; } = new byte[0];
            public byte[] EDT { get; set; } = new byte[0];
            public byte[] VH { get; set; } = new byte[0];
            public byte[] VB { get; set; } = new byte[0];

            public List<Tim> CameraTextures { get; set; } = new List<Tim>();
            public List<Tmd> EmbeddedObjectTmd { get; set; } = new List<Tmd>();
            public List<Tim> EmbeddedObjectTim { get; set; } = new List<Tim>();
            public List<Tmd> EmbeddedItemTmd { get; set; } = new List<Tmd>();
            public List<Tim> EmbeddedItemTim { get; set; } = new List<Tim>();
            public EmbeddedItemIcons EmbeddedItemIcons { get; set; }
            public EmbeddedEffectList EmbeddedEffects { get; set; }

            IRdt IRdtBuilder.ToRdt() => ToRdt();
            public Rdt1 ToRdt()
            {
                // Validate
                if (LIT.Length != 0x42)
                    throw new InvalidDataException("LIT must be 0x42 bytes long.");

                var offsetTable = new int[19];

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                bw.Write(Header);
                bw.Write(LIT);

                // Offsets placeholder
                var offsetTableOffset = ms.Position;
                for (var i = 0; i < 19; i++)
                {
                    bw.Write(0);
                }

                Debug.Assert(ms.Position == 0x94);
                bw.Write(RID);

                offsetTable[3] = (int)ms.Position;
                for (var i = 0; i < EmbeddedItemModelTable.Count; i++)
                {
                    bw.Write(0);
                    bw.Write(0);
                }

                offsetTable[2] = (int)ms.Position;
                for (var i = 0; i < EmbeddedObjectModelTable.Count; i++)
                {
                    bw.Write(0);
                    bw.Write(0);
                }

                offsetTable[0] = (int)ms.Position;
                bw.Write(RVD);
                bw.Write(-1);

                bw.Write(PRI);

                foreach (var tim in CameraTextures)
                    bw.Write(tim.Data);

                offsetTable[1] = (int)ms.Position;
                bw.Write(SCA);
                bw.Write(SCATerminator ?? SCA.Length);
                offsetTable[4] = (int)ms.Position;
                bw.Write(BLK);
                bw.Write((ushort)0);
                offsetTable[5] = (int)ms.Position;
                bw.Write(FLR);

                offsetTable[6] = (int)ms.Position;
                bw.Write(InitSCD.Data.ToArray());
                offsetTable[7] = (int)ms.Position;
                bw.Write(MainSCD.Data.ToArray());
                offsetTable[8] = (int)ms.Position;
                bw.Write(EventSCD.Data.ToArray());
                offsetTable[9] = (int)ms.Position;
                bw.Write(EMR!.Data);
                offsetTable[10] = (int)ms.Position;
                bw.Write(EDD!.Data);
                offsetTable[11] = (int)ms.Position;
                bw.Write(MSG);

                offsetTable[12] = (int)ms.Position;
                bw.Write(EmbeddedItemIcons.Data.ToArray());

                offsetTable[13] = (int)ms.Position;
                bw.Write(EmbeddedEffects.ESPID);

                var espTable = new int[EmbeddedEffects.Count];
                for (var i = 0; i < EmbeddedEffects.Count; i++)
                {
                    espTable[i] = (int)ms.Position;
                    bw.Write(EmbeddedEffects[i].Eff.Data.ToArray());
                }

                if (espTable.Length == 0)
                {
                    bw.Write(0, 28);
                    offsetTable[14] = (int)ms.Position;
                    bw.Write(0);
                }
                else
                {
                    for (var i = 0; i < 8 - espTable.Length; i++)
                        bw.Write(-1);
                    foreach (var o in espTable.Reverse())
                        bw.Write(o);
                    offsetTable[14] = (int)ms.Position - 4;
                }

                var itemTmdTable = new int[EmbeddedItemTmd.Count];
                for (var i = 0; i < EmbeddedItemTmd.Count; i++)
                {
                    itemTmdTable[i] = (int)ms.Position;
                    bw.Write(EmbeddedItemTmd[i].Data.ToArray());
                }

                var objectTmdTable = new int[EmbeddedObjectTmd.Count];
                for (var i = 0; i < EmbeddedObjectTmd.Count; i++)
                {
                    objectTmdTable[i] = (int)ms.Position;
                    bw.Write(EmbeddedObjectTmd[i].Data.ToArray());
                }

                offsetTable[16] = (int)ms.Position;
                bw.Write(EDT);
                offsetTable[17] = (int)ms.Position;
                bw.Write(VH);
                offsetTable[18] = (int)ms.Position;
                bw.Write(VB);

                var itemTimTable = new int[EmbeddedItemTim.Count];
                for (var i = 0; i < EmbeddedItemTim.Count; i++)
                {
                    itemTimTable[i] = (int)ms.Position;
                    bw.Write(EmbeddedItemTim[i].Data);
                }

                var objectTimTable = new int[EmbeddedObjectTim.Count];
                for (var i = 0; i < EmbeddedObjectTim.Count; i++)
                {
                    objectTimTable[i] = (int)ms.Position;
                    bw.Write(EmbeddedObjectTim[i].Data);
                }

                var espTimTable = new int[EmbeddedEffects.Count];
                for (var i = 0; i < EmbeddedEffects.Count; i++)
                {
                    espTimTable[i] = (int)ms.Position;
                    bw.Write(EmbeddedEffects[i].Tim.Data);
                }

                if (espTimTable.Length != 0)
                {
                    for (var i = 0; i < 8 - espTimTable.Length; i++)
                        bw.Write(-1);
                    foreach (var o in espTimTable.Reverse())
                        bw.Write(o);
                    offsetTable[15] = (int)ms.Position - 4;
                }

                // Write item TIM table
                ms.Position = offsetTable[3];
                for (var i = 0; i < EmbeddedItemModelTable.Count; i++)
                {
                    var item = EmbeddedItemModelTable[i];
                    if (item.Model != -1)
                        bw.Write(itemTmdTable[item.Model]);
                    else
                        bw.Write(0);
                    if (item.Texture != -1)
                        bw.Write(itemTimTable[item.Texture]);
                    else
                        bw.Write(0);
                }

                // Write object TIM table
                ms.Position = offsetTable[2];
                for (var i = 0; i < EmbeddedObjectModelTable.Count; i++)
                {
                    var obj = EmbeddedObjectModelTable[i];
                    if (obj.Model != -1)
                        bw.Write(objectTmdTable[obj.Model]);
                    else
                        bw.Write(0);
                    if (obj.Texture != -1)
                        bw.Write(objectTimTable[obj.Texture]);
                    else
                        bw.Write(0);
                }

                // Write offsets
                ms.Position = offsetTableOffset;
                for (var i = 0; i < 19; i++)
                {
                    bw.Write(offsetTable[i]);
                }

                var bytes = ms.ToArray();
                return new Rdt1(bytes);
            }
        }
    }
}

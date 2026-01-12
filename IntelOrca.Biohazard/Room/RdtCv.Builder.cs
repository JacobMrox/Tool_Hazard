using System.Collections.Generic;
using System.IO;

namespace IntelOrca.Biohazard.Room
{
    public partial class RdtCv
    {
        public class Builder : IRdtBuilder
        {
            public FileHeader Header { get; set; }
            public byte[] UnknownDataAfterHeader { get; set; } = new byte[0];
            public CvCameraList Cameras { get; set; }
            public byte[] LightingData { get; set; } = new byte[0];
            public List<Enemy> Enemies { get; set; } = new List<Enemy>();
            public List<RoomObject> Objects { get; set; } = new List<RoomObject>();
            public List<Item> Items { get; } = new List<Item>();
            public List<Effect> Effects { get; set; } = new List<Effect>();
            public List<Boundary> Boundaries { get; set; } = new List<Boundary>();
            public List<Aot> Aots { get; } = new List<Aot>();
            public List<AotTrigger> Triggers { get; set; } = new List<AotTrigger>();
            public List<Player> Players { get; set; } = new List<Player>();
            public List<AotEvent> Events { get; set; } = new List<AotEvent>();
            public byte[] Unknown1Data { get; set; } = new byte[0];
            public int Unknown1Count { get; set; }
            public int Unknown2 { get; set; }
            public int Unknown2Count { get; set; }
            public int ReactionCount { get; set; }
            public List<Reaction> Reactions { get; } = new List<Reaction>();
            public CvMsgList TextData { get; set; }
            public byte[] SysmesData { get; set; } = new byte[0];
            public CvModelList Models { get; set; }
            public CvMotionList Motions { get; set; }
            public ScdProcedureList Script { get; set; }
            public CvTextureList Textures { get; set; }

            public RdtCv ToRdt()
            {
                var header = Header;

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                bw.Write(header);

                ms.Position = 0x100;
                bw.Write(Cameras.Count);
                bw.Write(LightingData.Length / 224);
                bw.Write(Enemies.Count);
                bw.Write(Objects.Count);
                bw.Write(Items.Count);
                bw.Write(Effects.Count);
                bw.Write(Boundaries.Count);
                bw.Write(Aots.Count);
                bw.Write(Triggers.Count);
                bw.Write(Players.Count);
                bw.Write(Events.Count);
                bw.Write(Unknown1Count);
                bw.Write(Unknown2Count);
                bw.Write(ReactionCount);

                ms.Position = 0x180;
                bw.Write(UnknownDataAfterHeader);

                ms.Position = 0x46C;

                var tableOffsets = new int[16];
                if (Cameras.Data.Length != 0)
                {
                    tableOffsets[0] = (int)ms.Position;
                    bw.Write(Cameras.Data);
                }

                if (LightingData.Length != 0)
                {
                    tableOffsets[1] = (int)ms.Position;
                    bw.Write(LightingData);
                }

                if (Enemies.Count != 0)
                {
                    tableOffsets[2] = (int)ms.Position;
                    foreach (var enemy in Enemies)
                    {
                        bw.Write(enemy);
                    }
                }

                if (Objects.Count != 0)
                {
                    tableOffsets[3] = (int)ms.Position;
                    foreach (var obj in Objects)
                    {
                        bw.Write(obj);
                    }
                }

                if (Items.Count != 0)
                {
                    tableOffsets[4] = (int)ms.Position;
                    foreach (var item in Items)
                    {
                        bw.Write(item);
                    }
                }

                if (Effects.Count != 0)
                {
                    tableOffsets[5] = (int)ms.Position;
                    foreach (var effect in Effects)
                    {
                        bw.Write(effect);
                    }
                }

                if (Boundaries.Count != 0)
                {
                    tableOffsets[6] = (int)ms.Position;
                    foreach (var b in Boundaries)
                    {
                        bw.Write(b);
                    }
                }

                if (Aots.Count != 0)
                {
                    tableOffsets[7] = (int)ms.Position;
                    foreach (var aot in Aots)
                    {
                        bw.Write(aot);
                    }
                }

                if (Triggers.Count != 0)
                {
                    tableOffsets[8] = (int)ms.Position;
                    foreach (var trigger in Triggers)
                    {
                        bw.Write(trigger);
                    }
                }

                if (Players.Count != 0)
                {
                    tableOffsets[9] = (int)ms.Position;
                    foreach (var p in Players)
                    {
                        bw.Write(p);
                    }
                }

                if (Events.Count != 0)
                {
                    tableOffsets[10] = (int)ms.Position;
                    foreach (var evt in Events)
                    {
                        bw.Write(evt);
                    }
                }

                if (Unknown1Data.Length != 0)
                {
                    tableOffsets[11] = (int)ms.Position;
                    bw.Write(Unknown1Data);
                }

                tableOffsets[12] = Unknown2;

                if (Reactions.Count != 0)
                {
                    tableOffsets[13] = (int)ms.Position;
                    foreach (var reaction in Reactions)
                    {
                        bw.Write(reaction);
                    }
                }

                if (TextData.Data.Length != 0)
                {
                    tableOffsets[14] = (int)ms.Position;
                    bw.Write(TextData.Data);
                }

                if (SysmesData.Length != 0)
                {
                    tableOffsets[15] = (int)ms.Position;
                    bw.Write(SysmesData);
                }

                bw.Align(64);
                if (Models.Data.Length != 0)
                {
                    header.ModelOffset = (int)ms.Position;
                    var newModelList = Models.WithNewBaseOffset(header.ModelOffset);
                    bw.Write(newModelList.Data);
                }

                bw.Align(64);
                if (Motions.Data.Length != 0)
                {
                    header.MotionOffset = (int)ms.Position;
                    var newMotionList = Motions.WithNewBaseOffset(header.MotionOffset);
                    bw.Write(newMotionList.Data);
                }

                bw.Align(16);
                if (Script.Data.Length != 0)
                {
                    header.ScriptOffset = (int)ms.Position;
                    bw.Write(Script.Data);
                }

                bw.Align(4);
                if (Textures.Data.Length != 0)
                {
                    header.TextureOffset = (int)ms.Position;
                    var newTextureList = Textures.WithNewBaseOffset(header.TextureOffset);
                    bw.Write(newTextureList.Data);
                }

                ms.Position = 0x80;
                header.TableOffset = (int)ms.Position;
                foreach (var offset in tableOffsets)
                {
                    bw.Write(offset);
                }

                ms.Position = 0;
                bw.Write(header);

                Header = header;
                return new RdtCv(ms.ToArray());
            }

            IRdt IRdtBuilder.ToRdt() => ToRdt();
        }
    }
}

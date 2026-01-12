using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Model;

namespace IntelOrca.Biohazard.Room
{
    public unsafe partial class Rdt1 : IRdt
    {
        // 0x00: header.dat
        // 0x06: light.lit
        // 0x48: offsets
        //     0: zone.rvd
        //     1: collision.sca
        //     2: embedded object model table
        //     3: embedded item model table
        //     4: block.blk
        //     5: floor.flr
        //     6: init.scd
        //     7: main.scd
        //     8: events.scd
        //     9: animation.emr
        //    10: animation.emd
        //    11: message.msg
        //    12: embedded item PIX table
        //    13: ESP IDs
        //    14: embedded ESP EFF table
        //    15: embedded ESP TIM table
        //    16: sound/snd.edt
        //    17: sound/snd.vh
        //    18: sound/snd.vb
        // 0x94: camera.rid
        //       embedded item model table
        //       embedded object model table
        //       zone.rvd
        //       sprite.pri
        //       camera/spriteXX.tim
        //       collision.sca
        //       block.blk
        //       floor.flr
        //       scripts/main00.scd
        //       scripts/main01.scd
        //       scripts/subXX.scd
        //       scripts/animation.emr
        //       scripts/animation.edd
        //       message/mainXX.msg
        //       item/itemXX.pix
        //       ESP IDs
        //       effect/espXX.eff
        //       embedded ESP EFF table
        //       sound/snd.edt
        //       sound/snd.vh
        //       sound/snd.vb
        //       item/itemXX.tim
        //       object/objectXX.tim
        //       effect/espXX.tim
        //       embedded ESP TIM table

        private readonly RdtFileData _data;

        public BioVersion Version => BioVersion.Biohazard1;
        public ReadOnlyMemory<byte> Data => _data.Data;

        public Rdt1(string path)
            : this(File.ReadAllBytes(path))
        {
        }

        public Rdt1(ReadOnlyMemory<byte> data)
        {
            _data = new RdtFileData(data);
            _data.RegisterOffset(RdtFileChunkKinds.RDT1Header, 0, true);
            _data.RegisterOffset(RdtFileChunkKinds.RDT1LIT, 0x6, true);
            _data.RegisterOffset(RdtFileChunkKinds.RDT1Offsets, 0x48, true);
            _data.RegisterOffset(RdtFileChunkKinds.RDT1RID, 0x94, true);

            var offsets = Offsets;
            for (var i = 0; i < offsets.Length; i++)
            {
                var offset = offsets[i];
                if (offset != 0)
                {
                    if (i == 2 && Header.nOmodel == 0)
                        continue;
                    if ((i == 3 || i == 12) && Header.nItem == 0)
                        continue;
                    // ESP/EFF and ESP/TIM tables point to last element
                    if (i == 14 || i == 15)
                        offset -= 7 * 4;

                    _data.RegisterOffset(_offsetTableKinds[i], offset);
                }
            }

            // Things we know the length of
            if (Header.nOmodel != 0)
            {
                _data.RegisterLength(offsets[2], Header.nOmodel * 8);
            }
            if (Header.nItem != 0)
            {
                _data.RegisterLength(offsets[3], Header.nItem * 8);
                _data.RegisterLength(offsets[12], Header.nItem * EmbeddedItemIcon.Size);
            }
            _data.RegisterLength(offsets[13], 8);

            // Embedded stuff
            foreach (var cam in Cameras)
            {
                if (cam.tim_masks_offset != 0)
                    _data.RegisterOffset(RdtFileChunkKinds.RDT1EmbeddedCamTim, cam.tim_masks_offset, true);
                if (cam.masks_offset != 0)
                    _data.RegisterOffset(RdtFileChunkKinds.RDT1EmbeddedCamMask, cam.masks_offset, true);
            }
            foreach (var embeddedModel in EmbeddedObjectModelTable.Offsets)
            {
                if (embeddedModel.Model != 0)
                    _data.RegisterOffset(RdtFileChunkKinds.RDT1EmbeddedObjectTmd, embeddedModel.Model, true);
                if (embeddedModel.Texture != 0)
                    _data.RegisterOffset(RdtFileChunkKinds.RDT1EmbeddedObjectTim, embeddedModel.Texture, true);
            }
            foreach (var embeddedModel in EmbeddedItemModelTable.Offsets)
            {
                if (embeddedModel.Model != 0)
                    _data.RegisterOffset(RdtFileChunkKinds.RDT1EmbeddedItemTmd, embeddedModel.Model, true);
                if (embeddedModel.Texture != 0)
                    _data.RegisterOffset(RdtFileChunkKinds.RDT1EmbeddedItemTim, embeddedModel.Texture, true);
            }
            foreach (var embeddedEff in ESPEFF)
            {
                if (embeddedEff != 0 && embeddedEff != -1)
                    _data.RegisterOffset(RdtFileChunkKinds.RDT1EmbeddedEspEff, embeddedEff, true);
            }
            foreach (var embeddedTim in ESPTIM)
            {
                if (embeddedTim != -1)
                    _data.RegisterOffset(RdtFileChunkKinds.RDT1EmbeddedEspTim, embeddedTim, true);
            }
        }

        private ReadOnlyMemory<byte> GetChunk(int kind)
        {
            var chunk = _data.FindChunkByKind(kind);
            if (chunk == null)
                return ReadOnlyMemory<byte>.Empty;
            return chunk.Value.Memory;
        }

        public Rdt1Header Header => GetSpan<Rdt1Header>(0x00, 1)[0];
        public ReadOnlySpan<byte> LIT => GetChunk(RdtFileChunkKinds.RDT1LIT).Span;
        public ReadOnlySpan<int> Offsets => GetSpan<int>(0x48, 19);
        public ReadOnlySpan<byte> RID => GetChunk(RdtFileChunkKinds.RDT1RID).Span;
        public EmbeddedModelTable1 EmbeddedObjectModelTable => new EmbeddedModelTable1(GetChunk(RdtFileChunkKinds.RDT1EmbeddedModelTable));
        public EmbeddedModelTable1 EmbeddedItemModelTable => new EmbeddedModelTable1(GetChunk(RdtFileChunkKinds.RDT1EmbeddedItemTable));
        public ReadOnlySpan<byte> RVD => GetChunk(RdtFileChunkKinds.RDT1RVD).Span.TruncateBy(4);
        public ReadOnlySpan<byte> PRI
        {
            get
            {
                var minOffset = int.MaxValue;
                var maxOffset = int.MinValue;
                foreach (var chunk in _data.Chunks)
                {
                    if (chunk.Kind == RdtFileChunkKinds.RDT1EmbeddedCamMask)
                    {
                        minOffset = Math.Min(minOffset, chunk.Offset);
                        maxOffset = Math.Max(maxOffset, chunk.End);
                    }
                }
                if (minOffset == int.MaxValue)
                    return ReadOnlySpan<byte>.Empty;
                return Data.Slice(minOffset, maxOffset - minOffset).Span;
            }
        }
        public ReadOnlySpan<byte> SCA => GetChunk(RdtFileChunkKinds.RDT1SCA).Span.TruncateBy(4);
        public int SCATerminator => MemoryMarshal.Cast<byte, int>(GetChunk(RdtFileChunkKinds.RDT1SCA).Span.TruncateStartBy(-4))[0];
        public ReadOnlySpan<byte> BLK => GetChunk(RdtFileChunkKinds.RDT1BLK).Span.TruncateBy(2);
        public ReadOnlySpan<byte> FLR => GetChunk(RdtFileChunkKinds.RDT1FLR).Span;
        public ScdProcedureContainer InitSCD => new ScdProcedureContainer(GetChunk(RdtFileChunkKinds.RDT1InitSCD));
        public ScdProcedureContainer MainSCD => new ScdProcedureContainer(GetChunk(RdtFileChunkKinds.RDT1MainSCD));
        public ScdEventList EventSCD => new ScdEventList(GetChunk(RdtFileChunkKinds.RDT1EventSCD));
        public Emr EMR => new Emr(Version, GetChunk(RdtFileChunkKinds.RDT1EMR));
        public Edd1 EDD => new Edd1(Version, GetChunk(RdtFileChunkKinds.RDT1EDD));
        public ReadOnlySpan<byte> MSG => GetChunk(RdtFileChunkKinds.RDT1MSG).Span;
        public EmbeddedItemIcons RDT1EmbeddedItemIcons => new EmbeddedItemIcons(GetChunk(RdtFileChunkKinds.RDT1EmbeddedItemIcons));
        public ReadOnlySpan<byte> ESPIDs => GetChunk(RdtFileChunkKinds.RDT1ESPIDs).Span;
        public ReadOnlySpan<int> ESPEFF => MemoryMarshal.Cast<byte, int>(GetChunk(RdtFileChunkKinds.RDT1ESPEFFTable).Span);
        public ReadOnlySpan<int> ESPTIM => MemoryMarshal.Cast<byte, int>(GetChunk(RdtFileChunkKinds.RDT1ESPTIMTable).Span);
        public ReadOnlySpan<byte> EDT => GetChunk(RdtFileChunkKinds.RDT1EDT).Span;
        public ReadOnlySpan<byte> VH => GetChunk(RdtFileChunkKinds.RDT1VH).Span;
        public ReadOnlySpan<byte> VB => GetChunk(RdtFileChunkKinds.RDT1VB).Span;

        public ReadOnlySpan<Rdt1Camera> Cameras
        {
            get
            {
                var numCameras = Header.nCut;
                return GetSpan<Rdt1Camera>(0x94, numCameras);
            }
        }

        public ReadOnlySpan<Rdt1CameraSwitch> CameraSwitches
        {
            get
            {
                var offset = Offsets[0];
                var maxSwitches = (Data.Length - offset) / sizeof(Rdt1Camera);
                var switches = GetSpan<Rdt1CameraSwitch>(offset, maxSwitches);
                var numSwitches = 0;
                for (var i = 0; i < switches.Length; i++)
                {
                    if (switches[i].to == ushort.MaxValue &&
                        switches[i].from == ushort.MaxValue)
                    {
                        numSwitches = i;
                        break;
                    }
                }
                return switches.Slice(0, numSwitches);
            }
        }
        public ReadOnlySpan<Rdt1EmbeddedModel> EmbeddedObjectModels
        {
            get
            {
                var offset = Offsets[2];
                return GetSpan<Rdt1EmbeddedModel>(offset, Header.nOmodel);
            }
        }
        public ReadOnlySpan<Rdt1EmbeddedModel> EmbeddedItemModels
        {
            get
            {
                var offset = Offsets[3];
                return GetSpan<Rdt1EmbeddedModel>(offset, Header.nItem);
            }
        }
        public EmbeddedEffectList EmbeddedEffects
        {
            get
            {
                var ids = ESPIDs;
                var count = ids.ToArray().Count(x => x != 0xFF);
                var effects = new EmbeddedEffect[count];
                var effs = new List<Eff>();
                var tims = new List<Tim>();
                foreach (var chunk in _data.Chunks)
                {
                    if (chunk.Kind == RdtFileChunkKinds.RDT1EmbeddedEspEff)
                        effs.Add(new Eff(chunk.Memory));
                    else if (chunk.Kind == RdtFileChunkKinds.RDT1EmbeddedEspTim)
                        tims.Add(new Tim(chunk.Memory));
                }
                for (var i = 0; i < count; i++)
                {
                    effects[i] = new EmbeddedEffect(ids[i], effs[i], tims[i]);
                }
                return new EmbeddedEffectList(Version, effects);
            }
        }

        IRdtBuilder IRdt.ToBuilder() => ToBuilder();
        public Builder ToBuilder()
        {
            var builder = new Builder();

            var embeddedObjectTmdOffsets = new OffsetTracker();
            var embeddedObjectTimOffsets = new OffsetTracker();
            foreach (var pair in EmbeddedObjectModelTable.Offsets)
            {
                var tmdIndex = embeddedObjectTmdOffsets.GetOrAdd(pair.Model);
                var timIndex = embeddedObjectTimOffsets.GetOrAdd(pair.Texture);
                builder.EmbeddedObjectModelTable.Add(new ModelTextureIndex(tmdIndex, timIndex));
            }
            foreach (var offset in embeddedObjectTmdOffsets)
                builder.EmbeddedObjectTmd.Add(ReadEmbeddedTmd(offset));
            foreach (var offset in embeddedObjectTimOffsets)
                builder.EmbeddedObjectTim.Add(ReadEmbeddedTim(offset));

            var embeddedItemTmdOffsets = new OffsetTracker();
            var embeddedItemTimOffsets = new OffsetTracker();
            foreach (var pair in EmbeddedItemModelTable.Offsets)
            {
                var tmdIndex = embeddedItemTmdOffsets.GetOrAdd(pair.Model);
                var timIndex = embeddedItemTimOffsets.GetOrAdd(pair.Texture);
                builder.EmbeddedItemModelTable.Add(new ModelTextureIndex(tmdIndex, timIndex));
            }
            foreach (var offset in embeddedItemTmdOffsets)
                builder.EmbeddedItemTmd.Add(ReadEmbeddedTmd(offset));
            foreach (var offset in embeddedItemTimOffsets)
                builder.EmbeddedItemTim.Add(ReadEmbeddedTim(offset));

            foreach (var chunk in _data.Chunks)
            {
                if (chunk.Kind == RdtFileChunkKinds.RDT1EmbeddedCamTim)
                    builder.CameraTextures.Add(new Tim(chunk.Memory));
            }

            builder.EmbeddedEffects = EmbeddedEffects;

            builder.Header = Header;
            builder.LIT = LIT.ToArray();
            builder.RID = RID.ToArray();
            builder.RVD = RVD.ToArray();
            builder.PRI = PRI.ToArray();
            builder.SCA = SCA.ToArray();
            builder.SCATerminator = SCATerminator;
            builder.BLK = BLK.ToArray();
            builder.FLR = FLR.ToArray();
            builder.InitSCD = InitSCD;
            builder.MainSCD = MainSCD;
            builder.EventSCD = EventSCD;
            builder.EDD = EDD;
            builder.EMR = EMR;
            builder.MSG = MSG.ToArray();
            builder.EmbeddedItemIcons = RDT1EmbeddedItemIcons;
            builder.EDT = EDT.ToArray();
            builder.VH = VH.ToArray();
            builder.VB = VB.ToArray();
            return builder;
        }

        private Tmd ReadEmbeddedTmd(int offset)
        {
            var chunk = _data.FindChunkByOffset(offset);
            return new Tmd(chunk!.Value.Memory);
        }

        private Tim ReadEmbeddedTim(int offset)
        {
            var chunk = _data.FindChunkByOffset(offset);
            return new Tim(chunk!.Value.Memory);
        }

        private Eff ReadEmbeddedEff(int offset)
        {
            var chunk = _data.FindChunkByOffset(offset);
            return new Eff(chunk!.Value.Memory);
        }

        private ReadOnlySpan<T> GetSpan<T>(int offset, int count) where T : struct => Data.GetSafeSpan<T>(offset, count);

        private static readonly int[] _offsetTableKinds = new[]
        {
            RdtFileChunkKinds.RDT1RVD,
            RdtFileChunkKinds.RDT1SCA,
            RdtFileChunkKinds.RDT1EmbeddedModelTable,
            RdtFileChunkKinds.RDT1EmbeddedItemTable,
            RdtFileChunkKinds.RDT1BLK,
            RdtFileChunkKinds.RDT1FLR,
            RdtFileChunkKinds.RDT1InitSCD,
            RdtFileChunkKinds.RDT1MainSCD,
            RdtFileChunkKinds.RDT1EventSCD,
            RdtFileChunkKinds.RDT1EMR,
            RdtFileChunkKinds.RDT1EDD,
            RdtFileChunkKinds.RDT1MSG,
            RdtFileChunkKinds.RDT1EmbeddedItemIcons,
            RdtFileChunkKinds.RDT1ESPIDs,
            RdtFileChunkKinds.RDT1ESPEFFTable,
            RdtFileChunkKinds.RDT1ESPTIMTable,
            RdtFileChunkKinds.RDT1EDT,
            RdtFileChunkKinds.RDT1VH,
            RdtFileChunkKinds.RDT1VB,
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct Rdt1Header
        {
            public byte nSprite;
            public byte nCut;
            public byte nOmodel;
            public byte nItem;
            public byte nDoor;
            public byte nRoom_at;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rdt1Camera
        {
            public int masks_offset;
            public int tim_masks_offset;
            public int camera_from_x;
            public int camera_from_y;
            public int camera_from_z;
            public int camera_to_x;
            public int camera_to_y;
            public int camera_to_z;
            public fixed int unknown1[3];
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rdt1CameraSwitch
        {
            public ushort to;
            public ushort from;
            public short x1, y1;
            public short x2, y2;
            public short x3, y3;
            public short x4, y4;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rdt1EmbeddedModel
        {
            public uint tmd_offset;
            public uint tim_offset;
        }
    }
}

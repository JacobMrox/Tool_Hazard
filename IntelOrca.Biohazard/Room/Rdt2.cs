using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Model;

namespace IntelOrca.Biohazard.Room
{
    public sealed partial class Rdt2 : IRdt
    {
        // 0x00: header.dat
        // 0x08: offsets
        //     0: sound/snd.edt
        //     1: sound/snd.vh
        //     2: sound/snd.vb
        //     3: sound/snd.vh [trial]
        //     4: sound/snd.vb [trial]
        //     5: unknown.ova
        //     6: collision.sca
        //     7: camera.rid
        //     8: zone.rvd
        //     9: light.lit
        //    10: embedded object model table
        //    11: floor.flr
        //    12: block.blk
        //    13: message/mainXX.msg
        //    14: message/subXX.msg
        //    15: scroll.tim
        //    16: scripts/main00.scd
        //    17: scripts/subXX.scd (RE 2) | ESP IDs (RE 3)
        //    18: ESP IDs (RE 2) | embedded ESP EFF table (RE 3)
        //    19: embedded ESP EFF table (RE 2) | unknown (RE 3)
        //    20: effect/espXX.tim
        //    21: object/objectXX.tim
        //    22: animation/anim.rbj (RE 2)
        // 0x64: camera.rid
        //       embedded object model table
        //       zone.rvd
        //       light.lit
        //       sprite.pri
        //       collision.sca
        //       block.blk
        //       floor.flr
        //       scripts/main00.scd
        //       scripts/subXX.scd
        //       message/mainXX.msg
        //       message/subXX.msg
        //       scroll.tim
        //       object/objectXX.md1
        //       ESP IDs
        //       effect/espXX.eff
        //       embedded ESP EFF table
        //       animation/anim.rbj
        //       unknown.ova
        //       sound/snd.edt
        //       sound/snd.vh
        //       sound/snd.vb
        //       effect/espXX.tim
        //       object/objectXX.tim

        private readonly RdtFileData _data;

        public BioVersion Version { get; }
        public ReadOnlyMemory<byte> Data => _data.Data;

        public Rdt2(BioVersion version, string path)
            : this(version, File.ReadAllBytes(path))
        {
        }

        public Rdt2(BioVersion version, ReadOnlyMemory<byte> data)
        {
            Version = version;
            _data = new RdtFileData(data);
            _data.RegisterOffset(RdtFileChunkKinds.Header, 0, true);
            _data.RegisterOffset(RdtFileChunkKinds.OffsetTable, 8, true);

            var offsetKinds = Version == BioVersion.Biohazard2 ? _offsetTableKinds2 : _offsetTableKinds3;
            var offsets = Offsets;
            for (var i = 0; i < offsets.Length; i++)
            {
                var offset = offsets[i];
                if (offset != 0 && i != 5)
                {
                    // Ignore EFF tables (assumed to be at end of ESP ID block)
                    if (Version == BioVersion.Biohazard2 && i == 19)
                        continue;
                    if (Version == BioVersion.Biohazard3 && i == 18)
                        continue;

                    // In RE 3, 4 RDTs have a garbage number in offset 2, the rest always have (offset 1) + 1 in it.
                    if (Version == BioVersion.Biohazard3 && i == 2)
                        continue;

                    // Ignore object table if no objects
                    if (i == 10 && Header.nOmodel == 0)
                        continue;

                    _data.RegisterOffset(offsetKinds[i], offset);
                }
            }

            // Things we know the length of
            if (Header.nOmodel != 0)
                _data.RegisterLength(offsets[10], Header.nOmodel * 8);

            // Embedded stuff
            foreach (var camera in RID.Cameras)
            {
                if (camera.masks_offset > 0)
                {
                    _data.RegisterOffset(RdtFileChunkKinds.RDT2EmbeddedCamMask, (int)camera.masks_offset);
                }
            }

            var chunk = _data.FindChunkByKind(RdtFileChunkKinds.RDT2EmbeddedObjectTable);
            if (chunk != null)
            {
                var count = Header.nOmodel;
                var br = new BinaryReader(chunk.Value.Stream);
                var timOffsets = new HashSet<int>();
                var md1Offsets = new HashSet<int>();
                for (var i = 0; i < count; i++)
                {
                    var tim = br.ReadInt32();
                    if (tim != 0 && timOffsets.Add(tim))
                    {
                        _data.RegisterOffset(RdtFileChunkKinds.MD2TIMOBJECT, tim, true);
                    }

                    var md1 = br.ReadInt32();
                    if (md1 != 0 && md1Offsets.Add(md1))
                    {
                        _data.RegisterOffset(RdtFileChunkKinds.RDT2MD1OBJECT, md1, true);
                    }
                }
            }

            chunk = _data.FindChunkByKind(RdtFileChunkKinds.RDT2TIMESP);
            if (chunk != null && chunk.Value.Length != 0)
            {
                var timOffsets = MemoryMarshal.Cast<byte, int>(chunk.Value.Span.TruncateStartBy(-4 * 8));

                // Record where the TIM relative offsets are
                _data.RegisterOffset(RdtFileChunkKinds.RDT2ESPTIMTABLE, chunk.Value.Offset + chunk.Value.Length - (4 * 8));

                var numEsps = EspTable.Count;
                for (var i = 0; i < numEsps; i++)
                {
                    var index = 7 - i;
                    var offsetA = timOffsets[index];
                    _data.RegisterOffset(RdtFileChunkKinds.RDT2TIMESP, chunk.Value.Offset + offsetA, true);
                }
            }
        }

        private ReadOnlyMemory<byte> GetChunk(int kind)
        {
            var chunk = _data.FindChunkByKind(kind);
            if (chunk == null)
                return ReadOnlyMemory<byte>.Empty;
            return chunk.Value.Memory;
        }

        public Rdt2Header Header => GetSpan<Rdt2Header>(0x00, 1)[0];
        public ReadOnlySpan<int> Offsets =>
            Version == BioVersion.Biohazard2 ?
                GetSpan<int>(0x08, _offsetTableKinds2.Length) :
                GetSpan<int>(0x08, _offsetTableKinds3.Length);
        public ReadOnlySpan<byte> EDT => GetChunk(RdtFileChunkKinds.RDT2EDT).Span;
        public ReadOnlySpan<byte> VH => GetChunk(RdtFileChunkKinds.RDT2VH).Span;
        public ReadOnlySpan<byte> VB => GetChunk(RdtFileChunkKinds.RDT2VB).Span;
        public ReadOnlySpan<byte> TrialVH => GetChunk(RdtFileChunkKinds.EmbeddedTrialVH).Span;
        public ReadOnlySpan<byte> TrialVB => GetChunk(RdtFileChunkKinds.EmbeddedTrialVB).Span;
        public ReadOnlySpan<byte> OTA => GetChunk(RdtFileChunkKinds.RDT2OVA).Span;
        public ReadOnlySpan<byte> SCA => GetChunk(RdtFileChunkKinds.RDT2SCA).Span;
        public Rid RID => new Rid(GetChunk(RdtFileChunkKinds.RDT2RID));
        public ReadOnlySpan<byte> RVD => GetChunk(RdtFileChunkKinds.RDT2RVD).Span.TruncateBy(4);
        public ReadOnlySpan<byte> LIT => GetChunk(RdtFileChunkKinds.RDT2LIT).Span;
        public EmbeddedModelTable2 EmbeddedObjectModelTable => new EmbeddedModelTable2(GetChunk(RdtFileChunkKinds.RDT2EmbeddedObjectTable));
        public ReadOnlySpan<byte> FLR => GetChunk(RdtFileChunkKinds.RDT2FLR).Span.TruncateBy(2);
        public ushort FLRTerminator => MemoryMarshal.Cast<byte, ushort>(GetChunk(RdtFileChunkKinds.RDT2FLR).Span.TruncateStartBy(-2))[0];
        public ReadOnlySpan<byte> BLK => GetChunk(RdtFileChunkKinds.RDT2BLK).Span;
        public MsgList MSGJA => new MsgList(Version, MsgLanguage.Japanese, GetChunk(RdtFileChunkKinds.RDT2MSGJA));
        public MsgList MSGEN => new MsgList(Version, MsgLanguage.English, GetChunk(RdtFileChunkKinds.RDT2MSGEN));
        public Tim TIMSCROLL => new Tim(GetChunk(RdtFileChunkKinds.RDT2TIMSCROLL));
        public ScdProcedureList SCDINIT => new ScdProcedureList(Version, GetChunk(RdtFileChunkKinds.RDT2SCDINIT));
        public ScdProcedureList SCDMAIN => Version == BioVersion.Biohazard2 ?
            new ScdProcedureList(Version, GetChunk(RdtFileChunkKinds.RDT2SCDMAIN)) :
            new ScdProcedureList();
        public Etd ETD => Version == BioVersion.Biohazard3 ?
            new Etd(GetChunk(RdtFileChunkKinds.RDT3ETD)) :
            new Etd();
        public EspTable EspTable => new EspTable(Version, GetChunk(RdtFileChunkKinds.RDT2ESPID));
        public ReadOnlySpan<int> TIMOBJ => MemoryMarshal.Cast<byte, int>(GetChunk(RdtFileChunkKinds.ObjectTextures).Span);
        public Rbj RBJ => new Rbj(Version, GetChunk(RdtFileChunkKinds.RDT2RBJ));

        public ReadOnlySpan<byte> PRI
        {
            get
            {
                var minOffset = int.MaxValue;
                var maxOffset = int.MinValue;
                foreach (var chunk in _data.Chunks)
                {
                    if (chunk.Kind == RdtFileChunkKinds.RDT2EmbeddedCamMask)
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

        public ReadOnlySpan<Rdt2EmbeddedModel> EmbeddedObjectModels
        {
            get
            {
                var offset = Offsets[10];
                var count = Header.nOmodel;
                return GetSpan<Rdt2EmbeddedModel>(offset, count);
            }
        }

        public EmbeddedEffectList EmbeddedEffects
        {
            get
            {
                if (EspTable.Count == 0)
                {
                    return new EmbeddedEffectList();
                }

                var ids = EspTable.Ids;
                var count = ids.ToArray().Count(x => x != 0xFF);
                var effects = new EmbeddedEffect[count];
                var tims = new List<Tim>();
                foreach (var chunk in _data.Chunks)
                {
                    if (chunk.Kind == RdtFileChunkKinds.RDT2TIMESP)
                        tims.Add(new Tim(chunk.Memory));
                }
                for (var i = 0; i < count; i++)
                {
                    var eff = EspTable.GetEff(i);
                    var tim = i < tims.Count ? tims[i] : new Tim();
                    effects[i] = new EmbeddedEffect(ids[i], eff, tim);
                }
                return new EmbeddedEffectList(Version, effects);
            }
        }

        IRdtBuilder IRdt.ToBuilder() => ToBuilder();
        public Builder ToBuilder()
        {
            var builder = new Builder(Version);

            var embeddedObjectMd1Offsets = new OffsetTracker();
            var embeddedObjectTimOffsets = new OffsetTracker();
            foreach (var pair in EmbeddedObjectModelTable.Offsets)
            {
                var md1Index = embeddedObjectMd1Offsets.GetOrAdd(pair.Model);
                var timIndex = embeddedObjectTimOffsets.GetOrAdd(pair.Texture);
                builder.EmbeddedObjectModelTable.Add(new ModelTextureIndex(md1Index, timIndex));
            }
            foreach (var offset in embeddedObjectMd1Offsets)
                builder.EmbeddedObjectMd1.Add(ReadEmbeddedMd1(offset));
            foreach (var offset in embeddedObjectTimOffsets)
                builder.EmbeddedObjectTim.Add(ReadEmbeddedTim(offset));

            // Most RE 3 RDTs have 80 bytes of 0xFF, this ensures 100% identical rebuild
            if (Version == BioVersion.Biohazard3)
                builder.EspTable = EspTable;

            builder.EmbeddedEffects = EmbeddedEffects;

            builder.Header = Header;
            builder.RID = RID;
            builder.RVD = RVD.ToArray();
            builder.LIT = LIT.ToArray();
            builder.PRI = PRI.ToArray();
            builder.SCA = SCA.ToArray();
            builder.BLK = BLK.ToArray();
            builder.FLR = FLR.ToArray();
            builder.FLRTerminator = FLRTerminator;
            builder.SCDINIT = SCDINIT;
            builder.SCDMAIN = SCDMAIN;
            builder.ETD = ETD;
            builder.MSGJA = MSGJA;
            builder.MSGEN = MSGEN;
            builder.TIMSCROLL = TIMSCROLL;
            builder.RBJ = RBJ;
            builder.EDT = EDT.ToArray();
            builder.VH = VH.ToArray();
            builder.VB = VB.ToArray();

            // 4 RE 3 RDTs have a garbage number in offset 2 (this preserves it)
            if (Version == BioVersion.Biohazard3)
                builder.VBOFFSET = Offsets[2];

            return builder;
        }

        private Md1 ReadEmbeddedMd1(int offset)
        {
            var chunk = _data.FindChunkByOffset(offset);
            return new Md1(chunk!.Value.Memory);
        }

        private Tim ReadEmbeddedTim(int offset)
        {
            var chunk = _data.FindChunkByOffset(offset);
            return new Tim(chunk!.Value.Memory);
        }

        private ReadOnlySpan<T> GetSpan<T>(int offset, int count) where T : struct => Data.GetSafeSpan<T>(offset, count);

        private static readonly int[] _offsetTableKinds2 = new[]
        {
            RdtFileChunkKinds.RDT2EDT,
            RdtFileChunkKinds.RDT2VH,
            RdtFileChunkKinds.RDT2VB,
            RdtFileChunkKinds.EmbeddedTrialVH,
            RdtFileChunkKinds.EmbeddedTrialVB,
            RdtFileChunkKinds.RDT2OVA,
            RdtFileChunkKinds.RDT2SCA,
            RdtFileChunkKinds.RDT2RID,
            RdtFileChunkKinds.RDT2RVD,
            RdtFileChunkKinds.RDT2LIT,
            RdtFileChunkKinds.RDT2EmbeddedObjectTable,
            RdtFileChunkKinds.RDT2FLR,
            RdtFileChunkKinds.RDT2BLK,
            RdtFileChunkKinds.RDT2MSGJA,
            RdtFileChunkKinds.RDT2MSGEN,
            RdtFileChunkKinds.RDT2TIMSCROLL,
            RdtFileChunkKinds.RDT2SCDINIT,
            RdtFileChunkKinds.RDT2SCDMAIN,
            RdtFileChunkKinds.RDT2ESPID,
            RdtFileChunkKinds.RDT2EspEffTable,
            RdtFileChunkKinds.RDT2TIMESP,
            RdtFileChunkKinds.ObjectTextures,
            RdtFileChunkKinds.RDT2RBJ,
        };

        private static readonly int[] _offsetTableKinds3 = new[]
{
            RdtFileChunkKinds.RDT2EDT,
            RdtFileChunkKinds.RDT2VH,
            RdtFileChunkKinds.RDT2VB,
            RdtFileChunkKinds.EmbeddedTrialVH,
            RdtFileChunkKinds.EmbeddedTrialVB,
            RdtFileChunkKinds.RDT2OVA,
            RdtFileChunkKinds.RDT2SCA,
            RdtFileChunkKinds.RDT2RID,
            RdtFileChunkKinds.RDT2RVD,
            RdtFileChunkKinds.RDT2LIT,
            RdtFileChunkKinds.RDT2EmbeddedObjectTable,
            RdtFileChunkKinds.RDT2FLR,
            RdtFileChunkKinds.RDT2BLK,
            RdtFileChunkKinds.RDT2MSGJA,
            RdtFileChunkKinds.RDT2MSGEN,
            RdtFileChunkKinds.RDT2TIMSCROLL,
            RdtFileChunkKinds.RDT2SCDINIT,
            RdtFileChunkKinds.RDT2ESPID,
            RdtFileChunkKinds.RDT2EspEffTable,
            RdtFileChunkKinds.RDT3ETD,
            RdtFileChunkKinds.RDT2TIMESP,
            RdtFileChunkKinds.ObjectTextures
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct Rdt2Header
        {
            public byte nSprite;
            public byte nCut;
            public byte nOmodel;
            public byte nItem;
            public byte nDoor;
            public byte nRoom_at;
            public byte Reverb_lv;
            public byte unknown7;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rdt2EmbeddedModel
        {
            public uint tim_offset;
            public uint md1_offset;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rdt2Camera
        {
            public ushort unknown0;
            public ushort const0;
            public uint camera_from_x;
            public uint camera_from_y;
            public uint camera_from_z;
            public uint camera_to_x;
            public uint camera_to_y;
            public uint camera_to_z;
            public int masks_offset;
        }
    }
}

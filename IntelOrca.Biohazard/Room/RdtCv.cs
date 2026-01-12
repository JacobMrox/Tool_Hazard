using System;
using System.IO;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.Extensions;

namespace IntelOrca.Biohazard.Room
{
    public partial class RdtCv : IRdt
    {
        // 0x000: header
        // 0x080: offsets
        // 0x100: counts
        //        text
        //        sysmes
        //        models
        //        motion
        //        script
        //        texture

        private readonly RdtFileData _data;

        public BioVersion Version => BioVersion.BiohazardCv;
        public ReadOnlyMemory<byte> Data => _data.Data;

        public RdtCv(string path)
            : this(File.ReadAllBytes(path))
        {
        }

        public RdtCv(ReadOnlyMemory<byte> data)
        {
            _data = new RdtFileData(data);

            var header = Header;

            var tableOffsets = ScriptOffsets;
            for (var i = 0; i < _tableChunkKinds.Length; i++)
            {
                var kind = _tableChunkKinds[i];
                if (kind == RdtFileChunkKinds.RDTCVUnknown2)
                    continue;

                var offset = tableOffsets[i];
                if (offset != 0)
                {
                    _data.RegisterOffset(kind, tableOffsets[i]);
                }
            }

            _data.RegisterOffset(RdtFileChunkKinds.RDTCVHeader, 0);
            _data.RegisterOffset(RdtFileChunkKinds.RDTCVTableOffsets, header.TableOffset);
            _data.RegisterOffset(RdtFileChunkKinds.RDTCVTableCounts, 0x100);
            _data.RegisterOffset(RdtFileChunkKinds.RDTCVUDAH, 0x180);
            _data.RegisterOffset(RdtFileChunkKinds.RDTCVModel, header.ModelOffset);
            _data.RegisterOffset(RdtFileChunkKinds.RDTCVMotion, header.MotionOffset);
            _data.RegisterOffset(RdtFileChunkKinds.RDTCVScript, header.ScriptOffset);
            _data.RegisterOffset(RdtFileChunkKinds.RDTCVTexture, header.TextureOffset);
        }

        public Builder ToBuilder()
        {
            var builder = new Builder();
            builder.Header = Header;
            builder.UnknownDataAfterHeader = UnknownDataAfterHeader.ToArray();
            builder.Cameras = Cameras;
            builder.LightingData = LightingData.ToArray();
            builder.Enemies.AddRange(Enemies.ToArray());
            builder.Objects.AddRange(Objects.ToArray());
            builder.Items.AddRange(Items.ToArray());
            builder.Effects.AddRange(Effects.ToArray());
            builder.Boundaries.AddRange(Boundaries.ToArray());
            builder.Aots.AddRange(Aots.ToArray());
            builder.Triggers.AddRange(Triggers.ToArray());
            builder.Players.AddRange(Players.ToArray());
            builder.Events.AddRange(Events.ToArray());
            builder.Unknown1Data = Unknown1Data.ToArray();
            builder.Unknown1Count = Unknown1Count;
            builder.Unknown2 = Unknown2;
            builder.Unknown2Count = Unknown2Count;
            builder.ReactionCount = ReactionCount;
            builder.Reactions.AddRange(Reactions.ToArray());
            builder.TextData = TextData;
            builder.SysmesData = SysmesData.ToArray();
            builder.Models = Models;
            builder.Motions = Motions;
            builder.Script = Script;
            builder.Textures = Textures;
            return builder;
        }

        public FileHeader Header => Data.GetSafeSpan<FileHeader>(0, 1)[0];
        public int ScriptOffset => _data.FindChunkByKind(RdtFileChunkKinds.RDTCVScript)?.Offset ?? throw new Exception();
        private ReadOnlySpan<int> ScriptOffsets => Data.GetSafeSpan<int>(Header.TableOffset, 16);
        private ReadOnlySpan<int> ScriptCounts => Data.GetSafeSpan<int>(256, 14);

        public ReadOnlyMemory<byte> UnknownDataAfterHeader => GetChunkMemory(RdtFileChunkKinds.RDTCVUDAH);
        public CvCameraList Cameras => new CvCameraList(ScriptCounts[0], GetChunkMemory(RdtFileChunkKinds.RDTCVCamera));
        public ReadOnlyMemory<byte> LightingData => GetChunkMemory(RdtFileChunkKinds.RDTCVLighting);
        public ReadOnlySpan<Enemy> Enemies => GetChunkTypedSpan<Enemy>(RdtFileChunkKinds.RDTCVEnemy);
        public ReadOnlySpan<RoomObject> Objects => GetChunkTypedSpan<RoomObject>(RdtFileChunkKinds.RDTCVObject);
        public ReadOnlySpan<Item> Items => GetChunkTypedSpan<Item>(RdtFileChunkKinds.RDTCVItem);
        public ReadOnlySpan<Effect> Effects => GetChunkTypedSpan<Effect>(RdtFileChunkKinds.RDTCVEffect);
        public ReadOnlySpan<Boundary> Boundaries => GetChunkTypedSpan<Boundary>(RdtFileChunkKinds.RDTCVBoundary);
        public ReadOnlySpan<Aot> Aots => GetChunkTypedSpan<Aot>(RdtFileChunkKinds.RDTCVAot);
        public ReadOnlySpan<AotTrigger> Triggers => GetChunkTypedSpan<AotTrigger>(RdtFileChunkKinds.RDTCVTrigger);
        public ReadOnlySpan<Player> Players => GetChunkTypedSpan<Player>(RdtFileChunkKinds.RDTCVPlayer);
        public ReadOnlySpan<AotEvent> Events => GetChunkTypedSpan<AotEvent>(RdtFileChunkKinds.RDTCVEvent);
        public ReadOnlyMemory<byte> Unknown1Data => GetChunkMemory(RdtFileChunkKinds.RDTCVUnknown1);
        public int Unknown1Count => ScriptCounts[11];
        public int Unknown2 => ScriptOffsets[12];
        public int Unknown2Count => ScriptCounts[12];
        public int ReactionCount => ScriptCounts[13];
        public ReadOnlySpan<Reaction> Reactions => GetChunkTypedSpan<Reaction>(RdtFileChunkKinds.RDTCVAction);
        public CvMsgList TextData => new CvMsgList(GetChunkMemory(RdtFileChunkKinds.RDTCVText));
        public ReadOnlyMemory<byte> SysmesData => GetChunkMemory(RdtFileChunkKinds.RDTCVSysmes);
        public CvMotionList Motions
        {
            get
            {
                var chunk = _data.FindChunkByKind(RdtFileChunkKinds.RDTCVMotion);
                if (chunk == null)
                    throw new NotImplementedException();

                return new CvMotionList(chunk.Value.Offset, chunk.Value.Memory);
            }
        }
        public CvModelList Models
        {
            get
            {
                var chunk = _data.FindChunkByKind(RdtFileChunkKinds.RDTCVModel);
                if (chunk == null)
                    throw new NotImplementedException();

                return new CvModelList(chunk.Value.Offset, chunk.Value.Memory);
            }
        }
        public ScdProcedureList Script => new ScdProcedureList(BioVersion.BiohazardCv, GetChunkMemory(RdtFileChunkKinds.RDTCVScript));
        public CvTextureList Textures
        {
            get
            {
                var chunk = _data.FindChunkByKind(RdtFileChunkKinds.RDTCVTexture);
                if (chunk == null)
                    throw new NotImplementedException();

                return new CvTextureList(chunk.Value.Offset, chunk.Value.Memory);
            }
        }
        private ReadOnlySpan<T> GetTable<T>(int index) where T : struct
        {
            var offset = ScriptOffsets[index];
            var count = ScriptCounts[index];
            return Data.GetSafeSpan<T>(offset, count);
        }

        private ReadOnlySpan<T> GetChunkTypedSpan<T>(int kind) where T : struct
        {
            var memory = GetChunkMemory(kind);
            return MemoryMarshal.Cast<byte, T>(memory.Span);
        }

        private ReadOnlyMemory<byte> GetChunkMemory(int kind)
        {
            return _data.FindChunkByKind(kind)?.Memory ?? ReadOnlyMemory<byte>.Empty;
        }

        IRdtBuilder IRdt.ToBuilder() => ToBuilder();

        private static readonly int[] _tableChunkKinds = new[]
        {
            RdtFileChunkKinds.RDTCVCamera,
            RdtFileChunkKinds.RDTCVLighting,
            RdtFileChunkKinds.RDTCVEnemy,
            RdtFileChunkKinds.RDTCVObject,
            RdtFileChunkKinds.RDTCVItem,
            RdtFileChunkKinds.RDTCVEffect,
            RdtFileChunkKinds.RDTCVBoundary,
            RdtFileChunkKinds.RDTCVAot,
            RdtFileChunkKinds.RDTCVTrigger,
            RdtFileChunkKinds.RDTCVPlayer,
            RdtFileChunkKinds.RDTCVEvent,
            RdtFileChunkKinds.RDTCVUnknown1,
            RdtFileChunkKinds.RDTCVUnknown2,
            RdtFileChunkKinds.RDTCVAction,
            RdtFileChunkKinds.RDTCVText,
            RdtFileChunkKinds.RDTCVSysmes,
        };


        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct FileHeader
        {
            public int Version;
            public int Unk04;
            public int Unk08;
            public int Unk0C;
            public int TableOffset;
            public int ModelOffset;
            public int MotionOffset;
            public int ScriptOffset;
            public int TextureOffset;
            public int Unk24;
            public int Unk28;
            public int Unk2C;
            public int Unk30;
            public int Unk34;
            public int Unk38;
            public int Unk3C;
            public int Unk40;
            public int Unk44;
            public int Unk48;
            public int Unk4C;
            public int Unk50;
            public int Unk54;
            public int Unk58;
            public int Unk5C;
            public fixed byte Author[32];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Enemy
        {
            public int Header;
            public short Type;
            public short Effect;
            public short Variant;
            public short Index;
            public VectorF Position;
            public Vector32 Rotation;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct RoomObject
        {
            public int Type;
            public int Flags1;
            public int Flags2;
            public VectorF Position;
            public Vector32 Rotation;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Item
        {
            public byte Unk00;
            public byte Unk01;
            public byte Unk02;
            public byte Unk03;
            public int Type;
            public int Unk08;
            public VectorF Position;
            public Vector32 Rotation;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Effect
        {
            public int Type;
            public int Flags1;
            public int Flags2;
            public VectorF Position;
            public VectorF Size;
            public int Settings0;
            public int Settings1;
            public int Settings2;
            public int Settings3;
            public int Settings4;
            public int Settings5;
            public int Settings6;
            public int Settings7;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Boundary
        {
            public int Type;
            public int Flags1;
            public VectorF Position;
            public VectorF Size;
            public int Flags2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Aot
        {
            public byte Unk00;
            public byte Kind;
            public byte Unk02;
            public byte Unk03;
            public int Flags;
            public VectorF Position;
            public VectorF Size;
            public byte Stage;          // or item index
            public byte Room;           // or message id
            public byte ExitId;
            public byte Transition;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct AotTrigger
        {
            public int Type;
            public int Attrs1;
            public VectorF Position;
            public VectorF Size;
            public int Attrs2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Player
        {
            public VectorF Position;
            public int RotationY;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct AotEvent
        {
            public int Type;
            public int Attrs1;
            public VectorF Position;
            public VectorF Size;
            public int Attrs2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct Reaction
        {
            public uint Unk00;
            public uint Unk01;
            public fixed byte Data[2048];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct VectorF
        {
            public float X;
            public float Y;
            public float Z;

            public VectorF(float x, float y, float z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct Vector32
        {
            public int X;
            public int Y;
            public int Z;

            public Vector32(int x, int y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }
    }
}

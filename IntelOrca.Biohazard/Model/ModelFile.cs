using System;
using System.Collections.Generic;
using System.IO;

namespace IntelOrca.Biohazard.Model
{
    public abstract class ModelFile
    {
        private readonly OffsetDirectory _directory;

        public BioVersion Version { get; }
        public int NumChunks => _directory.NumChunks;

        public ModelFile(BioVersion version, Stream stream)
        {
            Version = version;
            _directory = version == BioVersion.Biohazard1 ?
                new OffsetDirectoryV1(this is PlwFile ? 2 : 5) :
                (OffsetDirectory)new OffsetDirectoryV2();
            _directory.Read(stream);
        }

        public ModelFile(BioVersion version, string path)
        {
            Version = version;
            _directory = version == BioVersion.Biohazard1 ?
                new OffsetDirectoryV1(this is PlwFile ? 2 : 5) :
                (OffsetDirectory)new OffsetDirectoryV2();

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            _directory.Read(fs);
        }

        public void Save(string path)
        {
            using var fs = new FileStream(path, FileMode.Create);
            _directory.Write(fs);
        }

        public void Save(Stream stream)
        {
            _directory.Write(stream);
        }

        protected virtual ReadOnlySpan<ChunkKind> ChunkKinds => new ReadOnlySpan<ChunkKind>();

        public ChunkKind GetChunkKind(int index)
        {
            var kinds = ChunkKinds;
            if (index < 0 || index >= kinds.Length)
                return ChunkKind.Unknown;
            return kinds[index];
        }

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8603 // Possible null reference return.
        public T GetChunk<T>(ChunkKind kind, int number)
        {
            var chunkIndex = GetChunkIndex(kind, number);
            return GetChunk<T>(chunkIndex);
        }

        public T GetChunk<T>(int index)
        {
            var kind = GetChunkKind(index);
            switch (kind)
            {
                case ChunkKind.Animation:
                    if (Version == BioVersion.Biohazard3)
                        return (T)(object)new Edd2(GetChunkData(index));
                    else
                        return (T)(object)new Edd1(Version, GetChunkData(index));
                case ChunkKind.Armature:
                    return (T)(object)new Emr(Version, GetChunkData(index));
                case ChunkKind.Mesh:
                    if (Version == BioVersion.Biohazard1)
                        return (T)(object)new Tmd(GetChunkData(index));
                    else if (Version == BioVersion.Biohazard2)
                        return (T)(object)new Md1(GetChunkData(index));
                    else
                        return (T)(object)new Md2(GetChunkData(index));
                case ChunkKind.Texture:
                    return (T)(object)new TimFile(GetChunkData(index));
                case ChunkKind.Morph:
                    return (T)(object)new MorphData(Version, GetChunkData(index));
                default:
                    return (T)(object)GetChunkData(index);
            }
        }

        public void SetChunk<T>(int index, T value)
        {
            var kind = GetChunkKind(index);
            switch (kind)
            {
                case ChunkKind.Animation:
                    SetChunkData(index, ((IEdd)(object)value).Data);
                    break;
                case ChunkKind.Armature:
                    SetChunkData(index, ((Emr)(object)value).Data);
                    break;
                case ChunkKind.Mesh:
                    SetChunkData(index, ((IModelMesh)(object)value).Data);
                    break;
                case ChunkKind.Texture:
                    SetChunkData(index, ((TimFile)(object)value).GetBytes());
                    break;
                case ChunkKind.Morph:
                    SetChunkData(index, ((MorphData)(object)value).Data);
                    break;
                default:
#pragma warning disable CS8605 // Unboxing a possibly null value.
                    SetChunkData(index, (ReadOnlyMemory<byte>)(object)value);
#pragma warning restore CS8605 // Unboxing a possibly null value.
                    break;
            }
        }
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

        protected virtual ReadOnlyMemory<byte> GetChunkData(int index) => _directory.GetChunk(index);
        protected void SetChunkData(int index, ReadOnlySpan<byte> data) => _directory.SetChunk(index, data.ToArray());
        protected void SetChunkData(int index, ReadOnlyMemory<byte> data) => SetChunkData(index, data.Span);
        protected void SetChunkData(int index, byte[] data) => SetChunkData(index, new ReadOnlySpan<byte>(data));

        private int GetChunkIndex(ChunkKind kind, int number)
        {
            for (var i = 0; i < _directory.NumChunks; i++)
            {
                var chunkKind = GetChunkKind(i);
                if (chunkKind == kind)
                {
                    if (number == 0)
                        return i;
                    number--;
                }
            }
            throw new ArgumentException("Chunk does not exist");
        }

        public ReadOnlyMemory<byte> GetChunk(ChunkKind kind, int number)
        {
            var chunkIndex = GetChunkIndex(kind, number);
            return GetChunkData(chunkIndex);
        }

        public void SetChunk(ChunkKind kind, int number, ReadOnlyMemory<byte> data) => SetChunk(kind, number, data.Span);

        public void SetChunk(ChunkKind kind, int number, ReadOnlySpan<byte> data)
        {
            var chunkIndex = GetChunkIndex(kind, number);
            SetChunkData(chunkIndex, data);
        }

        public MorphData GetMorph(int number) => new MorphData(Version, GetChunk(ChunkKind.Morph, number));
        public void SetMorph(int number, MorphData value) => SetChunk(ChunkKind.Morph, number, value.Data);

        public IEdd GetEdd(int number) => GetChunk<IEdd>(ChunkKind.Animation, number);
        public void SetEdd(int number, IEdd value) => SetChunk(ChunkKind.Animation, number, value.Data);

        public Emr GetEmr(int number) => new Emr(Version, GetChunk(ChunkKind.Armature, number));
        public void SetEmr(int number, Emr value) => SetChunk(ChunkKind.Armature, number, value.Data);

        public IModelMesh GetMesh(int number) => GetChunk<IModelMesh>(ChunkKind.Mesh, number);
        public void SetMesh(int number, IModelMesh value) => SetChunk(ChunkKind.Mesh, number, value.Data);

        public TimFile GetTim(int number) => new TimFile(GetChunk(ChunkKind.Texture, number));
        public void SetTim(int number, TimFile value) => SetChunk(ChunkKind.Texture, number, new ReadOnlySpan<byte>(value.GetBytes()));

        public Md1 Md1
        {
            get
            {
                if (Version != BioVersion.Biohazard2)
                    throw new InvalidOperationException();
                return new Md1(GetChunk(ChunkKind.Mesh, 0));
            }
            set
            {
                if (Version != BioVersion.Biohazard2)
                    throw new InvalidOperationException();
                SetChunk(ChunkKind.Mesh, 0, value.Data);
            }
        }

        public Md2 Md2
        {
            get
            {
                if (Version != BioVersion.Biohazard3)
                    throw new InvalidOperationException();
                return new Md2(GetChunk(ChunkKind.Mesh, 0));
            }
            set
            {
                if (Version != BioVersion.Biohazard3)
                    throw new InvalidOperationException();
                SetChunk(ChunkKind.Mesh, 0, value.Data);
            }
        }

        public static ModelFile FromFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var br = new BinaryReader(fs);
                var fileLength = fs.Length;
                var directoryOffset = br.ReadInt32();
                var numOffsets = br.ReadInt32();
                fs.Close();

                // RE 1 won't have a directory offset header
                if (directoryOffset + numOffsets * 4 != fileLength)
                {
                    if (path.EndsWith(".emw", StringComparison.OrdinalIgnoreCase))
                        return new PlwFile(BioVersion.Biohazard1, path);
                    return new EmdFile(BioVersion.Biohazard1, path);
                }

                switch (numOffsets)
                {
                    case 4:
                        if (path.EndsWith(".plw", StringComparison.OrdinalIgnoreCase))
                            return new PlwFile(BioVersion.Biohazard2, path);
                        return new PldFile(BioVersion.Biohazard2, path);
                    case 5:
                        return new PldFile(BioVersion.Biohazard3, path);
                    case 8:
                        return new EmdFile(BioVersion.Biohazard2, path);
                    case 9:
                        return new PlwFile(BioVersion.Biohazard3, path);
                    case 15:
                        return new EmdFile(BioVersion.Biohazard3, path);
                    default:
                        throw new InvalidDataException("Unsupported file type");
                }
            }
        }

        public enum ChunkKind
        {
            Unknown,
            Animation,
            Armature,
            Mesh,
            Texture,
            Morph,
        }

        private class OffsetDirectory
        {
            private readonly List<byte[]> _chunks = new List<byte[]>();

            public virtual void Read(Stream stream)
            {
            }

            public virtual void Write(Stream stream)
            {
            }

            protected void ReadDirectory(Stream stream, int directoryOffset, int numOffsets)
            {
                var br = new BinaryReader(stream);

                // Read directory
                stream.Position = directoryOffset;
                var offsets = new int[numOffsets + 1];
                for (int i = 0; i < numOffsets; i++)
                {
                    offsets[i] = br.ReadInt32();
                }
                offsets[numOffsets] = directoryOffset;

                // Check all offsets are in order
                var lastOffset = 0;
                foreach (var offset in offsets)
                {
                    if (offset < lastOffset)
                        throw new NotSupportedException("Offsets not in order");
                    lastOffset = offset;
                }

                // Read chunks
                for (int i = 0; i < numOffsets; i++)
                {
                    var len = offsets[i + 1] - offsets[i];
                    stream.Position = offsets[i];
                    AddChunk(br.ReadBytes(len));
                }
            }

            protected void AddChunk(byte[] data)
            {
                _chunks.Add(data);
            }

            public int NumChunks => _chunks.Count;
            public byte[] GetChunk(int index) => _chunks[index];
            public void SetChunk(int index, byte[] value) => _chunks[index] = value;
        }

        private class OffsetDirectoryV1 : OffsetDirectory
        {
            public int NumOffsets { get; }

            public OffsetDirectoryV1(int numOffsets)
            {
                NumOffsets = numOffsets;
            }

            public override void Read(Stream stream)
            {
                // Detect how many offsets there are
                var br = new BinaryReader(stream);
                var directoryPosition = (int)(stream.Length - (NumOffsets * 4));
                stream.Position = directoryPosition;

                var offsets = new List<int>();
                for (var i = 0; i < NumOffsets; i++)
                {
                    var offset = br.ReadInt32();
                    offsets.Add(offset);
                }
                offsets.Add(directoryPosition);

                if (offsets[0] != 0)
                {
                    offsets.Insert(0, 0);
                }

                // Read chunks
                for (int i = 0; i < offsets.Count - 1; i++)
                {
                    var len = offsets[i + 1] - offsets[i];
                    if (len != 0)
                    {
                        stream.Position = offsets[i];
                        AddChunk(br.ReadBytes(len));
                    }
                }
            }

            public override void Write(Stream stream)
            {
                var bw = new BinaryWriter(stream);

                // Chunks
                var offsets = new List<int>();
                for (var i = 0; i < NumChunks; i++)
                {
                    offsets.Add((int)stream.Position);
                    var chunk = GetChunk(i);
                    bw.Write(chunk);
                }

                // Directory
                for (var i = 0; i < NumOffsets; i++)
                {
                    var offsetIndex = offsets.Count - NumOffsets + i;
                    if (offsetIndex < 0)
                        bw.Write(0);
                    else
                        bw.Write(offsets[offsetIndex]);
                }
            }
        }

        private class OffsetDirectoryV2 : OffsetDirectory
        {
            public override void Read(Stream stream)
            {
                // Read header to find directory
                var br = new BinaryReader(stream);
                var directoryOffset = br.ReadInt32();
                var numOffsets = br.ReadInt32();

                ReadDirectory(stream, directoryOffset, numOffsets);
            }

            public override void Write(Stream stream)
            {
                var bw = new BinaryWriter(stream);

                // Header placeholder
                bw.Write(0);
                bw.Write(0);

                // Chunks
                var offsets = new List<int>();
                for (var i = 0; i < NumChunks; i++)
                {
                    offsets.Add((int)stream.Position);
                    var chunk = GetChunk(i);
                    bw.Write(chunk);
                }

                // Directory
                var directoryOffset = (int)stream.Position;
                foreach (int offset in offsets)
                {
                    bw.Write(offset);
                }

                // Header
                stream.Position = 0;
                bw.Write(directoryOffset);
                bw.Write(offsets.Count);
            }
        }
    }
}

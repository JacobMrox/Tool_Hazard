using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace IntelOrca.Biohazard.Room
{
    /// <summary>
    /// Represents the data in the RDT file which is a concatenated sequence
    /// of files. Not all file lengths are written into the RDT file and have
    /// to be derived. Offsets to various files can be anywhere within the file.
    /// </summary>
    internal class RdtFileData
    {
        private readonly List<Chunk> _chunks = new List<Chunk>();
        private ReadOnlyMemory<byte> _data;

        public IReadOnlyList<Chunk> Chunks => _chunks.ToArray();
        public ReadOnlyMemory<byte> Data => _data;
        public int Length => _data.Length;

        public RdtFileData(string path)
            : this(File.ReadAllBytes(path))
        {
        }

        public RdtFileData(ReadOnlyMemory<byte> data)
        {
            _data = data;
        }

        public Chunk RegisterChunk(int kind, int offset, int length)
        {
            RegisterOffset(kind, offset);
            RegisterLength(offset, length);
            return FindChunkByOffset(offset)!.Value;
        }

        public Chunk RegisterOffset(int kind, int offset, bool overwrite = false)
        {
            // Insert new chunk
            var position = FindInsertIndex(offset);
            if (position < _chunks.Count && _chunks[position].Offset == offset)
            {
                if (overwrite)
                {
                    _chunks[position] = _chunks[position].WithKind(kind);
                    return _chunks[position];
                }
                else
                {
                    var length = _chunks[position].Length;
                    _chunks[position] = _chunks[position].WithLength(0);
                    _chunks.Insert(position + 1, new Chunk(this, kind, offset, length));
                    return _chunks[position + 1];
                }
            }
            else
            {
                var length = position == _chunks.Count ?
                    _data.Length - offset :
                    _chunks[position].Offset - offset;
                _chunks.Insert(position, new Chunk(this, kind, offset, length));

                // Shrink previous chunk
                if (position > 0)
                {
                    var previousChunk = _chunks[position - 1];
                    var newLength = offset - previousChunk.Offset;
                    _chunks[position - 1] = previousChunk.WithLength(newLength);
                }
                return _chunks[position];
            }
        }

        public void RegisterLength(int offset, int length)
        {
            var position = _chunks.FindIndex(x => x.Offset == offset);
            if (position == -1)
                throw new ArgumentException("Offset was not found");

            var chunk = _chunks[position];
            if (length > chunk.Length)
            {
                throw new ArgumentException("Length was larger than the chunk.");
            }

            if (length == chunk.Length)
                return;

            _chunks.Insert(position + 1, new Chunk(this, RdtFileChunkKinds.Unknown, offset + length, chunk.Length - length));
            _chunks[position] = chunk.WithLength(length);
        }

        public Chunk? FindChunkByKind(int kind)
        {
            var index = _chunks.FindIndex(x => x.Kind == kind);
            return index == -1 ? (Chunk?)null : _chunks[index];
        }

        public Chunk? FindChunkByOffset(int offset)
        {
            var index = _chunks.FindIndex(x => x.Offset == offset);
            return index == -1 ? (Chunk?)null : _chunks[index];
        }

        public Chunk? FindChunkByOffset(int kind, int offset)
        {
            var index = _chunks.FindIndex(x => x.Kind == kind && x.Offset == offset);
            return index == -1 ? (Chunk?)null : _chunks[index];
        }

        public int Count => _chunks.Count;

        public Chunk this[int index] => _chunks[index];

        public void InsertData(int kind, int offset, ReadOnlySpan<byte> data)
        {
            var position = FindInsertIndex(offset);
            _chunks.Insert(position, new Chunk(this, kind, offset, 0));
            SetData(kind, offset, data);
        }

        public void SetData(int kind, int offset, ReadOnlySpan<byte> data)
        {
            var chunk = FindChunkByOffset(kind, offset)!.Value;
            var delta = data.Length - chunk.Length;
            var left = _data.Slice(0, chunk.Offset);
            var right = _data.Slice(chunk.End);
            var newData = new byte[left.Length + data.Length + right.Length];
            left.CopyTo(new Memory<byte>(newData, 0, left.Length));
            data.CopyTo(new Span<byte>(newData, left.Length, data.Length));
            right.CopyTo(new Memory<byte>(newData, left.Length + data.Length, right.Length));
            _data = newData;

            // Update offsets for all following chunks
            if (delta != 0)
            {
                var index = _chunks.FindIndex(x => x.Offset == offset);
                _chunks[index] = chunk.WithLength(data.Length);
                for (var i = index + 1; i < _chunks.Count; i++)
                {
                    _chunks[i] = _chunks[i].AddOffset(delta);
                }
            }
        }

        private int FindInsertIndex(int offset)
        {
            for (var i = 0; i < _chunks.Count; i++)
            {
                var chunk = _chunks[i];
                if (chunk.Offset >= offset)
                {
                    return i;
                }
            }
            return _chunks.Count;
        }

        public void Remove(Chunk chunk)
        {
            var index = _chunks.IndexOf(chunk);
            if (index != -1)
            {
                SetData(chunk.Kind, chunk.Offset, new byte[0]);
                _chunks.RemoveAt(index);
            }
        }

        [DebuggerDisplay("Kind = {Kind} Offset = {Offset} Length = {Length}")]
        public readonly struct Chunk
        {
            public RdtFileData Parent { get; }
            public int Kind { get; }
            public int Offset { get; }
            public int Length { get; }
            public int End => Offset + Length;

            public Chunk(RdtFileData parent, int kind, int offset, int length)
            {
                Parent = parent;
                Kind = kind;
                Offset = offset;
                Length = length;
            }

            public ReadOnlyMemory<byte> Memory => Parent._data.Slice(Offset, Length);
            public ReadOnlySpan<byte> Span => Memory.Span;
            public Stream Stream => new SpanStream(Memory);

            public Chunk WithKind(int value) => new Chunk(Parent, value, Offset, Length);
            public Chunk AddOffset(int value) => new Chunk(Parent, Kind, Offset + value, Length);
            public Chunk WithLength(int value) => new Chunk(Parent, Kind, Offset, value);
        }
    }
}

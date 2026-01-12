using System;

namespace IntelOrca.Biohazard
{
    public sealed class AfsFile
    {
        private AFSLib.AfsArchive _afsArchive;

        public ReadOnlyMemory<byte> Data { get; }

        public AfsFile(ReadOnlyMemory<byte> data)
        {
            Data = data;
            if (AFSLib.AfsArchive.TryFromFile(data.ToArray(), out var archive))
            {
                _afsArchive = archive;
            }
            else
            {
                throw new ArgumentException("Invalid AFS data", nameof(data));
            }
        }

        public int Count => _afsArchive.Files.Count;

        public ReadOnlyMemory<byte> GetFileData(int index)
        {
            return _afsArchive.Files[index].Data;
        }

        public ReadOnlyMemory<byte> GetFileData(string path)
        {
            foreach (var file in _afsArchive.Files)
            {
                if (file.Name == path)
                {
                    return file.Data;
                }
            }
            throw new ArgumentException("File not found", nameof(path));
        }

        public Builder ToBuilder()
        {
            return new Builder(Data);
        }

        public class Builder
        {
            private AFSLib.AfsArchive _afsArchive;

            public Builder(ReadOnlyMemory<byte> data)
            {
                if (AFSLib.AfsArchive.TryFromFile(data.ToArray(), out var archive))
                {
                    _afsArchive = archive;
                }
                else
                {
                    throw new ArgumentException("Invalid AFS data", nameof(data));
                }
            }

            public void Replace(int index, ReadOnlyMemory<byte> data) => Replace(index, data.ToArray());

            public void Replace(int index, byte[] data)
            {
                _afsArchive.Files[index].Data = data;
            }

            public AfsFile ToAfsFile()
            {
                return new AfsFile(_afsArchive.ToBytes());
            }
        }
    }
}

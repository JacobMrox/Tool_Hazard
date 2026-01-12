using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.Room
{
    public readonly struct ScdProcedureList
    {
        public BioVersion Version { get; }
        public ReadOnlyMemory<byte> Data { get; }

        public ScdProcedureList(BioVersion version, ReadOnlyMemory<byte> data)
        {
            Version = version;
            Data = data;
        }

        public int Count
        {
            get
            {
                if (Version == BioVersion.BiohazardCv)
                {
                    if (Data.Length < 4)
                        return 0;
                    return GetOffset(0) / 4;
                }
                else
                {
                    if (Data.Length < 2)
                        return 0;
                    return GetOffset(0) / 2;
                }
            }
        }

        public ScdProcedure this[int index]
        {
            get
            {
                var count = Count;
                var offset = GetOffset(index);
                var nextOffset = index == count - 1 ?
                    Data.Length :
                    GetOffset(index + 1);
                return new ScdProcedure(Version, Data[offset..nextOffset]);
            }
        }

        public int GetOffset(int index)
        {
            if (Version == BioVersion.BiohazardCv)
            {
                var offsets = MemoryMarshal.Cast<byte, int>(Data.Span);
                return offsets[index];
            }
            else
            {
                var offsets = MemoryMarshal.Cast<byte, ushort>(Data.Span);
                return offsets[index];
            }
        }

        public Builder ToBuilder()
        {
            var builder = new Builder(Version);
            for (var i = 0; i < Count; i++)
            {
                builder.Procedures.Add(this[i]);
            }
            return builder;
        }

        public class Builder
        {
            public BioVersion Version { get; }
            public List<ScdProcedure> Procedures { get; } = new List<ScdProcedure>();

            public Builder(BioVersion version)
            {
                Version = version;
            }

            public ScdProcedureList ToProcedureList()
            {
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                if (Version == BioVersion.BiohazardCv)
                {
                    var baseOffset = Procedures.Count * 4;
                    foreach (var procedure in Procedures)
                    {
                        bw.Write(baseOffset);
                        baseOffset += procedure.Data.Length;
                    }
                }
                else
                {
                    var baseOffset = Procedures.Count * 2;
                    foreach (var procedure in Procedures)
                    {
                        bw.Write((ushort)baseOffset);
                        baseOffset += procedure.Data.Length;
                    }
                }
                foreach (var procedure in Procedures)
                {
                    bw.Write(procedure.Data);
                }

                var bytes = ms.ToArray();
                return new ScdProcedureList(Version, bytes);
            }
        }
    }
}

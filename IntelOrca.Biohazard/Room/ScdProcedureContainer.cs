using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.Room
{
    public readonly struct ScdProcedureContainer
    {
        public BioVersion Version => BioVersion.Biohazard1;
        public ReadOnlyMemory<byte> Data { get; }

        public ScdProcedureContainer(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }

        public int Count
        {
            get
            {
                var count = 0;
                var address = 0;
                int offset;
                while ((offset = GetOffset(address)) != 0)
                {
                    address += offset;
                    count++;
                }
                return count;
            }
        }

        public ScdProcedure this[int index]
        {
            get
            {
                var address = 0;
                int offset;
                while ((offset = GetOffset(address)) != 0)
                {
                    if (index == 0)
                    {
                        return new ScdProcedure(Version, Data.Slice(address + 2, offset - 2));
                    }
                    index--;
                    address += offset;
                }
                throw new ArgumentOutOfRangeException();
            }
        }

        private ushort GetOffset(int address) => MemoryMarshal.Cast<byte, ushort>(Data.Span.Slice(address, 2))[0];

        public Builder ToBuilder()
        {
            var builder = new Builder();
            var count = Count;
            for (var i = 0; i < count; i++)
            {
                builder.Procedures.Add(this[i]);
            }
            return builder;
        }

        public class Builder
        {
            public List<ScdProcedure> Procedures { get; } = new List<ScdProcedure>();

            public ScdProcedureContainer ToContainer()
            {
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                foreach (var proc in Procedures)
                {
                    bw.Write((ushort)(proc.Data.Length + 2));
                    bw.Write(proc.Data);
                }
                bw.Write((ushort)0);

                // Align to next 4-byte boundary
                while (ms.Length % 4 != 0)
                {
                    bw.Write((byte)0);
                }
                return new ScdProcedureContainer(ms.ToArray());
            }
        }
    }
}

using System;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.Room
{
    public readonly struct CvMsgList
    {
        public ReadOnlyMemory<byte> Data { get; }

        public CvMsgList(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }

        public int Count
        {
            get
            {
                if (Data.Length < 4)
                    return 0;
                return MemoryMarshal.Cast<byte, int>(Data.Span)[0];
            }
        }

        public int GetOffset(int index)
        {
            var offsets = MemoryMarshal.Cast<byte, int>(Data.Span);
            return offsets[index + 1];
        }

        public CvMsg GetFirstMsg()
        {
            var firstOffset = Count * 4;
            return new CvMsg(Data.Slice(firstOffset));
        }

        public CvMsg this[int index]
        {
            get
            {
                var count = Count;
                var offset = GetOffset(index);
                var nextOffset = index == count - 1 ?
                    Data.Length :
                    GetOffset(index + 1);
                return new CvMsg(Data[offset..nextOffset]);
            }
        }
    }

    public readonly struct CvMsg
    {
        public ReadOnlyMemory<byte> Data { get; }

        public CvMsg(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }
    }
}

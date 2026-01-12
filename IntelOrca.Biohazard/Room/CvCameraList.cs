using System;

namespace IntelOrca.Biohazard.Room
{
    public readonly struct CvCameraList
    {
        public int Count { get; }
        public ReadOnlyMemory<byte> Data { get; }

        public CvCameraList(int count, ReadOnlyMemory<byte> data)
        {
            Count = count;
            Data = data;
        }
    }
}

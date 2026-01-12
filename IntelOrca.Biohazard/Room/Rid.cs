using System;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.Room
{
    public unsafe readonly struct Rid
    {
        public ReadOnlyMemory<byte> Data { get; }

        public Rid(ReadOnlyMemory<byte> data)
        {
            Data = data;
        }

        public int Count => Data.Length / sizeof(Camera);

        public ReadOnlySpan<Camera> Cameras => MemoryMarshal.Cast<byte, Camera>(Data.Span);

        public Rid WithPriOffset(int newOffset)
        {
            var data = Data.ToArray();
            var cameras = MemoryMarshal.Cast<byte, Camera>(data);
            if (cameras.Length > 0)
            {
                var baseOffset = uint.MaxValue;
                for (var i = 0; i < cameras.Length; i++)
                {
                    baseOffset = Math.Min(baseOffset, cameras[i].masks_offset);
                }
                for (var i = 0; i < cameras.Length; i++)
                {
                    cameras[i].masks_offset = (uint)(cameras[i].masks_offset - baseOffset + newOffset);
                }
            }
            return new Rid(data);
        }

        public struct Camera
        {
            public ushort unknown0;
            public ushort const0;
            public int camera_from_x;
            public int camera_from_y;
            public int camera_from_z;
            public int camera_to_x;
            public int camera_to_y;
            public int camera_to_z;
            public uint masks_offset;
        }
    }
}

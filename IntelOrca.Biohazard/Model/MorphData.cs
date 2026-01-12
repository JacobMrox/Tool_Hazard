using System;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.Extensions;

namespace IntelOrca.Biohazard.Model
{
    public sealed partial class MorphData
    {
        public BioVersion Version { get; }
        public ReadOnlyMemory<byte> Data { get; }

        public MorphData(BioVersion version, ReadOnlyMemory<byte> data)
        {
            Version = version;
            Data = data;
        }

        public int Unknown00 => GetSpan<int>(0, 1)[0];
        public int FirstPositionDataOffset => GetSpan<int>(4, 1)[0];
        public int MorphLength => GetSpan<int>(8, 1)[0];
        public int NumParts => (FirstPositionDataOffset - 12) / 16;

        public ReadOnlySpan<Emr.Vector> GetPartPositionData(int index) => GetSpan<Emr.Vector>(FirstPositionDataOffset + (index * MorphLength), MorphLength / 6);
        public MorphPart GetMorphHeader(int index) => GetSpan<MorphPart>(4 + (4 * NumParts), NumParts)[index];
        public ReadOnlySpan<Emr.Vector> GetMorphData(int index, int num)
        {
            var header = GetMorphHeader(index);
            return GetSpan<Emr.Vector>(header.Offset + (num * header.ElementSize), header.ElementSize / 6);
        }

        private ReadOnlySpan<T> GetSpan<T>(int offset, int count) where T : struct => Data.GetSafeSpan<T>(offset, count);

        public Builder ToBuilder()
        {
            var builder = new Builder(Version);
            builder.Unknown00 = Unknown00;
            for (var i = 0; i < NumParts; i++)
            {
                var skeleton = GetPartPositionData(i);
                builder.Skeletons.Add(skeleton.ToArray());
            }
            for (var i = 0; i < NumParts; i++)
            {
                var group = new Builder.MorphGroup();
                var header = GetMorphHeader(i);
                for (var j = 0; j < header.Count + 1; j++)
                {
                    group.Positions.Add(GetMorphData(i, j).ToArray());
                }
                group.Unknown = header.Unknown;
                builder.Groups.Add(group);
            }
            return builder;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct MorphPart
        {
            public int Offset;
            public int ElementSize;
            public int Count;
            public int Unknown;
        }
    }
}

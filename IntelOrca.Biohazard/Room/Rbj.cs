using System;
using System.Collections.Generic;
using System.IO;
using IntelOrca.Biohazard.Extensions;
using IntelOrca.Biohazard.Model;

namespace IntelOrca.Biohazard.Room
{
    public readonly struct Rbj
    {
        public BioVersion Version { get; }
        public ReadOnlyMemory<byte> Data { get; }

        public Rbj(BioVersion version, ReadOnlyMemory<byte> data)
        {
            Version = version;
            Data = data;
        }

        public int OffsetTableOffset => Data.GetSafeSpan<int>(0, 1)[0];
        public int Count => Data.GetSafeSpan<int>(4, 1)[0];
        public ReadOnlySpan<int> Offsets => Data.GetSafeSpan<int>(OffsetTableOffset, Count * 2);

        public RbjAnimation this[int index]
        {
            get
            {
                var emrCount = Count;
                if (index < 0 || index >= emrCount)
                    throw new ArgumentOutOfRangeException(nameof(index));

                var flagsOffset = Offsets[(index * 2) + 0];
                var emrOffset = flagsOffset + 4;
                var eddOffset = Offsets[(index * 2) + 1];
                var endOffset = index == emrCount - 1 ?
                    OffsetTableOffset :
                    Offsets[(index * 2) + 2];
                var flags = (EmrFlags)Data.GetSafeSpan<uint>(flagsOffset, 1)[0];
                var emr = Data.Slice(emrOffset, eddOffset - emrOffset);
                var edd = Data.Slice(eddOffset, endOffset - eddOffset);
                return new RbjAnimation(flags, new Edd1(Version, edd), new Emr(Version, emr));
            }
        }

        public Builder ToBuilder()
        {
            var builder = new Builder();
            for (var i = 0; i < Count; i++)
            {
                builder.Animations.Add(this[i]);
            }
            return builder;
        }

        public class Builder
        {
            public List<RbjAnimation> Animations { get; } = new List<RbjAnimation>();

            public Rbj ToRbj()
            {
                if (Animations.Count == 0)
                    return new Rbj();

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                bw.Write(0);
                bw.Write(Animations.Count);

                var offsets = new List<int>();
                foreach (var animation in Animations)
                {
                    offsets.Add((int)ms.Position);
                    bw.Write((uint)animation.Flags);
                    bw.Write(animation.Emr.Data);
                    offsets.Add((int)ms.Position);
                    bw.Write(animation.Edd.Data);
                }

                var offsetTableOffset = (int)ms.Position;
                foreach (var offset in offsets)
                {
                    bw.Write(offset);
                }

                ms.Position = 0;
                bw.Write(offsetTableOffset);

                var bytes = ms.ToArray();
                return new Rbj(Animations[0].Version, bytes);
            }
        }
    }
}

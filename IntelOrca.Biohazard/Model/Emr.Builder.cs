using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IntelOrca.Biohazard.Model
{
    public sealed partial class Emr
    {
        public class Builder
        {
            public BioVersion Version { get; }
            public ushort NumParts { get; set; }
            public List<Vector> RelativePositions { get; } = new List<Vector>();
            public List<byte[]> Armatures { get; } = new List<byte[]>();
            public List<Keyframe> KeyFrames { get; } = new List<Keyframe>();
            public ushort KeyFrameSize { get; set; }
            public ushort? ForceArmatureOffset { get; set; }

            public Builder(BioVersion version)
            {
                Version = version;
            }

            public Emr ToEmr()
            {
                if (RelativePositions.Count != Armatures.Count)
                    throw new InvalidOperationException("Number of relative positions does not match number of armatures.");

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);

                // Header
                bw.Write((ushort)0);
                bw.Write((ushort)0);
                bw.Write(NumParts);
                bw.Write(KeyFrameSize);

                // Positions
                foreach (var pos in RelativePositions)
                {
                    bw.Write(pos.x);
                    bw.Write(pos.y);
                    bw.Write(pos.z);
                }

                if (ForceArmatureOffset != null && RelativePositions.Count != 0)
                    ms.Position = ForceArmatureOffset.Value;

                // Armatures
                var armatureStartOffset = ForceArmatureOffset ?? ms.Position;
                var offset = Armatures.Count * 4;
                foreach (var armature in Armatures)
                {
                    bw.Write((ushort)armature.Length);
                    bw.Write((ushort)offset);
                    offset += armature.Length;
                }
                foreach (var armature in Armatures)
                {
                    bw.Write(armature);
                }

                // Align 4
                ms.Position = ((ms.Position + 3) / 4) * 4;

                // Key frames
                var keyFrameStartOffset = ms.Position;
                foreach (var kf in KeyFrames)
                {
                    var kfOffset = ms.Position;
                    if (Version == BioVersion.Biohazard3)
                    {
                        bw.Write(kf.Speed);
                        bw.Write(kf.Offset.y);
                    }
                    else
                    {
                        bw.Write(kf.Offset);
                        bw.Write(kf.Speed);
                    }

                    if (Version == BioVersion.Biohazard1)
                    {
                        foreach (var a in kf.Angles)
                        {
                            bw.Write(a);
                        }
                    }
                    else
                    {
                        using var nw = new NibbleWriter(ms);
                        foreach (var a in kf.Angles)
                        {
                            nw.WriteInt12(a.x);
                            nw.WriteInt12(a.y);
                            nw.WriteInt12(a.z);
                        }
                    }
                    var remaining = kfOffset + KeyFrameSize - ms.Position;
                    if (remaining < 0)
                        throw new Exception("Key frame too large");
                    while (remaining > 0)
                    {
                        bw.Write((byte)0);
                        remaining--;
                    }
                }

                Debug.Assert(ms.Position == keyFrameStartOffset + (KeyFrames.Count * KeyFrameSize));

                ms.Position = 0;
                bw.Write((ushort)armatureStartOffset);
                ms.Position = 2;
                bw.Write((ushort)keyFrameStartOffset);

                if (Version == BioVersion.Biohazard1)
                {
                    ms.Position = ms.Length;
                    bw.Write((int)(ms.Position - 8));
                }

                return new Emr(Version, ms.ToArray());
            }

            public class Keyframe : IEquatable<Keyframe>
            {
                public Vector Offset { get; set; }
                public Vector Speed { get; set; }
                public Vector[] Angles { get; set; } = new Vector[0];

                public override bool Equals(object? obj) => obj is Keyframe keyframe && Equals(keyframe);

                public bool Equals(Keyframe other)
                {
                    return Offset.Equals(other.Offset) &&
                           Speed.Equals(other.Speed) &&
                           Angles.SequenceEqual(other.Angles);
                }

                public override int GetHashCode()
                {
                    int hashCode = 1148422459;
                    hashCode = hashCode * -1521134295 + Offset.GetHashCode();
                    hashCode = hashCode * -1521134295 + Speed.GetHashCode();
                    hashCode = hashCode * -1521134295 + EqualityComparer<Vector[]>.Default.GetHashCode(Angles);
                    return hashCode;
                }

                public static bool operator ==(Keyframe? left, Keyframe? right) =>
                    ReferenceEquals(left, right) ||
                    (!ReferenceEquals(left, null) && !ReferenceEquals(right, null) && left.Equals(right));
                public static bool operator !=(Keyframe? left, Keyframe? right) => !(left == right);
            }
        }
    }

    internal class NibbleWriter : IDisposable
    {
        private readonly BinaryWriter _bw;
        private byte _value;
        private bool _isHalfWritten;

        public NibbleWriter(Stream stream)
        {
            _bw = new BinaryWriter(stream);
        }

        public void Dispose()
        {
            if (_isHalfWritten)
            {
                WriteNibble(0);
            }
        }

        public void WriteInt12(short value)
        {
            WriteNibble((byte)((value >> 0) & 0x0F));
            WriteNibble((byte)((value >> 4) & 0x0F));
            WriteNibble((byte)((value >> 8) & 0x0F));
        }

        public void WriteNibble(byte value)
        {
            if (!_isHalfWritten)
            {
                _value = value;
                _isHalfWritten = true;
            }
            else
            {
                _value = (byte)((value << 4) | _value);
                _bw.Write(_value);
                _value = 0;
                _isHalfWritten = false;
            }
        }
    }
}

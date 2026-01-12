using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.Extensions;

namespace IntelOrca.Biohazard.Model
{
    public sealed partial class Emr
    {
        public BioVersion Version { get; }
        public ReadOnlyMemory<byte> Data { get; }

        public Emr(BioVersion version, ReadOnlyMemory<byte> data)
        {
            Version = version;
            Data = data;
        }

        public ushort ArmatureOffset => GetSpan<ushort>(0, 1)[0];
        public ushort KeyFrameOffset => GetSpan<ushort>(2, 1)[0];
        public ushort NumParts => Data.Length >= 6 ? GetSpan<ushort>(4, 1)[0] : (ushort)0;
        public ushort KeyFrameSize => GetSpan<ushort>(6, 1)[0];
        public ushort NumArmatures
        {
            get
            {
                var armatureDataLen = KeyFrameOffset - 8;
                if (armatureDataLen == 0)
                    return 0;

                return NumParts;
            }
        }

        public Vector GetRelativePosition(int partIndex)
        {
            if (partIndex < 0 || partIndex >= NumParts)
                throw new ArgumentOutOfRangeException(nameof(partIndex));

            var offset = 8 + partIndex * 6;
            var values = GetSpan<short>(offset, 3);
            return new Vector()
            {
                x = values[0],
                y = values[1],
                z = values[2]
            };
        }

        public Vector GetFinalPosition(int targetPartIndex)
        {
            if (targetPartIndex < 0 || targetPartIndex >= NumParts)
                return new Vector();

            var positions = new Vector[NumParts];

            var stack = new Stack<byte>();
            stack.Push(0);

            while (stack.Count != 0)
            {
                var partIndex = stack.Pop();
                var pos = positions[partIndex];
                var rel = GetRelativePosition(partIndex);
                pos.x += rel.x;
                pos.y += rel.y;
                pos.z += rel.z;
                positions[partIndex] = pos;
                var children = GetArmatureParts(partIndex);
                foreach (var child in children)
                {
                    positions[child] = pos;
                    stack.Push(child);
                }
            }

            return positions[targetPartIndex];
        }

        public Armature GetArmature(int partIndex)
        {
            if (partIndex < 0 || partIndex >= NumParts)
                throw new ArgumentOutOfRangeException(nameof(partIndex));

            var offset = ArmatureOffset + partIndex * 4;
            var values = GetSpan<short>(offset, 2);
            return new Armature()
            {
                count = values[0],
                offset = values[1]
            };
        }

        public ReadOnlySpan<byte> GetArmatureParts(int partIndex)
        {
            var armature = GetArmature(partIndex);
            var offset = ArmatureOffset + armature.offset;
            return GetSpan<byte>(offset, armature.count);
        }

        public ReadOnlySpan<byte> KeyFrameData
        {
            get
            {
                var offset = KeyFrameOffset;
                var count = Data.Length - offset;
                return GetSpan<byte>(offset, count);
            }
        }

        public KeyFrame[] KeyFrames
        {
            get
            {
                if (KeyFrameSize == 0)
                    return new KeyFrame[0];

                var offset = KeyFrameOffset;
                var count = (Data.Length - offset) / KeyFrameSize;
                var result = new KeyFrame[count];
                for (var i = 0; i < count; i++)
                {
                    result[i] = new KeyFrame(this, offset + i * KeyFrameSize, KeyFrameSize);
                }
                return result;
            }
        }

        private ReadOnlySpan<T> GetSpan<T>(int offset, int count) where T : struct => Data.GetSafeSpan<T>(offset, count);

        public Builder ToBuilder()
        {
            var builder = new Builder(Version);
            builder.ForceArmatureOffset = ArmatureOffset;
            builder.NumParts = NumParts;
            var numArmatures = NumArmatures;
            for (var i = 0; i < numArmatures; i++)
            {
                builder.RelativePositions.Add(GetRelativePosition(i));
            }
            for (var i = 0; i < numArmatures; i++)
            {
                builder.Armatures.Add(GetArmatureParts(i).ToArray());
            }
            foreach (var kf in KeyFrames)
            {
                builder.KeyFrames.Add(new Builder.Keyframe()
                {
                    Offset = kf.Offset,
                    Speed = kf.Speed,
                    Angles = kf.Angles.ToArray()
                });
            }
            builder.KeyFrameSize = KeyFrameSize;
            return builder;
        }

        public Emr WithSkeleton(Emr emr)
        {
            var builder = ToBuilder();
            for (var i = 0; i < builder.RelativePositions.Count; i++)
            {
                if (emr.NumParts > i)
                {
                    builder.RelativePositions[i] = emr.GetRelativePosition(i);
                }
            }
            return builder.ToEmr();
        }

        public Emr WithKeyframes(Emr emr)
        {
            var builder = ToBuilder();
            builder.KeyFrameSize = emr.KeyFrameSize;
            builder.KeyFrames.Clear();
            builder.KeyFrames.AddRange(emr.ToBuilder().KeyFrames);
            return builder.ToEmr();
        }

        [DebuggerDisplay("({x}, {y}, {z})")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Vector : IEquatable<Vector>
        {
            public short x, y, z;

            public Vector(short x, short y, short z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }

            public override readonly bool Equals(object obj) => obj is Vector v && Equals(v);
            public readonly bool Equals(Vector other) => other.x == x && other.y == y && other.z == z;

            public override int GetHashCode()
            {
                int hashCode = 373119288;
                hashCode = hashCode * -1521134295 + x.GetHashCode();
                hashCode = hashCode * -1521134295 + y.GetHashCode();
                hashCode = hashCode * -1521134295 + z.GetHashCode();
                return hashCode;
            }

            public static bool operator ==(Vector left, Vector right) => left.Equals(right);
            public static bool operator !=(Vector left, Vector right) => !(left == right);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Armature
        {
            public short count;
            public short offset;
        }

        [DebuggerDisplay("offset = {offset} speed = {speed}")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public readonly unsafe struct KeyFrame
        {
            private readonly Emr _emr;
            private readonly int _offset;
            private readonly int _length;

            public ReadOnlySpan<byte> Span => GetSpan<byte>(0, _length);

            public Vector Offset
            {
                get
                {
                    if (_emr.Version == BioVersion.Biohazard3)
                    {
                        var y = GetSpan<short>(6, 1)[0];
                        return new Vector(0, y, 0);
                    }
                    else
                    {
                        return GetSpan<Vector>(0, 1)[0];
                    }
                }
            }
            public Vector Speed
            {
                get
                {
                    if (_emr.Version == BioVersion.Biohazard3)
                    {
                        return GetSpan<Vector>(0, 1)[0];
                    }
                    else
                    {
                        return GetSpan<Vector>(6, 1)[0];
                    }
                }
            }

            public ReadOnlySpan<byte> AngleData
            {
                get
                {
                    if (_emr.Version == BioVersion.Biohazard3)
                    {
                        var count = _length - 8;
                        return GetSpan<byte>(8, count);
                    }
                    else
                    {
                        var count = _length - 12;
                        return GetSpan<byte>(12, count);
                    }
                }
            }

            public int NumAngles
            {
                get
                {
                    if (_emr.Version == BioVersion.Biohazard1)
                        return AngleData.Length / 2 / 3;
                    else
                        return (AngleData.Length * 2 / 3) / 3;
                }
            }

            public Vector[] Angles
            {
                get
                {
                    var result = new Vector[NumAngles];
                    for (var i = 0; i < result.Length; i++)
                    {
                        result[i] = GetAngle(i);
                    }
                    return result;
                }
            }

            public KeyFrame(Emr emr, int offset, int length) : this()
            {
                _emr = emr;
                _offset = offset;
                _length = length;
            }

            public Vector GetAngle(int i)
            {
                if (_emr.Version == BioVersion.Biohazard1)
                {
                    var angles = MemoryMarshal.Cast<byte, short>(AngleData);
                    var offset = i * 3;
                    var x = angles[offset + 0];
                    var y = angles[offset + 1];
                    var z = angles[offset + 2];
                    return new Vector(x, y, z);
                }
                else if (_emr.Version == BioVersion.Biohazard2 ||
                         _emr.Version == BioVersion.Biohazard3)
                {
                    fixed (byte* ptr = AngleData)
                    {
                        var nibble = i * 9;
                        var byteIndex = nibble / 2;
                        var src = ptr + byteIndex;

                        // Read 3 nibbles
                        var x = ReadAngle(ref src, ref nibble);
                        var y = ReadAngle(ref src, ref nibble);
                        var z = ReadAngle(ref src, ref nibble);

                        return new Vector(x, y, z);
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }
            }

            private static short ReadAngle(ref byte* src, ref int nibble)
            {
                var a = ReadNibble(ref src, ref nibble);
                var b = ReadNibble(ref src, ref nibble);
                var c = ReadNibble(ref src, ref nibble);
                return (short)(c << 8 | b << 4 | a);
            }

            private static byte ReadNibble(ref byte* src, ref int nibble)
            {
                byte value;
                if ((nibble & 1) == 0)
                {
                    value = (byte)(*src & 0x0F);
                }
                else
                {
                    value = (byte)(*src >> 4);
                    src++;
                }
                nibble++;
                return value;
            }

            private ReadOnlySpan<T> GetSpan<T>(int offset, int count) where T : struct
            {
                var data = _emr.GetSpan<byte>(_offset + offset, _length - offset);
                return MemoryMarshal.Cast<byte, T>(data).Slice(0, count);
            }
        }
    }
}

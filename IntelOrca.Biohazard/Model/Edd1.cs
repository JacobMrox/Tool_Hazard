using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.Extensions;

namespace IntelOrca.Biohazard.Model
{
    public sealed partial class Edd1 : IEdd
    {
        public BioVersion Version { get; }
        public ReadOnlyMemory<byte> Data { get; }

        public Edd1(BioVersion version, ReadOnlyMemory<byte> data)
        {
            Version = version;
            Data = data;
        }

        public int AnimationCount => Animations.Length;
        public ReadOnlySpan<Animation> Animations
        {
            get
            {
                var firstOffset = GetSpan<ushort>(2, 1)[0];
                var count = firstOffset / 4;
                return GetSpan<Animation>(0, count);
            }
        }
        public ReadOnlySpan<Frame> GetFrames(int animationIndex)
        {
            var animation = Animations[animationIndex];
            var offset = animation.Offset;
            return GetSpan<Frame>(offset, animation.Count);
        }

        public int GetAnimationDuration(int index)
        {
            var frames = GetFrames(index);
            return frames.Length;
        }

        public int GetFrameIndex(int index, int time)
        {
            var frames = GetFrames(index);
            var frameIndex = Math.Max(0, Math.Min(frames.Length - 1, time));
            return frames[frameIndex].Index;
        }

        public int GetFrameFunction(int index, int time)
        {
            var frames = GetFrames(index);
            var frameIndex = Math.Max(0, Math.Min(frames.Length - 1, time));
            return frames[frameIndex].Flags;
        }

        IEddBuilder IEdd.ToBuilder() => ToBuilder();
        public Builder ToBuilder()
        {
            var builder = new Builder(Version);
            for (var i = 0; i < AnimationCount; i++)
            {
                var newAnimation = new Builder.Animation();
                newAnimation.Frames.Clear();
                newAnimation.Frames.AddRange(GetFrames(i).ToArray());
                builder.Animations.Add(newAnimation);
            }
            return builder;
        }

        private ReadOnlySpan<T> GetSpan<T>(int offset, int count) where T : struct => Data.GetSafeSpan<T>(offset, count);

        [DebuggerDisplay("Count = {Count} Offset = {Offset}")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Animation
        {
            public ushort Count { get; set; }
            public ushort Offset { get; set; }
        }

        [DebuggerDisplay("{Index} Flags = {Flags}")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Frame
        {
            private uint _value;

            public ushort Index
            {
                get => (ushort)(_value & 0xFFF);
                set => _value = (uint)(_value & ~0xFFF | (uint)value & 0xFFF);
            }

            public int Flags
            {
                get => (int)(_value >> 12);
                set => _value = ((uint)value << 12) | (_value & 0xFFF);
            }
        }
    }
}

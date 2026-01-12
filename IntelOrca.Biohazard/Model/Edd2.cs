using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using IntelOrca.Biohazard.Extensions;

namespace IntelOrca.Biohazard.Model
{
    public sealed partial class Edd2 : IEdd
    {
        public BioVersion Version => BioVersion.Biohazard3;
        public ReadOnlyMemory<byte> Data { get; }

        public Edd2(ReadOnlyMemory<byte> data)
        {
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
            var animation = Animations[index];
            var frames = GetFrames(index);
            return (int)(animation.StartFrame + frames[time].Index);
        }

        public int GetFrameFunction(int index, int time)
        {
            var animation = Animations[index];
            var frames = GetFrames(index);
            return frames[time].Function;
        }

        IEddBuilder IEdd.ToBuilder() => ToBuilder();

        public Builder ToBuilder()
        {
            var builder = new Builder();
            for (var i = 0; i < AnimationCount; i++)
            {
                builder.Animations.Add(new Builder.Animation()
                {
                    Frames = GetFrames(i).ToArray()
                });
            }
            return builder;
        }

        private ReadOnlySpan<T> GetSpan<T>(int offset, int count) where T : struct => Data.GetSafeSpan<T>(offset, count);

        [DebuggerDisplay("Count = {Count} Offset = {Offset} StartFrame = {StartFrame}")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Animation
        {
            public ushort Count { get; set; }
            public ushort Offset { get; set; }
            public ushort StartFrame { get; set; }
            public ushort Unknown { get; set; }
        }

        [DebuggerDisplay("{Index}")]
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Frame
        {
            public byte Index { get; set; }
            public byte Function { get; set; }
        }
    }
}

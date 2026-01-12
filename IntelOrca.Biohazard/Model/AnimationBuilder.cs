using System;
using System.Collections.Generic;
using System.Linq;
using EmrFrame = IntelOrca.Biohazard.Model.Emr.Builder.Keyframe;

namespace IntelOrca.Biohazard.Model
{
    public sealed class AnimationBuilder
    {
        private readonly IEdd _edd;
        private readonly Emr _emr;

        public BioVersion Version => _edd.Version;
        public List<Animation> Animations { get; } = new List<Animation>();

        public static AnimationBuilder FromEddEmr(IEdd edd, Emr emr)
        {
            var edd1 = (Edd1)edd;

            var result = new AnimationBuilder(edd, emr);
            var emrBuilder = emr.ToBuilder();
            for (var i = 0; i < edd1.AnimationCount; i++)
            {
                var animation = new Animation();
                var frames = edd1.GetFrames(i);
                foreach (var frame in frames)
                {
                    var emrFrame = emrBuilder.KeyFrames[frame.Index];
                    animation.Frames.Add(new Frame(frame.Flags, emrFrame));
                }
                result.Animations.Add(animation);
            }
            return result;
        }

        private AnimationBuilder(IEdd edd, Emr emr)
        {
            _edd = edd;
            _emr = emr;
        }

        public (IEdd, Emr) ToEddEmr()
        {
            var eddBuilder = (Edd1.Builder)_edd.ToBuilder();
            eddBuilder.Animations.Clear();
            var emrBuilder = _emr.ToBuilder();
            emrBuilder.KeyFrames.Clear();

            var cache = new Dictionary<EmrFrame, int>();
            foreach (var animation in Animations)
            {
                var frames = new List<Edd1.Frame>();
                for (var i = 0; i < animation.Frames.Count; i++)
                {
                    var frame = animation.Frames[i];
                    if (!cache.TryGetValue(frame.EmrFrame, out var frameIndex))
                    {
                        frameIndex = emrBuilder.KeyFrames.Count;
                        cache.Add(frame.EmrFrame, frameIndex);
                        emrBuilder.KeyFrames.Add(frame.EmrFrame);
                    }

                    frames.Add(new Edd1.Frame()
                    {
                        Flags = frame.Flags,
                        Index = (ushort)frameIndex
                    });
                }

                var newAnimation = new Edd1.Builder.Animation();
                newAnimation.Frames.Clear();
                newAnimation.Frames.AddRange(frames);
                eddBuilder.Animations.Add(newAnimation);
            }

            return (eddBuilder.ToEdd(), emrBuilder.ToEmr());
        }

        public class Animation
        {
            public List<Frame> Frames { get; } = new List<Frame>();

            public void ChangeSpeed(double xyz) => ChangeSpeed(xyz, xyz, xyz);
            public void ChangeSpeed(double x, double y, double z)
            {
                foreach (var frame in Frames)
                {
                    frame.ChangeSpeed(x, y, z);
                }
            }

            public void Stretch(double scale, bool looping)
            {
                var oldFrames = Frames.ToList();
                var lastOldFrame = oldFrames[oldFrames.Count - 1];
                if (looping)
                {
                    foreach (var frame in Frames)
                    {
                        var emrFrame = frame.EmrFrame;
                        var speed = emrFrame.Speed;
                        speed.x += lastOldFrame.EmrFrame.Speed.x;
                        oldFrames.Add(new Frame(0, new EmrFrame()
                        {
                            Offset = emrFrame.Offset,
                            Speed = speed,
                            Angles = emrFrame.Angles
                        }));
                    }
                }
                else
                {
                    for (var i = 0; i < Frames.Count; i++)
                    {
                        oldFrames.Add(lastOldFrame);
                    }
                }

                var oldCount = Frames.Count;
                var newCount = (int)Math.Round(oldCount * scale);
                var newFrames = new List<Frame>();
                for (var i = 0; i < newCount; i++)
                {
                    var t = i / (double)newCount;
                    var index = t * oldCount;
                    var mid = index - (int)index;
                    var srcA = oldFrames[(int)Math.Floor(index)];
                    var srcB = oldFrames[(int)Math.Ceiling(index)];
                    var emrFrameA = srcA.EmrFrame;
                    var emrFrameB = srcB.EmrFrame;
                    var emrFrameC = Lerp(emrFrameA, emrFrameB, mid);
                    newFrames.Add(new Frame(0, emrFrameC));
                }

                // Flags
                for (var i = 0; i < oldCount; i++)
                {
                    var frame = Frames[i];
                    if (frame.Flags != 0)
                    {
                        var newIndex = Math.Min(newCount - 1, (int)Math.Round(i * scale));
                        newFrames[newIndex].Flags = frame.Flags;
                    }
                }

                Frames.Clear();
                Frames.AddRange(newFrames);
            }

            public void Insert(int time)
            {
                var frames = Frames;
                if (frames.Count == 0)
                {
                    frames.Add(new Frame(0, new EmrFrame()));
                }
                else
                {
                    var left = frames[time];
                    var right = time == frames.Count - 1 ? frames[0] : frames[time + 1];
                    var mid = Lerp(left.EmrFrame, right.EmrFrame, 0.5);
                    frames.Insert(time + 1, new Frame(0, mid));
                }
            }

            private static EmrFrame Lerp(EmrFrame a, EmrFrame b, double t)
            {
                var result = new EmrFrame();
                result.Offset = Lerp(a.Offset, b.Offset, t);
                result.Speed = Lerp(a.Speed, b.Speed, t);
                result.Angles = new Emr.Vector[a.Angles.Length];
                for (var i = 0; i < a.Angles.Length; i++)
                {
                    result.Angles[i] = LerpAngle(a.Angles[i], b.Angles[i], t);
                }
                return result;
            }

            private static Emr.Vector Lerp(Emr.Vector a, Emr.Vector b, double t)
            {
                var result = new Emr.Vector();
                result.x = Lerp(a.x, b.x, t);
                result.y = Lerp(a.y, b.y, t);
                result.z = Lerp(a.z, b.z, t);
                return result;
            }

            private static short Lerp(short a, short b, double t)
            {
                var range = b - a;
                return (short)(a + (range * t));
            }

            private static Emr.Vector LerpAngle(Emr.Vector a, Emr.Vector b, double t)
            {
                var result = new Emr.Vector();
                result.x = LerpAngle(a.x, b.x, t);
                result.y = LerpAngle(a.y, b.y, t);
                result.z = LerpAngle(a.z, b.z, t);
                return result;
            }

            private static short LerpAngle(short a, short b, double t)
            {
                var range = b - a;
                if (b <= a)
                {
                    var altRange = (4096 - a) + b;
                    if (Math.Abs(altRange) < Math.Abs(range))
                    {
                        return (short)(Lerp(a, (short)(4096 + b), t) % 4096);
                    }
                    return Lerp(a, b, t);
                }
                else
                {
                    return LerpAngle(b, a, 1 - t);
                }
            }
        }

        public class Frame
        {
            public int Flags { get; set; }
            public EmrFrame EmrFrame { get; set; }

            public Frame(int flags, EmrFrame emrFrame)
            {
                Flags = flags;
                EmrFrame = emrFrame;
            }

            public void ChangeSpeed(double x, double y, double z)
            {
                var emrFrame = EmrFrame;
                var emrSpeed = emrFrame.Speed;
                emrSpeed.x = (short)Math.Round(emrSpeed.x * x);
                emrSpeed.y = (short)Math.Round(emrSpeed.y * y);
                emrSpeed.z = (short)Math.Round(emrSpeed.z * z);
                emrFrame.Speed = emrSpeed;
                EmrFrame = emrFrame;
            }
        }
    }
}

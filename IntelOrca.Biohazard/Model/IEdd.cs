using System;

namespace IntelOrca.Biohazard.Model
{
    public interface IEdd
    {
        BioVersion Version { get; }
        ReadOnlyMemory<byte> Data { get; }
        int AnimationCount { get; }
        int GetAnimationDuration(int index);
        int GetFrameIndex(int index, int time);
        int GetFrameFunction(int index, int time);
        IEddBuilder ToBuilder();
    }
}

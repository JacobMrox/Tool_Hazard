using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IntelOrca.Biohazard.Model
{
    public sealed partial class MorphData
    {
        public class Builder
        {
            public BioVersion Version { get; }
            public int Unknown00 { get; set; }
            public List<Emr.Vector[]> Skeletons { get; } = new List<Emr.Vector[]>();
            public List<MorphGroup> Groups { get; } = new List<MorphGroup>();

            public Builder(BioVersion version)
            {
                Version = version;
            }

            public unsafe MorphData ToMorphData()
            {
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                bw.Write(Unknown00);
                bw.Write(0);

                var skeletonLength = Skeletons.Max(x => x.Length) * sizeof(Emr.Vector);
                skeletonLength = ((skeletonLength + 3) / 4) * 4; // Round up to nearest multiple of 4
                bw.Write(skeletonLength);

                // Headers
                foreach (var morphGroup in Groups)
                {
                    bw.Write(0);
                    var elementLength = morphGroup.Positions.Max(x => x.Length) * sizeof(Emr.Vector);
                    bw.Write(elementLength);
                    bw.Write(morphGroup.Positions.Count - 1);
                    bw.Write(morphGroup.Unknown);
                }

                // Skeletons
                var skeletonDataOffset = (int)ms.Position;
                foreach (var skeleton in Skeletons)
                {
                    foreach (var partOffset in skeleton)
                    {
                        bw.Write(partOffset);
                    }

                    var padding = skeletonLength - (skeleton.Length * sizeof(Emr.Vector));
                    for (var i = 0; i < padding; i++)
                    {
                        bw.Write((byte)0);
                    }
                }

                // Morph data
                var morphDataOffsets = new int[Groups.Count];
                for (var i = 0; i < Groups.Count; i++)
                {
                    var morphGroup = Groups[i];
                    morphDataOffsets[i] = (int)ms.Position;
                    foreach (var positionGroup in morphGroup.Positions)
                    {
                        foreach (var position in positionGroup)
                        {
                            bw.Write(position);
                        }
                    }
                }

                // Write offsets
                ms.Position = 4;
                bw.Write(skeletonDataOffset);

                ms.Position = 12;
                for (var i = 0; i < Groups.Count; i++)
                {
                    bw.Write(morphDataOffsets[i]);
                    ms.Position += 16 - 4;
                }

                var data = ms.ToArray();
                return new MorphData(Version, data);
            }

            public class MorphGroup
            {
                public List<Emr.Vector[]> Positions { get; } = new List<Emr.Vector[]>();
                public int Unknown { get; set; }
            }
        }
    }
}

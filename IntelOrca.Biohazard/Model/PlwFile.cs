using System;
using System.IO;

namespace IntelOrca.Biohazard.Model
{
    public class PlwFile : ModelFile
    {
        private readonly static ChunkKind[] g_chunkKinds1 = new[]
        {
            ChunkKind.Armature,
            ChunkKind.Animation,
            ChunkKind.Mesh
        };

        private readonly static ChunkKind[] g_chunkKinds2 = new[]
        {
            ChunkKind.Animation,
            ChunkKind.Armature,
            ChunkKind.Mesh,
            ChunkKind.Texture,
        };

        private readonly static ChunkKind[] g_chunkKinds3 = new[]
        {
            ChunkKind.Animation,
            ChunkKind.Armature,
            ChunkKind.Unknown,
            ChunkKind.Unknown,
            ChunkKind.Unknown,
            ChunkKind.Unknown,
            ChunkKind.Unknown,
            ChunkKind.Mesh,
            ChunkKind.Texture,
        };

        public PlwFile(BioVersion version, string path)
            : base(version, path)
        {
        }

        public PlwFile(BioVersion version, Stream stream)
            : base(version, stream)
        {
        }

        protected override ReadOnlySpan<ChunkKind> ChunkKinds =>
            Version switch
            {
                BioVersion.Biohazard1 => CalculateKindsRE1(),
                BioVersion.Biohazard2 => g_chunkKinds2,
                BioVersion.Biohazard3 => g_chunkKinds3,
                _ => throw new NotSupportedException()
            };

        private ChunkKind[] CalculateKindsRE1()
        {
            var result = new ChunkKind[NumChunks];
            for (var i = 0; i < Math.Min(g_chunkKinds1.Length, result.Length); i++)
                result[i] = g_chunkKinds1[i];
            result[NumChunks - 1] = ChunkKind.Mesh;
            return result;
        }

        public TimFile Tim
        {
            get => GetTim(0);
            set => SetTim(0, value);
        }
    }
}

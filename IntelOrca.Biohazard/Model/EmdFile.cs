using System;
using System.IO;

namespace IntelOrca.Biohazard.Model
{
    public class EmdFile : ModelFile
    {
        private readonly static ChunkKind[] g_chunkKinds1A = new[]
        {
            ChunkKind.Armature,
            ChunkKind.Animation,
            ChunkKind.Mesh,
            ChunkKind.Texture,
        };

        private readonly static ChunkKind[] g_chunkKinds1B = new[]
        {
            ChunkKind.Armature,
            ChunkKind.Animation,
            ChunkKind.Armature,
            ChunkKind.Animation,
            ChunkKind.Mesh,
            ChunkKind.Texture,
        };

        private readonly static ChunkKind[] g_chunkKinds2 = new[]
        {
            ChunkKind.Morph,
            ChunkKind.Animation,
            ChunkKind.Armature,
            ChunkKind.Animation,
            ChunkKind.Armature,
            ChunkKind.Animation,
            ChunkKind.Armature,
            ChunkKind.Mesh,
        };

        private readonly static ChunkKind[] g_chunkKinds3 = new[]
        {
            ChunkKind.Morph,
            ChunkKind.Unknown,
            ChunkKind.Animation,
            ChunkKind.Armature,
            ChunkKind.Animation,
            ChunkKind.Armature,
            ChunkKind.Animation,
            ChunkKind.Armature,
            ChunkKind.Unknown,
            ChunkKind.Unknown,
            ChunkKind.Unknown,
            ChunkKind.Unknown,
            ChunkKind.Unknown,
            ChunkKind.Mesh,
            ChunkKind.Mesh,
        };

        public EmdFile(BioVersion version, string path)
            : base(version, path)
        {
        }

        public EmdFile(BioVersion version, Stream stream)
            : base(version, stream)
        {
        }

        protected override ReadOnlySpan<ChunkKind> ChunkKinds =>
            Version switch
            {
                BioVersion.Biohazard1 => NumChunks == 4 ? g_chunkKinds1A : g_chunkKinds1B,
                BioVersion.Biohazard2 => g_chunkKinds2,
                BioVersion.Biohazard3 => g_chunkKinds3,
                _ => throw new NotSupportedException()
            };
    }
}

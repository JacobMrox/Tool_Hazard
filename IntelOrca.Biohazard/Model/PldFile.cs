using System;
using System.IO;

namespace IntelOrca.Biohazard.Model
{
    public class PldFile : ModelFile
    {
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
            ChunkKind.Mesh,
            ChunkKind.Morph,
            ChunkKind.Texture,
        };

        public PldFile(BioVersion version, string path)
            : base(version, path)
        {
        }

        public PldFile(BioVersion version, Stream stream)
            : base(version, stream)
        {
        }

        protected override ReadOnlySpan<ChunkKind> ChunkKinds =>
            Version == BioVersion.Biohazard2 ? g_chunkKinds2 : g_chunkKinds3;

        public TimFile Tim
        {
            get => GetTim(0);
            set => SetTim(0, value);
        }
    }
}

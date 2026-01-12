using System;

namespace IntelOrca.Biohazard
{
    public sealed class PrsFile
    {
        private readonly ReadOnlyMemory<byte> _compressed;
        private ReadOnlyMemory<byte>? _uncompressed;
        private object _sync = new object();

        public ReadOnlyMemory<byte> Data => _compressed;

        public static PrsFile Compress(ReadOnlyMemory<byte> uncompressed)
        {
            // var bufferSize = 0x1FFF;
            var bufferSize = 256 - 1;
            var compressed = csharp_prs.Prs.Compress(uncompressed.ToArray(), bufferSize);
            return new PrsFile(compressed);
        }

        public PrsFile(ReadOnlyMemory<byte> compressed)
        {
            _compressed = compressed;
        }

        public unsafe ReadOnlyMemory<byte> Uncompressed
        {
            get
            {
                if (_uncompressed == null)
                {
                    lock (_sync)
                    {
                        if (_uncompressed == null)
                        {
                            var span = _compressed.Span;
                            fixed (byte* src = span)
                            {
                                _uncompressed = csharp_prs.Prs.Decompress(src, span.Length);
                            }
                        }
                    }
                }
                return _uncompressed.Value;
            }
        }
    }
}

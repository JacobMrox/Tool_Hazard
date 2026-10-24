namespace White_Day_Mod_Tool
{
    /// <summary>
    /// Reader/writer for the two bitmap-font layouts used by White Day (2001).
    /// Format behavior was verified against emuyia/wd-fonteditor (Apache-2.0),
    /// then reimplemented here with lossless preservation of non-glyph bytes.
    /// </summary>
    public sealed class FontParameters
    {
        private const int HeaderLength = 14;
        private const int TrailerLength = 2;
        private const int KnownGlyphCount = 3648;

        private byte[] _header = Array.Empty<byte>();
        private byte[] _trailer = Array.Empty<byte>();
        private byte[][] _glyphs = Array.Empty<byte[]>();
        private byte[] _widths = Array.Empty<byte>();
        private byte[] _reserved1 = Array.Empty<byte>();
        private byte[] _reserved2 = Array.Empty<byte>();

        public int PixelWidth { get; private set; }
        public int GlyphByteLength { get; private set; }
        public int GlyphCount => _glyphs.Length;
        public string SourcePath { get; private set; } = string.Empty;

        public void Load(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            byte[] file = File.ReadAllBytes(path);

            (int pixelWidth, int glyphByteLength) = file.Length switch
            {
                598288 => (18, 162),
                2371216 => (36, 648),
                171472 => throw new NotSupportedException(
                    "aa견고딕09.fnt is not supported by the reference editor."),
                _ => throw new InvalidDataException(
                    $"Unsupported White Day font size: {file.Length:N0} bytes. " +
                    "Supported layouts are the 18×18 FNT and 36×36 WFT fonts.")
            };

            int recordLength = glyphByteLength + 2;
            int expectedLength = HeaderLength + KnownGlyphCount * recordLength + TrailerLength;
            if (file.Length != expectedLength)
                throw new InvalidDataException("The font has an invalid or truncated record table.");

            PixelWidth = pixelWidth;
            GlyphByteLength = glyphByteLength;
            SourcePath = path;
            _header = file[..HeaderLength];
            _trailer = file[^TrailerLength..];
            _glyphs = new byte[KnownGlyphCount][];
            _widths = new byte[KnownGlyphCount];
            _reserved1 = new byte[KnownGlyphCount];
            _reserved2 = new byte[KnownGlyphCount];

            for (int i = 0; i < KnownGlyphCount; i++)
            {
                int recordOffset = HeaderLength + i * recordLength;
                _widths[i] = file[recordOffset];
                _reserved1[i] = file[recordOffset + 1];
                _reserved2[i] = file[recordOffset + 2];

                // The first packed bitmap byte shares the record position used
                // for glyph width in the game file and is blank when decoded.
                byte[] glyph = new byte[glyphByteLength];
                glyph[0] = 0;
                Buffer.BlockCopy(file, recordOffset + 3, glyph, 1, glyphByteLength - 1);
                _glyphs[i] = glyph;
            }
        }

        public byte[] GetGlyph(int index)
        {
            ValidateIndex(index);
            return (byte[])_glyphs[index].Clone();
        }

        public int GetGlyphWidth(int index)
        {
            ValidateIndex(index);
            return _widths[index];
        }

        public void SetGlyph(int index, ReadOnlySpan<byte> glyph, int width)
        {
            ValidateIndex(index);
            if (glyph.Length != GlyphByteLength)
                throw new ArgumentException(
                    $"Glyph data must be exactly {GlyphByteLength} bytes.", nameof(glyph));
            if (width < 0 || width >= PixelWidth)
                throw new ArgumentOutOfRangeException(
                    nameof(width), $"Glyph width must be between 0 and {PixelWidth - 1}.");

            byte[] copy = glyph.ToArray();
            copy[0] = 0;
            _glyphs[index] = copy;
            _widths[index] = (byte)width;
        }

        public void Save(string path)
        {
            EnsureLoaded();
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            int recordLength = GlyphByteLength + 2;
            byte[] output = new byte[
                HeaderLength + GlyphCount * recordLength + _trailer.Length];

            Buffer.BlockCopy(_header, 0, output, 0, _header.Length);
            for (int i = 0; i < GlyphCount; i++)
            {
                int recordOffset = HeaderLength + i * recordLength;
                output[recordOffset] = _widths[i];
                output[recordOffset + 1] = _reserved1[i];
                output[recordOffset + 2] = _reserved2[i];
                Buffer.BlockCopy(
                    _glyphs[i], 1, output, recordOffset + 3, GlyphByteLength - 1);
            }

            Buffer.BlockCopy(
                _trailer, 0, output, output.Length - _trailer.Length, _trailer.Length);
            File.WriteAllBytes(path, output);
        }

        private void ValidateIndex(int index)
        {
            EnsureLoaded();
            if ((uint)index >= (uint)GlyphCount)
                throw new ArgumentOutOfRangeException(nameof(index));
        }

        private void EnsureLoaded()
        {
            if (_glyphs.Length == 0)
                throw new InvalidOperationException("No White Day font is loaded.");
        }
    }
}

using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace White_Day_Mod_Tool
{
    public static class BitmapExtensions
    {
        public static Bitmap DecodeWhiteDayGlyph(
            ReadOnlySpan<byte> packedPixels,
            int pixelWidth,
            int displayScale = 1)
        {
            int rowBytes = pixelWidth / 2;
            if (pixelWidth <= 0 || (pixelWidth & 1) != 0 ||
                packedPixels.Length != rowBytes * pixelWidth)
            {
                throw new ArgumentException("Invalid packed White Day glyph data.");
            }

            using var source = new Bitmap(pixelWidth, pixelWidth, PixelFormat.Format32bppArgb);
            for (int y = 0; y < pixelWidth; y++)
            {
                for (int x = 0; x < pixelWidth; x++)
                {
                    byte packed = packedPixels[y * rowBytes + x / 2];
                    int nibble = (x & 1) == 0 ? packed >> 4 : packed & 0x0F;
                    int intensity = nibble * 17;
                    source.SetPixel(x, y, Color.FromArgb(intensity, intensity, intensity));
                }
            }

            if (displayScale <= 1)
                return new Bitmap(source);

            var scaled = new Bitmap(
                pixelWidth * displayScale,
                pixelWidth * displayScale,
                PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(scaled);
            graphics.Clear(Color.FromArgb(24, 24, 24));
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(
                source,
                new Rectangle(0, 0, scaled.Width, scaled.Height),
                0,
                0,
                source.Width,
                source.Height,
                GraphicsUnit.Pixel);
            return scaled;
        }

        public static byte[] EncodeWhiteDayGlyph(Image image, int pixelWidth)
        {
            ArgumentNullException.ThrowIfNull(image);
            int rowBytes = pixelWidth / 2;
            byte[] packed = new byte[rowBytes * pixelWidth];

            using var normalized = new Bitmap(pixelWidth, pixelWidth, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(normalized))
            {
                graphics.Clear(Color.White);
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(
                    image,
                    new Rectangle(0, 0, pixelWidth, pixelWidth),
                    0,
                    0,
                    image.Width,
                    image.Height,
                    GraphicsUnit.Pixel);
            }

            for (int y = 0; y < pixelWidth; y++)
            {
                for (int x = 0; x < pixelWidth; x += 2)
                {
                    int high = ToGrayscaleNibble(normalized.GetPixel(x, y));
                    int low = ToGrayscaleNibble(normalized.GetPixel(x + 1, y));
                    packed[y * rowBytes + x / 2] = (byte)((high << 4) | low);
                }
            }

            // This byte is represented by the width field in the on-disk record.
            packed[0] = 0;
            return packed;
        }

        public static Bitmap CreateIndexedGlyphBitmap(
            ReadOnlySpan<byte> packedPixels,
            int pixelWidth)
        {
            int rowBytes = pixelWidth / 2;
            if (packedPixels.Length != rowBytes * pixelWidth)
                throw new ArgumentException("Invalid packed White Day glyph data.");

            var bitmap = new Bitmap(pixelWidth, pixelWidth, PixelFormat.Format4bppIndexed);
            ColorPalette palette = bitmap.Palette;
            for (int i = 0; i < palette.Entries.Length; i++)
            {
                int intensity = i * 17;
                palette.Entries[i] = Color.FromArgb(intensity, intensity, intensity);
            }
            bitmap.Palette = palette;

            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format4bppIndexed);
            try
            {
                int stride = Math.Abs(data.Stride);
                byte[] row = Enumerable.Repeat((byte)0xFF, stride).ToArray();
                for (int y = 0; y < pixelWidth; y++)
                {
                    packedPixels.Slice(y * rowBytes, rowBytes).CopyTo(row);
                    IntPtr destination = IntPtr.Add(data.Scan0, y * data.Stride);
                    Marshal.Copy(row, 0, destination, row.Length);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }

        private static int ToGrayscaleNibble(Color color)
        {
            if (color.A < 128)
                return 15;

            int luminance =
                (color.R * 299 + color.G * 587 + color.B * 114 + 500) / 1000;
            return Math.Clamp((luminance * 15 + 127) / 255, 0, 15);
        }
    }
}

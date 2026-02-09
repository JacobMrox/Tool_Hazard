using IntelOrca.Biohazard;
using System.Drawing.Imaging;

namespace Tool_Hazard.Sony_PS1
{
    public static class TimPng
    {
        // Same "page" logic as your EmdTool: each 128px-wide page uses a new CLUT.
        // Works for 8bpp multi-clut TIMs and is harmless for 16bpp.
        private static int DefaultClutSelector(int x, int y) => x / 128;

        public static Bitmap ExportToBitmap(TimFile tim, Func<int, int, int>? getClutIndex = null)
        {
            getClutIndex ??= DefaultClutSelector;

            var bmp = new Bitmap(tim.Width, tim.Height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* dstBase = (byte*)data.Scan0;
                for (int y = 0; y < tim.Height; y++)
                {
                    uint* dstRow = (uint*)(dstBase + y * data.Stride);
                    for (int x = 0; x < tim.Width; x++)
                    {
                        int clut = getClutIndex(x, y);
                        uint argb = tim.GetPixel(x, y, clut);
                        dstRow[x] = argb;
                    }
                }
            }

            bmp.UnlockBits(data);
            return bmp;
        }

        public static void ExportTimToPng(string timPath, string pngPath, Func<int, int, int>? getClutIndex = null)
        {
            var tim = new TimFile(timPath);
            using var bmp = ExportToBitmap(tim, getClutIndex);
            bmp.Save(pngPath, ImageFormat.Png);
        }

        public static TimFile ImportPngTo8bppTim(string pngPath)
        {
            using var bitmap = (Bitmap)Image.FromFile(pngPath);

            // Matches your EmdTool: create 8bpp tim, then build CLUT per 128px page.
            var tim = new TimFile(bitmap.Width, bitmap.Height, 8);

            int clutIndex = 0;
            for (int x0 = 0; x0 < bitmap.Width; x0 += 128)
            {
                var srcBounds = new Rectangle(x0, 0, Math.Min(bitmap.Width - x0, 128), bitmap.Height);

                ushort[] colours = GetColours(bitmap, srcBounds);
                tim.SetPalette(clutIndex, colours);

                // Import pixels into tim for this page
                for (int y = srcBounds.Top; y < srcBounds.Bottom; y++)
                {
                    for (int x = srcBounds.Left; x < srcBounds.Right; x++)
                    {
                        var c32 = bitmap.GetPixel(x, y);
                        tim.SetPixel(x, y, clutIndex, (uint)c32.ToArgb());
                    }
                }

                clutIndex++;
            }

            return tim;
        }

        public static void ImportPngToTimFile(string pngPath, string timOutPath)
        {
            var tim = ImportPngTo8bppTim(pngPath);
            tim.Save(timOutPath);
        }

        // Same palette harvesting logic as your EmdTool.GetColours
        private static ushort[] GetColours(Bitmap bitmap, Rectangle area)
        {
            var coloursList = new ushort[256];
            int coloursIndex = 1; // reserve 0 as transparent

            var seen = new HashSet<ushort>();

            for (int y = area.Top; y < area.Bottom; y++)
            {
                for (int x = area.Left; x < area.Right; x++)
                {
                    var c32 = bitmap.GetPixel(x, y);
                    var c16 = TimFile.Convert32to16((uint)c32.ToArgb());

                    if (seen.Add(c16))
                    {
                        coloursList[coloursIndex++] = c16;
                        if (coloursIndex == 256)
                            return coloursList;
                    }
                }
            }

            return coloursList;
        }
    }
}

using System;
using System.Runtime.InteropServices;

namespace IntelOrca.Biohazard.Extensions
{
    public static class TimFileExtensions
    {
        public static TimFile ExportPage(this TimFile tim, int page)
        {
            var palette = tim.GetPalette(page);
            var timFile = new TimFile(128, 256, 8);
            timFile.SetPalette(0, palette);
            var srcX = page * 128;
            for (var y = 0; y < 256; y++)
            {
                for (var x = 0; x < 128; x++)
                {
                    var p = tim.GetRawPixel(srcX + x, y);
                    timFile.SetRawPixel(x, y, p);
                }
            }
            return timFile;
        }

        public static void ImportPage(this TimFile target, int targetPage, TimFile source)
        {
            var palette = source.GetPalette(0);
            target.SetPalette(targetPage, palette);

            var minWidth = (targetPage + 1) * 128;
            if (target.Width < minWidth)
            {
                target.ResizeImage(minWidth, target.Height);
            }

            var dstX = targetPage * 128;
            for (var y = 0; y < 256; y++)
            {
                for (var x = 0; x < 128; x++)
                {
                    var p = source.GetRawPixel(x, y);
                    target.SetRawPixel(dstX + x, y, p);
                }
            }
        }

        public static void SwapPages(this TimFile target, int pageA, int pageB)
        {
            var pA = ExportPage(target, pageA);
            var pB = ExportPage(target, pageB);
            ImportPage(target, pageA, pB);
            ImportPage(target, pageB, pA);
        }

        public static TimFile WithWeaponTexture(this TimFile texture, TimFile weaponTexture, int page = 1)
        {
            var xOffset = page * 128;
            var tim = texture.Clone();
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 56; x++)
                {
                    var p16 = weaponTexture.GetPixel(x, y);
                    var p8 = tim.FindBestPaletteEntry(page, p16);
                    tim.SetRawPixel(xOffset + 72 + x, 224 + y, p8);
                }
            }
            return tim;
        }

        public static Bmp ToBmp(this TimFile tim, Func<int, int, int>? getClutIndex = null)
        {
            getClutIndex ??= (x, y) => 0;
            var pixels = tim.GetPixels(getClutIndex);
            return Bmp.FromArgb(tim.Width, tim.Height, MemoryMarshal.Cast<uint, int>(pixels));
        }
    }
}

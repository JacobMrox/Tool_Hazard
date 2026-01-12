using System.Drawing;
using System.Drawing.Imaging;

namespace White_Day_Mod_Tool
{
    public static class BitmapExtensions
    {
        public static Bitmap Crop(this Bitmap source, Rectangle section)
        {
            Bitmap bmp = new Bitmap(section.Width, section.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.DrawImage(source, 0, 0, section, GraphicsUnit.Pixel);
            }
            return bmp;
        }
    }
}

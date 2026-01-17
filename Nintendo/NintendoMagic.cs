using System;
using System.Text;

namespace Tool_Hazard.Nintendo
{
    internal static class NintendoMagic
    {
        public static string GuessExtension(ReadOnlySpan<byte> data)
        {
            if (data.Length >= 4)
            {
                // Nitro SDK / NSB* formats (ASCII magic at start)
                string m4 = Encoding.ASCII.GetString(data.Slice(0, 4));
                switch (m4)
                {
                    case "BCA0": return ".nsbca"; // animation
                    case "BMD0": return ".nsbmd"; // model
                    case "BTX0": return ".nsbtx"; // texture
                    case "BTP0": return ".nsbtp"; // texture pattern (sometimes)
                    case "BMA0": return ".nsbma"; // animation (alt)
                }
            }

            // BMP: "BM"
            if (data.Length >= 2 && data[0] == (byte)'B' && data[1] == (byte)'M')
                return ".bmp";

            return ""; // unknown
        }
    }
}
// RE4 PACK texture stores + image codecs.
// Adapted from JADERLINK_MODEL_VIEWER (MIT):
//   ALL_VIEWER/RE4_UHD_MODEL_VIEWER/src/LoadUhdPack.cs
//   ALL_VIEWER/RE4_X360PS3_MODEL_VIEWER/src/LoadX360PS3Pack.cs
//   ALL_VIEWER/RE4_PS4NS_MODEL_VIEWER/src/LoadPs4NsPack.cs
//   ALL_VIEWER/DDS_LIB/DDSReaderSharp.cs (Kenji Sasaki MIT-derived) — subset reimplemented
//   ALL_VIEWER/TGA_LIB/TGASharpLib.cs (Alex Green MIT) — subset reimplemented
// PACK layout (UHD/X360/PS4NS): UInt32 PackID, UInt32 Amount, offsets[], then entries:
//   UInt32 fileLength, UInt32 FFFFFFFF, UInt32 PackID_, UInt32 Type, image[fileLength].
// Offsets are UInt32 (UHD LE / X360 BE) or UInt64 (PS4/NS LE).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace Tool_Hazard.Biohazard.RE4
{
    public enum Re4PackKind { UhdLittleEndian, X360BigEndian, Ps4SwitchWide }

    public sealed class Re4PackEntry
    {
        public int Index;
        public uint PackIdHeader;
        public uint Type;
        public string Format = "Unknown"; // DDS | TGA | GNF | Unknown
        public byte[] ImageBytes = Array.Empty<byte>();
        public long Offset;
        public string TextureKey => $"{PackIdHeader:X8}/{Index:D4}";
    }

    public sealed class Re4PackDocument
    {
        public string SourcePath = "";
        public Re4PackKind Kind;
        public Re4Endian Endian;
        public uint PackId;
        public byte[] RawBytes = Array.Empty<byte>();
        public List<Re4PackEntry> Entries = new();
        public string Description = "";

        public static Re4PackDocument Load(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            return Decode(data, path);
        }

        public static Re4PackDocument Decode(byte[] data, string? name = null)
        {
            if (data.Length < 8) throw new InvalidDataException("PACK file is too small.");
            // Try UHD LE first, then BE, then wide.
            foreach (var (kind, endian, wide) in new[] { (Re4PackKind.UhdLittleEndian, Re4Endian.Little, false), (Re4PackKind.X360BigEndian, Re4Endian.Big, false), (Re4PackKind.Ps4SwitchWide, Re4Endian.Little, true) })
            {
                try { return TryDecode(data, kind, endian, wide, name); }
                catch (InvalidDataException) { }
            }
            throw new InvalidDataException("Unrecognized PACK layout (tried UHD LE, X360 BE, PS4/NS wide).");
        }

        private static Re4PackDocument TryDecode(byte[] data, Re4PackKind kind, Re4Endian e, bool wide, string? name)
        {
            uint packId = Rd(data, 0, e); uint amount = Rd(data, 4, e);
            if (amount > 0x100000) throw new InvalidDataException("Invalid PACK entry count.");
            int stride = wide ? 8 : 4;
            if (8L + amount * stride > data.Length) throw new InvalidDataException("PACK offset table is truncated.");
            var offsets = new long[amount];
            for (int i = 0; i < amount; i++)
            {
                int o = 8 + i * stride;
                offsets[i] = wide ? (long)BitConverter.ToUInt64(data, o) : Rd(data, o, e);
            }
            var doc = new Re4PackDocument { SourcePath = name ?? "", Kind = kind, Endian = e, PackId = packId, RawBytes = data, Description = kind.ToString() };
            for (int i = 0; i < amount; i++)
            {
                if (offsets[i] == 0) continue;
                int o = (int)offsets[i];
                if (o < 0 || o + 16 > data.Length) throw new InvalidDataException($"PACK entry {i} offset is outside the file.");
                uint len = Rd(data, o, e); Rd(data, o + 4, e); uint pid = Rd(data, o + 8, e); uint type = Rd(data, o + 12, e);
                if (o + 16 + len > data.Length) throw new InvalidDataException($"PACK entry {i} image is truncated.");
                byte[] img = new byte[len];
                Array.Copy(data, o + 16, img, 0, (int)len);
                doc.Entries.Add(new Re4PackEntry { Index = i, PackIdHeader = pid, Type = type, ImageBytes = img, Offset = offsets[i], Format = DetectFormat(img) });
            }
            if (doc.Entries.Count == 0) throw new InvalidDataException("PACK contains no entries.");
            return doc;
        }

        public void Save(string outputPath)
        {
            // Rebuild offsets sequentially; PackID header + Amount preserved.
            bool wide = Kind == Re4PackKind.Ps4SwitchWide;
            int stride = wide ? 8 : 4;
            int tableEnd = 8 + Entries.Count * stride;
            var ordered = Entries.OrderBy(x => x.Index).ToList();
            var newOffsets = new long[ordered.Count];
            int cursor = tableEnd;
            foreach (var ent in ordered)
            {
                newOffsets[ent.Index] = ent.ImageBytes.Length == 0 ? 0 : cursor;
                if (ent.ImageBytes.Length != 0) cursor = checked(cursor + 16 + ent.ImageBytes.Length);
            }
            // Rebuild to full count (includes zero holes) for safety.
            uint amount = (uint)(ordered.Count == 0 ? 0 : Math.Max(ordered.Max(x => x.Index) + 1, ordered.Count));
            // Count includes zero-offset holes from original; use max index + 1.
            amount = (uint)(ordered.Count == 0 ? 0 : ordered.Max(x => x.Index) + 1);
            int tableSize = 8 + (int)amount * stride;
            cursor = tableSize;
            var offsetMap = new Dictionary<int, long>();
            foreach (var ent in ordered)
            {
                if (ent.ImageBytes.Length == 0) continue;
                offsetMap[ent.Index] = cursor;
                cursor = checked(cursor + 16 + ent.ImageBytes.Length);
            }
            using var ms = new MemoryStream();
            WriteU32(ms, PackId, Endian); WriteU32(ms, amount, Endian);
            for (int i = 0; i < amount; i++)
            {
                long v = offsetMap.TryGetValue(i, out long x) ? x : 0;
                if (wide) ms.Write(BitConverter.GetBytes((ulong)v), 0, 8);
                else WriteU32(ms, (uint)v, Endian);
            }
            foreach (var ent in ordered.OrderBy(x => x.Index))
            {
                if (ent.ImageBytes.Length == 0) continue;
                WriteU32(ms, (uint)ent.ImageBytes.Length, Endian);
                WriteU32(ms, 0xFFFFFFFFu, Endian);
                WriteU32(ms, ent.PackIdHeader == 0 ? PackId : ent.PackIdHeader, Endian);
                WriteU32(ms, ent.Type, Endian);
                ms.Write(ent.ImageBytes, 0, ent.ImageBytes.Length);
            }
            File.WriteAllBytes(outputPath, ms.ToArray());
        }

        private static uint Rd(byte[] d, int o, Re4Endian e) => e == Re4Endian.Little ? BitConverter.ToUInt32(d, o) : (uint)((d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]);
        private static void WriteU32(Stream s, uint v, Re4Endian e)
        {
            if (e == Re4Endian.Little) s.Write(BitConverter.GetBytes(v), 0, 4);
            else { s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }
        }

        public static string DetectFormat(byte[] img)
        {
            if (img.Length < 4) return "Unknown";
            uint m = BitConverter.ToUInt32(img, 0);
            if (m == 0x20534444u) return "DDS";
            if (m == 0x20464E47u) return "GNF";
            if (m == 0x00020000u || m == 0x000A0000u) return "TGA";
            return "Unknown";
        }
    }

    // Minimal DDS (uncompressed + DXT1/3/5) and TGA (uncompressed+RLE truecolor/colormapped)
    // decoders sufficient for RE4 PACK textures. GNF is passed through raw (Scarlet decode
    // intentionally not vendored; GNF entries export/import as raw bytes).
    public static class Re4TextureCodec
    {
        public static Bitmap? DecodeToBitmap(byte[] image, out string format)
        {
            format = Re4PackDocument.DetectFormat(image);
            try
            {
                if (format == "DDS") return DdsToBitmap(image);
                if (format == "TGA") return TgaToBitmap(image);
            }
            catch { }
            return null;
        }

        public static byte[] EncodeDdsUncompressed(Bitmap bmp)
        {
            using var b = (Bitmap)bmp.Clone();
            int w = b.Width, h = b.Height;
            byte[] dds = new byte[128 + w * h * 4];
            byte[] magic = System.Text.Encoding.ASCII.GetBytes("DDS ");
            magic.CopyTo(dds, 0);
            WriteI32(dds, 4, 124); WriteI32(dds, 8, 0x1007); // CAPS|HEIGHT|WIDTH|PIXELFORMAT
            WriteI32(dds, 12, h); WriteI32(dds, 16, w);
            WriteI32(dds, 20, w * 4); // pitch
            WriteI32(dds, 24, 0); WriteI32(dds, 28, 0); WriteI32(dds, 32, 0);
            // PF: dwSize=32 at 76, dwFlags=DDPF_ALPHAPIXELS|DDPF_RGB (0x41) at 80, FourCC=0 at 84,
            // bitcount=32 at 88, masks at 92..104, caps DDSCAPS_TEXTURE at 108.
            WriteI32(dds, 76, 32); WriteI32(dds, 80, 0x41); WriteI32(dds, 84, 0); WriteI32(dds, 88, 32);
            WriteI32(dds, 92, unchecked((int)0x00FF0000u)); WriteI32(dds, 96, unchecked((int)0x0000FF00u));
            WriteI32(dds, 100, unchecked((int)0x000000FFu)); WriteI32(dds, 104, unchecked((int)0xFF000000u));
            WriteI32(dds, 108, 0x1000); // DDSCAPS_TEXTURE
            var rect = new Rectangle(0, 0, w, h);
            var data = b.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    byte* src = (byte*)data.Scan0;
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int si = y * data.Stride + x * 4;
                            int di = 128 + (y * w + x) * 4;
                            // LockBits Format32bppArgb memory order is BGRA; DDS A8R8G8B8 is also BGRA.
                            dds[di + 0] = src[si + 0]; // B
                            dds[di + 1] = src[si + 1]; // G
                            dds[di + 2] = src[si + 2]; // R
                            dds[di + 3] = src[si + 3]; // A
                        }
                }
            }
            finally { b.UnlockBits(data); }
            _ = rect;
            return dds;
        }

        public static byte[] EncodeTgaUncompressed(Bitmap bmp)
        {
            using var b = (Bitmap)bmp.Clone();
            int w = b.Width, h = b.Height;
            byte[] tga = new byte[18 + w * h * 4];
            tga[2] = 2; // uncompressed truecolor
            tga[12] = (byte)(w & 0xFF); tga[13] = (byte)(w >> 8);
            tga[14] = (byte)(h & 0xFF); tga[15] = (byte)(h >> 8);
            tga[16] = 32; tga[17] = 0x88; // 32bpp + top-left origin
            var rect = new Rectangle(0, 0, w, h);
            var data = b.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    byte* src = (byte*)data.Scan0;
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int si = y * data.Stride + x * 4;
                            int di = 18 + (y * w + x) * 4;
                            // Both LockBits (BGRA) and TGA truecolor are BGRA byte order.
                            tga[di + 0] = src[si + 0]; tga[di + 1] = src[si + 1];
                            tga[di + 2] = src[si + 2]; tga[di + 3] = src[si + 3];
                        }
                }
            }
            finally { b.UnlockBits(data); }
            return tga;
        }

        // DDS decode subset: uncompressed A8R8G8B8/X8R8G8B8 + DXT1/DXT3/DXT5.
        private static Bitmap DdsToBitmap(byte[] buf)
        {
            if (buf.Length < 128) throw new InvalidDataException("DDS too small (header).");
            if (System.Text.Encoding.ASCII.GetString(buf, 0, 4) != "DDS ") throw new InvalidDataException("Missing DDS magic.");
            if (BitConverter.ToUInt32(buf, 4) != 124) throw new InvalidDataException("Invalid DDS header size.");
            int h = BitConverter.ToInt32(buf, 12); int w = BitConverter.ToInt32(buf, 16);
            if (w <= 0 || h <= 0 || w > 8192 || h > 8192) throw new InvalidDataException("Invalid DDS dimensions.");
            string fourCc = System.Text.Encoding.ASCII.GetString(buf, 80, 4);
            if (fourCc == "DXT1" || fourCc == "DXT3" || fourCc == "DXT5")
            {
                var rgb = DxtDecode(buf, 128, w, h, fourCc);
                return RgbToBitmap(w, h, rgb);
            }
            // Uncompressed: mask order computed from PF masks.
            uint rMask = BitConverter.ToUInt32(buf, 92), gMask = BitConverter.ToUInt32(buf, 96),
                 bMask = BitConverter.ToUInt32(buf, 100), aMask = BitConverter.ToUInt32(buf, 104);
            var rgb2 = new uint[w * h];
            for (int i = 0; i < w * h; i++)
            {
                uint px = BitConverter.ToUInt32(buf, 128 + i * 4);
                uint r = Extract(px, rMask), g = Extract(px, gMask), b = Extract(px, bMask), a = Extract(px, aMask);
                rgb2[i] = 0xFF000000u | (r << 16) | (g << 8) | b;
                if (aMask != 0 && a == 0) rgb2[i] = 0;
            }
            return RgbToBitmap(w, h, rgb2);
        }
        private static uint Extract(uint px, uint mask)
        {
            if (mask == 0) return 0;
            int shift = 0; while (((mask >> shift) & 1) == 0) shift++;
            uint shifted = mask >> shift;
            int bits = 0; uint m = shifted; while ((m & 1) == 1) { bits++; m >>= 1; }
            uint v = (px & mask) >> shift;
            uint max = bits >= 32 ? uint.MaxValue : (1u << bits) - 1;
            return max == 0 ? 0 : v * 255 / max;
        }
        private static uint[] DxtDecode(byte[] buf, int offset, int w, int h, string fmt)
        {
            var rgb = new uint[w * h];
            int blocks = ((w + 3) / 4) * ((h + 3) / 4);
            int blockSize = fmt == "DXT1" ? 8 : 16;
            for (int blk = 0; blk < blocks; blk++)
            {
                int bx = (blk % ((w + 3) / 4)) * 4, by = (blk / ((w + 3) / 4)) * 4;
                int src = offset + blk * blockSize;
                uint c0 = BitConverter.ToUInt16(buf, src + (fmt == "DXT1" ? 0 : 8)) | ((uint)BitConverter.ToUInt16(buf, src + (fmt == "DXT1" ? 2 : 10)) << 16);
                if (fmt != "DXT1") c0 = BitConverter.ToUInt16(buf, src + 8) | ((uint)BitConverter.ToUInt16(buf, src + 10) << 16);
                uint c1 = BitConverter.ToUInt16(buf, src + (fmt == "DXT1" ? 2 : 10)) | ((uint)0 << 16);
                if (fmt == "DXT1") { c0 = BitConverter.ToUInt16(buf, src); c1 = BitConverter.ToUInt16(buf, src + 2); }
                int bitsOff = fmt == "DXT1" ? src + 4 : src + 12;
                uint bits = BitConverter.ToUInt32(buf, bitsOff);
                uint[] palette = DxtPalette(c0, c1, fmt == "DXT1");
                // Alpha for DXT3/DXT5 simplified: interpolate from explicit alpha block when present.
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                    {
                        int px = bx + x, py = by + y;
                        if (px >= w || py >= h) continue;
                        int idx = (int)((bits >> ((y * 4 + x) * 2)) & 3);
                        uint color = palette[idx];
                        if (fmt == "DXT3") color = AlphaDxt3(buf, src, x, y, color);
                        else if (fmt == "DXT5") color = AlphaDxt5(buf, src, x, y, color);
                        rgb[py * w + px] = color;
                    }
            }
            return rgb;
        }
        private static uint[] DxtPalette(uint c0, uint c1, bool dxt1)
        {
            uint r0 = (c0 >> 11) & 31, g0 = (c0 >> 5) & 63, b0 = c0 & 31;
            uint r1 = (c1 >> 11) & 31, g1 = (c1 >> 5) & 63, b1 = c1 & 31;
            uint[] pal = new uint[4];
            pal[0] = 0xFF000000u | ((r0 * 255 / 31) << 16) | ((g0 * 255 / 63) << 8) | (b0 * 255 / 31);
            pal[1] = 0xFF000000u | ((r1 * 255 / 31) << 16) | ((g1 * 255 / 63) << 8) | (b1 * 255 / 31);
            pal[2] = 0xFF000000u | (((2 * r0 + r1) / 3 * 255 / 31) << 16) | (((2 * g0 + g1) / 3 * 255 / 63) << 8) | (((2 * b0 + b1) / 3 * 255 / 31));
            if (dxt1 && c0 <= c1) pal[3] = 0;
            else pal[3] = 0xFF000000u | (((r0 + 2 * r1) / 3 * 255 / 31) << 16) | (((g0 + 2 * g1) / 3 * 255 / 63) << 8) | (((b0 + 2 * b1) / 3 * 255 / 31));
            return pal;
        }
        private static uint AlphaDxt3(byte[] buf, int src, int x, int y, uint color)
        {
            int p = src + ((y * 4 + x) / 2);
            int a = (buf[p] >> ((y * 4 + x) % 2 * 4)) & 15;
            return (color & 0x00FFFFFFu) | ((uint)(a * 17) << 24);
        }
        private static uint AlphaDxt5(byte[] buf, int src, int x, int y, uint color)
        {
            int a0 = buf[src], a1 = buf[src + 1];
            uint[] pal = new uint[8];
            for (int i = 0; i < 8; i++) pal[i] = (uint)((i < 2 ? (i == 0 ? a0 : a1) : (i < 4 ? ((6 - i) * a0 + (i - 2) * a1) / 4 : ((i - 4) * a0 + (10 - i) * a1) / 4)) * 17 << 24);
            ulong bits = 0; for (int i = 0; i < 6; i++) bits |= (ulong)buf[src + 2 + i] << (i * 8);
            int idx = (int)((bits >> ((y * 4 + x) * 3)) & 7);
            return (color & 0x00FFFFFFu) | pal[idx];
        }
        private static Bitmap RgbToBitmap(int w, int h, uint[] rgb)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);
            try
            {
                unsafe
                {
                    byte* dst = (byte*)data.Scan0;
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            uint rgbv = rgb[y * w + x];
                            int di = y * data.Stride + x * 4;
                            dst[di + 0] = (byte)(rgbv & 0xFF); dst[di + 1] = (byte)((rgbv >> 8) & 0xFF);
                            dst[di + 2] = (byte)((rgbv >> 16) & 0xFF); dst[di + 3] = (byte)((rgbv >> 24) & 0xFF);
                        }
                }
            }
            finally { bmp.UnlockBits(data); }
            return bmp;
        }

        // TGA subset: uncompressed/RLE truecolor 16/24/32 + colormapped 8-bit.
        private static Bitmap TgaToBitmap(byte[] buf)
        {
            if (buf.Length < 18) throw new InvalidDataException("TGA too small.");
            int idLen = buf[0]; int mapType = buf[1]; int imgType = buf[2];
            int mapLen = BitConverter.ToUInt16(buf, 5);
            int mapBpp = buf[7];
            int w = BitConverter.ToUInt16(buf, 12); int h = BitConverter.ToUInt16(buf, 14);
            int bpp = buf[16]; int desc = buf[17];
            if (w <= 0 || h <= 0 || w > 8192 || h > 8192) throw new InvalidDataException("Invalid TGA dimensions.");
            int p = 18 + idLen;
            var palette = new uint[mapLen];
            if (mapType == 1)
            {
                int bytes = (mapBpp + 7) / 8;
                for (int i = 0; i < mapLen; i++)
                {
                    uint v = 0;
                    for (int b = 0; b < bytes && p + b < buf.Length; b++) v |= (uint)buf[p + b] << (b * 8);
                    palette[i] = BgraToArgb(v, bytes);
                    p += bytes;
                }
            }
            bool topOrigin = (desc & 0x20) != 0;
            var rgb = new uint[w * h];
            bool rle = imgType == 10 || imgType == 11;
            if (imgType != 2 && imgType != 1 && !rle) throw new InvalidDataException($"Unsupported TGA image type {imgType}.");
            int bytesPerPixel = (bpp + 7) / 8;
            if (bytesPerPixel <= 0 || bytesPerPixel > 4) throw new InvalidDataException($"Unsupported TGA bpp {bpp}.");
            int total = w * h;
            var pixels = new uint[total];
            if (!rle)
            {
                uint ReadPixel()
                {
                    uint v = 0;
                    for (int b = 0; b < bytesPerPixel; b++) { if (p >= buf.Length) throw new InvalidDataException("TGA pixel data truncated."); v |= (uint)buf[p++] << (b * 8); }
                    if (imgType == 1) return palette[(int)v];
                    return BgraToArgb(v, bytesPerPixel);
                }
                for (int i = 0; i < total; i++) pixels[i] = ReadPixel();
            }
            else
            {
                int i = 0;
                while (i < total)
                {
                    if (p >= buf.Length) throw new InvalidDataException("TGA RLE data truncated.");
                    int count = buf[p++] + 1;
                    bool isPacket = (buf[p - 1] & 0x80) != 0;
                    if (isPacket)
                    {
                        uint v = 0;
                        for (int b = 0; b < bytesPerPixel; b++) { if (p >= buf.Length) throw new InvalidDataException("TGA RLE data truncated."); v |= (uint)buf[p++] << (b * 8); }
                        uint argb = imgType == 11 ? palette[(int)v] : BgraToArgb(v, bytesPerPixel);
                        for (int k = 0; k < count && i < total; k++) pixels[i++] = argb;
                    }
                    else
                    {
                        for (int k = 0; k < count && i < total; k++)
                        {
                            uint v = 0;
                            for (int b = 0; b < bytesPerPixel; b++) { if (p >= buf.Length) throw new InvalidDataException("TGA RLE data truncated."); v |= (uint)buf[p++] << (b * 8); }
                            pixels[i++] = imgType == 11 ? palette[(int)v] : BgraToArgb(v, bytesPerPixel);
                        }
                    }
                }
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int srcY = topOrigin ? y : h - 1 - y;
                    rgb[y * w + x] = pixels[srcY * w + x];
                }
            return RgbToBitmap(w, h, rgb);
        }
        private static uint BgraToArgb(uint v, int bytes)
        {
            if (bytes == 1) { uint g = v * 255 / 255; return 0xFF000000u | (g << 16) | (g << 8) | g; }
            if (bytes == 2) { int r = (int)(((v >> 10) & 31) * 255 / 31), g = (int)(((v >> 5) & 31) * 255 / 31), b = (int)((v & 31) * 255 / 31); return 0xFF000000u | ((uint)(r << 16)) | ((uint)(g << 8)) | (uint)b; }
            if (bytes == 3) { uint b = v & 0xFF, g = (v >> 8) & 0xFF, r = (v >> 16) & 0xFF; return 0xFF000000u | (r << 16) | (g << 8) | b; }
            return ((v >> 24) << 24) | (((v >> 16) & 0xFF) << 16) | (((v >> 8) & 0xFF) << 8) | (v & 0xFF);
        }
        private static void WriteI32(byte[] d, int o, int v) => BitConverter.GetBytes(v).CopyTo(d, o);
    }
}

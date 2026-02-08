// Biohazard/FileEditor/FileEncoding.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Tool_Hazard.Biohazard.FileEditor
{
    public sealed class FileEncoding
    {
        public sealed record Entry(int CodePoint, int Encode, int Width, int Indent);

        private readonly List<Entry> _tableSortedByCodePoint = new();
        private int[] _widthByEncode = Array.Empty<int>();
        private int _unknownEncode;

        public int DefaultWidth { get; private set; } = 8;
        public int DefaultIndent { get; private set; } = 0;

        public static FileEncoding Load(string xmlPath)
        {
            var enc = new FileEncoding();
            enc.LoadFromXml(xmlPath);
            return enc;
        }

        public void LoadFromXml(string xmlPath)
        {
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException("encoding.xml not found", xmlPath);

            var doc = XDocument.Load(xmlPath, LoadOptions.None);
            var root = doc.Root;
            if (root is null || !string.Equals(root.Name.LocalName, "Encoding", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid encoding.xml: root element must be <Encoding>.");

            DefaultWidth = ReadIntAttr(root, "width", 8);
            DefaultIndent = ReadIntAttr(root, "indent", 0);

            var entries = new List<Entry>();
            int highestEncode = 0;

            foreach (var e in root.Elements().Where(x => string.Equals(x.Name.LocalName, "Entry", StringComparison.OrdinalIgnoreCase)))
            {
                var chStr = (string?)e.Attribute("Char") ?? "";
                if (string.IsNullOrEmpty(chStr))
                    continue;

                var codePoint = FirstUnicodeScalar(chStr);
                var encode = ReadIntAttr(e, "Encode", 0);

                var w = ReadIntAttr(e, "width", DefaultWidth);
                var ind = ReadIntAttr(e, "indent", DefaultIndent);

                entries.Add(new Entry(codePoint, encode, w, ind));
                if (encode > highestEncode) highestEncode = encode;
            }

            _tableSortedByCodePoint.Clear();
            _tableSortedByCodePoint.AddRange(entries.OrderBy(x => x.CodePoint));

            // Unknown symbol: prefer '?', otherwise space, otherwise 0
            _unknownEncode = FindEncode((int)'?');
            if (_unknownEncode == 0 && !entries.Any(x => x.CodePoint == '?'))
            {
                var sp = FindEncode((int)' ');
                _unknownEncode = sp != 0 ? sp : 0;
            }

            var size = Math.Max(highestEncode, entries.Count) + 1;
            _widthByEncode = new int[size];
            for (int i = 0; i < size; i++)
                _widthByEncode[i] = DefaultWidth;

            foreach (var en in entries)
            {
                if ((en.Encode >> 8) >= 0xEE)
                    continue;

                var idx = en.Encode & 0xFFFF;
                if ((uint)idx < (uint)_widthByEncode.Length)
                    _widthByEncode[idx] = en.Width;
            }
        }

        public int FindEncode(int codePoint)
        {
            int lo = 0;
            int hi = _tableSortedByCodePoint.Count - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) / 2);
                int midCp = _tableSortedByCodePoint[mid].CodePoint;
                if (midCp < codePoint) lo = mid + 1;
                else if (midCp > codePoint) hi = mid - 1;
                else return _tableSortedByCodePoint[mid].Encode;
            }
            return _unknownEncode;
        }

        public int FindEncode(char ch) => FindEncode((int)ch);

        public int GetWidthByEncode(int encode)
        {
            var idx = encode & 0xFFFF;
            if ((uint)idx < (uint)_widthByEncode.Length)
                return _widthByEncode[idx];
            return DefaultWidth;
        }

        private static int ReadIntAttr(XElement el, string name, int fallback)
        {
            var a = (string?)el.Attribute(name);
            if (a is null) return fallback;
            if (TryParseInt(a, out var v))
                return v;
            return fallback;
        }

        private static bool TryParseInt(string s, out int value)
        {
            s = s.Trim();

            // hex with 0x prefix
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

            // decimal
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;

            // hex without 0x (if it contains A-F)
            bool looksHex = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'))
                {
                    looksHex = true;
                    break;
                }
            }
            if (looksHex)
                return int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

            value = 0;
            return false;
        }

        private static int FirstUnicodeScalar(string s)
        {
            if (string.IsNullOrEmpty(s)) return '?';
            var span = s.AsSpan();
            if (char.IsHighSurrogate(span[0]) && span.Length >= 2 && char.IsLowSurrogate(span[1]))
                return char.ConvertToUtf32(span[0], span[1]);
            return span[0];
        }
    }
}

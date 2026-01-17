using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Tool_Hazard.Biohazard.MSG
{
    /// <summary>
    /// Loads a Classic REbirth-style encoding.xml mapping of byte->string.
    /// Schema tolerant: tries common attribute names and/or element text.
    /// </summary>
    public sealed class EncodingDictionary
    {
        public IReadOnlyDictionary<byte, string> ByteToText => _byteToText;
        public IReadOnlyDictionary<string, byte> TextToByte => _textToByte;

        private readonly Dictionary<byte, string> _byteToText = new();
        private readonly Dictionary<string, byte> _textToByte = new(StringComparer.Ordinal);

        // For longest-match encoding
        public int MaxTokenLength { get; private set; } = 1;

        public string? SourcePath { get; private set; }

        public static EncodingDictionary Load(string xmlPath)
        {
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException("Encoding XML not found.", xmlPath);

            var doc = XDocument.Load(xmlPath, LoadOptions.PreserveWhitespace);
            var dict = new EncodingDictionary { SourcePath = xmlPath };
            dict.Parse(doc);
            dict.BuildReverseMap();
            return dict;
        }

        private void Parse(XDocument doc)
        {
            var elements = doc.Descendants().ToList();

            foreach (var el in elements)
            {
                // Case-insensitive attribute fetch
                string? GetAttr(params string[] names)
                {
                    foreach (var a in el.Attributes())
                    {
                        foreach (var n in names)
                        {
                            if (string.Equals(a.Name.LocalName, n, StringComparison.OrdinalIgnoreCase))
                                return a.Value;
                        }
                    }
                    return null;
                }

                // Your file: Encode + Char
                var byteStr = GetAttr("Encode", "Byte", "B", "Code", "Id", "Value");
                if (byteStr is null) continue;

                if (!TryParseByte(byteStr, out var b))
                    continue;

                var tokenStr = GetAttr("Char", "C", "Text", "Glyph", "String");
                var token = tokenStr != null
                    ? UnescapeToken(tokenStr)
                    : UnescapeToken((el.Value ?? string.Empty).Trim('\r', '\n'));

                if (string.IsNullOrEmpty(token))
                    continue;

                _byteToText[b] = token;
            }

            if (_byteToText.Count == 0)
                throw new InvalidDataException("No valid byte->text mappings were found in the XML.");
        }

        private void BuildReverseMap()
        {
            _textToByte.Clear();
            MaxTokenLength = 1;

            // Prefer 1:1 where possible; if duplicates exist, last wins.
            foreach (var kv in _byteToText)
            {
                var token = kv.Value;
                _textToByte[token] = kv.Key;
                if (token.Length > MaxTokenLength)
                    MaxTokenLength = token.Length;
            }
        }

        private static bool TryParseByte(string s, out byte b)
        {
            s = s.Trim();

            // Accept:
            //  - "0x41"
            //  - "41" (hex or dec; we’ll try dec first, then hex)
            //  - "65"
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return byte.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
            }

            // decimal first
            if (byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out b))
                return true;

            // then hex
            return byte.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
        }

        private static string UnescapeToken(string token)
        {
            // Common escapes used in these encoding files
            // (safe to keep minimal; you can expand later if needed)
            return token
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t");
        }
    }
}

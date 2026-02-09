using System.Text;

namespace Tool_Hazard.Biohazard.MSG
{
    public static class MsgCodec
    {
        public static string Decode(byte[] data, EncodingDictionary dict, bool stopAtZero = false)
        {
            var sb = new StringBuilder(data.Length);

            foreach (var b in data)
            {
                if (stopAtZero && b == 0x00)
                    break;

                if (dict.ByteToText.TryGetValue(b, out var token))
                    sb.Append(token);
                else
                    sb.Append($"<{b:X2}>"); // visible fallback instead of losing info
            }

            return sb.ToString();
        }

        public static byte[] Encode(string text, EncodingDictionary dict)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));

            var bytes = new List<byte>(text.Length);

            // Longest-match encoding (supports multi-char tokens if dictionary uses them)
            int i = 0;
            while (i < text.Length)
            {
                byte b;

                // Greedy: try the longest possible token first
                var maxLen = Math.Min(dict.MaxTokenLength, text.Length - i);
                bool matched = false;

                for (int len = maxLen; len >= 1; len--)
                {
                    var slice = text.Substring(i, len);
                    if (dict.TextToByte.TryGetValue(slice, out b))
                    {
                        bytes.Add(b);
                        i += len;
                        matched = true;
                        break;
                    }
                }

                if (matched)
                    continue;

                // If user left our "<AF>" style fallback tokens, allow encoding them back
                // Format: <XX> where XX is hex.
                if (TryConsumeHexToken(text, i, out b, out int consumed))
                {
                    bytes.Add(b);
                    i += consumed;
                    continue;
                }

                // Unknown character/token
                var ch = text[i];
                throw new InvalidDataException(
                    $"No encoding mapping for character/token at position {i}: U+{(int)ch:X4} '{ch}'.");
            }

            return bytes.ToArray();
        }

        private static bool TryConsumeHexToken(string text, int index, out byte b, out int consumed)
        {
            b = 0;
            consumed = 0;

            // <XX>
            if (index + 4 > text.Length) return false;
            if (text[index] != '<' || text[index + 3] != '>') return false;

            var hex = text.Substring(index + 1, 2);
            if (!byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out b))
                return false;

            consumed = 4;
            return true;
        }
    }
}

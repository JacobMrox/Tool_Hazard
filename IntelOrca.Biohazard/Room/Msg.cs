using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace IntelOrca.Biohazard.Room
{
    public readonly struct Msg
    {
        private const string EnTable = " .___()__“”_0123456789:_,\"!?_ABCDEFGHIJKLMNOPQRSTUVWXYZ[/]'-_abcdefghijklmnopqrstuvwxyz_________";
        private const byte Green = 0xF9;
        private const byte StartText = 0xFA;
        private const byte YesNoQuestion = 0xFB;
        private const byte LineBreak = 0xFC;
        private const byte PageBreak = 0xFD;
        private const byte EndText = 0xFE;

        public BioVersion Version { get; }
        public MsgLanguage Language { get; }
        public ReadOnlyMemory<byte> Data { get; }

        public Msg(BioVersion version, MsgLanguage language, ReadOnlyMemory<byte> data)
        {
            Version = version;
            Language = language;
            Data = data;
        }

        public Msg(BioVersion version, MsgLanguage language, string s)
        {
            Version = version;
            Language = language;
            Data = Create(s);
        }

        private static ReadOnlyMemory<byte> Create(string s)
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write(StartText);
            bw.Write((byte)2);
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '@')
                {
                    var p = 0x40;
                    if (i < s.Length - 2)
                    {
                        var h = new string(new char[] { s[++i], s[++i] });
                        if (int.TryParse(h, NumberStyles.HexNumber, null, out var result))
                        {
                            p = result;
                        }
                    }
                    bw.Write(LineBreak);
                    bw.Write(YesNoQuestion);
                    bw.Write((byte)p);
                }
                else if (c == '\n')
                {
                    bw.Write(LineBreak);
                }
                else if (c == '#')
                {
                    bw.Write(PageBreak);
                    bw.Write((byte)0x00);
                }
                else if (c == '{')
                {
                    bw.Write(Green);
                    bw.Write((byte)0x01);
                }
                else if (c == '}')
                {
                    bw.Write(Green);
                    bw.Write((byte)0x00);
                }
                else
                {
                    var index = EnTable.IndexOf(c);
                    if (index == -1)
                        bw.Write((byte)0);
                    else
                        bw.Write((byte)index);
                }
            }
            bw.Write(EndText);
            bw.Write((byte)0);
            return ms.ToArray();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            var span = Data.Span;
            for (var i = 0; i < span.Length; i++)
            {
                var b = span[i];
                switch (b)
                {
                    case Green:
                    {
                        var p = span[++i];
                        if (p == 1)
                            sb.Append('{');
                        else
                            sb.Append('}');
                        break;
                    }
                    case StartText:
                        i++;
                        break;
                    case YesNoQuestion:
                    {
                        var p = span[++i];
                        sb.Append('@');
                        sb.Append(p.ToString("X2"));
                        break;
                    }
                    case LineBreak:
                        sb.Append('\n');
                        break;
                    case PageBreak:
                        sb.Append('#');
                        i++;
                        break;
                    case EndText:
                        i = span.Length - 1;
                        break;
                    default:
                        if (b < EnTable.Length)
                        {
                            sb.Append(EnTable[b]);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}

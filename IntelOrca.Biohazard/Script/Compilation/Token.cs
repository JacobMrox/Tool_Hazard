using System.Diagnostics;

namespace IntelOrca.Biohazard.Script.Compilation
{
    [DebuggerDisplay("{Kind} | {Text}")]
    internal readonly struct Token
    {
        private readonly string _content;
        private readonly int _offset;

        public TokenKind Kind { get; }
        public string Path { get; }
        public short Line { get; }
        public short Column { get; }
        public short Length { get; }

        public string Text => _content.Substring(_offset, Length);

        public Token(TokenKind kind, string path, int line, int column)
        {
            _content = "";
            _offset = 0;
            Kind = kind;
            Path = path;
            Line = (short)line;
            Column = (short)column;
            Length = 0;
        }

        public Token(string content, int offset, TokenKind kind, string path, int line, int column, int length)
        {
            _content = content;
            _offset = offset;
            Path = path;
            Kind = kind;
            Line = (short)line;
            Column = (short)column;
            Length = (short)length;
        }
    }
}

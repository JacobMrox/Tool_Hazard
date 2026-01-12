using System.Collections.Generic;

namespace IntelOrca.Biohazard.Script.Compilation
{
    internal abstract class LexerBase
    {
        private string _s = "";
        private int _sIndex;

        private int _offset;
        private int _line;
        private int _column;

        protected IFileIncluder Includer { get; }
        protected string Path { get; private set; } = "";
        protected int CurrentTokenLength => _sIndex - _offset;

        public ErrorList Errors { get; }

        public LexerBase(IFileIncluder includer, ErrorList errors)
        {
            Includer = includer;
            Errors = errors;
        }

        public IEnumerable<Token> GetTokens(string path)
        {
            var content = Includer.GetContent(path);
            if (content == null)
            {
                Errors.AddError(path, 0, 0, ErrorCodes.FileNotFound, string.Format(ErrorCodes.GetMessage(ErrorCodes.FileNotFound), path));
                return new Token[0];
            }
            return GetTokens(path, content);
        }

        private IEnumerable<Token> GetTokens(string path, string script)
        {
            Path = path;
            _s = script;

            while (true)
            {
                var c = PeekChar();
                if (c == char.MinValue)
                {
                    yield return new Token(TokenKind.EOF, Path, _line, _column);
                    break;
                }
                else
                {
                    var token = GetNextToken();
                    ValidateToken(in token);
                    yield return token;
                }
            }
        }

        protected virtual void Begin() { }
        protected abstract Token GetNextToken();
        protected abstract bool ValidateToken(in Token token);

        protected void EmitError(in Token token, int code, params object[] args)
        {
            Errors.AddError(token.Path, token.Line, token.Column, code, string.Format(ErrorCodes.GetMessage(code), args));
        }

        protected char GetLastReadChar()
        {
            return _s[_sIndex - 1];
        }

        protected Token CreateToken(TokenKind kind)
        {
            var length = _sIndex - _offset;
            var token = new Token(_s, _offset, kind, Path, _line, _column, length);
            if (kind == TokenKind.NewLine)
            {
                _line++;
                _column = 0;
            }
            else
            {
                _column += length;
            }
            _offset = _sIndex;
            return token;
        }

        protected bool ParseNewLine()
        {
            var c = PeekChar();
            if (c != '\n' && c != '\r')
                return false;

            c = ReadChar();
            if (c == '\r')
            {
                c = PeekChar();
                if (c == '\n')
                {
                    ReadChar();
                }
            }
            return true;
        }

        protected bool ParseWhitespace()
        {
            var c = PeekChar();
            if (!char.IsWhiteSpace(c) || IsNewLine(c))
                return false;
            do
            {
                ReadChar();
                c = PeekChar();
            } while (c != char.MinValue && char.IsWhiteSpace(c) && !IsNewLine(c));
            return true;
        }

        private static bool IsNewLine(char c) => c == '\r' || c == '\n';

        protected bool ParseString()
        {
            var c = PeekChar();
            if (c != '"')
                return false;
            ReadChar();

            var escaped = false;
            while (PeekChar() != '\0')
            {
                c = ReadChar();
                if (!escaped && c == '"')
                    break;
                escaped = c == '\\';
            }
            return true;
        }

        protected bool ParseNumber()
        {
            var c = PeekChar();
            if ((c < '0' || c > '9') && c != '-')
                return false;

            if (c == '-')
            {
                c = PeekChar(skip: 1);
                if (c < '0' || c > '9')
                    return false;
                c = ReadChar();
            }

            while (true)
            {
                c = PeekChar();
                if (!char.IsLetterOrDigit(c))
                {
                    break;
                }
                ReadChar();
            }
            return true;
        }

        protected bool Parse(char c)
        {
            if (PeekChar() != c)
                return false;

            ReadChar();
            return true;
        }

        protected bool Parse(string s)
        {
            var remaining = _s.Length - _sIndex;
            if (remaining < s.Length)
                return false;

            if (string.Compare(_s, _sIndex, s, 0, s.Length) != 0)
                return false;

            // If we are parsing a keyword, we must not have a letter/digit straight after
            var nextIndex = _sIndex + s.Length;
            if (_s.Length > nextIndex && char.IsLetter(s[0]) && (char.IsLetterOrDigit(_s[nextIndex]) || _s[nextIndex] == '_'))
                return false;

            _sIndex += s.Length;
            return true;
        }

        protected bool ParseAny()
        {
            ReadChar();
            return true;
        }

        protected char PeekChar(int skip = 0)
        {
            var offset = _sIndex + skip;
            if (offset >= _s.Length)
                return char.MinValue;
            return _s[offset];
        }

        protected char PeekNonWhitespaceChar()
        {
            var i = 0;
            while (true)
            {
                var c = PeekChar(i);
                if (c == '\0' || !char.IsWhiteSpace(c))
                {
                    return c;
                }
                i++;
            }
        }

        protected char ReadChar()
        {
            var result = PeekChar();
            _sIndex++;
            return result;
        }

        protected char GetLastChar()
        {
            if (_sIndex == 0)
                return char.MinValue;
            return _s[_sIndex - 1];
        }
    }
}

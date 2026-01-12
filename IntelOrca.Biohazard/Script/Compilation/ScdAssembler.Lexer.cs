using System;

namespace IntelOrca.Biohazard.Script.Compilation
{
    public partial class ScdAssembler
    {
        private class Lexer : LexerBase
        {
            private bool _expectingOpcode;

            public Lexer(IFileIncluder includer, ErrorList errors)
                : base(includer, errors)
            {
            }

            protected override void Begin()
            {
                _expectingOpcode = true;
            }

            protected override Token GetNextToken()
            {
                if (ParseNewLine())
                {
                    _expectingOpcode = true;
                    return CreateToken(TokenKind.NewLine);
                }
                if (ParseWhitespace())
                    return CreateToken(TokenKind.Whitespace);
                if (ParseComment())
                    return CreateToken(TokenKind.Comment);
                if (ParseDirective())
                {
                    _expectingOpcode = false;
                    return CreateToken(TokenKind.Directive);
                }
                if (ParseNumber())
                    return CreateToken(TokenKind.Number);
                if (ParseSymbol())
                {
                    if (GetLastChar() == ':')
                    {
                        return CreateToken(TokenKind.Label);
                    }
                    else
                    {
                        if (_expectingOpcode)
                        {
                            _expectingOpcode = false;
                            return CreateToken(TokenKind.Opcode);
                        }
                        else
                        {
                            return CreateToken(TokenKind.Symbol);
                        }
                    }
                }
                if (ParseOperator())
                {
                    var length = CurrentTokenLength;
                    if (length == 1)
                    {
                        var ch = GetLastReadChar();
                        if (ch == ',')
                            return CreateToken(TokenKind.Comma);
                        else if (ch == '+')
                            return CreateToken(TokenKind.Plus);
                        else if (ch == '-')
                            return CreateToken(TokenKind.Minus);
                        else if (ch == '|')
                            return CreateToken(TokenKind.Pipe);
                    }
                    return CreateToken(TokenKind.Unknown);
                }
                throw new Exception();
            }

            protected override bool ValidateToken(in Token token)
            {
                if (token.Kind == TokenKind.Number)
                {
                }
                else if (token.Kind == TokenKind.Comma)
                {
                    if (token.Text != ",")
                    {
                        EmitError(in token, ErrorCodes.InvalidOperator, token.Text);
                        return false;
                    }
                }
                return true;
            }

            private bool ParseComment()
            {
                var c = PeekChar();
                if (c != ';')
                    return false;
                do
                {
                    ReadChar();
                    c = PeekChar();
                } while (c != char.MinValue && c != '\r' && c != '\n');
                return true;
            }

            private bool ParseDirective()
            {
                var c = PeekChar();
                if (c != '.')
                    return false;
                do
                {
                    ReadChar();
                } while (!PeekSeparator());
                return true;
            }

            private bool ParseSymbol()
            {
                if (PeekSeparator())
                    return false;

                do
                {
                    ReadChar();
                } while (!PeekSeparator());
                return true;
            }

            private bool ParseOperator()
            {
                do
                {
                    ReadChar();
                } while (!PeekSeparator());
                return true;
            }

            private bool PeekSeparator()
            {
                var c = PeekChar();
                if (c == '_' || c == ':' || char.IsLetterOrDigit(c))
                    return false;
                return true;
            }
        }
    }
}

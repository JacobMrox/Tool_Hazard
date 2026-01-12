using System;
using System.Collections.Generic;
using System.Linq;

namespace IntelOrca.Biohazard.Script.Compilation
{
    public partial class ScdCompiler
    {

        private class Lexer
        {
            private readonly Dictionary<string, Macro> _macros = new Dictionary<string, Macro>();
            private readonly IFileIncluder _includer;
            private readonly ErrorList _errors;
            private readonly HashSet<Macro> _cycleDetection = new HashSet<Macro>();

            public Lexer(IFileIncluder includer, ErrorList errors)
            {
                _includer = includer;
                _errors = errors;
            }

            public IEnumerable<Token> GetTokens(string path)
            {
                var scanner = new Scanner(_includer, _errors);
                var reader = new TokenReader(scanner.GetTokens(path));
                var processors = new Func<string, TokenReader, IEnumerable<Token>?>[]
                {
                    ProcessInclude,
                    ProcessDefine,
                    ProcessSymbol,
                    ProcessPassThrough,
                };
                while (true)
                {
                    if (reader.Peek().Kind == TokenKind.EOF)
                    {
                        yield return reader.Read();
                        break;
                    }
                    foreach (var pp in processors)
                    {
                        var expandedTokens = pp(path, reader);
                        if (expandedTokens != null)
                        {
                            foreach (var t in expandedTokens)
                            {
                                yield return t;
                            }
                            break;
                        }
                    }
                }
            }

            private IEnumerable<Token> Expand(string path, IEnumerable<Token> tokens)
            {
                var reader = new TokenReader(tokens);
                var processors = new Func<string, TokenReader, IEnumerable<Token>?>[]
                {
                    ProcessSymbol,
                    ProcessPassThrough,
                };
                while (reader.Peek().Kind != TokenKind.EOF)
                {
                    foreach (var pp in processors)
                    {
                        var expandedTokens = pp(path, reader);
                        if (expandedTokens != null)
                        {
                            foreach (var t in expandedTokens)
                            {
                                yield return t;
                            }
                            break;
                        }
                    }
                }
            }

            private IEnumerable<Token>? ProcessInclude(string path, TokenReader reader)
            {
                var token = reader.Peek();
                if (token.Kind != TokenKind.Directive || token.Text != "#include")
                    return null;

                reader.Read();
                reader.SkipWhitespace();
                var pathToken = reader.Peek();
                if (pathToken.Kind != TokenKind.String)
                {
                    EmitError(in pathToken, ErrorCodes.ExpectedPath);
                    return new Token[0];
                }
                else
                {
                    reader.Read();
                    var includePath = pathToken.Text.Substring(1, pathToken.Text.Length - 2);
                    var fullPath = _includer.GetIncludePath(path, includePath);
                    return GetTokens(fullPath).TakeWhile(t => t.Kind != TokenKind.EOF);
                }
            }

            private IEnumerable<Token>? ProcessDefine(string path, TokenReader reader)
            {
                var token = reader.Peek();
                if (token.Kind != TokenKind.Directive || token.Text != "#define")
                    return null;

                reader.Read();
                reader.SkipWhitespace();
                var nameToken = reader.Peek();
                if (nameToken.Kind != TokenKind.Symbol)
                {
                    EmitError(in nameToken, ErrorCodes.ExpectedMacroName);
                    return new Token[0];
                }
                reader.Read();

                var macroName = nameToken.Text;
                var parameters = ReadMacroParameters(reader);
                var tokens = ReadMacroBody(reader);
                _macros.Add(macroName, new Macro(macroName, parameters, tokens));
                return new Token[0];
            }

            private string[] ReadMacroParameters(TokenReader reader)
            {
                reader.SkipWhitespace();
                var t = reader.Peek();
                if (t.Kind != TokenKind.OpenParen)
                {
                    return new string[0];
                }
                reader.Read();

                var pList = new List<string>();
                while (true)
                {
                    reader.SkipWhitespace();
                    var pToken = reader.Peek();
                    if (pToken.Kind == TokenKind.CloseParen)
                    {
                        reader.Read();
                        break;
                    }
                    else if (pToken.Kind != TokenKind.Symbol)
                    {
                        EmitError(in t, ErrorCodes.ExpectedOperand);
                        return new string[0];
                    }
                    reader.Read();
                    pList.Add(pToken.Text);

                    reader.SkipWhitespace();
                    var cToken = reader.Peek();
                    if (cToken.Kind == TokenKind.Comma)
                    {
                        reader.Read();
                    }
                    else if (cToken.Kind != TokenKind.CloseParen)
                    {
                        EmitError(in t, ErrorCodes.ExpectedOperand);
                        return new string[0];
                    }
                }
                return pList.ToArray();
            }

            private Token[] ReadMacroBody(TokenReader reader)
            {
                reader.SkipWhitespace();
                var tokens = new List<Token>();
                var continueLine = false;
                while (true)
                {
                    var t = reader.Peek();
                    if (t.Kind == TokenKind.EOF)
                    {
                        if (continueLine)
                        {
                            EmitError(in t, ErrorCodes.InvalidExpression);
                        }
                        break;
                    }
                    else if (t.Kind == TokenKind.BackSlash)
                    {
                        reader.Read();
                        continueLine = true;
                    }
                    else if (t.Kind == TokenKind.Comment)
                    {
                        reader.Read();
                    }
                    else if (t.Kind == TokenKind.NewLine)
                    {
                        if (continueLine)
                        {
                            reader.Read();
                            continueLine = false;
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        reader.Read();
                        tokens.Add(t);
                    }
                }

                // Trim whitespace tokens
                while (tokens.Count != 0 && tokens[tokens.Count - 1].Kind == TokenKind.Whitespace)
                {
                    tokens.RemoveAt(tokens.Count - 1);
                }
                return tokens.ToArray();
            }

            private IEnumerable<Token>? ProcessSymbol(string path, TokenReader reader)
            {
                var token = reader.Peek();
                if (token.Kind != TokenKind.Symbol)
                    return null;

                if (!_macros.TryGetValue(token.Text, out var macro))
                    return null;

                if (!_cycleDetection.Add(macro))
                {
                    EmitError(in token, ErrorCodes.RecursiveMacro);
                    return null;
                }

                try
                {

                    reader.Read();
                    reader.SkipWhitespace();
                    var pToken = reader.Peek();
                    if (pToken.Kind != TokenKind.OpenParen)
                    {
                        if (macro.Parameters.Length == 0)
                        {
                            return Expand(path, macro.Tokens);
                        }
                        else
                        {
                            EmitError(in pToken, ErrorCodes.ExpectedOperand);
                            return new Token[0];
                        }
                    }
                    reader.Read();

                    var nestLevel = 1;
                    var arguments = new List<Token[]>();
                    var argument = new List<Token>();
                    while (true)
                    {
                        var t = reader.Read();
                        if (t.Kind == TokenKind.OpenParen)
                        {
                            argument.Add(t);
                            nestLevel++;
                        }
                        else if (t.Kind == TokenKind.CloseParen)
                        {
                            nestLevel--;
                            if (nestLevel == 0)
                                break;
                            else
                                argument.Add(t);
                        }
                        else if (t.Kind == TokenKind.Comma && nestLevel == 1)
                        {
                            arguments.Add(argument.ToArray());
                            argument.Clear();
                        }
                        else
                        {
                            argument.Add(t);
                        }
                    }
                    if (argument.Count != 0)
                    {
                        arguments.Add(argument.ToArray());
                    }
                    argument.Clear();

                    if (arguments.Count != macro.Parameters.Length)
                    {
                        EmitError(in token, ErrorCodes.IncorrectNumberOfOperands);
                        return new Token[0];
                    }
                    return Expand(path, macro.GetTokens(arguments.ToArray()));
                }
                finally
                {
                    _cycleDetection.Remove(macro);
                }
            }

            private IEnumerable<Token>? ProcessPassThrough(string path, TokenReader reader)
            {
                yield return reader.Read();
            }

            protected void EmitError(in Token token, int code, params object[] args)
            {
                _errors.AddError(token.Path, token.Line, token.Column, code, string.Format(ErrorCodes.GetMessage(code), args));
            }

            private class Macro
            {
                public string Name { get; }
                public string[] Parameters { get; }
                public Token[] Tokens { get; }

                public Macro(string name, string[] parameters, Token[] tokens)
                {
                    Name = name;
                    Parameters = parameters;
                    Tokens = tokens;
                }

                public IEnumerable<Token> GetTokens(Token[][] arguments)
                {
                    foreach (var token in Tokens)
                    {
                        if (token.Kind == TokenKind.Symbol)
                        {
                            var parameterIndex = FindParameter(token.Text);
                            if (parameterIndex != -1)
                            {
                                foreach (var t in arguments[parameterIndex])
                                {
                                    yield return t;
                                }
                                continue;
                            }
                        }
                        yield return token;
                    }
                }

                private int FindParameter(string name)
                {
                    return Array.IndexOf(Parameters, name);
                }
            }
        }
    }
}

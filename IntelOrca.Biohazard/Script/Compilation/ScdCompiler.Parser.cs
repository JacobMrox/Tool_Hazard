using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IntelOrca.Biohazard.Model;

namespace IntelOrca.Biohazard.Script.Compilation
{
    public partial class ScdCompiler
    {
        private class Parser
        {
            private readonly ErrorList _errors;
            private Token[] _tokens = new Token[0];
            private int _tokenIndex;

            public Parser(ErrorList errors)
            {
                _errors = errors;
            }

            public SyntaxTree BuildSyntaxTree(Token[] tokens)
            {
                _tokens = tokens
                    .Where(x => !IsTrivialToken(x.Kind))
                    .ToArray();
                var parsers = new Func<SyntaxNode?>[]
                {
                    ParseDirective,
                    ParseProcedure,
                };

                var nodes = new List<SyntaxNode>();
                while (!ParseToken(TokenKind.EOF))
                {
                    var foundParser = false;
                    foreach (var parser in parsers)
                    {
                        var node = parser();
                        if (node != null)
                        {
                            nodes.Add(node);
                            foundParser = true;
                            break;
                        }
                    }
                    if (!foundParser)
                    {
                        if (_errors.ErrorCount == 0)
                        {
                            EmitError(in PeekToken(), ErrorCodes.InvalidSyntax);
                        }
                        break;
                    }
                }
                return new SyntaxTree(new BlockSyntaxNode(nodes.ToArray()));
            }

            private static bool IsTrivialToken(TokenKind kind)
            {
                return kind == TokenKind.NewLine ||
                    kind == TokenKind.Whitespace ||
                    kind == TokenKind.Comment;
            }

            private ref readonly Token PeekToken()
            {
                return ref _tokens[_tokenIndex];
            }

            private ref readonly Token ReadToken()
            {
                ref var token = ref _tokens[_tokenIndex];
                if (token.Kind != TokenKind.EOF)
                    _tokenIndex++;
                return ref token;
            }

            private ref readonly Token LastToken => ref _tokens[_tokenIndex - 1];

            private bool ParseToken(TokenKind kind)
            {
                ref readonly var token = ref PeekToken();
                if (token.Kind != kind)
                    return false;

                ReadToken();
                return true;
            }

            private SyntaxNode? ParseDirective()
            {
                if (!ParseToken(TokenKind.Directive))
                    return null;

                ref readonly var lastToken = ref LastToken;
                var directive = lastToken.Text;
                if (directive == "#version")
                {
                    if (!ParseToken(TokenKind.Number))
                    {
                        EmitError(in lastToken, ErrorCodes.ExpectedScdVersionNumber);
                        return null;
                    }
                    return new VersionSyntaxNode(LastToken);
                }
                else if (directive == "#message")
                {
                    if (!ParseToken(TokenKind.Number))
                    {
                        EmitError(in lastToken, ErrorCodes.ExpectedOperand);
                        return null;
                    }
                    var id = int.Parse(LastToken.Text);
                    if (!ParseToken(TokenKind.String))
                    {
                        EmitError(in lastToken, ErrorCodes.ExpectedOperand);
                        return null;
                    }
                    var text = ConvertStringToken(in LastToken);
                    return new MessageTextSyntaxNode(id, text);
                }
                else if (directive == "#animation")
                {
                    if (!ParseToken(TokenKind.Number))
                    {
                        EmitError(in lastToken, ErrorCodes.ExpectedOperand);
                        return null;
                    }
                    var id = int.Parse(LastToken.Text);

                    if (!ParseToken(TokenKind.Number))
                    {
                        EmitError(in lastToken, ErrorCodes.ExpectedOperand);
                        return null;
                    }
                    var flags = (EmrFlags)int.Parse(LastToken.Text);

                    if (!ParseToken(TokenKind.String))
                    {
                        EmitError(in lastToken, ErrorCodes.ExpectedOperand);
                        return null;
                    }
                    var text = ConvertStringToken(in LastToken);
                    return new AnimationSyntaxNode(id, flags, text);
                }
                else if (directive == "#object")
                {
                    if (!ParseToken(TokenKind.Number))
                    {
                        EmitError(in lastToken, ErrorCodes.ExpectedOperand);
                        return null;
                    }
                    var id = int.Parse(LastToken.Text);

                    if (!ParseToken(TokenKind.String))
                    {
                        EmitError(in lastToken, ErrorCodes.ExpectedOperand);
                        return null;
                    }
                    var text = ConvertStringToken(in LastToken);
                    return new ObjectSyntaxNode(id, text);
                }
                else
                {
                    EmitError(in lastToken, ErrorCodes.UnknownDirective, lastToken.Text);
                    return null;
                }
            }

            private string ConvertStringToken(in Token token)
            {
                var s = token.Text;
                var sb = new StringBuilder();
                for (var i = 1; i < s.Length - 1; i++)
                {
                    var c = s[i];
                    if (c == '\\')
                    {
                        c = s[++i];
                        if (c == 'n')
                        {
                            sb.Append('\n');
                        }
                        else
                        {
                            EmitError(in token, ErrorCodes.InvalidOperand);
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                return sb.ToString();
            }

            private ProcedureSyntaxNode? ParseProcedure()
            {
                if (!ParseToken(TokenKind.Proc))
                    return null;

                if (!ParseToken(TokenKind.Symbol))
                {
                    EmitError(in LastToken, ErrorCodes.ExpectedProcedureName);
                    return null;
                }

                ref readonly var nameToken = ref LastToken;
                var block = ParseExpectedBlock();
                return new ProcedureSyntaxNode(nameToken, block);
            }

            private BlockSyntaxNode? ParseBlock()
            {
                var token = PeekToken();
                if (token.Kind != TokenKind.OpenBlock)
                    return null;

                ReadToken();
                var nodes = new List<SyntaxNode>();
                SyntaxNode? opcode;
                while ((opcode = ParseStatement()) != null)
                {
                    nodes.Add(opcode);
                }
                ParseExpected(TokenKind.CloseBlock);
                return new BlockSyntaxNode(nodes.ToArray());
            }

            private BlockSyntaxNode? ParseExpectedBlock()
            {
                if (PeekToken().Kind != TokenKind.OpenBlock)
                {
                    ParseExpected(TokenKind.OpenBlock);
                    return null;
                }
                return ParseBlock();
            }

            private SyntaxNode? ParseStatement()
            {
                var parsers = new Func<SyntaxNode?>[]
                {
                    ParseBlock,
                    ParseIfStatement,
                    ParseWhileStatement,
                    ParseDoWhileStatement,
                    ParseRepeatStatement,
                    ParseSwitchStatement,
                    ParseBreakStatement,
                    ParseReturnStatement,
                    ParseGotoStatement,
                    ParseForkStatement,
                    ParseOpcodeOrLabel
                };
                SkipSemicolons();
                foreach (var parser in parsers)
                {
                    if (parser() is SyntaxNode node)
                    {
                        if (node is OpcodeSyntaxNode ||
                            node is BreakSyntaxNode ||
                            node is ReturnSyntaxNode ||
                            node is GotoSyntaxNode ||
                            (node is ForkSyntaxNode forkNode && !(forkNode.Invocation is BlockSyntaxNode)))
                        {
                            ParseExpected(TokenKind.Semicolon);
                        }
                        return node;
                    }
                }
                return null;
            }

            private void SkipSemicolons()
            {
                while (PeekToken().Kind == TokenKind.Semicolon)
                {
                    ReadToken();
                }
            }

            private ForkSyntaxNode? ParseForkStatement()
            {
                if (!ParseToken(TokenKind.Fork))
                    return null;

                ref readonly var token = ref PeekToken();
                if (token.Kind == TokenKind.OpenBlock)
                {
                    var block = ParseExpectedBlock();
                    if (block == null)
                        return null;

                    return new ForkSyntaxNode(block);
                }
                else if (token.Kind == TokenKind.Symbol)
                {
                    ReadToken();
                    return new ForkSyntaxNode(new LiteralSyntaxNode(token));
                }
                else
                {
                    EmitError(in token, ErrorCodes.ExpectedProcedureName);
                    return null;
                }
            }

            private IfSyntaxNode? ParseIfStatement()
            {
                if (!ParseToken(TokenKind.If))
                    return null;

                var condition = ParseCondition();
                if (condition == null)
                    return null;

                var ifBlock = ParseExpectedBlock();
                var elseBlock = null as BlockSyntaxNode;
                if (ParseToken(TokenKind.Else))
                {
                    elseBlock = ParseExpectedBlock();
                }
                return new IfSyntaxNode(condition, ifBlock, elseBlock);
            }

            private WhileSyntaxNode? ParseWhileStatement()
            {
                if (!ParseToken(TokenKind.While))
                    return null;

                var condition = ParseCondition();
                if (condition == null)
                    return null;

                var block = ParseExpectedBlock();
                return new WhileSyntaxNode(condition, block);
            }

            private DoWhileSyntaxNode? ParseDoWhileStatement()
            {
                if (!ParseToken(TokenKind.Do))
                    return null;

                var block = ParseExpectedBlock();

                if (!ParseExpected(TokenKind.While))
                    return null;

                var condition = ParseCondition();
                if (condition == null)
                    return null;

                if (!ParseExpected(TokenKind.Semicolon))
                    return null;

                return new DoWhileSyntaxNode(block, condition);
            }

            private RepeatSyntaxNode? ParseRepeatStatement()
            {
                if (!ParseToken(TokenKind.Repeat))
                    return null;

                var count = null as ExpressionSyntaxNode;
                if (ParseToken(TokenKind.OpenParen))
                {
                    count = ParseExpression();
                    if (count == null)
                        return null;
                    ParseExpected(TokenKind.CloseParen);
                }

                var block = ParseExpectedBlock();
                if (block == null)
                    return null;

                return new RepeatSyntaxNode(count, block);
            }

            private SwitchSyntaxNode? ParseSwitchStatement()
            {
                if (!ParseToken(TokenKind.Switch))
                    return null;

                var variable = ParseExpression();
                if (variable == null)
                    return null;

                if (!ParseExpected(TokenKind.OpenBlock))
                    return null;

                var cases = new List<CaseSyntaxNode>();
                CaseSyntaxNode? caseNode;
                while ((caseNode = ParseSwitchCase()) != null)
                {
                    cases.Add(caseNode);
                }

                if (!ParseExpected(TokenKind.CloseBlock))
                    return null;

                return new SwitchSyntaxNode(variable, cases.ToArray());
            }

            private CaseSyntaxNode? ParseSwitchCase()
            {
                var value = null as ExpressionSyntaxNode;
                var token = PeekToken();
                if (token.Kind == TokenKind.Case)
                {
                    ReadToken();
                    value = ParseExpression();
                    if (value == null)
                        return null;
                }
                else if (token.Kind == TokenKind.Default)
                {
                    ReadToken();
                }
                else
                {
                    return null;
                }

                if (!ParseExpected(TokenKind.Colon))
                    return null;

                var block = ParseCaseBlock();
                if (block == null)
                    return null;
                return new CaseSyntaxNode(value, block);
            }

            private BlockSyntaxNode? ParseCaseBlock()
            {
                var nodes = new List<SyntaxNode>();
                while (true)
                {
                    ref readonly var token = ref PeekToken();
                    if (token.Kind == TokenKind.Case || token.Kind == TokenKind.Default || token.Kind == TokenKind.CloseBlock)
                        break;

                    var opcode = ParseStatement();
                    if (opcode != null)
                    {
                        nodes.Add(opcode);
                        continue;
                    }
                    break;
                }

                return new BlockSyntaxNode(nodes.ToArray());
            }

            private BreakSyntaxNode? ParseBreakStatement()
            {
                if (PeekToken().Kind != TokenKind.Break)
                    return null;

                ReadToken();
                return new BreakSyntaxNode();
            }

            private ReturnSyntaxNode? ParseReturnStatement()
            {
                if (PeekToken().Kind != TokenKind.Return)
                    return null;

                ReadToken();
                return new ReturnSyntaxNode();
            }

            private GotoSyntaxNode? ParseGotoStatement()
            {
                if (PeekToken().Kind != TokenKind.Goto)
                    return null;

                ref readonly var gotoToken = ref ReadToken();

                var symbolToken = PeekToken();
                if (symbolToken.Kind != TokenKind.Symbol)
                {
                    EmitError(in gotoToken, ErrorCodes.ExpectedLabelName);
                    return null;
                }

                ReadToken();
                return new GotoSyntaxNode(symbolToken);
            }

            private ConditionalExpressionSyntaxNode? ParseCondition()
            {
                if (!ParseExpected(TokenKind.OpenParen))
                    return null;

                var conditions = new List<OpcodeSyntaxNode>();
                do
                {
                    var condition = ParseOpcode();
                    if (condition == null)
                    {
                        EmitWarning(in LastToken, ErrorCodes.ExpectedCondition);
                        break;
                    }
                    conditions.Add(condition);
                } while (ParseToken(TokenKind.AmpersandAmpersand));

                if (!ParseExpected(TokenKind.CloseParen))
                    return null;

                return new ConditionalExpressionSyntaxNode(conditions.ToArray());
            }

            private SyntaxNode? ParseOpcodeOrLabel()
            {
                var token = PeekToken();
                if (token.Kind != TokenKind.Symbol)
                    return null;

                var symbol = ReadToken();
                if (ParseToken(TokenKind.Colon))
                    return new LabelSyntaxNode(symbol);

                return ParseOpcodeRemainder();
            }

            private OpcodeSyntaxNode? ParseOpcode()
            {
                if (!ParseToken(TokenKind.Symbol))
                    return null;

                return ParseOpcodeRemainder();
            }

            private OpcodeSyntaxNode? ParseOpcodeRemainder()
            {
                ref readonly var opcodeToken = ref LastToken;
                if (!ParseToken(TokenKind.OpenParen))
                {
                    EmitError(in LastToken, ErrorCodes.ExpectedOpenParen);
                    return null;
                }

                var operands = new List<SyntaxNode>();
                while (true)
                {
                    var operand = ParseOperand();
                    if (operand != null)
                    {
                        operands.Add(operand);
                        if (ParseToken(TokenKind.Comma))
                            continue;
                    }
                    if (ParseToken(TokenKind.CloseParen))
                        break;
                    EmitError(in LastToken, ErrorCodes.ExpectedComma);
                    SkipOpcode();
                    break;
                }

                return new OpcodeSyntaxNode(opcodeToken, operands.ToArray());
            }

            private SyntaxNode? ParseOperand()
            {
                ref readonly var token = ref PeekToken();
                if (token.Kind != TokenKind.Symbol &&
                    token.Kind != TokenKind.Number &&
                    token.Kind != TokenKind.OpenParen)
                {
                    return null;
                }
                return ParseExpression();
            }

            private ExpressionSyntaxNode? ParseExpression()
            {
                ExpressionSyntaxNode? expression;
                ref readonly var token = ref PeekToken();
                switch (token.Kind)
                {
                    case TokenKind.OpenParen:
                        token = ref ReadToken();
                        expression = ParseExpression();
                        if (expression == null)
                            return null;

                        token = ref PeekToken();
                        if (token.Kind != TokenKind.CloseParen)
                        {
                            EmitError(in token, ErrorCodes.ExpectedCloseParen);
                            return null;
                        }
                        ReadToken();
                        break;
                    case TokenKind.Symbol:
                    case TokenKind.Number:
                        token = ref ReadToken();
                        expression = new LiteralSyntaxNode(token);
                        break;
                    default:
                        EmitError(in token, ErrorCodes.InvalidExpression);
                        return null;
                }

                ref readonly var nextToken = ref PeekToken();
                if (IsBinaryOperator(nextToken.Kind))
                {
                    ReadToken();
                    var rhs = ParseExpression();
                    if (rhs == null)
                        return null;

                    var kind = nextToken.Kind switch
                    {
                        TokenKind.Plus => ExpressionKind.Add,
                        TokenKind.Minus => ExpressionKind.Subtract,
                        TokenKind.Asterisk => ExpressionKind.Multiply,
                        TokenKind.FowardSlash => ExpressionKind.Divide,
                        TokenKind.Pipe => ExpressionKind.BitwiseOr,
                        TokenKind.Ampersand => ExpressionKind.BitwiseAnd,
                        TokenKind.LShift => ExpressionKind.LogicalShiftLeft,
                        TokenKind.RShift => ExpressionKind.ArithmeticShiftRight,
                        _ => throw new NotSupportedException()
                    };
                    return new BinaryExpressionSyntaxNode(kind, expression, rhs);
                }
                else
                {
                    return expression;
                }
            }

            private static bool IsBinaryOperator(TokenKind kind) =>
                kind switch
                {
                    TokenKind.Plus => true,
                    TokenKind.Minus => true,
                    TokenKind.Asterisk => true,
                    TokenKind.FowardSlash => true,
                    TokenKind.Pipe => true,
                    TokenKind.Ampersand => true,
                    TokenKind.LShift => true,
                    TokenKind.RShift => true,
                    _ => false
                };

            private void SkipOpcode()
            {
                while (true)
                {
                    ref readonly var token = ref PeekToken();
                    if (token.Kind == TokenKind.EOF)
                    {
                        return;
                    }
                    else if (token.Kind == TokenKind.Semicolon)
                    {
                        ReadToken();
                        return;
                    }
                    else if (token.Kind == TokenKind.OpenBlock ||
                             token.Kind == TokenKind.CloseBlock)
                    {
                        return;
                    }
                    ReadToken();
                }
            }

            private bool ParseExpected(TokenKind kind)
            {
                if (ParseToken(kind))
                {
                    return true;
                }
                else
                {
                    var errorCode = kind switch
                    {
                        TokenKind.OpenParen => ErrorCodes.ExpectedOpenParen,
                        TokenKind.CloseParen => ErrorCodes.ExpectedCloseParen,
                        TokenKind.OpenBlock => ErrorCodes.ExpectedOpenBlock,
                        TokenKind.CloseBlock => ErrorCodes.ExpectedCloseBlock,
                        TokenKind.Colon => ErrorCodes.ExpectedColon,
                        TokenKind.Semicolon => ErrorCodes.ExpectedSemicolon,
                        TokenKind.While => ErrorCodes.ExpectedWhile,
                        _ => throw new NotImplementedException(),
                    };
                    EmitError(in PeekToken(), errorCode);
                    return false;
                }
            }

            private void EmitError(in Token token, int code, params object[] args)
            {
                _errors.AddError(token.Path, token.Line, token.Column, code, string.Format(ErrorCodes.GetMessage(code), args));
            }

            private void EmitWarning(in Token token, int code, params object[] args)
            {
                _errors.AddWarning(token.Path, token.Line, token.Column, code, string.Format(ErrorCodes.GetMessage(code), args));
            }
        }

        private class SyntaxTree
        {
            public BlockSyntaxNode Root { get; }

            public SyntaxTree() : this(new BlockSyntaxNode(new SyntaxNode[0]))
            {
            }

            public SyntaxTree(BlockSyntaxNode root)
            {
                Root = root;
            }
        }

        private class SyntaxNode
        {
            public virtual IEnumerable<SyntaxNode> Children
            {
                get
                {
                    yield break;
                }
            }
        }

        private class VersionSyntaxNode : SyntaxNode
        {
            public Token VersionToken { get; }

            public VersionSyntaxNode(Token versionToken)
            {
                VersionToken = versionToken;
            }
        }

        private class MessageTextSyntaxNode : SyntaxNode
        {
            public int Id { get; }
            public string Text { get; }

            public MessageTextSyntaxNode(int id, string text)
            {
                Id = id;
                Text = text;
            }
        }

        private class AnimationSyntaxNode : SyntaxNode
        {
            public int Id { get; }
            public EmrFlags Flags { get; }
            public string Path { get; }

            public AnimationSyntaxNode(int id, EmrFlags flags, string path)
            {
                Id = id;
                Flags = flags;
                Path = path;
            }
        }

        private class ObjectSyntaxNode : SyntaxNode
        {
            public int Id { get; }
            public string Path { get; }

            public ObjectSyntaxNode(int id, string path)
            {
                Id = id;
                Path = path;
            }
        }

        private class ProcedureSyntaxNode : SyntaxNode
        {
            public Token NameToken { get; }
            public BlockSyntaxNode? Block { get; }

            public ProcedureSyntaxNode(Token nameToken, BlockSyntaxNode? block)
            {
                NameToken = nameToken;
                Block = block;
            }

            public override IEnumerable<SyntaxNode> Children
            {
                get
                {
                    if (Block != null)
                        yield return Block;
                }
            }
        }

        private class BlockSyntaxNode : SyntaxNode
        {
            public SyntaxNode[] Statements { get; }

            public BlockSyntaxNode(SyntaxNode[] statements)
            {
                Statements = statements;
            }

            public override IEnumerable<SyntaxNode> Children => Statements;
        }

        private class ForkSyntaxNode : SyntaxNode
        {
            public SyntaxNode Invocation { get; }

            public ForkSyntaxNode(SyntaxNode invocation)
            {
                Invocation = invocation;
            }
        }

        private class IfSyntaxNode : SyntaxNode
        {
            public ConditionalExpressionSyntaxNode Condition { get; }
            public BlockSyntaxNode? IfBlock { get; }
            public BlockSyntaxNode? ElseBlock { get; }

            public IfSyntaxNode(ConditionalExpressionSyntaxNode condition, BlockSyntaxNode? ifBlock, BlockSyntaxNode? elseBlock)
            {
                Condition = condition;
                IfBlock = ifBlock;
                ElseBlock = elseBlock;
            }

            public override IEnumerable<SyntaxNode> Children
            {
                get
                {
                    if (Condition != null)
                        yield return Condition;
                    if (IfBlock != null)
                        yield return IfBlock;
                    if (ElseBlock != null)
                        yield return ElseBlock;
                }
            }
        }

        private class WhileSyntaxNode : SyntaxNode
        {
            public ConditionalExpressionSyntaxNode Condition { get; }
            public BlockSyntaxNode? Block { get; }

            public WhileSyntaxNode(ConditionalExpressionSyntaxNode condition, BlockSyntaxNode? block)
            {
                Condition = condition;
                Block = block;
            }

            public override IEnumerable<SyntaxNode> Children
            {
                get
                {
                    if (Condition != null)
                        yield return Condition;
                    if (Block != null)
                        yield return Block;
                }
            }
        }

        private class DoWhileSyntaxNode : SyntaxNode
        {
            public BlockSyntaxNode? Block { get; }
            public ConditionalExpressionSyntaxNode Condition { get; }

            public DoWhileSyntaxNode(BlockSyntaxNode? block, ConditionalExpressionSyntaxNode condition)
            {
                Block = block;
                Condition = condition;
            }

            public override IEnumerable<SyntaxNode> Children
            {
                get
                {
                    if (Block != null)
                        yield return Block;
                    if (Condition != null)
                        yield return Condition;
                }
            }
        }

        private class RepeatSyntaxNode : SyntaxNode
        {
            public ExpressionSyntaxNode? Count { get; }
            public BlockSyntaxNode Block { get; }

            public RepeatSyntaxNode(ExpressionSyntaxNode? count, BlockSyntaxNode block)
            {
                Count = count;
                Block = block;
            }

            public override IEnumerable<SyntaxNode> Children
            {
                get
                {
                    if (Count != null)
                        yield return Count;
                    yield return Block;
                }
            }
        }

        private class SwitchSyntaxNode : SyntaxNode
        {
            public ExpressionSyntaxNode Variable { get; }
            public CaseSyntaxNode[] Cases { get; }

            public SwitchSyntaxNode(ExpressionSyntaxNode variable, CaseSyntaxNode[] cases)
            {
                Variable = variable;
                Cases = cases;
            }

            public override IEnumerable<SyntaxNode> Children
            {
                get
                {
                    if (Variable != null)
                        yield return Variable;
                    foreach (var c in Cases)
                        yield return c;
                }
            }
        }

        private class CaseSyntaxNode : SyntaxNode
        {
            public ExpressionSyntaxNode? Value { get; }
            public BlockSyntaxNode Block { get; }

            public CaseSyntaxNode(ExpressionSyntaxNode? value, BlockSyntaxNode block)
            {
                Value = value;
                Block = block;
            }

            public override IEnumerable<SyntaxNode> Children
            {
                get
                {
                    if (Value != null)
                        yield return Value;
                    if (Block != null)
                        yield return Block;
                }
            }
        }

        private class BreakSyntaxNode : SyntaxNode
        {
        }

        private class ReturnSyntaxNode : SyntaxNode
        {
        }

        private class GotoSyntaxNode : SyntaxNode
        {
            public Token Destination { get; }

            public GotoSyntaxNode(Token destination)
            {
                Destination = destination;
            }
        }

        private class ConditionalExpressionSyntaxNode : SyntaxNode
        {
            public SyntaxNode[] Conditions { get; }

            public ConditionalExpressionSyntaxNode(SyntaxNode[] conditions)
            {
                Conditions = conditions;
            }

            public override IEnumerable<SyntaxNode> Children => Conditions;
        }

        private class LabelSyntaxNode : SyntaxNode
        {
            public Token LabelToken { get; }

            public LabelSyntaxNode(Token labelToken)
            {
                LabelToken = labelToken;
            }
        }

        private class OpcodeSyntaxNode : SyntaxNode
        {
            public Token OpcodeToken { get; }
            public SyntaxNode[] Operands { get; }

            public OpcodeSyntaxNode(Token opcodeToken, SyntaxNode[] operands)
            {
                OpcodeToken = opcodeToken;
                Operands = operands;
            }

            public override IEnumerable<SyntaxNode> Children => Operands;
        }

        private abstract class ExpressionSyntaxNode : SyntaxNode
        {
            public abstract ExpressionKind Kind { get; }
        }

        private class BinaryExpressionSyntaxNode : ExpressionSyntaxNode
        {
            public override ExpressionKind Kind { get; }
            public ExpressionSyntaxNode Left { get; }
            public ExpressionSyntaxNode Right { get; }

            public BinaryExpressionSyntaxNode(ExpressionKind kind, ExpressionSyntaxNode left, ExpressionSyntaxNode right)
            {
                Kind = kind;
                Left = left;
                Right = right;
            }
        }

        private class LiteralSyntaxNode : ExpressionSyntaxNode
        {
            public override ExpressionKind Kind => ExpressionKind.Literal;
            public Token LiteralToken { get; }

            public LiteralSyntaxNode(Token literalToken)
            {
                LiteralToken = literalToken;
            }
        }

        public enum ExpressionKind
        {
            Literal,
            Add,
            Subtract,
            Multiply,
            Divide,
            BitwiseOr,
            BitwiseAnd,
            LogicalShiftLeft,
            ArithmeticShiftRight,
        }
    }
}

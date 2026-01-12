using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using IntelOrca.Biohazard.Model;
using IntelOrca.Biohazard.Room;

namespace IntelOrca.Biohazard.Script.Compilation
{
    public partial class ScdCompiler
    {
        private class Generator
        {
            private readonly ErrorList _errors;
            private readonly string _path;
            private readonly List<IRdtEditOperation> _operations = new List<IRdtEditOperation>();

            private BioVersion? _version;
            private IConstantTable _constantTable = new Bio1ConstantTable();
            private HashSet<string> _procedureNames = new HashSet<string>();
            private List<ProcedureBuilder> _procedures = new List<ProcedureBuilder>();
            private ProcedureBuilder _currentProcedure = new ProcedureBuilder("");
            private List<BlockSyntaxNode> _anonymousProcedures = new List<BlockSyntaxNode>();

            public IRdtEditOperation[] Operations => _operations.ToArray();

            public Generator(ErrorList errors, string path)
            {
                _errors = errors;
                _path = path;
            }

            public int Generate(SyntaxTree tree)
            {
                _procedureNames = GetAllProcedureNames(tree);
                Visit(tree.Root);
                VisitAnonymousProcedures();
                if (_errors.ErrorCount != 0)
                {
                    return 1;
                }

                if (_version == BioVersion.Biohazard3)
                {
                    _operations.Add(new ScdRdtEditOperation(BioScriptKind.Init, GenerateScd("main", "aot")));
                }
                else
                {
                    _operations.Add(new ScdRdtEditOperation(BioScriptKind.Init, GenerateScd("init")));
                    _operations.Add(new ScdRdtEditOperation(BioScriptKind.Main, GenerateScd("main", "aot")));
                }
                return 0;
            }

            private void VisitAnonymousProcedures()
            {
                for (var i = 0; i < _anonymousProcedures.Count; i++)
                {
                    var anonymousProcedure = _anonymousProcedures[i];
                    VisitAnonymousProcedureNode(i, anonymousProcedure);
                }
            }

            private HashSet<string> GetAllProcedureNames(SyntaxTree tree)
            {
                var names = new HashSet<string>();
                var stack = new Stack<SyntaxNode>();
                stack.Push(tree.Root);
                while (stack.Count != 0)
                {
                    var node = stack.Pop();
                    if (node is ProcedureSyntaxNode procNode)
                    {
                        var procName = procNode.NameToken.Text;
                        if (!names.Add(procName))
                        {
                            EmitError(procNode.NameToken, ErrorCodes.ProcedureNameAlreadyDefined, procName);
                        }
                    }
                    else
                    {
                        foreach (var child in node.Children)
                        {
                            stack.Push(child);
                        }
                    }
                }
                return names;
            }

            private ScdProcedureList GenerateScd(params string[] startProcs)
            {
                // Make sure order is correct and create empty procedures for missing required ones
                var procedures = _procedures.ToList();
                foreach (var procName in startProcs.Reverse())
                {
                    var index = procedures.FindIndex(x => x.Name == procName);
                    if (index == -1)
                    {
                        var proc = new ProcedureBuilder(procName);
                        proc.Write((byte)OpcodeV2.EvtEnd);
                        proc.Write((byte)0x00);
                        procedures.Insert(0, proc);
                    }
                    else
                    {
                        var proc = procedures[index];
                        procedures.RemoveAt(index);
                        procedures.Insert(0, proc);
                    }
                }

                // Removed unreferenced procedures
                var referencedProcedures = new HashSet<ProcedureBuilder>();
                var searchStack = new Stack<ProcedureBuilder>();
                for (var i = 0; i < startProcs.Length; i++)
                {
                    referencedProcedures.Add(procedures[i]);
                    searchStack.Push(procedures[i]);
                }
                while (searchStack.Count != 0)
                {
                    var proc = searchStack.Pop();
                    foreach (var procRef in proc.ProcedureReferences)
                    {
                        var targetProc = _procedures.First(x => x.Name == procRef.Name);
                        if (referencedProcedures.Add(targetProc))
                        {
                            searchStack.Push(targetProc);
                        }
                    }
                }
                procedures.RemoveAll(x => !referencedProcedures.Contains(x));

                foreach (var proc in procedures)
                {
                    proc.FixProceduresReferences(procedures.Select(x => x.Name).ToArray());
                }

                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                ms.Position += procedures.Count * 2;
                for (var i = 0; i < procedures.Count; i++)
                {
                    var backupPosition = ms.Position;
                    ms.Position = i * 2;
                    bw.Write((ushort)backupPosition);
                    ms.Position = backupPosition;
                    bw.Write(procedures[i].ToArray());
                }
                var bytes = ms.ToArray();
                return new ScdProcedureList(_version!.Value, bytes);
            }

            private void VisitChildren(SyntaxNode node)
            {
                foreach (var child in node.Children)
                {
                    Visit(child);
                }
            }

            private void Visit(SyntaxNode node)
            {
                switch (node)
                {
                    case VersionSyntaxNode versionNode:
                        VisitVersionNode(versionNode);
                        break;
                    case MessageTextSyntaxNode messageTextNode:
                        VisitMessageTextNode(messageTextNode);
                        break;
                    case AnimationSyntaxNode animationNode:
                        VisitAnimationNode(animationNode);
                        break;
                    case ObjectSyntaxNode objectNode:
                        VisitObjectNode(objectNode);
                        break;
                    case ProcedureSyntaxNode procedureNode:
                        VisitProcedureNode(procedureNode);
                        break;
                    case ForkSyntaxNode forkNode:
                        VisitForkNode(forkNode);
                        break;
                    case IfSyntaxNode ifNode:
                        VisitIfNode(ifNode);
                        break;
                    case WhileSyntaxNode whileNode:
                        VisitWhileNode(whileNode);
                        break;
                    case DoWhileSyntaxNode doWhileNode:
                        VisitDoWhileNode(doWhileNode);
                        break;
                    case RepeatSyntaxNode repeatNode:
                        VisitRepeatNode(repeatNode);
                        break;
                    case SwitchSyntaxNode switchNode:
                        VisitSwitchNode(switchNode);
                        break;
                    case BreakSyntaxNode breakNode:
                        VisitBreakNode(breakNode);
                        break;
                    case ReturnSyntaxNode returnNode:
                        VisitReturnNode(returnNode);
                        break;
                    case GotoSyntaxNode gotoNode:
                        VisitGotoNode(gotoNode);
                        break;
                    case LabelSyntaxNode labelNode:
                        VisitLabelNode(labelNode);
                        break;
                    case OpcodeSyntaxNode opcodeNode:
                        VisitOpcodeNode(opcodeNode);
                        break;
                    default:
                        VisitChildren(node);
                        break;
                }
            }

            private void VisitVersionNode(VersionSyntaxNode versionNode)
            {
                var versionToken = versionNode.VersionToken;
                if (_version != null)
                {
                    EmitError(in versionToken, ErrorCodes.ScdTypeAlreadySpecified);
                    return;
                }

                var version = ParseNumber(in versionToken);
                _version = version switch
                {
                    1 => BioVersion.Biohazard1,
                    2 => BioVersion.Biohazard2,
                    3 => BioVersion.Biohazard3,
                    _ => null
                };
                if (_version == null)
                {
                    EmitError(in versionToken, ErrorCodes.InvalidScdVersionNumber);
                    return;
                }

                _constantTable = ConstantTable.FromVersion(_version.Value);
            }

            private void VisitMessageTextNode(MessageTextSyntaxNode messageTextNode)
            {
                _operations.Add(new TextRdtEditOperation(MsgLanguage.Japanese, messageTextNode.Id, new Msg(_version!.Value, MsgLanguage.Japanese, messageTextNode.Text)));
                _operations.Add(new TextRdtEditOperation(MsgLanguage.English, messageTextNode.Id, new Msg(_version!.Value, MsgLanguage.English, messageTextNode.Text)));
            }

            private void VisitAnimationNode(AnimationSyntaxNode animationNode)
            {
                var eddPath = Path.Combine(Path.GetDirectoryName(_path), animationNode.Path);
                try
                {
                    var emrPath = Path.ChangeExtension(eddPath, ".emr");
                    var edd = new Edd1(BioVersion.Biohazard2, File.ReadAllBytes(eddPath));
                    var emr = new Emr(BioVersion.Biohazard2, File.ReadAllBytes(emrPath));
                    _operations.Add(new AnimationRdtEditOperation(
                        animationNode.Id, new RbjAnimation(animationNode.Flags, edd, emr)));
                }
                catch (Exception)
                {
                    _errors.AddError(_path, 0, 0, ErrorCodes.FileNotFound, eddPath);
                }
            }

            private void VisitObjectNode(ObjectSyntaxNode objectNode)
            {
                var md1Path = Path.Combine(Path.GetDirectoryName(_path), objectNode.Path);
                try
                {
                    var timPath = Path.ChangeExtension(md1Path, ".tim");
                    var md1 = new Md1(File.ReadAllBytes(md1Path));
                    var tim = new Tim(File.ReadAllBytes(timPath));
                    _operations.Add(new ObjectRdtEditOperation(objectNode.Id, md1, tim));
                }
                catch (Exception)
                {
                    _errors.AddError(_path, 0, 0, ErrorCodes.FileNotFound, md1Path);
                }
            }

            private void VisitProcedureNode(ProcedureSyntaxNode procedureNode)
            {
                _currentProcedure = new ProcedureBuilder(procedureNode.NameToken.Text);
                VisitChildren(procedureNode);
                WriteCurrentProcedure();
            }

            private void VisitAnonymousProcedureNode(int index, BlockSyntaxNode blockNode)
            {
                _currentProcedure = new ProcedureBuilder(GetAnonymousProcedureName(index));
                VisitChildren(blockNode);
                WriteCurrentProcedure();
            }

            private void WriteCurrentProcedure()
            {
                var missingLabels = _currentProcedure.GetMissingLabels();
                foreach (var labelToken in missingLabels)
                {
                    EmitError(in labelToken, ErrorCodes.UnknownLabel, labelToken.Text);
                    return;
                }

                _currentProcedure.Align();
                _currentProcedure.Write(ConvertOpcode(OpcodeV2.EvtEnd));
                _currentProcedure.Write((byte)0);
                _currentProcedure.FixLabels();
                _procedures.Add(_currentProcedure);
            }

            private void VisitForkNode(ForkSyntaxNode forkNode)
            {
                _currentProcedure.Align();
                _currentProcedure.Write(ConvertOpcode(OpcodeV2.EvtExec));
                _currentProcedure.Write((byte)0xFF);
                _currentProcedure.Write(ConvertOpcode(OpcodeV2.Gosub));
                if (forkNode.Invocation is LiteralSyntaxNode literal)
                {
                    _currentProcedure.WriteProcedureRef(literal.LiteralToken);
                }
                else if (forkNode.Invocation is BlockSyntaxNode block)
                {
                    _anonymousProcedures.Add(block);
                    _currentProcedure.WriteProcedureRef(_anonymousProcedures.Count - 1);
                }
            }

            private void VisitIfNode(IfSyntaxNode ifNode)
            {
                _currentProcedure.Align();
                _currentProcedure.Write(ConvertOpcode(OpcodeV2.IfelCk));
                _currentProcedure.Write((byte)0);
                var elseLabel = _currentProcedure.WriteLabelRef(2, 2);
                var endIfLabel = elseLabel;

                Visit(ifNode.Condition);

                if (ifNode.IfBlock != null)
                {
                    Visit(ifNode.IfBlock);
                }
                if (ifNode.ElseBlock != null)
                {
                    _currentProcedure.Align();
                    _currentProcedure.Write(ConvertOpcode(OpcodeV2.ElseCk));
                    _currentProcedure.Write((byte)0);
                    endIfLabel = _currentProcedure.WriteLabelRef(2, -2);
                    _currentProcedure.WriteLabel(elseLabel);
                    Visit(ifNode.ElseBlock);
                }
                else
                {
                    _currentProcedure.Align();
                    _currentProcedure.Write(ConvertOpcode(OpcodeV2.EndIf));
                    _currentProcedure.Write((byte)0);
                }
                _currentProcedure.Align();
                _currentProcedure.WriteLabel(endIfLabel);
            }

            private void VisitWhileNode(WhileSyntaxNode whileNode)
            {
                _currentProcedure.Align();
                _currentProcedure.Write(ConvertOpcode(OpcodeV2.While));
                var bodyLabel = _currentProcedure.WriteLabelRef(1, 3);
                var endLabel = _currentProcedure.WriteLabelRef(2, 2);

                Visit(whileNode.Condition);
                _currentProcedure.WriteLabel(bodyLabel);
                if (whileNode.Block != null)
                {
                    Visit(whileNode.Block);
                }

                _currentProcedure.Align();
                _currentProcedure.Write(ConvertOpcode(OpcodeV2.Ewhile));
                _currentProcedure.Write((byte)0);
                _currentProcedure.WriteLabel(endLabel);
            }

            private void VisitDoWhileNode(DoWhileSyntaxNode whileNode)
            {
                _currentProcedure.Align();
                _currentProcedure.Write(ConvertOpcode(OpcodeV2.Do));
                _currentProcedure.Write((byte)0);
                var endLabel = _currentProcedure.WriteLabelRef(2, 2);
                if (whileNode.Block != null)
                {
                    Visit(whileNode.Block);
                }

                _currentProcedure.Align();
                _currentProcedure.Write(ConvertOpcode(OpcodeV2.Edwhile));
                _currentProcedure.WriteLabelRef(endLabel, 1, 1);
                Visit(whileNode.Condition);
                _currentProcedure.Align();
                _currentProcedure.WriteLabel(endLabel);
            }

            private void VisitRepeatNode(RepeatSyntaxNode repeatNode)
            {
                _currentProcedure.Align();
                if (repeatNode.Count == null)
                {
                    var beginLabel = _currentProcedure.WriteLabel();
                    Visit(repeatNode.Block);
                    _currentProcedure.Align();
                    _currentProcedure.Write(ConvertOpcode(OpcodeV2.Goto));
                    _currentProcedure.Write((byte)0xFF);
                    _currentProcedure.Write((byte)0xFF);
                    _currentProcedure.Write((byte)0x00);
                    _currentProcedure.WriteLabelRef(beginLabel, 2, -4);
                }
                else
                {
                    _currentProcedure.Write(ConvertOpcode(OpcodeV2.For));
                    _currentProcedure.Write((byte)0);
                    var endLabel = _currentProcedure.WriteLabelRef(2, 4);
                    _currentProcedure.Write((ushort)ProcessOperand(repeatNode.Count));
                    Visit(repeatNode.Block);
                    _currentProcedure.Align();
                    _currentProcedure.Write(ConvertOpcode(OpcodeV2.Next));
                    _currentProcedure.Write((byte)0);
                    _currentProcedure.WriteLabel(endLabel);
                }
            }

            private void VisitSwitchNode(SwitchSyntaxNode switchNode)
            {
                _currentProcedure.Align();
                _currentProcedure.Write(ConvertOpcode(OpcodeV2.Switch));
                _currentProcedure.Write((byte)ProcessOperand(switchNode.Variable));
                var endLabel = _currentProcedure.WriteLabelRef(2, 2);

                foreach (var caseNode in switchNode.Cases)
                {
                    if (caseNode.Value is ExpressionSyntaxNode valueNode)
                    {
                        _currentProcedure.Write(ConvertOpcode(OpcodeV2.Case));
                        _currentProcedure.Write((byte)0);
                        var endCaseLabel = _currentProcedure.WriteLabelRef(2, 4);
                        _currentProcedure.Write((byte)ProcessOperand(valueNode));
                        Visit(caseNode.Block);
                        _currentProcedure.Align();
                        _currentProcedure.WriteLabel(endCaseLabel);
                    }
                    else
                    {
                        _currentProcedure.Align();
                        _currentProcedure.Write(ConvertOpcode(OpcodeV2.Default));
                        _currentProcedure.Write((byte)0);
                        Visit(caseNode.Block);
                        _currentProcedure.Align();
                    }
                }

                _currentProcedure.Align();
                _currentProcedure.Write(ConvertOpcode(OpcodeV2.Eswitch));
                _currentProcedure.Write((byte)0);
                _currentProcedure.WriteLabel(endLabel);
            }

            private void VisitBreakNode(BreakSyntaxNode breakNode)
            {
                _currentProcedure.Align();
                _currentProcedure.Write(ConvertOpcode(OpcodeV2.Break));
                _currentProcedure.Write((byte)0);
            }

            private void VisitReturnNode(ReturnSyntaxNode returnNode)
            {
                _currentProcedure.Align();
                _currentProcedure.Write(ConvertOpcode(OpcodeV2.Return));
                _currentProcedure.Write((byte)0);
            }

            private void VisitLabelNode(LabelSyntaxNode labelNode)
            {
                _currentProcedure.Align();
                _currentProcedure.WriteLabel(name: labelNode.LabelToken.Text);
            }

            private void VisitGotoNode(GotoSyntaxNode gotoNode)
            {
                var label = _currentProcedure.FindOrPromiseLabel(gotoNode.Destination);
                _currentProcedure.Align();
                _currentProcedure.Write(ConvertOpcode(OpcodeV2.Goto));
                _currentProcedure.Write((byte)255);
                _currentProcedure.Write((byte)255);
                _currentProcedure.Write((byte)0);
                _currentProcedure.WriteLabelRef(label, 2, -4);
            }

            private void VisitOpcodeNode(OpcodeSyntaxNode opcodeNode)
            {
                var opcodeRaw = _constantTable.FindOpcode(opcodeNode.OpcodeToken.Text, isEventOpcode: false);
                if (opcodeRaw == null)
                {
                    if (opcodeNode.Operands.Length == 0)
                    {
                        _currentProcedure.Write(ConvertOpcode(OpcodeV2.Gosub));
                        var procName = opcodeNode.OpcodeToken.Text;
                        if (!_procedureNames.Contains(procName))
                        {
                            EmitError(opcodeNode.OpcodeToken, ErrorCodes.UnknownProcedure, procName);
                        }
                        _currentProcedure.WriteProcedureRef(opcodeNode.OpcodeToken);
                    }
                    else if (opcodeNode.OpcodeToken.Text == "unk")
                    {
                        foreach (var operand in opcodeNode.Operands)
                        {
                            WriteOperand('u', operand);
                        }
                    }
                    else
                    {
                        EmitError(opcodeNode.OpcodeToken, ErrorCodes.UnknownOpcode, opcodeNode.OpcodeToken.Text);
                    }
                    return;
                }

                var opcodeSignature = _constantTable.GetOpcodeSignature(opcodeRaw.Value);
                var opcodeLength = _constantTable.GetInstructionSize(opcodeRaw.Value, null);
                var colonIndex = opcodeSignature.IndexOf(':');
                if (colonIndex == -1)
                {
                    var length = opcodeLength;
                    if (opcodeSignature != "")
                        length--;
                    opcodeSignature = new string('u', length);
                }
                else
                {
                    opcodeSignature = opcodeSignature.Substring(colonIndex + 1);
                }
                var numArguments = opcodeSignature.Length;

                var operands = opcodeNode.Children.ToArray();
                if (operands.Length != numArguments)
                {
                    EmitError(opcodeNode.OpcodeToken, ErrorCodes.IncorrectNumberOfOperands);
                }

                if ((opcodeLength & 1) == 0)
                {
                    _currentProcedure.Align();
                }
                _currentProcedure.Write(opcodeRaw.Value);

                var numOperands = Math.Min(operands.Length, numArguments);
                for (var i = 0; i < numOperands; i++)
                {
                    WriteOperand(opcodeSignature[i], operands[i]);
                }
            }

            private void WriteOperand(char argType, SyntaxNode operand)
            {
                var argLen = char.IsUpper(argType) || argType == '@' || argType == '~' ? 2 : 1;
                if (ProcessProcedureRef(operand, out var procRefToken))
                {
                    if (argLen == 2)
                    {
                        EmitError(in procRefToken, ErrorCodes.InvalidOperand);
                    }
                    else
                    {
                        _currentProcedure.WriteProcedureRef(in procRefToken);
                    }
                }
                else
                {
                    var value = ProcessOperand(operand);
                    if (argLen == 2)
                    {
                        _currentProcedure.Write((short)value);
                    }
                    else
                    {
                        _currentProcedure.Write((byte)value);
                    }
                }
            }

            private bool ProcessProcedureRef(SyntaxNode node, out Token procRefToken)
            {
                if (node is LiteralSyntaxNode literal && literal.LiteralToken.Kind == TokenKind.Symbol)
                {
                    var tokenText = literal.LiteralToken.Text;
                    if (_procedureNames.Contains(tokenText))
                    {
                        procRefToken = literal.LiteralToken;
                        return true;
                    }
                }
                procRefToken = default;
                return false;
            }

            private int ProcessOperand(SyntaxNode node)
            {
                return ProcessExpression((ExpressionSyntaxNode)node);
            }

            private int ProcessExpression(ExpressionSyntaxNode node)
            {
                if (node is BinaryExpressionSyntaxNode binaryNode)
                {
                    var lhs = ProcessExpression(binaryNode.Left);
                    var rhs = ProcessExpression(binaryNode.Right);
                    return node.Kind switch
                    {
                        ExpressionKind.Add => lhs + rhs,
                        ExpressionKind.Subtract => lhs - rhs,
                        ExpressionKind.Multiply => lhs * rhs,
                        ExpressionKind.Divide => lhs / rhs,
                        ExpressionKind.BitwiseOr => lhs | rhs,
                        ExpressionKind.BitwiseAnd => lhs & rhs,
                        ExpressionKind.LogicalShiftLeft => lhs << rhs,
                        ExpressionKind.ArithmeticShiftRight => lhs >> rhs,
                        _ => throw new NotSupportedException()
                    };
                }
                else if (node is LiteralSyntaxNode literalNode)
                {
                    var token = literalNode.LiteralToken;
                    if (token.Kind == TokenKind.Number)
                    {
                        return ParseNumber(in token);
                    }
                    else if (token.Kind == TokenKind.Symbol)
                    {
                        var tokenText = token.Text;
                        if (_procedureNames.Contains(tokenText))
                        {
                            EmitError(in token, ErrorCodes.InvalidExpression);
                            return 0;
                        }
                        else
                        {
                            var value = _constantTable.GetConstantValue(tokenText);
                            if (value == null)
                            {
                                value = 0;
                                EmitError(in token, ErrorCodes.UnknownSymbol, tokenText);
                            }
                            return value.Value;
                        }
                    }
                    else
                    {
                        EmitError(in token, ErrorCodes.InvalidOperand);
                    }
                }
                return 0;
            }

            private int ParseNumber(in Token token)
            {
                return token.Text.StartsWith("0x")
                    ? int.Parse(token.Text.Substring(2), System.Globalization.NumberStyles.HexNumber)
                    : int.Parse(token.Text);
            }

            private void EmitError(in Token token, int code, params object[] args)
            {
                _errors.AddError(token.Path, token.Line, token.Column, code, string.Format(ErrorCodes.GetMessage(code), args));
            }

            private void EmitWarning(in Token token, int code, params object[] args)
            {
                _errors.AddWarning(token.Path, token.Line, token.Column, code, string.Format(ErrorCodes.GetMessage(code), args));
            }

            internal static string GetAnonymousProcedureName(int index)
            {
                return $"<anon>_{index}";
            }

            private byte ConvertOpcode(OpcodeV2 opcode)
            {
                if (_version == BioVersion.Biohazard2)
                    return (byte)opcode;

                return opcode switch
                {
                    OpcodeV2.EvtExec => (byte)OpcodeV3.EvtExec,
                    OpcodeV2.EvtEnd => (byte)OpcodeV3.EvtEnd,
                    OpcodeV2.IfelCk => (byte)OpcodeV3.IfelCk,
                    OpcodeV2.ElseCk => (byte)OpcodeV3.ElseCk,
                    OpcodeV2.EndIf => (byte)OpcodeV3.EndIf,
                    OpcodeV2.Gosub => (byte)OpcodeV3.Gosub,
                    OpcodeV2.For => (byte)OpcodeV3.For,
                    OpcodeV2.Next => (byte)OpcodeV3.EndFor,
                    OpcodeV2.Do => (byte)OpcodeV3.Do,
                    OpcodeV2.While => (byte)OpcodeV3.While,
                    OpcodeV2.Ewhile => (byte)OpcodeV3.Ewhile,
                    OpcodeV2.Edwhile => (byte)OpcodeV3.Edwhile,
                    OpcodeV2.Switch => (byte)OpcodeV3.Switch,
                    OpcodeV2.Case => (byte)OpcodeV3.Case,
                    OpcodeV2.Default => (byte)OpcodeV3.Default,
                    OpcodeV2.Eswitch => (byte)OpcodeV3.Eswitch,
                    OpcodeV2.Goto => (byte)OpcodeV3.Goto,
                    OpcodeV2.Break => (byte)OpcodeV3.Break,
                    _ => throw new NotImplementedException(),
                };
            }
        }

        [DebuggerDisplay("Name = {Name}")]
        private class ProcedureBuilder
        {
            private readonly MemoryStream _ms = new MemoryStream();
            private BinaryWriter _bw;
            private readonly List<Label> _labels = new List<Label>();
            private readonly List<LabelReference> _labelReferences = new List<LabelReference>();
            private readonly List<int> _labelOffsets = new List<int>();
            private readonly List<ProcedureReference> _procedureReferences = new List<ProcedureReference>();
            private readonly Dictionary<int, Token> _promisedLabels = new Dictionary<int, Token>();

            public string Name { get; }
            public int Offset => (int)_ms.Position;
            public List<ProcedureReference> ProcedureReferences => _procedureReferences;

            public ProcedureBuilder(string name)
            {
                Name = name;
                _bw = new BinaryWriter(_ms);
            }

            public void Align()
            {
                if ((Offset & 1) == 1)
                {
                    Write((byte)OpcodeV2.Nop);
                }
            }

            public Label? FindLabel(string name)
            {
                var index = _labels.FindIndex(x => x.Name == name);
                return index == -1 ? null : (Label?)_labels[index];
            }

            public Label FindOrPromiseLabel(Token labelToken)
            {
                var foundLabel = FindLabel(labelToken.Text);
                if (foundLabel != null)
                    return foundLabel.Value;

                var label = new Label(_labels.Count, labelToken.Text);
                _labels.Add(label);
                _promisedLabels.Add(label.Id, labelToken);
                return label;
            }

            public Label WriteLabel(string? name = null)
            {
                if (name != null)
                {
                    var foundLabel = FindLabel(name);
                    if (foundLabel.HasValue)
                    {
                        return WriteLabel(foundLabel.Value);
                    }
                }

                var label = new Label(_labels.Count, name);
                _labels.Add(label);
                return WriteLabel(label);
            }

            public Label WriteLabel(Label label)
            {
                while (_labelOffsets.Count <= label.Id)
                    _labelOffsets.Add(0);
                _labelOffsets[label.Id] = Offset;
                _promisedLabels.Remove(label.Id);
                return label;
            }

            public Label WriteLabelRef(byte size, int relativeBaseAddress)
            {
                var label = new Label(_labels.Count);
                _labels.Add(label);
                return WriteLabelRef(label, size, relativeBaseAddress);
            }

            public Label WriteLabelRef(Label label, byte size, int relativeBaseAddress)
            {
                _labelReferences.Add(new LabelReference(label, Offset, size, Offset + relativeBaseAddress));
                if (size == 1)
                    Write((byte)0);
                else if (size == 2)
                    Write((ushort)0);
                else
                    throw new NotSupportedException();
                return label;
            }

            public void WriteProcedureRef(in Token token)
            {
                _procedureReferences.Add(new ProcedureReference(token, Offset));
                Write((byte)0);
            }

            public void WriteProcedureRef(int anonymousProcedureIndex)
            {
                _procedureReferences.Add(new ProcedureReference(anonymousProcedureIndex, Offset));
                Write((byte)0);
            }

            public Token[] GetMissingLabels()
            {
                return _promisedLabels.Values.ToArray();
            }

            public void FixLabels()
            {
                foreach (var reference in _labelReferences)
                {
                    var targetAddress = _labelOffsets[reference.Label.Id];
                    var value = targetAddress - reference.BaseAddress;
                    _ms.Position = reference.WriteOffset;
                    if (reference.WriteLength == 1)
                        Write((byte)value);
                    else if (reference.WriteLength == 2)
                        Write((short)value);
                    else
                        throw new NotSupportedException();
                }
            }

            public void FixProceduresReferences(string[] procedureNames)
            {
                foreach (var reference in _procedureReferences)
                {
                    _ms.Position = reference.WriteOffset;
                    var index = Array.IndexOf(procedureNames, reference.Name);
                    if (index == -1)
                        throw new InvalidOperationException();
                    Write((byte)index);
                }
            }

            public byte[] ToArray() => _ms.ToArray();

            public void Write(short value) => _bw.Write(value);
            public void Write(ushort value) => _bw.Write(value);
            public void Write(byte value) => _bw.Write(value);
        }

        [DebuggerDisplay("Label #{Id}")]
        private readonly struct Label
        {
            public int Id { get; }
            public string? Name { get; }

            public Label(int id, string? name = null)
            {
                Id = id;
                Name = name;
            }
        }

        private readonly struct LabelReference
        {
            public Label Label { get; }
            public int WriteOffset { get; }
            public byte WriteLength { get; }
            public int BaseAddress { get; }

            public LabelReference(Label label, int writeOffset, byte writeLength, int baseAddress)
            {
                Label = label;
                WriteOffset = writeOffset;
                WriteLength = writeLength;
                BaseAddress = baseAddress;
            }
        }

        [DebuggerDisplay("Name = {Name} WriteOffset = {WriteOffset}")]
        private readonly struct ProcedureReference
        {
            public int AnonymousProcedureIndex { get; }
            public Token Token { get; }
            public int WriteOffset { get; }

            public bool IsAnonymousProcedure => AnonymousProcedureIndex != -1;
            public string Name => IsAnonymousProcedure ? Generator.GetAnonymousProcedureName(AnonymousProcedureIndex) : Token.Text;

            public ProcedureReference(Token token, int writeOffset)
            {
                AnonymousProcedureIndex = -1;
                Token = token;
                WriteOffset = writeOffset;
            }

            public ProcedureReference(int anonymousProcedureIndex, int writeOffset)
            {
                AnonymousProcedureIndex = anonymousProcedureIndex;
                Token = default;
                WriteOffset = writeOffset;
            }
        }
    }
}

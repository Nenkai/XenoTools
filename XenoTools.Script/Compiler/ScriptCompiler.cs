using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using XenoTools.Script.Instructions;

using Esprima;
using Esprima.Ast;
using XenoTools.Script.Entities;

namespace XenoTools.Script.Compiler;

public class ScriptCompiler
{
    public static readonly List<string> OCs =
    [
        // ocBuiltinRegist
        "builtin",

        // ocThreadRegist
        "thread",

        // ocBdatRegist
        "bdat",

        // ocMsgRegist
        "msgYuka",
        "msgNpc",

        // ocUnitRegist
        "unit",
        "obj",
        "point",
        "effect",
        "attr",

        // ocCfpRegist
        "cfp"
    ];

    // actual privates
    private Dictionary<string, int> _definedStatics = [];
    private List<VmVariable> _statics = [];
    private FunctionInfo _currentFunctionFrame;
    private uint _currentPc;
    private FunctionInfo _currentFrameForParsing;
    private List<Statement> _topLevelStatements = [];

    /// <summary>
    /// To keep track of the current break controlled blocks, to compile break statements.
    /// </summary>
    private Stack<ControlBlock> _breakControlBlocks = new();

    /// <summary>
    /// To keep track of the current continue controlled blocks, to compile continue statements.
    /// </summary>
    private Stack<ControlBlock> _continueControlBlocks = new();

    // for the final state
    private List<VMInstructionBase> _code = [];
    private List<int> _intPool = [];
    private List<float> _fixedPool = [];
    private List<string> _stringPool = [];
    private List<string> _identifierPool = [];
    private Dictionary<string, FunctionInfo> _funcPool = [];
    private Dictionary<string, PluginImport> _pluginImportPool = [];
    private List<StackLocals> _localPool = [];
    private Dictionary<string, ObjectConstructor> _ocPool = [];
    private List<SystemAttribute> _systemAttributes = [];

    private int _lastFunctionID;
    private int _lastPluginImportID;
    private short _lastLocalPoolID;
    private int _lastStaticID;
    private int _lastLocalID;
    private short _lastIdentifierIndex;
    private int _lastOCID;

    private string _fileName;

    public ScriptCompiler(string fileName)
    {
        _fileName = fileName;
    }

    public CompiledScriptState Compile(Esprima.Ast.Script script)
    {
        // All top level statements are put into one _main_ function as the entrypoint
        short mainNameId = (short)AddIdentifier("_main_");
        var topLevelFunc = new FunctionInfo()
        {
            NameID = mainNameId,
            HasReturnValue = false,
            ID = _lastFunctionID++,
        };
        _funcPool.Add("_main_", topLevelFunc);

        // Scan the script for all the function declarations, top level statements & statics
        ScanScript(script.Body);

        // All top level statements have been scanned, create the _main_ entrypoint
        var func = new FunctionDeclaration(
            id: new Identifier("_main_"),
            parameters: NodeList.Create(Enumerable.Empty<Expression>()),
            body: new BlockStatement(NodeList.Create(_topLevelStatements)),
            false,
            false);
        CompileFunctionDeclaration(func, allowMain: true);

        // Proceed to compile the rest of the script.
        CompileStatements(script.Body);

        // System attributes are used to tell/name scripts aka packages apart, they are mandatory.
        // Must end with -1.
        int fileNameID = AddIdentifier(_fileName);
        _systemAttributes.Add(new SystemAttribute() { NameID = (short)fileNameID });
        _systemAttributes.Add(new SystemAttribute() { NameID = -1 });

        return new CompiledScriptState
        {
            EntryPointFunctionID = (uint)_funcPool["_main_"].ID,
            Code = _code,
            IdentifierPool = _identifierPool,
            IntPool = _intPool,
            FixedPool = _fixedPool,
            StringPool = _stringPool,
            FuncPool = _funcPool,
            PluginImportPool = _pluginImportPool,
            StaticPool = _statics,
            LocalPool = _localPool,
            OCPool = _ocPool,
            SystemAttributes = _systemAttributes,
        };
    }

    /// <summary>
    /// Scan the script for all identifiers/functions.
    /// This will work as a "first pass" scan to check for defined symbols.
    /// </summary>
    /// <param name="node"></param>
    private void ScanScript(NodeList<Statement> body)
    {
        foreach (Statement statement in body)
        {
            if (statement.Type == Nodes.FunctionDeclaration || statement.Type == Nodes.StaticDeclaration)
                ScanNode(statement);
            else
            {
                if (statement.Type != Nodes.SourceFileStatement)
                    _topLevelStatements.Add(statement);
            }
        }

        foreach (var statements in body)
        {
            if (statements.Type == Nodes.StaticDeclaration)
            {
                AddIdentifier(statements.As<StaticDeclaration>().Declaration.Declarations[0].Id.As<Identifier>().Name);
            }
        }
    }

    private void ScanNode(Node node, bool allowDeclarator = false)
    {
        if (node.Type == Nodes.VariableDeclaration && !allowDeclarator)
            return;

        if (node is FunctionDeclaration function)
        {
            AddIdentifier(function.Id.Name);

            FunctionInfo frame = new()
            {
                ID = _lastFunctionID++,
                NameID = (short)_identifierPool.IndexOf(function.Id.Name),
                NumArguments = (ushort)function.Params.Count
            };
            _currentFrameForParsing = frame;

            if (!_funcPool.TryAdd(function.Id.Name, frame))
                ThrowCompilationError(node, CompilationErrorMessages.FunctionRedeclaration);
        }
        else if (node is Identifier ident)
        {
            if (_currentFrameForParsing is null)
                return;

            AddIdentifier(ident.Name);
        }
        else if (node is StaticDeclaration staticDecl)
        {
            if (_currentFrameForParsing is not null)
                ThrowCompilationError(CompilationErrorMessages.StaticDeclarationInFunction);

            PreProcessStaticDeclaration(staticDecl);
        }

        if (node.ChildNodes.Count > 0)
        {
            foreach (var subNode in node.ChildNodes)
            {
                if (subNode is null)
                    continue;

                ScanNode(subNode);
            }
        }

        if (node is FunctionDeclaration)
            _currentFrameForParsing = null;
    }

    public void CompileStatements(NodeList<Statement> nodes)
    {
        foreach (var n in nodes)
            CompileStatement(n);
    }

    public void CompileBlockStatement(BlockStatement block)
    {
        CompileStatements(block.Body);
    }

    public void CompileStatement(Node node)
    {
        if (node.Type == Nodes.SourceFileStatement)
            return; // TODO

        if (_currentFunctionFrame is null && (node.Type != Nodes.FunctionDeclaration && node.Type != Nodes.StaticDeclaration))
            return;

        if (node.Type == Nodes.StaticDeclaration)
            return;

        switch (node.Type)
        {
            case Nodes.ExpressionStatement:
                CompileExpressionStatement(node.As<ExpressionStatement>()); break;

            case Nodes.FunctionDeclaration:
                CompileFunctionDeclaration(node.As<FunctionDeclaration>()); break;

            case Nodes.BlockStatement:
                CompileBlockStatement(node.As<BlockStatement>()); break;

            case Nodes.IfStatement:
                CompileIfStatement(node.As<IfStatement>()); break;

            case Nodes.ForStatement:
                CompileForStatement(node.As<ForStatement>()); break;

            case Nodes.WhileStatement:
                CompileWhileStatement(node.As<WhileStatement>()); break;

            case Nodes.DoWhileStatement:
                CompileDoWhileStatement(node.As<DoWhileStatement>()); break;

            case Nodes.StaticDeclaration:
                PreProcessStaticDeclaration(node.As<StaticDeclaration>()); break;

            case Nodes.VariableDeclaration:
                CompileVariableDeclaration(node.As<VariableDeclaration>()); break;

            case Nodes.BreakStatement:
                CompileBreak(node.As<BreakStatement>()); break;

            case Nodes.ContinueStatement:
                CompileContinue(node.As<ContinueStatement>()); break;

            case Nodes.SwitchStatement:
                CompileSwitch(node.As<SwitchStatement>()); break;

            case Nodes.ReturnStatement:
                CompileReturnStatement(node.As<ReturnStatement>()); break;

            default:
                ThrowCompilationError(node, CompilationErrorMessages.UnsupportedStatementType);
                break;
        }
    }

    private void CompileReturnStatement(ReturnStatement retnStatement)
    {
        if (retnStatement.Argument is not null)
        {
            CompileExpression(retnStatement.Argument);
            _currentFunctionFrame.HasReturnValue = true;
        }
        InsertInstruction(new VmRet());
    }

    private void CompileVariableDeclaration(VariableDeclaration varDecl)
    {
        if (_currentFunctionFrame is null)
            ThrowCompilationError(varDecl, CompilationErrorMessages.CannotDeclareLocalsInTopLevel);

        foreach (VariableDeclarator declarator in varDecl.Declarations)
        {
            Identifier id = declarator.Id.As<Identifier>();
            AddIdentifier(id.Name);

            if (_currentFunctionFrame.LocalPoolIndex == -1)
            {
                _currentFunctionFrame.LocalPoolIndex = _lastLocalPoolID++;
                _localPool.Add(new StackLocals());
            }

            StackLocals stackLocals = _localPool[_currentFunctionFrame.LocalPoolIndex];

            if (stackLocals.NamedLocals.ContainsKey(id.Name))
                ThrowCompilationError(id, CompilationErrorMessages.VariableRedeclaration);

            VmVariable local = ProcessDeclarator(declarator, varDecl.Kind);
            local.ID = _lastLocalID++;

            stackLocals.NamedLocals.Add(id.Name, local);
            stackLocals.Locals.Add(local);

        }
    }

    private void CompileFunctionDeclaration(FunctionDeclaration funcDecl, bool allowMain = false)
    {
        if (_currentFunctionFrame is not null)
            ThrowCompilationError(funcDecl, CompilationErrorMessages.NestedFunctionDeclaration);

        if (!allowMain && funcDecl.Id.Name == "_main_")
            ThrowCompilationError(funcDecl, CompilationErrorMessages.MainFunctionAlreadyDeclared);

        _currentFunctionFrame = _funcPool[funcDecl.Id.Name];
        _currentFunctionFrame.CodeStartOffset = _currentPc;

        foreach (Expression arg in funcDecl.Params)
        {
            if (arg.Type != Nodes.Identifier)
                ThrowCompilationError(arg, "Expected identifier for function argument");

            Identifier argIdentifier = arg.As<Identifier>();

            AddIdentifier(argIdentifier.Name);
            _currentFunctionFrame.Arguments.Add(argIdentifier.Name);
        }

        /*
        var test = new ExpressionStatement(new CallExpression(
            callee: new StaticMemberExpression(new Identifier("deb"), new Identifier("put"), false),
            args: NodeList.Create<Expression>(new[] { new Literal(funcDecl.Id.Name, funcDecl.Id.Name) }),
            optional: false
        ));

        CompileStatement(test);
        */

        CompileStatement(funcDecl.Body);

        _currentFunctionFrame.CodeEndOffset = _currentPc;
        if (funcDecl.Id.Name == "_main_")
            InsertInstruction(new VmExit());
        else
            InsertInstruction(new VmRet());

        if (_currentFunctionFrame.LocalPoolIndex != -1)
            _currentFunctionFrame.NumLocals = (ushort)_localPool[_currentFunctionFrame.LocalPoolIndex].NamedLocals.Count;

        _currentFunctionFrame = null;
        _lastLocalID = 0;
    }

    private void PreProcessStaticDeclaration(StaticDeclaration staticDecl)
    {
        if (_currentFunctionFrame is not null)
            ThrowCompilationError(staticDecl, CompilationErrorMessages.StaticDeclarationInFunction);

        VariableDeclaration decl = staticDecl.Declaration;
        foreach (VariableDeclarator dec in decl.Declarations)
        {
            Identifier id = dec.Id.As<Identifier>();
            if (_definedStatics.ContainsKey(id.Name))
                ThrowCompilationError(id, CompilationErrorMessages.StaticAlreadyDeclared);

            VmVariable local = ProcessDeclarator(dec, decl.Kind, isStatic: true);
            _definedStatics.Add(id.Name, local.ID);
        }
    }

    private VmVariable ProcessDeclarator(VariableDeclarator decl, VariableDeclarationKind kind, bool isStatic = false)
    {
        var type = kind switch
        {
            VariableDeclarationKind.Int => LocalType.Int,
            VariableDeclarationKind.Fixed => LocalType.Fixed,
            VariableDeclarationKind.String => LocalType.String,
            VariableDeclarationKind.Array => LocalType.Array,
        };

        VmVariable scVar = new VmVariable()
        {
            Type = type,
            ID = _lastStaticID++,
        };

        if (decl.Init.Type == Nodes.Literal)
        {
            Literal literal = decl.Init.As<Literal>();
            if (literal.TokenType == TokenType.StringLiteral)
            {
                if (!_stringPool.Contains((string)literal.Value))
                    _stringPool.Add((string)literal.Value);

                scVar.Value = _stringPool.IndexOf((string)literal.Value);
            }
            else
                scVar.Value = literal.Value;

            if (isStatic)
                _statics.Add(scVar);
        }
        else if (decl.Init.Type == Nodes.ArrayExpression)
        {
            ArrayExpression arrayExp = decl.Init.As<ArrayExpression>();
            scVar.Type = LocalType.Array;
            scVar.ArraySize = (uint)arrayExp.Elements.Count;
            scVar.Value = _lastStaticID;

            if (isStatic)
            {
                _statics.Add(scVar);
                ProcessArray(arrayExp, _statics, ref _lastStaticID);
            }
            else
            {
                var locals = _localPool[_currentFunctionFrame.LocalPoolIndex].Locals;
                ProcessArray(arrayExp, locals, ref _lastLocalID);
            }

        }
        else
            ThrowCompilationError(decl.Init, CompilationErrorMessages.UnexpectedVariableDeclaratorType);

        return scVar;
    }

    private void ProcessArray(ArrayExpression arrayExp, List<VmVariable> varList, ref int lastIdx)
    {
        int start = lastIdx;
        foreach (var elem in arrayExp.Elements)
        {
            VmVariable arrElem = new VmVariable();
            arrElem.ID = lastIdx++;

            if (elem.Type == Nodes.ArrayExpression)
            {
                arrElem.Type = LocalType.Array;
            }
            else if (elem.Type == Nodes.UnaryExpression)
            {
                var unary = elem.As<UnaryExpression>();
                if (unary.Argument.Type == Nodes.Literal && unary.Operator == UnaryOperator.Minus)
                {
                    Literal literal = unary.Argument.As<Literal>();

                    switch (literal.NumericTokenType)
                    {
                        case NumericTokenType.Float:
                            CompileFloatLiteral(-(float)literal.Value);
                            break;

                        case NumericTokenType.Integer:
                            CompileIntegerLiteral(-(int)literal.Value);
                            break;

                        default:
                            ThrowCompilationError(literal, CompilationErrorMessages.UnaryInvalidLiteralType);
                            break;
                    }
                }
                else
                    ThrowCompilationError(elem, CompilationErrorMessages.UnsupportedArrayElement);
            }
            else if (elem.Type == Nodes.Literal)
            {
                Literal literal = elem.As<Literal>();
                if (literal.NumericTokenType == NumericTokenType.Integer)
                {
                    arrElem.Type = LocalType.Int;
                    arrElem.Value = literal.Value;
                }
                else if (literal.NumericTokenType == NumericTokenType.Float)
                {
                    arrElem.Type = LocalType.Fixed;
                    arrElem.Value = literal.Value;
                }
                else if (literal.TokenType == TokenType.StringLiteral)
                {
                    arrElem.Type = LocalType.String;

                    if (!_stringPool.Contains(literal.Value))
                        _stringPool.Add((string)literal.Value);

                    arrElem.Value = _stringPool.IndexOf((string)literal.Value);
                }
            }
            else
                ThrowCompilationError(elem, CompilationErrorMessages.UnsupportedArrayElement);

            _statics.Add(arrElem);
        }


        for (int i = 0; i < arrayExp.Elements.Count; i++)
        {
            VmVariable subArr = _statics[start + i];
            if (subArr.Type == LocalType.Array)
            {
                subArr.Type = LocalType.Array;
                subArr.ArraySize = (uint)arrayExp.Elements.Count;
                subArr.Value = lastIdx;

                ProcessArray(arrayExp.Elements[i].As<ArrayExpression>(), varList, ref lastIdx);
            }
        }
    }

    private void CompileExpressionStatement(ExpressionStatement expStatement)
    {
        if (expStatement.Expression.Type == Nodes.UpdateExpression)
            CompileUpdateExpression(expStatement.Expression.As<UpdateExpression>(), keepResult: false);
        else if (expStatement.Expression.Type == Nodes.CallExpression)
            CompileCall(expStatement.Expression.As<CallExpression>(), popReturnValue: true);
        else
            CompileExpression(expStatement.Expression);
    }

    private void CompileExpression(Expression exp)
    {
        switch (exp.Type)
        {
            case Nodes.Identifier:
                CompileIdentifier(exp.As<Identifier>());
                break;

            case Nodes.CallExpression:
                CompileCall(exp.As<CallExpression>());
                break;

            case Nodes.Literal:
                CompileLiteral(exp.As<Literal>());
                break;

            case Nodes.UnaryExpression:
                CompileUnaryExpression(exp.As<UnaryExpression>()); break;

            case Nodes.BinaryExpression:
                CompileBinaryExpression(exp.As<BinaryExpression>()); break;

            case Nodes.AssignmentExpression:
                CompileAssignmentExpression(exp.As<AssignmentExpression>()); break;

            case Nodes.LogicalExpression:
                CompileLogicalExpression(exp.As<BinaryExpression>()); break;

            case Nodes.MemberExpression:
                CompileMemberExpression(exp.As<MemberExpression>()); break;

            case Nodes.UpdateExpression:
                CompileUpdateExpression(exp.As<UpdateExpression>()); break;

            default:
                ThrowCompilationError(exp, CompilationErrorMessages.UnsupportedExpression);
                break;
        }
    }

    private void CompileUpdateExpression(UpdateExpression exp, bool keepResult = true)
    {
        CompileExpression(exp.Argument);

        VMInstructionBase inst = exp.Operator switch
        {
            UnaryOperator.Increment => new VmIncrement(),
            UnaryOperator.Decrement => new VmDecrement(),
            _ => throw new Exception("Unsupported update expression"),
        };

        InsertInstruction(inst);

        if (exp.Argument.Type != Nodes.CallExpression)
        {
            CompileExpressionAssignment(exp.Argument);

            if (keepResult)
                CompileExpression(exp.Argument);
        }
    }

    private void CompileMemberExpression(MemberExpression exp)
    {
        if (exp is ComputedMemberExpression compMember)
        {
            if (compMember.Property.Type == Nodes.Literal)
            {
                Literal lit = compMember.Property.As<Literal>();
                if (lit.NumericTokenType != NumericTokenType.Integer)
                    ThrowCompilationError(lit, CompilationErrorMessages.ExpectedExpressionOrNumberInArrayAccess);
            }

            CompileExpression(compMember.Object);
            CompileExpression(compMember.Property);
            InsertInstruction(new VmLoadArray());
        }
        else if (exp is AttributeMemberExpression attrMember)
        {
            if (attrMember.Property.Type != Nodes.Identifier)
                ThrowCompilationError(attrMember.Property, CompilationErrorMessages.ExpectedExpressionOrNumberInAttributeAccess);

            CompileExpression(attrMember.Object);

            Identifier propIdentifier = attrMember.Property.As<Identifier>();
            AddIdentifier(propIdentifier.Name);

            int idIndex = _identifierPool.IndexOf(propIdentifier.Name);
            if (idIndex == -1)
                ThrowCompilationError(propIdentifier, "bug? identifier not found?");

            if (idIndex <= byte.MaxValue)
                InsertInstruction(new VmGetter((byte)idIndex));
            else if (idIndex <= ushort.MaxValue)
                InsertInstruction(new VmGetter_Word((ushort)idIndex));
            else
                ThrowCompilationError(propIdentifier, CompilationErrorMessages.ExceededMaximumIdentifierCount);
        }
    }

    private void CompileLogicalExpression(BinaryExpression binExp)
    {
        if (binExp.Operator == BinaryOperator.LogicalOr || binExp.Operator == BinaryOperator.LogicalAnd)
        {
            CompileExpression(binExp.Right);
            CompileExpression(binExp.Left);

            if (binExp.Operator == BinaryOperator.LogicalOr)
                InsertInstruction(new VmLogicalOr());
            else
                InsertInstruction(new VmLogicalAnd());
        }
    }

    private void CompileUnaryExpression(UnaryExpression unaryExpr)
    {
        if (unaryExpr.Argument.Type == Nodes.Literal && unaryExpr.Operator == UnaryOperator.Minus)
        {
            Literal literal = unaryExpr.Argument.As<Literal>();

            switch (literal.NumericTokenType)
            {
                case NumericTokenType.Float:
                    CompileFloatLiteral(-(float)literal.Value);
                    break;

                case NumericTokenType.Integer:
                    CompileIntegerLiteral(-(int)literal.Value);
                    break;

                default:
                    ThrowCompilationError(literal, CompilationErrorMessages.UnaryInvalidLiteralType);
                    break;
            }

            return;
        }
        else if (unaryExpr.Operator == UnaryOperator.LogicalNot)
        {
            CompileExpression(unaryExpr.Argument);
            InsertInstruction(new VmLogicalNot());
        }
        else
            ThrowCompilationError(unaryExpr, CompilationErrorMessages.UnsupportedUnaryExpression);
    }

    private void CompileAssignmentExpression(AssignmentExpression assignmentExpression)
    {
        if (assignmentExpression.Operator == AssignmentOperator.Assign)
        {
            CompileExpression(assignmentExpression.Right);
            CompileExpressionAssignment(assignmentExpression.Left);
        }
        else
        {
            VMInstructionBase inst = assignmentExpression.Operator switch
            {
                AssignmentOperator.PlusAssign => new VmAdd(),
                AssignmentOperator.MinusAssign => new VmSubtract(),
                AssignmentOperator.TimesAssign => new VmMultiply(),
                AssignmentOperator.DivideAssign => new VmDivide(),
                AssignmentOperator.ModuloAssign => new VmModulo(),
                AssignmentOperator.LeftShiftAssign => new VmBitwiseLeftShift(),
                AssignmentOperator.RightShiftAssign => new VmBitwiseRightShift(),
                AssignmentOperator.BitwiseOrAssign => new VmBitwiseOr(),
                AssignmentOperator.BitwiseAndAssign => new VmBitwiseAnd(),
                _ => null
            };

            if (inst is null)
                ThrowCompilationError(assignmentExpression, CompilationErrorMessages.UnsupportedAssignmentExpression);

            CompileExpression(assignmentExpression.Right);
            CompileExpression(assignmentExpression.Left);

            InsertInstruction(inst);

            CompileExpressionAssignment(assignmentExpression.Left);
        }

    }

    private void CompileExpressionAssignment(Expression exp)
    {
        if (exp.Type == Nodes.Identifier)
        {
            CompileIdentifierAssignment(exp.As<Identifier>());
        }
        else if (exp.Type == Nodes.MemberExpression)
        {
            if (exp is AttributeMemberExpression attrMemberExpr)
            {
                if (attrMemberExpr.Object.Type != Nodes.Identifier)
                    ThrowCompilationError(attrMemberExpr.Object, CompilationErrorMessages.InvalidAttributeMemberExpressionAssignment);

                if (attrMemberExpr.Property.Type != Nodes.Identifier)
                    ThrowCompilationError(attrMemberExpr.Property, CompilationErrorMessages.InvalidAttributeMemberExpressionAssignment);

                CompileIdentifier(attrMemberExpr.Object.As<Identifier>());

                Identifier propIdentifier = attrMemberExpr.Property.As<Identifier>();
                AddIdentifier(propIdentifier.Name);

                int idIndex = _identifierPool.IndexOf(propIdentifier.Name);
                if (idIndex == -1)
                    ThrowCompilationError(propIdentifier, "bug? identifier not found?");

                if (idIndex <= byte.MaxValue)
                    InsertInstruction(new VmSetter((byte)idIndex));
                else if (idIndex <= ushort.MaxValue)
                    InsertInstruction(new VmSetter_Word((ushort)idIndex));
                else
                    ThrowCompilationError(propIdentifier, CompilationErrorMessages.ExceededMaximumIdentifierCount);
            }
            else if (exp is ComputedMemberExpression compMemberExpr)
            {
                CompileExpression(compMemberExpr.Object);

                if (compMemberExpr.Property.Type == Nodes.Literal)
                {
                    Literal lit = compMemberExpr.Property.As<Literal>();
                    if (lit.NumericTokenType != NumericTokenType.Integer)
                        ThrowCompilationError(lit, CompilationErrorMessages.ExpectedExpressionOrNumberInArrayAccess);
                }

                CompileExpression(compMemberExpr.Property);

                InsertInstruction(new VmStoreArray());
            }
            else
                ThrowCompilationError(exp, CompilationErrorMessages.UnexpectedMemberAssignmentType);
        }
        else
            ThrowCompilationError(exp, CompilationErrorMessages.InvalidAssignmentTarget);
    }

    private void CompileIdentifierAssignment(Identifier id)
    {
        DeclType type = GetVarType(id.Name);

        switch (type)
        {
            case DeclType.Local:
                int localIndex = _localPool[_currentFunctionFrame.LocalPoolIndex].NamedLocals[id.Name].ID;
                switch (localIndex)
                {
                    case 0:
                        InsertInstruction(new VmStore0()); break;
                    case 1:
                        InsertInstruction(new VmStore1()); break;
                    case 2:
                        InsertInstruction(new VmStore2()); break;
                    case 3:
                        InsertInstruction(new VmStore3()); break;

                    default:
                        InsertInstruction(new VmStore((byte)localIndex)); break;
                }
                break;

            case DeclType.Static:
                int staticIndex = _definedStatics[id.Name];
                if (staticIndex <= byte.MaxValue)
                    InsertInstruction(new VmStoreStatic((byte)staticIndex));
                else if (staticIndex <= ushort.MaxValue)
                    InsertInstruction(new VmStoreStatic_Word((ushort)staticIndex));
                else
                    ThrowCompilationError(id, CompilationErrorMessages.ExceededMaximumStaticCount);
                break;

            case DeclType.Function:
                ThrowCompilationError(id, CompilationErrorMessages.AssignToFunction);
                break;

            case DeclType.FunctionArgument:
                int argIndex = _currentFunctionFrame.Arguments.IndexOf(id.Name);
                if (argIndex == -1)
                    ThrowCompilationError(id, "bug? Undeclared argument?");

                switch (argIndex)
                {
                    case 0:
                        InsertInstruction(new VmStoreArgument0()); break;
                    case 1:
                        InsertInstruction(new VmStoreArgument1()); break;
                    case 2:
                        InsertInstruction(new VmStoreArgument2()); break;
                    case 3:
                        InsertInstruction(new VmStoreArgument3()); break;

                    default:
                        if (argIndex <= byte.MaxValue)
                            InsertInstruction(new VmStoreArgument((byte)argIndex));
                        else
                            ThrowCompilationError(id, CompilationErrorMessages.ExceededMaximumArgumentCount);
                        break;
                }
                break;

            default:
                ThrowCompilationError(id, "Assignment to invalid identifier");
                break;
        }
    }

    public void CompileSwitch(SwitchStatement switchStatement)
    {
        SwitchBlock switchCtx = EnterSwitch();

        CompileExpression(switchStatement.Discriminant);

        int startOffset = (int)_currentPc;

        // switch cases have to be sorted (game uses bsearch)
        SortedList<long, SwitchCase> orderedSwitchCases = new();

        int numBranches = 0;
        bool hasDefault = false;
        for (int i = 0; i < switchStatement.Cases.Count; i++)
        {
            SwitchCase swCase = switchStatement.Cases[i];
            if (swCase.Test is not null)
            {
                if (swCase.Test.Type != Nodes.Literal)
                    ThrowCompilationError(swCase.Test, CompilationErrorMessages.ExpectedIntegerLiteralForSwitchTest);

                Literal lit = swCase.Test.As<Literal>();
                if (lit.NumericTokenType != NumericTokenType.Integer)
                    ThrowCompilationError(lit, CompilationErrorMessages.ExpectedIntegerLiteralForSwitchTest);

                if (orderedSwitchCases.ContainsKey((int)lit.Value))
                    ThrowCompilationError(lit, CompilationErrorMessages.DuplicateSwitchCaseTest);

                orderedSwitchCases.Add((int)lit.Value, swCase);

                numBranches++;
                if (numBranches > byte.MaxValue)
                    ThrowCompilationError(switchStatement, CompilationErrorMessages.TooManySwitchCases);
            }
            else
            {
                hasDefault = true;
                orderedSwitchCases.Add(long.MaxValue, swCase); // long hack for sorting
            }
        }

        var swInstruction = new VmSwitch() { NumBranches = (byte)numBranches };
        InsertInstruction(swInstruction);

        foreach (SwitchCase swCase in orderedSwitchCases.Values)
        {
            if (swCase.Test is not null) // Actual case
            {
                Literal lit = swCase.Test.As<Literal>();
                swInstruction.Branches.Add(new VmSwitch.VmSwitchBranch(
                    (int)lit.Value,
                    (int)(_currentPc - startOffset)
                ));

                CompileStatements(swCase.Consequent);
            }
            else // Default case
            {
                swInstruction.DefaultCaseRelativeJumpOffset = (int)(_currentPc - startOffset);

                if (swCase.Consequent.Count != 0 && swCase.Consequent[0].Type != Nodes.BreakStatement)
                    CompileStatements(swCase.Consequent);
            }
        }

        // Update break case jumps
        for (int i = 0; i < switchCtx.BreakJumps.Count; i++)
        {
            (int PC, VmJump Instruction) swCase = switchCtx.BreakJumps[i];
            swCase.Instruction.JumpRelativeOffset = (short)(_currentPc - swCase.PC);
        }

        if (!hasDefault)
            swInstruction.DefaultCaseRelativeJumpOffset = (int)(_currentPc - startOffset);

        swInstruction.NumBranches = (byte)numBranches;

        LeaveSwitch();
    }


    private DeclType GetVarType(string identifier, bool allowOC = false)
    {
        if (allowOC && OCs.Contains(identifier))
            return DeclType.OC;

        // order should not be changed
        if (_currentFunctionFrame is not null)
        {
            if (_currentFunctionFrame.LocalPoolIndex != -1 && _localPool[_currentFunctionFrame.LocalPoolIndex].NamedLocals.ContainsKey(identifier))
                return DeclType.Local;
            if (_currentFunctionFrame.Arguments.Contains(identifier))
                return DeclType.FunctionArgument;
        }

        if (_definedStatics.ContainsKey(identifier))
            return DeclType.Static;
        else if (_funcPool.ContainsKey(identifier))
            return DeclType.Function;
        else 
            return DeclType.Undefined;
    }

    private void CompileIfStatement(IfStatement ifStatement)
    {
        CompileTestStatement(ifStatement.Test);

        uint previousPc = (ushort)_currentPc;
        var jumpIfFalse = new VmJumpFalse();
        InsertInstruction(jumpIfFalse);

        CompileStatement(ifStatement.Consequent);

        if (ifStatement.Alternate is not null)
        {
            var alternateSkip = new VmJump();
            int previousPc2 = (ushort)_currentPc;
            InsertInstruction(alternateSkip);

            jumpIfFalse.JumpRelativeOffset = (short)((_currentPc - previousPc));

            CompileStatement(ifStatement.Alternate);
            alternateSkip.JumpRelativeOffset = (short)((_currentPc - previousPc2));
        }
        else
        {
            jumpIfFalse.JumpRelativeOffset = (short)((_currentPc - previousPc));
        }
    }

    private void CompileForStatement(ForStatement forStatement)
    {
        LoopBlock loopBlock = EnterLoop();

        if (forStatement.Init is not null)
        {
            switch (forStatement.Init.Type)
            {
                case Nodes.VariableDeclaration:
                    CompileVariableDeclaration(forStatement.Init as VariableDeclaration); break;
                case Nodes.AssignmentExpression:
                    CompileAssignmentExpression(forStatement.Init as AssignmentExpression); break;
                case Nodes.Identifier:
                    CompileIdentifier(forStatement.Init as Identifier); break;
                case Nodes.CallExpression:
                    CompileCall(forStatement.Init as CallExpression, popReturnValue: true);
                    break;
                default:
                    ThrowCompilationError(forStatement.Init, "Unsupported for init type");
                    break;
            }
        }

        int testOffset = (int)_currentPc;
        if (forStatement.Test is not null)
            CompileTestStatement(forStatement.Test);

        int bodyStartOffset = (int)_currentPc;
        var bodyJump = new VmJumpFalse();
        InsertInstruction(bodyJump);
        CompileStatement(forStatement.Body);

        if (forStatement.Update is not null)
            CompileTestStatement(forStatement.Update);

        var jumpBack = new VmJump();
        jumpBack.JumpRelativeOffset = (short)(testOffset - _currentPc);
        InsertInstruction(jumpBack);

        // Reached bottom, proceed to do update
        // But first, process continue if any
        foreach (var (JumpLocation, Instruction) in loopBlock.ContinueJumps)
            Instruction.JumpRelativeOffset = (short)(testOffset - JumpLocation);

        bodyJump.JumpRelativeOffset = (short)(_currentPc - bodyStartOffset);

        // Process break jumps before doing the final exit
        foreach (var (JumpLocation, Instruction) in loopBlock.BreakJumps)
            Instruction.JumpRelativeOffset = (short)(_currentPc - JumpLocation);

        LeaveLoop();
    }

    private void CompileWhileStatement(WhileStatement whileStatement)
    {
        LoopBlock loopBlock = EnterLoop();

        int testOffset = (int)_currentPc;
        if (whileStatement is not null)
            CompileTestStatement(whileStatement.Test);

        int bodyStartOffset = (int)_currentPc;
        var bodyJump = new VmJumpFalse();
        InsertInstruction(bodyJump);

        CompileStatement(whileStatement.Body);
        var jumpBack = new VmJump();
        jumpBack.JumpRelativeOffset = (short)(testOffset - _currentPc);
        InsertInstruction(jumpBack);

        // Reached bottom, proceed to do update
        // But first, process continue if any
        foreach (var (JumpLocation, Instruction) in loopBlock.ContinueJumps)
            Instruction.JumpRelativeOffset = (short)(testOffset - JumpLocation);

        bodyJump.JumpRelativeOffset = (short)(_currentPc - bodyStartOffset);

        // Process break jumps before doing the final exit
        foreach (var (JumpLocation, Instruction) in loopBlock.BreakJumps)
            Instruction.JumpRelativeOffset = (short)(_currentPc - JumpLocation);

        LeaveLoop();
    }

    private void CompileDoWhileStatement(DoWhileStatement doWhile)
    {
        LoopBlock loopBlock = EnterLoop();

        int bodyStartOffset = (int)_currentPc;
        CompileStatement(doWhile.Body);

        int testInsOffset = (int)_currentPc;
        if (doWhile.Test is not null)
            CompileExpression(doWhile.Test);

        // Reached bottom, proceed to do update
        // But first, process continue if any
        foreach (var (JumpLocation, Instruction) in loopBlock.ContinueJumps)
            Instruction.JumpRelativeOffset = (short)(testInsOffset - JumpLocation);

        int endloopJumperOffset = (short)_currentPc;
        var jumpBack = new VmJumpFalse();
        InsertInstruction(jumpBack);

        var startJmp = new VmJump();
        startJmp.JumpRelativeOffset = (short)(bodyStartOffset - _currentPc);
        InsertInstruction(startJmp);

        // Process break jumps before doing the final exit
        int loopEndIndex = (int)_currentPc;
        foreach (var (JumpLocation, Instruction) in loopBlock.BreakJumps)
            Instruction.JumpRelativeOffset = (short)(loopEndIndex - JumpLocation);

        jumpBack.JumpRelativeOffset = (short)(_currentPc - endloopJumperOffset);
        LeaveLoop();
    }

    private void CompileTestStatement(Expression testExpression)
    {
        if (testExpression.Type == Nodes.UpdateExpression)
        {
            CompileUpdateExpression(testExpression as UpdateExpression, keepResult: false);
        }
        else
        {
            CompileExpression(testExpression);
        }
    }

    public void CompileContinue(ContinueStatement continueStatement)
    {
        if (_continueControlBlocks.Count == 0)
            ThrowCompilationError(continueStatement, CompilationErrorMessages.ContinueWithoutContextualScope);

        LoopBlock loop = (LoopBlock)GetLastContinueControlledScope();

        VmJump continueJmp = new VmJump();
        loop.ContinueJumps.Add(((int)_currentPc, continueJmp));
        InsertInstruction(continueJmp);
    }

    public void CompileBreak(BreakStatement breakStatement)
    {
        var scope = GetLastBreakControlledScope();
        if (scope is LoopBlock loopCtx)
        {
            VmJump breakJmp = new VmJump();
            loopCtx.BreakJumps.Add(((int)_currentPc, breakJmp));
            InsertInstruction(breakJmp);
        }
        else if (scope is SwitchBlock swContext)
        {
            VmJump breakJmp = new VmJump();
            swContext.BreakJumps.Add(((int)_currentPc, breakJmp));
            InsertInstruction(breakJmp);
        }
        else
        {
            ThrowCompilationError(breakStatement, CompilationErrorMessages.BreakWithoutContextualScope);
        }
    }

    private void CompileCall(CallExpression call, bool popReturnValue = false)
    {
        if (call.Callee.Type == Nodes.Identifier)
        {
            Identifier identifier = call.Callee.As<Identifier>();
            if (identifier.Name == "next")
            {
                if (call.Arguments.Count > 0)
                    ThrowCompilationError(call, CompilationErrorMessages.InvalidNextWithArguments);

                InsertInstruction(new VmNext());
                return;
            }
            else if (identifier.Name == "typeof")
            {
                if (call.Arguments.Count != 1)
                    ThrowCompilationError(call, CompilationErrorMessages.MissingTypeOfArgument);

                CompileExpression(call.Arguments[0]);
                InsertInstruction(new VmTypeOf());
                return;
            }
            else if (identifier.Name == "sizeof")
            {
                if (call.Arguments.Count != 1)
                    ThrowCompilationError(call, CompilationErrorMessages.MissingTypeOfArgument);

                CompileExpression(call.Arguments[0]);
                InsertInstruction(new VmSizeOf());
                return;
            }
        }

        for (int i = call.Arguments.Count - 1; i >= 0; i--)
        {
            Expression arg = call.Arguments[i];
            CompileExpression(arg);
        }

        if (!popReturnValue)
        {
            CompileIntegerLiteral(call.Arguments.Count | 0x100, allowSpecifiedIntegerInstruction: true);
        }
        else
            CompileIntegerLiteral(call.Arguments.Count, allowSpecifiedIntegerInstruction: true);

        if (call.Callee is Identifier funcIdentifier)
        {
            DeclType type = GetVarType(funcIdentifier.Name, allowOC: true);
            if (type == DeclType.OC)
            {
                AddOC(funcIdentifier.Name);

                int idx = _ocPool[funcIdentifier.Name].ID;
                if (idx < byte.MaxValue)
                    InsertInstruction(new VmGetOC((byte)idx));
                else if (idx < ushort.MaxValue)
                    InsertInstruction(new VmGetOC_Word((ushort)idx));
                else
                    ThrowCompilationError(funcIdentifier, CompilationErrorMessages.ExceededMaximumOCCount);
            }
            else if (type == DeclType.Function)
            {
                Identifier id = call.Callee.As<Identifier>();
                FunctionInfo functionFrame = _funcPool[id.Name];
                if (functionFrame.ID <= byte.MaxValue)
                    InsertInstruction(new VmCall((byte)functionFrame.ID));
                else if (functionFrame.ID <= ushort.MaxValue)
                    InsertInstruction(new VmCall_Word((ushort)functionFrame.ID));
                else
                    ThrowCompilationError(id, CompilationErrorMessages.ExceededMaximumFunctionCount);
            }
            else if (type == DeclType.Local)
            {
                // Call indirect
                CompileExpression(call.Callee);
                InsertInstruction(new VmCallIndirect());
            }
            else
                ThrowCompilationError(call.Callee, CompilationErrorMessages.CallToUndeclaredFunction);
        }
        else if (call.Callee.Type == Nodes.MemberExpression)
        {
            MemberExpression memberExpression = (MemberExpression)call.Callee;
            if (memberExpression.Object.Type != Nodes.Identifier)
                ThrowCompilationError(memberExpression.Object, "Unexpected syntax");

            if (memberExpression.Property.Type != Nodes.Identifier)
                ThrowCompilationError(memberExpression.Property, "Unexpected syntax");

            if (call.Callee is OCMemberExpression)
            {
                CompileIdentifier(memberExpression.Object.As<Identifier>());

                Identifier propId = memberExpression.Property.As<Identifier>();
                AddIdentifier(propId.Name);

                int idx = _identifierPool.IndexOf(propId.Name);
                if (idx == -1)
                    ThrowCompilationError(propId, "bug? identifier not found?");

                if (idx <= byte.MaxValue)
                    InsertInstruction(new VmSend((byte)idx));
                else if (idx <= ushort.MaxValue)
                    InsertInstruction(new VmSend_Word((ushort)idx));
                else
                    ThrowCompilationError(propId, CompilationErrorMessages.ExceededMaximumIdentifierCount);
            }
            else if (call.Callee is StaticMemberExpression)
            {
                string objName = memberExpression.Object.As<Identifier>().Name;
                string propName = memberExpression.Property.As<Identifier>().Name;

                string pluginPath = $"{objName}::{propName}";
                if (!_pluginImportPool.TryGetValue(pluginPath, out PluginImport pluginImport))
                {
                    AddIdentifier(objName);
                    AddIdentifier(propName);

                    int objNameIdIndex = _identifierPool.IndexOf(objName);
                    int propNameIdIndex = _identifierPool.IndexOf(propName);

                    pluginImport = new PluginImport()
                    {
                        ID = _lastPluginImportID++,
                        PluginNameIDIndex = (ushort)objNameIdIndex, 
                        FunctionNameIDIndex = (ushort)propNameIdIndex };
                    _pluginImportPool.Add(pluginPath, pluginImport);
                }

                if (pluginImport.ID >= 0 && pluginImport.ID <= byte.MaxValue)
                    InsertInstruction(new VmPlugin((byte)pluginImport.ID));
                else if (pluginImport.ID >= 0 && pluginImport.ID <= ushort.MaxValue)
                    InsertInstruction(new VmPlugin_Word((ushort)pluginImport.ID));
                else
                    ThrowCompilationError(call.Callee, CompilationErrorMessages.ExceededMaximumPluginImports);
            }
        }
        else
            ThrowCompilationError(call.Callee, CompilationErrorMessages.UnsupportedCallType);
    }

    private void CompileBinaryExpression(BinaryExpression binExpression)
    {
        if (binExpression.Right.Type == Nodes.Literal && (binExpression.Operator == BinaryOperator.Plus || binExpression.Operator == BinaryOperator.Minus))
        {
            // something +/- 1? Short to increment/decrement

            Literal lit = binExpression.Right.As<Literal>();
            if (lit.NumericTokenType == NumericTokenType.Integer && ((int)lit.Value) == 1)
            {
                CompileExpression(binExpression.Left);
                if (binExpression.Operator == BinaryOperator.Plus)
                    InsertInstruction(new VmIncrement());
                else
                    InsertInstruction(new VmDecrement());

                return;
            }
        }
        
        CompileExpression(binExpression.Right);
        CompileExpression(binExpression.Left);

        switch (binExpression.Operator)
        {
            case BinaryOperator.Equal:
                InsertInstruction(new VmEquals());
                break;
            case BinaryOperator.NotEqual:
                InsertInstruction(new VmNotEquals());
                break;
            case BinaryOperator.Greater:
                InsertInstruction(new VmGreaterThan());
                break;
            case BinaryOperator.GreaterOrEqual:
                InsertInstruction(new VmGreaterOrEquals());
                break;
            case BinaryOperator.Less:
                InsertInstruction(new VmLesserThan());
                break;
            case BinaryOperator.LessOrEqual:
                InsertInstruction(new VmLesserOrEquals());
                break;
            case BinaryOperator.Plus:
                InsertInstruction(new VmAdd());
                break;
            case BinaryOperator.Minus:
                InsertInstruction(new VmSubtract());
                break;
            case BinaryOperator.Times:
                InsertInstruction(new VmMultiply());
                break;
            case BinaryOperator.Modulo:
                InsertInstruction(new VmModulo());
                break;
            case BinaryOperator.Divide:
                InsertInstruction(new VmDivide());
                break;
            case BinaryOperator.BitwiseAnd:
                InsertInstruction(new VmBitwiseAnd());
                break;
            case BinaryOperator.BitwiseOr:
                InsertInstruction(new VmBitwiseOr());
                break;
            case BinaryOperator.LeftShift:
                InsertInstruction(new VmBitwiseLeftShift());
                break;
            case BinaryOperator.RightShift:
                InsertInstruction(new VmBitwiseRightShift());
                break;
            default:
                ThrowCompilationError(binExpression, "Unsupported binary operator");
                break;
        }
    }

    private void CompileIdentifier(Identifier identifier)
    {
        DeclType type = GetVarType(identifier.Name);

        switch (type)
        {
            case DeclType.Static:
                if (!_definedStatics.TryGetValue(identifier.Name, out int staticIdx))
                    ThrowCompilationError(identifier, "bug? Undeclared static?");

                if (staticIdx <= byte.MaxValue)
                    InsertInstruction(new VmLoadStatic((byte)staticIdx));
                else if (staticIdx <= ushort.MaxValue)
                    InsertInstruction(new VmLoadStatic_Word((ushort)staticIdx));
                else
                    ThrowCompilationError(identifier, CompilationErrorMessages.ExceededMaximumStaticCount);

                break;

            case DeclType.Function:
                FunctionInfo functionFrame = _funcPool[identifier.Name];
                if (functionFrame.ID <= byte.MaxValue)
                    InsertInstruction(new VmLoadFunction((byte)functionFrame.ID));
                else if (functionFrame.ID <= ushort.MaxValue)
                    InsertInstruction(new VmLoadFunction_Word((ushort)functionFrame.ID));
                else
                    ThrowCompilationError(identifier, CompilationErrorMessages.ExceededMaximumFunctionCount);

                break;

            case DeclType.Local:
                int idIndex = _localPool[_currentFunctionFrame.LocalPoolIndex].NamedLocals[identifier.Name].ID;
                if (idIndex == -1)
                    ThrowCompilationError(identifier, "bug? Undeclared local?");

                if (idIndex > byte.MaxValue)
                    ThrowCompilationError(identifier, CompilationErrorMessages.ExceededMaximumLocalsCount);

                switch (idIndex)
                {
                    case 0:
                        InsertInstruction(new VmLoad0()); break;
                    case 1:
                        InsertInstruction(new VmLoad1()); break;
                    case 2:
                        InsertInstruction(new VmLoad2()); break;
                    case 3:
                        InsertInstruction(new VmLoad3()); break;

                    default:
                        if (idIndex <= byte.MaxValue)
                            InsertInstruction(new VmLoad((byte)idIndex));
                        else
                            ThrowCompilationError(identifier, CompilationErrorMessages.ExceededMaximumLocalsCount);
                        break;
                }
                break;

            case DeclType.FunctionArgument:
                int argIndex = _currentFunctionFrame.Arguments.IndexOf(identifier.Name);
                if (argIndex == -1)
                    ThrowCompilationError(identifier, "bug? Undeclared argument?");

                switch (argIndex)
                {
                    case 0:
                        InsertInstruction(new VmLoadArgument0()); break;
                    case 1:
                        InsertInstruction(new VmLoadArgument1()); break;
                    case 2:
                        InsertInstruction(new VmLoadArgument2()); break;
                    case 3:
                        InsertInstruction(new VmLoadArgument3()); break;

                    default:
                        if (argIndex <= byte.MaxValue)
                            InsertInstruction(new VmLoadArgument((byte)argIndex));
                        else
                            ThrowCompilationError(identifier, CompilationErrorMessages.ExceededMaximumArgumentCount);
                        break;
                }
                break;

            case DeclType.Undefined:
                ThrowCompilationError(identifier, CompilationErrorMessages.UndefinedIdentifier);
                break;

            default:
                throw new NotImplementedException();
        }
    }

    private void CompileLiteral(Literal literal)
    {
        if (literal.NumericTokenType == NumericTokenType.Integer)
        {
            CompileIntegerLiteral((int)literal.Value, sourceNode: literal);
        }
        else if (literal.NumericTokenType == NumericTokenType.Float)
        {
            CompileFloatLiteral((float)literal.Value, sourceNode: literal);
        }
        else if (literal.TokenType == TokenType.BooleanLiteral)
        {
            if ((bool)literal.Value)
                InsertInstruction(new VmLoadTrue());
            else
                InsertInstruction(new VmLoadFalse());
        }
        else if (literal.TokenType == TokenType.StringLiteral)
        {
            string str = literal.StringValue;
            CompileStringLiteral(str, sourceNode: literal);
        }
        else if (literal.TokenType == TokenType.NilLiteral)
        {
            InsertInstruction(new VmLoadNil());
        }
        else
        {
            ThrowCompilationError(literal, CompilationErrorMessages.UnaryInvalidLiteral);
        }
    }

    private void CompileStringLiteral(string str, Node sourceNode = null)
    {
        if (!_stringPool.Contains(str))
            _stringPool.Add(str);

        int idx = _stringPool.IndexOf(str);
        if (idx <= byte.MaxValue)
            InsertInstruction(new VmPoolString((byte)idx));
        else if (idx < ushort.MaxValue)
            InsertInstruction(new VmPoolString_Word((ushort)idx));
        else
            ThrowCompilationError(sourceNode, CompilationErrorMessages.StringPoolIndexTooBig);
    }

    private void CompileFloatLiteral(float value, Node sourceNode = null)
    {
        if (!_fixedPool.Contains(value))
            _fixedPool.Add(value);

        int idx = _fixedPool.IndexOf(value);
        if (idx <= byte.MaxValue)
            InsertInstruction(new VmPoolFloat((byte)idx));
        else if (idx <= ushort.MaxValue)
            InsertInstruction(new VmPoolFloat_Word((ushort)idx));
        else
            ThrowCompilationError(sourceNode, CompilationErrorMessages.FixedPoolIndexTooBig);
    }

    private void CompileIntegerLiteral(int value, Node sourceNode = null, bool allowSpecifiedIntegerInstruction = false)
    {
        switch (value)
        {
            case 0:
                InsertInstruction(new VmConst0()); break;
            case 1:
                InsertInstruction(new VmConst1()); break;
            case 2:
                InsertInstruction(new VmConst2()); break;
            case 3:
                InsertInstruction(new VmConst3()); break;
            case 4:
                InsertInstruction(new VmConst4()); break;
            default:

                // For some reason CONST_I and CONST_I_W are only allowed for call argument numbers
                if (allowSpecifiedIntegerInstruction && value >= 0 && value <= byte.MaxValue)
                    InsertInstruction(new VmConstInteger((byte)value));
                else if (allowSpecifiedIntegerInstruction && value >= 0 && value <= ushort.MaxValue)
                    InsertInstruction(new VmConstInteger_Word((ushort)value));
                else
                {
                    if (!_intPool.Contains(value))
                        _intPool.Add(value);

                    int idx = _intPool.IndexOf(value);
                    if (idx <= byte.MaxValue)
                        InsertInstruction(new VmPoolInt((byte)idx));
                    else if (idx <= ushort.MaxValue)
                        InsertInstruction(new VmPoolInt_Word((ushort)idx));
                    else
                        ThrowCompilationError(sourceNode, CompilationErrorMessages.IntPoolIndexTooBig);
                }
                break;
        }
    }

    private int AddIdentifier(string id)
    {
        if (!_identifierPool.Contains(id))
        {
            _identifierPool.Add(id);
            return _lastIdentifierIndex++;
        }

        return -1;
    }

    private short AddOC(string id)
    {
        AddIdentifier(id);
        if (!_ocPool.ContainsKey(id))
            _ocPool.Add(id, new ObjectConstructor() { ID = _lastOCID++, NameID = _identifierPool.IndexOf(id) });

        return -1;
    }

    private void LeaveSwitch()
    {
        _breakControlBlocks.Pop();
    }

    private void LeaveLoop()
    {
        _breakControlBlocks.Pop();
        _continueControlBlocks.Pop();
    }

    private SwitchBlock EnterSwitch()
    {
        var scope = new SwitchBlock();
        _breakControlBlocks.Push(scope);
        return scope;
    }

    private LoopBlock EnterLoop()
    {
        LoopBlock loopCtx = new LoopBlock();
        _breakControlBlocks.Push(loopCtx);
        _continueControlBlocks.Push(loopCtx);
        return loopCtx;
    }

    public ControlBlock GetLastBreakControlledScope()
    {
        if (_breakControlBlocks.Count > 0)
            return _breakControlBlocks.Peek();

        return null;
    }

    public ControlBlock GetLastContinueControlledScope()
    {
        if (_continueControlBlocks.Count > 0)
            return _continueControlBlocks.Peek();

        return null;
    }

    private void InsertInstruction(VMInstructionBase inst)
    {
        _code.Add(inst);
        _currentPc += (1 + (uint)inst.GetSize());
    }

    private void ThrowCompilationError(Node node, string message)
    {
        throw GetCompilationError(node, message);
    }

    private void ThrowCompilationError(string message)
    {
        throw new ScriptCompilationException(message);
    }

    private ScriptCompilationException GetCompilationError(Node node, string message)
    {
        return new ScriptCompilationException(GetSourceNodeString(node, message));
    }

    private string GetSourceNodeString(Node node, string message)
    {
        return $"{message} at {node.Location.Source}:{node.Location.Start.Line}";
    }
}

public enum DeclType
{
    Undefined,

    Static,
    Local,
    Function,
    FunctionArgument,
    OC,
}

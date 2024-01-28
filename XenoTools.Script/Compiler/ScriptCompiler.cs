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
    private FunctionInfo _mainFunction;
    private FunctionInfo _currentFunctionFrame;
    private uint _currentPc;

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

    public CompiledScriptState Compile(string code)
    {
        var err = new ScriptErrorHandler();
        var parse = new AbstractSyntaxTree(code, new ParserOptions() { ErrorHandler = err }, OnNodeParsed);
        var script = parse.ParseScript();

        if (err.HasErrors())
        {
            ThrowCompilationError("Error while compiling");
            return null;
        }

        if (_mainFunction is null)
            ThrowCompilationError(CompilationErrorMessages.MissingMainFunction);

        InsertInstruction(new VmExit());

        CompileStatements(script.Body);

        short s = AddIdentifier("script.sb");
        _systemAttributes.Add(new SystemAttribute() { NameID = s });
        _systemAttributes.Add(new SystemAttribute() { NameID = -1 });

        return new CompiledScriptState
        {
            EntryPointFunctionID = (uint)_mainFunction.ID,
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
    /// Callback for each parsed node.
    /// This will work as a "first pass" scan to check for defined symbols.
    /// </summary>
    /// <param name="node"></param>
    private void OnNodeParsed(Node node)
    {
        if (node is FunctionDeclaration function)
        {
            foreach (Identifier argId in function.Params)
            {
                AddIdentifier(argId.Name);
            }

            FunctionInfo frame = new()
            {
                ID = _lastFunctionID++,
                NameID = AddIdentifier(function.Id.Name),
                NumArguments = (ushort)function.Params.Count
            };

            if (function.Id.Name == "_main_")
            {
                if (_mainFunction is not null)
                    ThrowCompilationError(CompilationErrorMessages.MainFunctionAlreadyDeclared);

                _mainFunction = frame;
            }

            if (!_funcPool.TryAdd(function.Id.Name, frame))
                ThrowCompilationError(CompilationErrorMessages.FunctionRedeclaration);
        }
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
        if (_currentFunctionFrame is null && (node.Type != Nodes.FunctionDeclaration && node.Type != Nodes.StaticDeclaration))
            ThrowCompilationError(CompilationErrorMessages.StatementInTopFrame);

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
                CompileStaticDeclaration(node.As<StaticDeclaration>()); break;

            case Nodes.VariableDeclaration:
                CompileVariableDeclaration(node.As<VariableDeclaration>()); break;

            case Nodes.BreakStatement:
                CompileBreak(node.As<BreakStatement>()); break;

            case Nodes.ContinueStatement:
                CompileContinue(node.As<ContinueStatement>()); break;

            default:
                throw new NotImplementedException();
        }
    }

    private void CompileVariableDeclaration(VariableDeclaration varDecl)
    {
        if (_currentFunctionFrame == _mainFunction)
            ThrowCompilationError(CompilationErrorMessages.CannotDeclareLocalsInMain);

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

            if (stackLocals.Locals.ContainsKey(id.Name))
                ThrowCompilationError(CompilationErrorMessages.VariableRedeclaration);

            VmVariable local = ProcessDeclarator(declarator, varDecl.Kind);
            local.ID = _lastLocalID++;
            stackLocals.Locals.Add(id.Name, local);
        }
    }

    private void CompileFunctionDeclaration(FunctionDeclaration funcDecl)
    {
        if (_currentFunctionFrame is not null)
            ThrowCompilationError(CompilationErrorMessages.NestedFunctionDeclaration);

        _currentFunctionFrame = _funcPool[funcDecl.Id.Name];
        _currentFunctionFrame.CodeStartOffset = _currentPc;

        CompileStatement(funcDecl.Body);

        if (_currentFunctionFrame == _mainFunction)
            InsertInstruction(new VmExit());
        else
            InsertInstruction(new VmRet());

        _currentFunctionFrame.CodeEndOffset = _currentPc;

        if (_currentFunctionFrame.LocalPoolIndex != -1)
            _currentFunctionFrame.NumLocals = (ushort)_localPool[_currentFunctionFrame.LocalPoolIndex].Locals.Count;

        _currentFunctionFrame = null;
        _lastLocalID = 0;
    }

    private void CompileStaticDeclaration(StaticDeclaration staticDecl)
    {
        if (_currentFunctionFrame is not null)
            ThrowCompilationError(CompilationErrorMessages.StaticDeclarationInFunction);

        VariableDeclaration decl = staticDecl.Declaration;
        foreach (VariableDeclarator dec in decl.Declarations)
        {
            Identifier id = dec.Id.As<Identifier>();
            if (_definedStatics.ContainsKey(id.Name))
                ThrowCompilationError(CompilationErrorMessages.StaticAlreadyDeclared);

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
                _statics.Add(scVar);

            ProcessArray(arrayExp, isStatic);
        }
        else
            ThrowCompilationError(CompilationErrorMessages.UnexpectedVariableDeclaratorType);

        return scVar;
    }

    private void ProcessArray(ArrayExpression arrayExp, bool isStatic = false)
    {
        int start = _lastStaticID;
        foreach (var elem in arrayExp.Elements)
        {
            VmVariable arrElem = new VmVariable();
            arrElem.ID = _lastStaticID++;

            if (elem.Type == Nodes.ArrayExpression)
            {
                arrElem.Type = LocalType.Array;
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
                ThrowCompilationError("Unsupported array element");

            _statics.Add(arrElem);
        }

        for (int i = 0; i < arrayExp.Elements.Count; i++)
        {
            VmVariable subArr = _statics[start + i];
            if (subArr.Type == LocalType.Array)
            {
                subArr.Type = LocalType.Array;
                subArr.ArraySize = (uint)arrayExp.Elements.Count;
                subArr.Value = _lastStaticID;
                
                ProcessArray(arrayExp.Elements[i].As<ArrayExpression>());
            }
        }

    }

    private void CompileExpressionStatement(ExpressionStatement expStatement)
    {
        if (expStatement.Expression.Type == Nodes.UpdateExpression)
            CompileUpdateExpression(expStatement.Expression.As<UpdateExpression>(), keepResult: false);
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
                ThrowCompilationError(CompilationErrorMessages.UnsupportedExpression);
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

        CompileExpressionAssignment(exp.Argument);

        if (keepResult)
            CompileExpression(exp.Argument);
    }

    private void CompileMemberExpression(MemberExpression exp)
    {
        if (exp is ComputedMemberExpression compMember)
        {
            if (compMember.Property.Type == Nodes.Literal)
            {
                Literal lit = compMember.Property.As<Literal>();
                if (lit.NumericTokenType != NumericTokenType.Integer)
                    ThrowCompilationError(CompilationErrorMessages.ExpectedExpressionOrNumberInArrayAccess);
            }

            CompileExpression(compMember.Object);
            CompileExpression(compMember.Property);
            InsertInstruction(new VmLoadArray());
        }
    }

    private void CompileLogicalExpression(BinaryExpression binExp)
    {
        if (binExp.Operator == BinaryOperator.LogicalOr || binExp.Operator == BinaryOperator.LogicalAnd)
        {
            CompileExpression(binExp.Left);
            CompileExpression(binExp.Right);

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
                    ThrowCompilationError(CompilationErrorMessages.UnaryInvalidLiteralType);
                    break;
            }

            return;
        }
        else
            ThrowCompilationError(CompilationErrorMessages.UnsupportedUnaryExpression);
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
                ThrowCompilationError(CompilationErrorMessages.UnsupportedAssignmentExpression);

            CompileExpression(assignmentExpression.Left);
            CompileExpression(assignmentExpression.Right);

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
                if (attrMemberExpr.Object.Type != Nodes.Identifier || attrMemberExpr.Property.Type != Nodes.Identifier)
                    ThrowCompilationError("Invalid attribute member expression object/property type");

                CompileIdentifier(attrMemberExpr.Object.As<Identifier>());

                Identifier propIdentifier = attrMemberExpr.Property.As<Identifier>();
                AddIdentifier(propIdentifier.Name);

                int idIndex = _identifierPool.IndexOf(propIdentifier.Name);

                if (idIndex == -1)
                    ThrowCompilationError("huh");

                if (idIndex <= byte.MaxValue)
                    InsertInstruction(new VmSetter((byte)idIndex));
                else if (idIndex <= ushort.MaxValue)
                    InsertInstruction(new VmSetter_Word((ushort)idIndex));
                else
                    ThrowCompilationError("id index too large");
            }
            else if (exp is ComputedMemberExpression compMemberExpr)
            {
                CompileExpression(compMemberExpr.Object);

                if (compMemberExpr.Property.Type == Nodes.Literal)
                {
                    Literal lit = compMemberExpr.Property.As<Literal>();
                    if (lit.NumericTokenType != NumericTokenType.Integer)
                        ThrowCompilationError(CompilationErrorMessages.ExpectedExpressionOrNumberInArrayAccess);
                }

                CompileExpression(compMemberExpr.Property);

                InsertInstruction(new VmStoreArray());
            }
            else
                ThrowCompilationError(CompilationErrorMessages.UnexpectedMemberAssignmentType);
        }
        else
            ThrowCompilationError(CompilationErrorMessages.InvalidAssignmentTarget);
    }

    private void CompileIdentifierAssignment(Identifier id)
    {
        DeclType type = GetVarType(id.Name);

        switch (type)
        {
            case DeclType.Local:
                int localIndex = _localPool[_currentFunctionFrame.LocalPoolIndex].Locals[id.Name].ID;
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
                InsertInstruction(new VmStoreStatic((byte)staticIndex));
                break;

            case DeclType.Function:
                ThrowCompilationError(CompilationErrorMessages.AssignToFunction);
                break;

            default:
                throw new NotImplementedException();
        }
    }

    private DeclType GetVarType(string identifier)
    {
        if (OCs.Contains(identifier))
            return DeclType.OC;
        if (_definedStatics.ContainsKey(identifier))
            return DeclType.Static;
        else if (_funcPool.ContainsKey(identifier))
            return DeclType.Function;
        else if (_currentFunctionFrame is not null)
        {
            if (_currentFunctionFrame.LocalPoolIndex != -1 && _localPool[_currentFunctionFrame.LocalPoolIndex].Locals.ContainsKey(identifier))
                return DeclType.Local;
        }

        return DeclType.Undefined;
    }

    private void CompileIfStatement(IfStatement ifStatement)
    {
        CompileTestStatement(ifStatement.Test);

        uint previousPc = (ushort)_currentPc;
        var jumpIfFalse = new VmJumpFalse();
        InsertInstruction(jumpIfFalse);

        CompileStatement(ifStatement.Consequent);
        jumpIfFalse.JumpRelativeOffset = (short)((_currentPc - previousPc));

        if (ifStatement.Alternate is not null)
        {
            var alternateSkip = new VmJump();
            InsertInstruction(alternateSkip);
            previousPc = (ushort)_currentPc;

            CompileStatement(ifStatement.Alternate);
            alternateSkip.JumpRelativeOffset = (short)((_currentPc - previousPc));
        }
    }

    private void CompileForStatement(ForStatement forStatement)
    {
        LoopBlock loopBlock = EnterLoop();

        if (forStatement.Init is not null)
            CompileStatement(forStatement.Init);

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
            ThrowCompilationError(CompilationErrorMessages.ContinueWithoutContextualScope);

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
            ThrowCompilationError(CompilationErrorMessages.BreakWithoutContextualScope);
        }
    }

    private void CompileCall(CallExpression call)
    {
        for (int i = call.Arguments.Count - 1; i >= 0; i--)
        {
            Expression arg = call.Arguments[i];
            CompileExpression(arg);
        }

        CompileIntegerLiteral(call.Arguments.Count);

        if (call.Callee is Identifier funcIdentifier)
        {
            DeclType type = GetVarType(funcIdentifier.Name);
            if (type == DeclType.OC)
            {
                AddOC(funcIdentifier.Name);

                byte idx = (byte)_ocPool[funcIdentifier.Name].NameID;
                InsertInstruction(new VmGetOC(idx));
            }
            else if (type == DeclType.Function)
            {
                FunctionInfo functionFrame = _funcPool[call.Callee.As<Identifier>().Name];
                InsertInstruction(new VmCall((byte)functionFrame.ID));
            }
            else
                ThrowCompilationError(CompilationErrorMessages.CallToUndeclaredFunction);
        }
        else if (call.Callee.Type == Nodes.MemberExpression)
        {
            MemberExpression memberExpression = (MemberExpression)call.Callee;
            if (memberExpression.Object.Type != Nodes.Identifier || memberExpression.Property.Type != Nodes.Identifier)
                ThrowCompilationError("Unexpected syntax");

            if (call.Callee is AttributeMemberExpression)
            {
                CompileIdentifier(memberExpression.Object.As<Identifier>());

                int idx = _identifierPool.IndexOf(memberExpression.Property.As<Identifier>().Name);

                if (idx <= byte.MaxValue)
                    InsertInstruction(new VmSend((byte)idx));
                else if (idx <= ushort.MaxValue)
                    InsertInstruction(new VmSend_Word((ushort)idx));
                else
                    ThrowCompilationError("Idx too large");
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
                    InsertInstruction(new VmSend_Word((ushort)pluginImport.ID));
                else
                    ThrowCompilationError("Idx too large");
            }
        }
        else
            ThrowCompilationError("Unsupported callee type");
    }

    private void CompileBinaryExpression(BinaryExpression binExpression)
    {
        CompileExpression(binExpression.Right);
        CompileExpression(binExpression.Left);

        switch (binExpression.Operator)
        {
            case BinaryOperator.Plus:
                break;
            case BinaryOperator.Minus:
                break;
            case BinaryOperator.Times:
                break;
            case BinaryOperator.Divide:
                break;
            case BinaryOperator.Modulo:
                break;
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
            case BinaryOperator.BitwiseAnd:
                break;
            case BinaryOperator.BitwiseOr:
                break;
            case BinaryOperator.BitwiseXOr:
                break;
            case BinaryOperator.LeftShift:
                break;
            case BinaryOperator.RightShift:
                break;
            case BinaryOperator.UnsignedRightShift:
                break;
            case BinaryOperator.InstanceOf:
                break;
            case BinaryOperator.In:
                break;
            case BinaryOperator.LogicalAnd:
                break;
            case BinaryOperator.LogicalOr:
                break;
            case BinaryOperator.Exponentiation:
                break;
            case BinaryOperator.NullishCoalescing:
                break;
            default:
                break;
        }
        ;
    }

    private void CompileIdentifier(Identifier identifier)
    {
        DeclType type = GetVarType(identifier.Name);

        switch (type)
        {
            case DeclType.Static:
                if (!_definedStatics.TryGetValue(identifier.Name, out int staticIdx))
                    ThrowCompilationError("aa");

                InsertInstruction(new VmLoadStatic((byte)staticIdx));
                break;

            case DeclType.Function:
                FunctionInfo functionFrame = _funcPool[identifier.Name];
                InsertInstruction(new VmLoadFunction((byte)functionFrame.ID));
                break;

            case DeclType.Local:
                int idIndex = _localPool[_currentFunctionFrame.LocalPoolIndex].Locals[identifier.Name].ID;
                if (idIndex == -1)
                    ThrowCompilationError("aa");

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
                        InsertInstruction(new VmLoad((byte)idIndex)); break;
                }
                break;

            case DeclType.Undefined:
                ThrowCompilationError(CompilationErrorMessages.UndefinedIdentifier);
                break;

            default:
                throw new NotImplementedException();
        }
    }

    private void CompileLiteral(Literal literal)
    {
        if (literal.NumericTokenType == NumericTokenType.Integer)
        {
            CompileIntegerLiteral((int)literal.Value);
        }
        else if (literal.NumericTokenType == NumericTokenType.Float)
        {
            CompileFloatLiteral((float)literal.Value);
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
            CompileStringLiteral(str);
        }
        else if (literal.TokenType == TokenType.NilLiteral)
        {
            InsertInstruction(new VmLoadNil());
        }
        else
        {
            ThrowCompilationError(CompilationErrorMessages.UnaryInvalidLiteral);
        }
    }

    private void CompileStringLiteral(string str)
    {
        if (!_stringPool.Contains(str))
            _stringPool.Add(str);

        int idx = _stringPool.IndexOf(str);
        if (idx <= byte.MaxValue)
            InsertInstruction(new VmPoolString((byte)idx));
        else if (idx < ushort.MaxValue)
            InsertInstruction(new VmPoolString_Word((ushort)idx));
        else
            ThrowCompilationError(CompilationErrorMessages.PoolIndexTooBig);
    }

    private void CompileFloatLiteral(float value)
    {
        if (!_fixedPool.Contains(value))
            _fixedPool.Add(value);

        int idx = _fixedPool.IndexOf(value);
        if (idx <= byte.MaxValue)
            InsertInstruction(new VmPoolFloat((byte)idx));
        else if (idx <= ushort.MaxValue)
            InsertInstruction(new VmPoolFloat_Word((ushort)idx));
        else
            ThrowCompilationError(CompilationErrorMessages.PoolIndexTooBig);
    }

    private void CompileIntegerLiteral(int value)
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
                if (value >= 0 && value <= byte.MaxValue)
                    InsertInstruction(new VmConstInteger((byte)value));
                else if (value >= 0 && value <= ushort.MaxValue)
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
                        ThrowCompilationError(CompilationErrorMessages.PoolIndexTooBig);
                }
                break;
        }
    }

    private short AddIdentifier(string id)
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
        short identifier = AddIdentifier(id);
        if (identifier != -1 && !_ocPool.ContainsKey(id))
            _ocPool.Add(id, new ObjectConstructor() { NameID = identifier });

        return -1;
    }

    private void LeaveLoop()
    {
        _breakControlBlocks.Pop();
        _continueControlBlocks.Pop();
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

    private void ThrowCompilationError(string message)
    {
        throw new Exception(message);
    }
}

public enum DeclType
{
    Undefined,

    Static,
    Local,
    Function,
    OC,
}

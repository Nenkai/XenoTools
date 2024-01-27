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

            case Nodes.StaticDeclaration:
                CompileStaticDeclaration(node.As<StaticDeclaration>()); break;

            case Nodes.VariableDeclaration:
                CompileVariableDeclaration(node.As<VariableDeclaration>()); break;

            default:
                throw new NotImplementedException();
        }
    }

    private void CompileVariableDeclaration(VariableDeclaration varDecl)
    {
        if (_currentFunctionFrame is null)
            ThrowCompilationError(CompilationErrorMessages.NestedFunctionDeclaration);

        foreach (VariableDeclarator declarator in varDecl.Declarations)
        {
            Identifier id = declarator.Id.As<Identifier>();
            AddIdentifier(id.Name);

            if (_currentFunctionFrame.LocalPoolIndex == -1)
            {
                _currentFunctionFrame.LocalPoolIndex = _lastLocalPoolID;
                _localPool.Add(new StackLocals());
            }

            StackLocals stackLocals = _localPool[_currentFunctionFrame.LocalPoolIndex];

            if (stackLocals.Locals.ContainsKey(id.Name))
                ThrowCompilationError(CompilationErrorMessages.VariableRedeclaration);

            VmVariable local = ProcessDeclarator(declarator, varDecl.Kind);
            local.ID = _lastLocalID++;
            stackLocals.Locals.Add(id.Name, local);

            if (declarator.Init is not null)
            {
                CompileExpression(declarator.Init);
                CompileIdentifierAssignment(declarator.Id.As<Identifier>());
            }
        }
    }

    private void CompileFunctionDeclaration(FunctionDeclaration funcDecl)
    {
        _currentFunctionFrame = _funcPool[funcDecl.Id.Name];
        _currentFunctionFrame.CodeStartOffset = _currentPc;

        CompileStatement(funcDecl.Body);

        if (_currentFunctionFrame == _mainFunction)
            InsertInstruction(new VmExit());
        else
        InsertInstruction(new VmRet());

        _currentFunctionFrame.CodeEndOffset = _currentPc;
        _currentFunctionFrame = null;
        _lastLocalID = 0;
    }

    private void CompileStaticDeclaration(StaticDeclaration staticDecl)
    {
        VariableDeclaration decl = staticDecl.Declaration;
        foreach (VariableDeclarator dec in decl.Declarations)
        {
            Identifier id = dec.Id.As<Identifier>();
            if (_definedStatics.ContainsKey(id.Name))
                ThrowCompilationError(CompilationErrorMessages.StaticAlreadyDeclared);

            VmVariable local = ProcessDeclarator(dec, decl.Kind);
            local.ID = _lastStaticID++;
            _definedStatics.Add(id.Name, local.ID);
        }
    }

    private VmVariable ProcessDeclarator(VariableDeclarator decl, VariableDeclarationKind kind)
    {
        var type = kind switch
        {
            VariableDeclarationKind.Int => LocalType.Int,
            VariableDeclarationKind.Fixed => LocalType.Fixed,
            VariableDeclarationKind.String => LocalType.String,
            VariableDeclarationKind.Array => LocalType.Array,
        };

        VmVariable local = new VmVariable()
        {
            Type = type,
        };

        if (decl.Init.Type == Nodes.Literal)
        {
            Literal literal = decl.Init.As<Literal>();
            local.Value = literal.Value;
        }
        else if (decl.Init.Type == Nodes.ArrayExpression)
        {
            ArrayExpression arrayExp = decl.Init.As<ArrayExpression>();

            VmVariable arrayLocal = new VmVariable();
            arrayLocal.ID = _lastStaticID++;
            arrayLocal.Type = LocalType.Array;
            arrayLocal.ArraySize = (uint)arrayExp.Elements.Count;
            arrayLocal.Value = _lastStaticID;
            _statics.Add(arrayLocal);

            RecurseArray(arrayExp);
        }
        else
            ThrowCompilationError("Unsupported declarator type");

        return local;
    }

    private void RecurseArray(ArrayExpression arrayExp)
    {
        int start = _lastStaticID;
        foreach (var elem in arrayExp.Elements)
        {
            VmVariable elemLocal = new VmVariable();
            elemLocal.ID = _lastStaticID++;

            if (elem.Type == Nodes.ArrayExpression)
            {
                elemLocal.Type = LocalType.Array;
            }
            else if (elem.Type == Nodes.Literal)
            {
                Literal literal = elem.As<Literal>();
                if (literal.NumericTokenType == NumericTokenType.Integer)
                {
                    elemLocal.Type = LocalType.Int;
                    elemLocal.Value = literal.Value;
                }
                else if (literal.NumericTokenType == NumericTokenType.Float)
                {
                    elemLocal.Type = LocalType.Fixed;
                    elemLocal.Value = literal.Value;
                }
                else if (literal.TokenType == TokenType.StringLiteral)
                {
                    elemLocal.Type = LocalType.String;

                    if (!_stringPool.Contains(literal.Value))
                        _stringPool.Add((string)literal.Value);

                    elemLocal.Value = _stringPool.IndexOf((string)literal.Value);
                }
            }
            else
                ThrowCompilationError("Unsupported array element");

            _statics.Add(elemLocal);
        }

        for (int i = 0; i < arrayExp.Elements.Count; i++)
        {
            VmVariable tmp = _statics[start + i];
            if (tmp.Type == LocalType.Array)
            {
                tmp.Type = LocalType.Array;
                tmp.ArraySize = (uint)arrayExp.Elements.Count;
                tmp.Value = _lastStaticID;
                
                RecurseArray(arrayExp.Elements[i].As<ArrayExpression>());
            }
        }

    }

    private void CompileExpressionStatement(ExpressionStatement expStatement)
    {
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

            default:
                ThrowCompilationError(CompilationErrorMessages.UnsupportedExpression);
                break;
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
                    CompileFloat(-(float)literal.Value);
                    break;

                case NumericTokenType.Integer:
                    CompileInteger(-(int)literal.Value);
                    break;

                default:
                    ThrowCompilationError("Unexpected syntax");
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

            if (assignmentExpression.Left.Type == Nodes.Identifier)
            {
                Identifier id = assignmentExpression.Left.As<Identifier>();
                CompileIdentifierAssignment(id);
            }
            else if (assignmentExpression.Left is AttributeMemberExpression attrMemberExpr)
            {
                if (attrMemberExpr.Object.Type != Nodes.Identifier || attrMemberExpr.Property.Type != Nodes.Identifier)
                    ThrowCompilationError("Incorrect");

                CompileIdentifier(attrMemberExpr.Object.As<Identifier>());

                Identifier propIdentifier = attrMemberExpr.Property.As<Identifier>();
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
            else
                ThrowCompilationError("Not supported");
        }
        else
            ThrowCompilationError(CompilationErrorMessages.UnsupportedAssignmentExpression);
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
        CompileExpression(ifStatement.Test);

        var jumpIfFalse = new VmJumpFalse();
        InsertInstruction(jumpIfFalse);

        uint previousPc = (ushort)_currentPc;
        CompileStatement(ifStatement.Consequent);
        jumpIfFalse.JumpRelativeOffset = (ushort)(jumpIfFalse.GetSize() + (_currentPc - previousPc));

        if (ifStatement.Alternate is not null)
        {
            var alternateSkip = new VmJump();
            InsertInstruction(alternateSkip);
            previousPc = (ushort)_currentPc;

            CompileStatement(ifStatement.Alternate);
            alternateSkip.JumpRelativeOffset = (ushort)(jumpIfFalse.GetSize() + (_currentPc - previousPc));
        }
    }

    private void CompileForStatement(ForStatement forStatement)
    {
        CompileStatement(forStatement.Init);
        CompileExpression(forStatement.Test);

        var jumpBack = new VmJump();
        jumpBack.JumpRelativeOffset = 0; // Todo

        var bodyJump = new VmJumpFalse();
        InsertInstruction(bodyJump);
        CompileStatement(forStatement.Body);
        CompileExpression(forStatement.Update);
        bodyJump.JumpRelativeOffset = 0; // TODO

        InsertInstruction(jumpBack);
    }

    private void CompileCall(CallExpression call)
    {
        for (int i = call.Arguments.Count - 1; i >= 0; i--)
        {
            Expression arg = call.Arguments[i];
            CompileExpression(arg);
        }

        CompileInteger(call.Arguments.Count);

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
                int staticIndex = 0; // fixme _statics.IndexOf(identifier.Name);
                if (staticIndex == -1)
                    ThrowCompilationError("aa");

                InsertInstruction(new VmLoadStatic((byte)staticIndex));
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
            CompileInteger((int)literal.Value);
        }
        else if (literal.NumericTokenType == NumericTokenType.Float)
        {
            CompileFloat((float)literal.Value);
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
            throw new NotImplementedException();
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

    private void CompileFloat(float value)
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

    private void CompileInteger(int value)
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

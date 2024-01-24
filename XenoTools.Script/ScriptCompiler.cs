using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using XenoTools.Script.Instructions;

using Esprima;
using Esprima.Ast;

namespace XenoTools.Script
{
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

        public List<string> Statics { get; set; } = new();

        public List<VMInstructionBase> Code { get; set; } = new();

        public List<int> IntPool { get; set; } = new();
        public List<float> FixedPool { get; set; } = new();
        public List<string> StringPool { get; set; } = new();
        public List<string> IdentifierPool { get; set; } = new();
        public List<string> FuncPool { get; set; } = new();
        public List<string> PluginImportPool { get; set; } = new();

        public FunctionFrame _currentFunctionFrame;

        public void Compile(string code)
        {
            var parse = new AdhocAbstractSyntaxTree(code, new ParserOptions(), OnNodeParsed);

            var script = parse.ParseScript();

            CompileStatements(script.Body);
        }

        /// <summary>
        /// Callback for each parsed node.
        /// This will work as a "first pass" scan to check for defined symbols.
        /// </summary>
        /// <param name="node"></param>
        private void OnNodeParsed(Node node)
        {
            if (node is Identifier id)
            {
                AddIdentifier(id.Name);
            }
            else if (node is FunctionDeclaration function)
            {
                foreach (Identifier argId in function.Params)
                {
                    AddIdentifier(argId.Name);
                }

                AddIdentifier(function.Id.Name);
                FuncPool.Add(function.Id.Name);
            }
            else if (node.Type == Nodes.CallExpression)
            {
                CallExpression call = node.As<CallExpression>();
                if (call.Callee is StaticMemberExpression staticMemberExpr)
                {
                    string objName = staticMemberExpr.Object.As<Identifier>().Name;
                    string propName = staticMemberExpr.Property.As<Identifier>().Name;

                    if (!PluginImportPool.Contains($"{objName}::{propName}"))
                        PluginImportPool.Add($"{objName}::{propName}");
                }
            }
        }

        private void AddIdentifier(string id)
        {
            if (!IdentifierPool.Contains(id))
                IdentifierPool.Add(id);
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
                ThrowCompilationError(CompilationErrorMessage.NestedFunctionDeclaration);

            foreach (VariableDeclarator declarator in varDecl.Declarations)
            {
                Identifier id = declarator.Id.As<Identifier>();
                if (_currentFunctionFrame.Locals.Contains(id.Name))
                    ThrowCompilationError(CompilationErrorMessage.VariableRedeclaration);

                _currentFunctionFrame.Locals.Add(id.Name);

                if (declarator.Init is not null)
                {
                    CompileExpression(declarator.Init);
                    CompileIdentifierAssignment(declarator.Id.As<Identifier>());
                }
            }
        }

        private void CompileFunctionDeclaration(FunctionDeclaration funcDecl)
        {
            _currentFunctionFrame = new FunctionFrame();

            CompileStatement(funcDecl.Body);
            InsertInstruction(new VmRet());

            _currentFunctionFrame = null;
        }

        private void CompileStaticDeclaration(StaticDeclaration staticDecl)
        {
            Identifier id = staticDecl.Declaration.Id.As<Identifier>();
            if (!Statics.Contains(id.Name))
                Statics.Add(id.Name);
            else
                ThrowCompilationError(CompilationErrorMessage.StaticAlreadyDeclared);
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
                    ThrowCompilationError(CompilationErrorMessage.UnsupportedExpression);
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
                        CompileFloat(-((float)literal.Value));
                        break;

                    case NumericTokenType.Integer:
                        CompileInteger(-((int)literal.Value));
                        break;

                    default:
                        ThrowCompilationError("Unexpected syntax");
                        break;
                }

                return;
            }
            else
                ThrowCompilationError(CompilationErrorMessage.UnsupportedUnaryExpression);
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
                    int idIndex = IdentifierPool.IndexOf(propIdentifier.Name);

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
                ThrowCompilationError(CompilationErrorMessage.UnsupportedAssignmentExpression);
        }

        private void CompileIdentifierAssignment(Identifier id)
        {
            DeclType type = GetVarType(id.Name);

            switch (type)
            {
                case DeclType.Local:
                    int localIndex = _currentFunctionFrame.Locals.IndexOf(id.Name);
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
                    int staticIndex = Statics.IndexOf(id.Name);
                    InsertInstruction(new VmStoreStatic((byte)staticIndex));
                    break;

                case DeclType.Function:
                    ThrowCompilationError(CompilationErrorMessage.AssignToFunction);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        private DeclType GetVarType(string identifier)
        {
            if (OCs.Contains(identifier))
                return DeclType.OC;
            if (Statics.Contains(identifier))
                return DeclType.Static;
            else if (FuncPool.Contains(identifier))
                return DeclType.Function;
            else if (_currentFunctionFrame is not null)
            {
                if (_currentFunctionFrame.Locals.Contains(identifier))
                    return DeclType.Local;
            }

            return DeclType.Undefined;
        }

        private void CompileIfStatement(IfStatement ifStatement)
        {
            CompileExpression(ifStatement.Test);
            
            var jumpIfFalse = new VmJumpFalse();
            InsertInstruction(jumpIfFalse);
            CompileStatement(ifStatement.Consequent);
            jumpIfFalse.JumpRelativeOffset = 0; // TODO

            if (ifStatement.Alternate is not null)
            {
                var alternateSkip = new VmJump();
                InsertInstruction(alternateSkip);
                CompileStatement(ifStatement.Alternate);
                alternateSkip.JumpRelativeOffset = 0; // TODO
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
                    byte idx = (byte)OCs.IndexOf(call.Callee.As<Identifier>().Name);
                    InsertInstruction(new VmGetOC(idx));
                }
                else if (type == DeclType.Function)
                {
                    byte idx = (byte)FuncPool.IndexOf(call.Callee.As<Identifier>().Name);
                    InsertInstruction(new VmCall(idx));
                }
                else
                    ThrowCompilationError(CompilationErrorMessage.CallToUndeclaredFunction);
            }
            else if (call.Callee.Type == Nodes.MemberExpression)
            {
                MemberExpression memberExpression = (MemberExpression)call.Callee;
                if (memberExpression.Object.Type != Nodes.Identifier || memberExpression.Property.Type != Nodes.Identifier)
                    ThrowCompilationError("Unexpected syntax");

                if (call.Callee is AttributeMemberExpression)
                {
                    CompileIdentifier(memberExpression.Object.As<Identifier>());

                    int idx = IdentifierPool.IndexOf(memberExpression.Property.As<Identifier>().Name);

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

                    int idx = PluginImportPool.IndexOf($"{objName}::{propName}");
                    if (idx == -1)
                        ThrowCompilationError("Compiler error - plugin import not found?");

                    if (idx >= 0 && idx <= byte.MaxValue)
                        InsertInstruction(new VmPlugin((byte)idx));
                    else if (idx >= 0 && idx <= ushort.MaxValue)
                        InsertInstruction(new VmSend_Word((ushort)idx));
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
                    int staticIndex = Statics.IndexOf(identifier.Name);
                    if (staticIndex == -1)
                        ThrowCompilationError("aa");

                    InsertInstruction(new VmLoadStatic((byte)staticIndex));
                    break;

                case DeclType.Function:
                    int functionIndex = FuncPool.IndexOf(identifier.Name);
                    if (functionIndex == -1)
                        ThrowCompilationError("aa");

                    InsertInstruction(new VmLoadFunction((byte)functionIndex));
                    break;

                case DeclType.Local:
                    int idIndex = IdentifierPool.IndexOf(identifier.Name);
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
                    ThrowCompilationError(CompilationErrorMessage.UndefinedIdentifier);
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
            if (!StringPool.Contains(str))
                StringPool.Add(str);

            int idx = StringPool.IndexOf(str);
            if (idx <= byte.MaxValue)
                InsertInstruction(new VmPoolString((byte)idx));
            else if (idx < ushort.MaxValue)
                InsertInstruction(new VmPoolString_Word((ushort)idx));
            else
                ThrowCompilationError(CompilationErrorMessage.PoolIndexTooBig);
        }

        private void CompileFloat(float value)
        {
            if (!FixedPool.Contains(value))
                FixedPool.Add(value);

            int idx = FixedPool.IndexOf(value);
            if (idx <= byte.MaxValue)
                InsertInstruction(new VmPoolFloat((byte)idx));
            else if (idx <= ushort.MaxValue)
                InsertInstruction(new VmPoolFloat_Word((ushort)idx));
            else
                ThrowCompilationError(CompilationErrorMessage.PoolIndexTooBig);
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
                        if (!IntPool.Contains(value))
                            IntPool.Add(value);

                        int idx = IntPool.IndexOf(value);
                        if (idx <= byte.MaxValue)
                            InsertInstruction(new VmPoolInt((byte)idx));
                        else if (idx <= ushort.MaxValue)
                            InsertInstruction(new VmPoolInt_Word((ushort)idx));
                        else
                            ThrowCompilationError(CompilationErrorMessage.PoolIndexTooBig);
                    }
                    break;
            }
        }

        private void InsertInstruction(VMInstructionBase inst)
        {
            Code.Add(inst);
        }

        private void ThrowCompilationError(string message)
        {
            throw new Exception(message);
        }
    }

    public class CompilationErrorMessage
    {
        public const string PoolIndexTooBig = "Int pool index too large.";
        public const string CallToUndeclaredFunction = "Undeclared function";
        public const string StaticAlreadyDeclared = "Static was already declared.";
        public const string AssignToFunction = "Attempted to assign to a function.";
        public const string NestedFunctionDeclaration = "Attempted to declare variable outside function";
        public const string VariableRedeclaration = "Local variable is already declared.";
        public const string UnsupportedExpression = "Unsupported expression";
        public const string UnsupportedUnaryExpression = "Unsupported unary expression type";
        public const string UnsupportedAssignmentExpression = "Unsupported assignment expression type";
        public const string UndefinedIdentifier = "Undefined identifier";

    }

    public enum DeclType
    {
        Undefined,

        Static,
        Local,
        Function,
        OC,
    }
}

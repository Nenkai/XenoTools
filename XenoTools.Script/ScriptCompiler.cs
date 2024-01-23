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

        public FunctionFrame FunctionFrame { get; set; }
        public List<string> Statics { get; set; } = new();

        public List<VMInstructionBase> Code { get; set; } = new();

        public List<int> IntPool { get; set; } = new();
        public List<string> IdentifierPool { get; set; } = new();
        public List<string> FuncPool { get; set; } = new();

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
                    CompileExpressionStatement(node as ExpressionStatement); break;

                case Nodes.FunctionDeclaration:
                    CompileFunctionDeclaration(node as FunctionDeclaration); break;

                case Nodes.BlockStatement:
                    CompileBlockStatement(node as BlockStatement); break;

                case Nodes.IfStatement:
                    CompileIfStatement(node as IfStatement); break;

                case Nodes.ForStatement:
                    CompileForStatement(node as ForStatement); break;

                case Nodes.StaticDeclaration:
                    CompileStaticDeclaration(node as StaticDeclaration); break;

                default:
                    throw new NotImplementedException();
            }
        }

        private void CompileFunctionDeclaration(FunctionDeclaration funcDecl)
        {
            CompileStatement(funcDecl.Body);
            InsertInstruction(new VmRet());
        }

        private void CompileStaticDeclaration(StaticDeclaration staticDecl)
        {
            Identifier id = staticDecl.Declaration.Id as Identifier;
            if (!Statics.Contains(id.Name))
                Statics.Add(id.Name);
            else
                ThrowCompilationError(CompilationErrors.StaticAlreadyDeclared);
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
                    CompileIdentifier(exp as Identifier);
                    break;

                case Nodes.CallExpression:
                    CompileCall(exp as CallExpression);
                    break;

                case Nodes.Literal:
                    CompileLiteral(exp as Literal);
                    break;

                case Nodes.BinaryExpression:
                    CompileBinaryExpression(exp as BinaryExpression); break;

                case Nodes.AssignmentExpression:
                    CompileAssignmentExpression(exp as AssignmentExpression); break;

                default:
                    throw new NotImplementedException();
            }
        }

        private void CompileAssignmentExpression(AssignmentExpression assignmentExpression)
        {
            if (assignmentExpression.Operator == AssignmentOperator.Assign)
            {
                CompileExpression(assignmentExpression.Right);

                var id = assignmentExpression.Left as Identifier;
                DeclType type = GetVarType(id.Name);

                switch (type)
                {
                    case DeclType.Static:
                        int staticIndex = Statics.IndexOf(id.Name);
                        InsertInstruction(new VmStoreStatic((byte)staticIndex));
                        break;

                    case DeclType.Function:
                        ThrowCompilationError(CompilationErrors.AssignToFunction);
                        break;

                    default:
                        throw new NotImplementedException();
                }
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

            return DeclType.Local;
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

        private void CompileCall(CallExpression call, bool popReturnValue = false)
        {
            foreach (Expression argExpr in call.Arguments)
            {
                CompileExpression(argExpr);
            }

            CompileInteger(call.Arguments.Count);

            if (call.Callee is Identifier funcIdentifier)
            {
                DeclType type = GetVarType(funcIdentifier.Name);
                if (type == DeclType.OC)
                {
                    byte idx = (byte)OCs.IndexOf(((Identifier)call.Callee).Name);
                    InsertInstruction(new VmGetOC(idx));
                }
                else if (type == DeclType.Function)
                {
                    CompileExpression(call.Callee);

                    byte idx = (byte)FuncPool.IndexOf(((Identifier)call.Callee).Name);
                    InsertInstruction(new VmCall(idx));
                }
                else
                    ThrowCompilationError(CompilationErrors.CallToUndeclaredFunction);
            }


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
                    InsertInstruction(new VmGreaterOrEquals());
                    break;
                case BinaryOperator.GreaterOrEqual:
                    InsertInstruction(new VmGreaterOrEquals());
                    break;
                case BinaryOperator.Less:
                    InsertInstruction(new VmLesserOrEquals());
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
                    InsertInstruction(new VmLoadStatic((byte)staticIndex));
                    break;

                case DeclType.Function:
                    int functionIndex = FuncPool.IndexOf(identifier.Name);
                    InsertInstruction(new VmLoadFunction((byte)functionIndex));
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
                throw new NotImplementedException();
            }
            else if (literal.TokenType == TokenType.BooleanLiteral)
            {
                if ((bool)literal.Value)
                    InsertInstruction(new VmLoadTrue());
                else
                    InsertInstruction(new VmLoadFalse());
            }
            else
            {
                throw new NotImplementedException();
            }
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
                    if (value > byte.MinValue && value < byte.MaxValue)
                        InsertInstruction(new VmConstInteger((byte)value));
                    else if (value > short.MinValue && value < short.MaxValue)
                        InsertInstruction(new VmConstInteger_Word((short)value));
                    else
                    {
                        if (!IntPool.Contains(value))
                            IntPool.Add(value);

                        int idx = IntPool.IndexOf(value);
                        if (idx > byte.MaxValue)
                            InsertInstruction(new VmPoolInt((byte)idx));
                        else if (idx < short.MaxValue)
                            InsertInstruction(new VmPoolInt_Word((byte)idx));
                        else
                            ThrowCompilationError(CompilationErrors.PoolIndexTooBig);
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

    public class CompilationErrors
    {
        public const string PoolIndexTooBig = "Int pool index too large.";
        public const string CallToUndeclaredFunction = "Undeclared function";
        public const string StaticAlreadyDeclared = "Static was already declared.";
        public const string AssignToFunction = "Attempted to assign to a function.";
    }

    public enum DeclType
    {
        Static,
        Local,
        Function,
        OC
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Compiler;

public class CompilationErrorMessages
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
    public const string FunctionRedeclaration = "Function already declared";
    public const string MissingMainFunction = "_main_ function missing";
    public const string MainFunctionAlreadyDeclared = "_main_ was already declared.";
}

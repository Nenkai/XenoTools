using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Compiler;

public class CompilationErrorMessages
{
    // General declarations
    public const string CallToUndeclaredFunction = "Undeclared function";
    public const string StaticAlreadyDeclared = "Static was already declared";
    public const string VariableRedeclaration = "Local variable is already declared";
    public const string UndefinedIdentifier = "Undefined identifier";
    public const string FunctionRedeclaration = "Function already declared";

    // _main_
    public const string MissingMainFunction = "_main_ function missing";
    public const string MainFunctionAlreadyDeclared = "_main_ was already declared";
    public const string CannotDeclareLocalsInMain = "Locals cannot be declared in _main_";

    // Weird syntax
    public const string StatementInTopFrame = "Non static or function declarations are not allowed in the top frame";
    public const string AssignToFunction = "Attempted to assign to a function";
    public const string NestedFunctionDeclaration = "Attempted to declare variable outside function";
    public const string StaticDeclarationInFunction = "Invalid static declaration inside function";

    // Limit checks
    public const string ExceededMaximumPluginImports = "Maximum amount of plugin imports reached? (> 65535)";
    public const string IntPoolIndexTooBig = "Maximum amount of ints in pool reached? (> 65535)";
    public const string FixedPoolIndexTooBig = "Maximum amount of fixed/floats in pool reached? (> 65535)";
    public const string StringPoolIndexTooBig = "Maximum amount of strings in pool reached? (> 65535)";
    public const string ExceededMaximumIdentifierCount = "Maximum amount of identifiers reached? (> 65535)";

    // Switch
    public const string TooManySwitchCases = "Too many switch cases (> 255)";
    public const string BreakWithoutContextualScope = "Invalid break statement without loop or switch block";
    public const string ContinueWithoutContextualScope = "Invalid continue statement without loop block";

    // Unsupported
    public const string UnsupportedExpression = "Unsupported expression";
    public const string UnsupportedUnaryExpression = "Unsupported unary expression type";
    public const string UnsupportedAssignmentExpression = "Unsupported assignment expression type";
    public const string ExpectedExpressionOrNumberInArrayAccess = "Expected an expression or integer in array access";
    public const string UnexpectedMemberAssignmentType = "Unexpected member assignment type";
    public const string InvalidAssignmentTarget = "Invalid assignment target type";
    public const string UnaryInvalidLiteralType = "Invalid literal type for unary operation";
    public const string UnaryInvalidLiteral = "Invalid literal type";
    public const string UnexpectedVariableDeclaratorType = "Unexpected variable declaration type";
    public const string InvalidAttributeMemberExpressionAssignment = "Invalid attribute member expression object/property type - expected identifiers";
    public const string UnsupportedArrayElement = "Unsupported array element";
    public const string UnsupportedStatementType = "Unsupported or invalid statement";
    public const string UnsupportedCallType = "Unsupported call type";



}

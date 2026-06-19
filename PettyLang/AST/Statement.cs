using PettyLang.Semantic;

namespace PettyLang.AST;

public abstract class Statement(Position position) : ASTNode(position);

public class StatementExpression(Expression expr) : Statement(expr.Position)
{
    public readonly Expression Expression = expr;
}

public class VarDeclStatement(Position position, string name, IdentifierExpression? type, Expression value) : Statement(position)
{
    public readonly string Name = name;
    public readonly Expression Value = value;
    public readonly IdentifierExpression? Type = type;
    public VarSymbol Resolved = null!;
}

public class VarAssigmentStatement(Position position, IdentifierExpression target, string @operator, Expression value) : Statement(position)
{
    public readonly IdentifierExpression Target = target;
    public readonly string Operator = @operator;
    public readonly Expression Value = value;
}

public class BlockStatement(Position position, Statement[] statements) : Statement(position)
{
    public readonly Statement[] Statements = statements;
}

public class FuncParameter(Position position, string name, IdentifierExpression type)
{
    public readonly Position Position = position;
    public readonly string Name = name;
    public readonly IdentifierExpression Type = type;
}

public class FuncDefineStatement(Position position, string name, FuncParameter[] parameters, IdentifierExpression? returnType) : Statement(position)
{
    public readonly string Name = name;
    public readonly FuncParameter[] Parameters = parameters;
    public readonly IdentifierExpression? ReturnType = returnType;
}
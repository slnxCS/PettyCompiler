using PettyLang.Semantic;

namespace PettyLang.AST;

public abstract class Expression(Position position) : ASTNode(position);

public class IntExpression(int number, Position position) : Expression(position)
{
    public readonly int Number = number;
}

public class FloatExpression(double number, Position position) : Expression(position)
{
    public readonly double Number = number;
}

public class StringExpression(string str, Position position) : Expression(position)
{
    public readonly string String = str;
}

public class IdentifierExpressionPart(Position position, string id, Expression[][] funcCallArguments, 
    Expression[]? arrayAppealArgumentsBeforeFuncCall, Expression[]? arrayAppealArgumentsAftersFuncCall) : Expression(position)
{
    public readonly string ID = id;
    public readonly Expression[][] FuncCallsArguments = funcCallArguments;

    public readonly Expression[]? ArrayAppealArgumentsBeforeFuncCall = arrayAppealArgumentsBeforeFuncCall;
    public readonly Expression[]? ArrayAppealArgumentsAftersFuncCall = arrayAppealArgumentsAftersFuncCall;
}

public class IdentifierExpression : Expression
{
    public readonly Expression FirstPart;
    public readonly bool IsFirstIdentifier;
    public Symbol Resolved = null!;

    public readonly IdentifierExpressionPart[] OtherParts;

    public Expression Last => OtherParts.Length == 0 ? FirstPart : OtherParts[OtherParts.Length - 1];

    public IdentifierExpression(Position position, Expression firstPart, IdentifierExpressionPart[] otherParts) : base(position)
    {
        FirstPart = firstPart;
        OtherParts = otherParts;
        IsFirstIdentifier = firstPart is IdentifierExpressionPart;
    }
}

public class BinaryExpression(Position position, Expression left, string @operator, Expression right) : Expression(position)
{
    public readonly Expression Left = left, Right = right;
    public Symbol LeftSymbol = null!, RightSymbol = null!;
    public readonly string Operator = @operator;
}
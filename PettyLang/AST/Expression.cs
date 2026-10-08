using PettyLang.Semantic;

namespace PettyLang.AST;

public abstract class Expression(Position position) : ASTNode(position);

public class IntExpression(int number, Position position) : Expression(position)
{
    public Int32InstanceSymbol Resolved = null!;
    public readonly int Number = number;
}

public class FloatExpression(float number, Position position) : Expression(position)
{
    public Float32InstanceSymbol Resolved = null!;
    public readonly float Number = number;
}

public class BoolExpression(bool val, Position position) : Expression(position)
{
    public BoolInstanceSymbol Resolved = null!;
    public readonly bool Value = val;
}

public class StringExpression(string str, Position position) : Expression(position)
{
    public readonly string String = str;
}

public class IdentifierExpressionPart(Position position, string id, Expression[][] funcCallArguments, 
    Expression[]? arrayAppealArgumentsBeforeFuncCall, Expression[]? arrayAppealArgumentsAftersFuncCall) : Expression(position)
{
    public byte[] SelfPushBytes => Resolved.GetPushBytes(this);
    public int Index;

    public readonly string ID = id;
    public readonly Expression[][] FuncCallsArguments = funcCallArguments;

    public readonly Expression[]? ArrayAppealArgumentsBeforeFuncCall = arrayAppealArgumentsBeforeFuncCall;
    public readonly Expression[]? ArrayAppealArgumentsAftersFuncCall = arrayAppealArgumentsAftersFuncCall;

    public Symbol Resolved = null!;
    public FunctionOverload? ResolvedOverload = null;
    public FunctionParameter[]? ResolvedParameters = null;
    public IdentifierExpression Parent = null!;

    public IdentifierExpressionPart? Last() => Index == 0 ? null : Index == 1 ? 
        Parent.IsFirstIdentifier ? Parent.FirstPart as IdentifierExpressionPart: null : Parent.OtherParts[Index - 2];
}

public class IdentifierExpression : Expression
{
    public readonly Expression FirstPart;
    public Symbol? FirstPartResolved;
    public readonly bool IsFirstIdentifier = false;
    public Symbol Resolved = null!;

    public readonly IdentifierExpressionPart[] OtherParts;

    public Expression Last => OtherParts.Length == 0 ? FirstPart : OtherParts[OtherParts.Length - 1];

    public IdentifierExpression(Position position, Expression firstPart, IdentifierExpressionPart[] otherParts) : base(position)
    {
        FirstPart = firstPart;
        OtherParts = otherParts;
        if (FirstPart is IdentifierExpressionPart _firstPart) 
        {
            _firstPart.Parent = this;
            IsFirstIdentifier = true;
            _firstPart.Index = 0;
        }
        else if (FirstPart is IdentifierExpression) throw new Exception("какого хрена вообще?!?");
        for (int i = 0; i < otherParts.Length; i++) 
        {
            var part = otherParts[i];
            part.Index = i + 1;
            part.Parent = this;
        }
    }
}

public class BinaryExpression(Position position, Expression left, string @operator, Expression right) : Expression(position)
{
    public readonly Expression Left = left, Right = right;
    public Symbol LeftSymbol = null!, RightSymbol = null!;
    public readonly string Operator = @operator;
}

public class AsExpression(Expression value, IdentifierExpression asType, Position position) : Expression(position)
{
    public readonly Expression Value = value;
    public readonly IdentifierExpression AsType = asType;

    public Symbol ResolvedValue = null!;
    public ClassSymbol ResolvedAsType = null!;
}
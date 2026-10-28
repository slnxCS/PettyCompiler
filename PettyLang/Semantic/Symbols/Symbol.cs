using PettyLang.AST;
using PettyLang.Errors;

namespace PettyLang.Semantic;

public abstract class Symbol
{

    protected Symbol(string name, int? id, ClassSymbol? type, Scope declaredIn, Position position)
    {
        Name = name;
        ID = id ?? 0;
        Type = type ?? (this as ClassSymbol)!;
        DeclaredIn = declaredIn;
        Position = position;
    }

    public string Name { get; protected set; }
    public int ID;
    public ClassSymbol Type;
    public virtual Scope? Members => null;
    public Scope DeclaredIn;
    public readonly Position Position;

    public virtual void CompileBinaryOperation(BinaryExpression expression, Compiler.Compiler compiler)
    {
        throw new NotSupportedOperandsError(GetFullName(), expression.RightSymbol.GetFullName(), expression.Operator, expression.Position);
    }

    public virtual ClassSymbol VisitBinary(ClassInstanceSymbol instance, BinaryExpression expression)
    {
        throw new NotSupportedOperandsError(GetFullName(), instance.GetFullName(), expression.Operator, expression.Position);
    }

    public virtual ClassSymbol ResolveCall(FunctionParameter[] arguments, IdentifierExpression id, IdentifierExpressionPart part)
    {
        throw new NotCallableError(GetFullName(), part.Position);
    }

    public virtual ClassSymbol ResolveCast(ClassSymbol castType, AsExpression expression)
    {
        if (castType == Type)
            return Type;
        throw new NotSupportedCastTypeError(GetFullName(), castType.GetFullName(), expression.Position);
    }

    public virtual void CompileCastBytes(ClassSymbol castType, AsExpression expression, Compiler.Compiler compiler)
    {
        if (castType == Type) 
        {
            compiler.CompileExpression(expression.Value);
            return;
        }
        throw new NotSupportedCastTypeError(GetFullName(), castType.GetFullName(), expression.Position);
    }

    public virtual byte[] GetBytesForCall(FunctionParameter[] arguments, IdentifierExpression id, IdentifierExpressionPart part)
    {
        throw new NotCallableError(GetFullName(), part.Position);
    }
    
    public virtual byte[] GetPushBytes(IdentifierExpressionPart? idPart)
    {
        throw new NotSupportedException($"Cannot push '{GetFullName()}'");
    }

    public virtual string GetFullName()
    {
        return Name;
    }
}
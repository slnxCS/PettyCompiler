namespace PettyLang.Semantic;

using PettyLang.AST;
using PettyLang.Compiler;

public class VarSymbol : Symbol
{
    public ClassInstanceSymbol Value;

    public VarSymbol(string name, Position position, Scope declaredIn, ClassSymbol type, ClassInstanceSymbol value) : 
        base(name, declaredIn.GetFreeVarID(), type, declaredIn, position)
    {
        Value = value;
    }

    public bool IsGlobal => DeclaredIn.Type == ScopeType.Global;

    public override byte[] GetPushBytes(IdentifierExpressionPart? idPart)
    {
        if (DeclaredIn.Self == null) {
            return [(byte)(IsGlobal ? OpCode.LOAD_GLOBAL : OpCode.LOAD_LOCAL), .. BitConverter.GetBytes(ID)];
        }
        else
        {
            return [(byte)OpCode.LOAD_FIELD, .. BitConverter.GetBytes(ID)];
        }
    }

    public override void CompileBinaryOperation(BinaryExpression expression, Compiler compiler)
    {
        Value.CompileBinaryOperation(expression, compiler);
    }

    public override Scope? Members => Value.Members;

    public override ClassSymbol VisitBinary(ClassInstanceSymbol instance, BinaryExpression expression)
    {
        return Value.VisitBinary(instance, expression);
    }

    public override ClassSymbol ResolveCast(ClassSymbol castType, AsExpression expression)
    {
        return Value.ResolveCast(castType, expression);
    }

    public override void CompileCastBytes(ClassSymbol castType, AsExpression expression, Compiler compiler)
    {
        Value.CompileCastBytes(castType, expression, compiler);
    }

    public override byte[] GetBytesForCall(FunctionParameter[] arguments, IdentifierExpression id, IdentifierExpressionPart part)
    {
        return Value.GetBytesForCall(arguments, id, part);
    }

    public override ClassSymbol ResolveCall(FunctionParameter[] arguments, IdentifierExpression id, IdentifierExpressionPart part)
    {
        return Value.ResolveCall(arguments, id, part);
    }
}
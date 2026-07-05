using System.Reflection;
using PettyLang.AST;
using PettyLang.Semantic;

namespace PettyLang.Compiler;

public enum OpCode : byte
{
    PUSH_CONSTANT = 1,
    STORE_LOCAL = 2,
    STORE_GLOBAL = 3,
    LOAD_LOCAL = 5,
    LOAD_GLOBAL = 6,
    
    ADD_INT = 10,
    SUB_INT = 11,
    DIV_INT = 12,
    MUL_INT = 13,
}

public class Compiler
{
    public Compiler(Statement[] AST)
    {
        ASTNodes = AST;
    }

    private readonly Statement[] ASTNodes;
    private List<byte> compiled = new();
    public readonly ConstantPool ConstantPool = new();
    public int GlobalsLenght { get; private set; } = 0;

    static Dictionary<string, OpCode> intOperators = new()
    {
        {"+", OpCode.ADD_INT},
        {"-", OpCode.SUB_INT},
        {"/", OpCode.DIV_INT},
        {"*", OpCode.MUL_INT}
    };

    void CompileVarDef(VarDeclStatement var)
    {
        CompileExpression(var.Value);
        if (var.Resolved.IsGlobal) GlobalsLenght++;
        Emit(var.Resolved.IsGlobal ? OpCode.STORE_GLOBAL : OpCode.STORE_LOCAL);
        Emit(var.Resolved.ID);
    }

    void CompileExpression(Expression ex)
    {
        switch (ex)
        {
            case IntExpression _int : 
                Emit(OpCode.PUSH_CONSTANT);
                Emit(ConstantPool.Add(new IntConstant(_int.Number)));
            break;
            case IdentifierExpression id : 
                CompileIdentifierExpression(id);
            break;

            case BinaryExpression bin :
                CompileBinary(bin);
            break;

            default : throw new NotImplementedException(ex.ToString());
        }
    }

    void CompileBinary(BinaryExpression bin)
    {
        CompileExpression(bin.Left);
        CompileExpression(bin.Right);

        if (bin.LeftSymbol != BuiltIn.Int32Class || bin.RightSymbol != BuiltIn.Int32Class)
            throw new NotImplementedException();
        
        Emit(intOperators[bin.Operator]);
    }

    void CompileIdentifierExpression(IdentifierExpression id)
    {
        Emit(id.Resolved.IsGlobal ? OpCode.LOAD_GLOBAL : OpCode.LOAD_LOCAL);
        Emit(id.Resolved.ID);
    }

    void CompileStatement(Statement st)
    {
        switch (st)
        {
            case VarDeclStatement var : 
                CompileVarDef(var);
                break;

            case StatementExpression expr : 
                CompileExpression(expr.Expression);
            break;
        }
    }

    public void Emit(OpCode code)
    {
        compiled.Add((byte)code);
    }

    public void Emit(int value)
    {
        compiled.AddRange(BitConverter.GetBytes(value));
    }

    public void Emit(ulong value)
    {
        compiled.AddRange(BitConverter.GetBytes(value));
    }

    void CompileStatements(Statement[] sts)
    {
        foreach (var st in sts) 
        {
            CompileStatement(st);
        }
    }

    public byte[] Comiple()
    {
        CompileStatements(ASTNodes);
        return compiled.ToArray();
    }
}
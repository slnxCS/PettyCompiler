using System.Reflection;
using PettyLang.AST;
using PettyLang.Semantic;

namespace PettyLang.Compiler;

public enum OpCode : byte
{
    PUSH = 1,
    STORE_LOCAL = 2,
    STORE_GLOBAL = 3,
    INT = 4,
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

    readonly Statement[] ASTNodes;
    List<byte> compiled = new();

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
    }

    void CompileExpression(Expression ex)
    {
        switch (ex)
        {
            case IntExpression _int : 
                Emit(OpCode.PUSH);
                Emit(OpCode.INT);
                Emit((int)_int.Number);
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

        if (bin.LeftSymbol.Type != BuiltIn.IntClass)
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
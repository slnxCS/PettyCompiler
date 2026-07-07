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

    HALT = 15,
}

public class Compiler
{
    public Compiler(Statement[] AST)
    {
        ASTNodes = AST;
    }

    private readonly Statement[] ASTNodes;
    private readonly BinaryWriter compilerWriter = new();
    public readonly BinaryWriter globalWriter = new();
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
        if (var.Resolved.IsGlobal)
        {
            CompileExpression(var.Value, globalWriter);
            compilerWriter.Emit(OpCode.STORE_GLOBAL);
        }
        else
        {
            CompileExpression(var.Value, compilerWriter);
            compilerWriter.Emit(OpCode.STORE_LOCAL);
        }

        compilerWriter.Emit(var.Resolved.ID);
    }

    void CompileExpression(Expression ex, IBinaryWriter? writer = null)
    {
        if (writer == null)
            writer = compilerWriter;
        switch (ex)
        {
            case IntExpression _int : 
                writer.Emit(OpCode.PUSH_CONSTANT);
                writer.Emit(ConstantPool.Add(new IntConstant(_int.Number)));
            break;
            case IdentifierExpression id : 
                CompileIdentifierExpression(id,  writer);
            break;

            case BinaryExpression bin :
                CompileBinary(bin, writer);
            break;

            default : throw new NotImplementedException(ex.ToString());
        }
    }

    void CompileBinary(BinaryExpression bin, IBinaryWriter writer)
    {
        CompileExpression(bin.Left);
        CompileExpression(bin.Right);

        if (bin.LeftSymbol != BuiltIn.Int32Class || bin.RightSymbol != BuiltIn.Int32Class)
            throw new NotImplementedException();
        
        writer.Emit(intOperators[bin.Operator]);
    }

    void CompileIdentifierExpression(IdentifierExpression id, IBinaryWriter writer)
    {
        writer.Emit(id.Resolved.IsGlobal ? OpCode.LOAD_GLOBAL : OpCode.LOAD_LOCAL);
        writer.Emit(id.Resolved.ID);
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

    void CompileStatements(Statement[] sts)
    {
        foreach (var st in sts) 
        {
            CompileStatement(st);
        }
        compilerWriter.WriteByte((byte)OpCode.HALT);
    }

    public byte[] Comiple()
    {
        CompileStatements(ASTNodes);
        return compilerWriter.GetWritedBytesArray();
    }
}
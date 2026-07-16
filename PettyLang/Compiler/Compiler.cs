using System.Reflection;
using PettyLang.AST;
using PettyLang.Errors;
using PettyLang.Semantic;

namespace PettyLang.Compiler;

public enum OpCode : byte
{
    PUSH_CONSTANT = 1,
    STORE_LOCAL = 2,
    STORE_GLOBAL = 3,
    LOAD_LOCAL = 5,
    LOAD_GLOBAL = 6,

    SYS_CALL = 4,

    RET = 7,
    CALL = 8,
    RESERVE_LOCALS = 9,
    
    ADD_INT = 10,
    SUB_INT = 11,
    DIV_INT = 12,
    MUL_INT = 13,
    ADD_FLOAT = 17,
    SUB_FLOAT = 18,
    MUL_FLOAT = 19,
    DIV_FLOAT = 20,

    HALT = 15,

}

public class Compiler
{
    public Compiler(Statement[] AST, Analyzer analyzer)
    {
        ASTNodes = AST;
        this.analyzer = analyzer;
    }

    private readonly Analyzer analyzer;

    private readonly Statement[] ASTNodes;
    private readonly ByteWriter compilerWriter = new();
    public readonly ByteWriter GlobalWriter = new();
    public readonly ByteWriter FunctionsWriter = new();
    public int GlobalsLenght { get; private set; } = 0;

    static Dictionary<string, OpCode> intOperators = new()
    {
        {"+", OpCode.ADD_INT},
        {"-", OpCode.SUB_INT},
        {"/", OpCode.DIV_INT},
        {"*", OpCode.MUL_INT},
    };

    static Dictionary<string, OpCode> floatOperators = new()
    {
        {"+", OpCode.ADD_FLOAT},
        {"-", OpCode.SUB_FLOAT},
        {"/", OpCode.DIV_FLOAT},
        {"*", OpCode.MUL_FLOAT},
    };

    void CompileVarDef(VarDeclStatement var, IByteWriter writer)
    {
        if (!var.Resolved.IsGlobal)
        {
            CompileExpression(var.Value, writer);
            writer.Emit(OpCode.STORE_LOCAL);
            writer.Emit(var.Resolved.ID);
        }
    }

    void CompileAssign(VarAssigmentStatement statement, IByteWriter writer)
    {
        if (statement.Operator != "=")
            throw new NotImplementedException();

        if (statement.Resolved == null)
            throw new Error("The value is null. Stop the compiler", "Compiler", statement.Position);

        CompileExpression(statement.Value, writer);
        writer.Emit(statement.Resolved.IsGlobal ? OpCode.STORE_GLOBAL : OpCode.STORE_LOCAL);
        writer.Emit(statement.Resolved.ID);
    }

    void CompileExpression(Expression ex, IByteWriter? writer = null)
    {
        if (writer == null)
            writer = compilerWriter;
        switch (ex)
        {
            case IntExpression _int : 
                writer.Emit(OpCode.PUSH_CONSTANT);
                writer.Emit(ConstantPool.Add(new IntConstant(_int.Number)));
            break;
            case FloatExpression _float : 
                writer.Emit(OpCode.PUSH_CONSTANT);
                writer.Emit(ConstantPool.Add(new FloatConstant(_float.Number)));
            break;
            case IdentifierExpression id : 
                CompileIdentifierExpression(id,  writer);
            break;

            case BinaryExpression bin :
                CompileBinary(bin, writer);
            break;

            case IdentifierExpressionPart idPart :
                CompileIdentifierExpressionPart(idPart, writer);
            break;

            default : throw new NotImplementedException(ex.ToString());
        }
    }

    void CompileBinary(BinaryExpression bin, IByteWriter writer)
    {
        CompileExpression(bin.Left, writer);
        CompileExpression(bin.Right, writer);

        if (bin.LeftSymbol.Type == BuiltIn.Int32Class && bin.RightSymbol.Type == BuiltIn.Int32Class)
            writer.Emit(intOperators[bin.Operator]);
        else if (bin.LeftSymbol.Type == BuiltIn.Float32Class && bin.RightSymbol.Type == BuiltIn.Float32Class)
            writer.Emit(floatOperators[bin.Operator]);

        else throw new NotImplementedException();
    }

    void CompileIdentifierExpressionPart(IdentifierExpressionPart part, IByteWriter writer)
    {
        if (part.ResolvedOverload != null)
        {
            var ov = part.ResolvedOverload;
            for (int i = 0; i < part.FuncCallsArguments[0].Length; i++)
            {
                CompileExpression(part.FuncCallsArguments[0][i], writer);
            }
            
            writer.Emit(ov is BuiltInFunctionOverload ? OpCode.SYS_CALL : OpCode.CALL);
            writer.Emit(ov.ID);
            if (ov is not BuiltInFunctionOverload) writer.Emit(ov.Arity);
            return;
        }

        var sym = part.Resolved;

        if (sym is VarSymbol var)
        {
            writer.Emit(var.IsGlobal ? OpCode.LOAD_GLOBAL : OpCode.LOAD_LOCAL);
            writer.Emit(var.ID);
        }
        else throw new NotImplementedException(part.Resolved.ToString());
    }

    void CompileIdentifierExpression(IdentifierExpression id, IByteWriter writer)
    {
        CompileExpression(id.FirstPart, writer);
    }

    void CompileReturn(ReturnStatement statement, IByteWriter writer)
    {
        if (statement.Value != null)
            CompileExpression(statement.Value, writer);

        writer.Emit(OpCode.RET);
    }

    void CompileStatement(Statement st, IByteWriter? writer = null)
    {
        if (writer == null)
            writer = compilerWriter;
        switch (st)
        {
            case ReturnStatement ret :
                CompileReturn(ret, writer);
            break;

            case VarDeclStatement var : 
                CompileVarDef(var, writer);
            break;

            case StatementExpression expr : 
                CompileExpression(expr.Expression, writer);
            break;

            case VarAssigmentStatement varAssigment :
                CompileAssign(varAssigment, writer);
            break;

            case FuncDefineStatement funcDef : break;
        }
    }    

    void CompileStatements(Statement[] sts, IByteWriter writer)
    {
        foreach (var st in sts) 
        {
            CompileStatement(st, writer);
        }
    }

    private void WriteGlobals()
    {
        foreach (var global in analyzer.GlobalVariables)
        {
            if (!global.Resolved.IsGlobal)
                throw new NotImplementedException("There should be here a warning");
            CompileExpression(global.Value, GlobalWriter);
            GlobalWriter.Emit(OpCode.STORE_GLOBAL);
            GlobalWriter.Emit(global.Resolved.ID);
        }
    }

    private void WriteFunctions()
    {
        foreach (var func in analyzer.Functions)
        {
            FunctionsWriter.Emit(OpCode.RESERVE_LOCALS);
            FunctionsWriter.Emit(func.Resolved!.LocalsCount);
            CompileStatements(func.Block.Statements, FunctionsWriter);
            FunctionsWriter.Emit(OpCode.RET);
            FunctionsWriter.Emit(OpCode.HALT);
        }
    }

    public byte[] Comiple()
    {
        WriteGlobals();
        WriteFunctions();
        CompileStatements(ASTNodes, compilerWriter);
        compilerWriter.Emit(OpCode.CALL);
        compilerWriter.Emit(Analyzer.MainFunction!.ID);
        compilerWriter.Emit(0);
        return compilerWriter.GetWritedBytesArray();
    }
}
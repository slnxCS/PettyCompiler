using System.Numerics;
using System.Reflection;
using System.Text;
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
    CAST_FROM_FLOAT32_TO_INT32 = 14,
    CAST_FROM_INT32_TO_FLOAT32 = 16,

    JMP_IF_FALSE = 26,
    JMP_IF_TRUE = 27,
    JMP = 28,
    INT_EQ = 29,

    ALLOCATE_OBJ = 25,
    CALL_METHOD = 30,
    LOAD_FIELD = 31,
    STORE_FIELD = 32,
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
    public List<byte> compiled = new();

    public void Emit(int value)
    {
        compiled.AddRange(BitConverter.GetBytes(value));
    }

    public void Emit(string text)
    {
        compiled.AddRange(Encoding.UTF8.GetBytes(text));
    }

    public void Emit(float value)
    {
        compiled.AddRange(BitConverter.GetBytes(value));
    }

    public void Emit(OpCode opCode)
    {
        compiled.Add((byte)opCode);
    }

    public void Emit(byte[] bytes)
    {
        compiled.AddRange(bytes);
    }

    public int GlobalsLenght { get; private set; } = 0;

    void CompileVarDef(VarDeclStatement var)
    {
        if (!var.Resolved.IsGlobal)
        {
            CompileExpression(var.Value);
            Emit(OpCode.STORE_LOCAL);
            Emit(var.Resolved.ID);
        }
    }

    void CompileAssign(VarAssigmentStatement statement)
    {
        if (statement.Operator != "=")
            throw new NotImplementedException();

        if (statement.Resolved == null)
            throw new NullReferenceException();

        CompileExpression(statement.Value);
        if (statement.Resolved.DeclaredIn.Type == ScopeType.Instance)
        {
            Emit(statement.Resolved.GetOwnerPushBytes());
            Emit(OpCode.STORE_FIELD);
        }
        else 
        {
            Emit(statement.Resolved.IsGlobal ? OpCode.STORE_GLOBAL : OpCode.STORE_LOCAL);
        }

        Emit(statement.Resolved.ID);
    }

    public void CompileExpression(Expression ex)
    {
        switch (ex)
        {
            case IntExpression _int : 
                compiled.AddRange(_int.Resolved.GetPushBytes());
            break;

            case FloatExpression _float : 
                compiled.AddRange(_float.Resolved.GetPushBytes());
            break;

            case BoolExpression _bool : 
                compiled.AddRange(_bool.Resolved.GetPushBytes());
            break;

            case IdentifierExpression id : 
                CompileIdentifierExpression(id);
            break;

            case IdentifierExpressionPart idPart :
                CompileIdentifierExpressionPart(idPart);
            break;

            case BinaryExpression bin :
                bin.LeftSymbol.CompileBinaryOperation(bin, this);
            break;

            case AsExpression @as :
                @as.ResolvedValue.CompileCastBytes(@as.ResolvedAsType, @as, this);
            break;

            default : throw new NotImplementedException(ex.ToString());
        }
    }

    void CompileIdentifierExpressionPart(IdentifierExpressionPart part)
    {
        if (part.ResolvedOverload != null)
        {
            var ov = part.ResolvedOverload;
            for (int i = 0; i < part.FuncCallsArguments[0].Length; i++)
            {
                CompileExpression(part.FuncCallsArguments[0][i]);
            }
            
            compiled.AddRange(ov.Parent.GetBytesForCall(part.ResolvedParameters!, part.Parent, part));
            return;
        }

        var sym = part.Resolved;

        compiled.AddRange(sym.GetPushBytes());
    }

    void CompileIdentifierExpression(IdentifierExpression id)
    {
        CompileExpression(id.FirstPart);
        foreach (var part in id.OtherParts) 
        {
            CompileIdentifierExpressionPart(part);
        }
    }

    void CompileReturn(ReturnStatement statement)
    {
        if (statement.Value != null)
            CompileExpression(statement.Value);

        Emit(OpCode.RET);
    }

    void CompileStatement(Statement st)
    {
        switch (st)
        {
            case ReturnStatement ret :
                CompileReturn(ret);
            break;

            case VarDeclStatement var : 
                CompileVarDef(var);
            break;

            case StatementExpression expr : 
                CompileExpression(expr.Expression);
            break;

            case VarAssigmentStatement varAssigment :
                CompileAssign(varAssigment);
            break;

            case BlockStatement block : 
                CompileStatements(block.Statements);
            break;

            case FuncDefineStatement : break;
            case ClassDefineStatement : break;

            case IfStatement @if :
                CompileIf(@if);
            break;    

            default : 
                throw new NotImplementedException(st.ToString());
        }
    }    

    void CompileIf(IfStatement statement)
    {   
        CompileExpression(statement.Condition);
        Emit(OpCode.JMP_IF_FALSE);
        var oldLen = compiled.Count;
        CompileStatements(statement.Block.Statements);
        var newLen = compiled.Count + 4;
        compiled.InsertRange(oldLen, BitConverter.GetBytes(newLen));
    }

    void CompileStatements(Statement[] sts)
    {
        foreach (var st in sts) 
        {
            CompileStatement(st);
        }
    }

    private void WriteGlobals()
    {
        Emit(analyzer.GlobalVariables.Count);
        foreach (var global in analyzer.GlobalVariables)
        {
            if (!global.Resolved.IsGlobal)
                throw new NotImplementedException("There should be here a warning");
            CompileExpression(global.Value);
            Emit(OpCode.STORE_GLOBAL);
            Emit(global.Resolved.ID);
        }
        Emit(OpCode.HALT);
    }

    private void SortFunctionsByID(List<FuncDefineStatement> functions)
    {
        functions = functions.OrderBy(x => x.Resolved?.ID 
            ?? throw new Error($"Function {x.Name} is not resolved", "compiler", x.Position)).ToList();
    }

    private void SortClassesByID()
    {
        analyzer.Classes = analyzer.Classes.OrderBy(x => x.Resolved?.ID 
            ?? throw new Error($"Class '{x.Name}' is not resolved", "compiler", x.Position)).ToList();
    }


    private void WriteFunctions(List<FuncDefineStatement> functions) {
        Emit(functions.Count);

        SortFunctionsByID(functions);

        for (int i = 0; i < functions.Count; i++)
        {
            var func = functions[i];
            Emit(OpCode.RESERVE_LOCALS);
            Emit(func.Resolved!.LocalsCount);
            CompileStatements(func.Block.Statements);
            Emit(OpCode.RET);
            Emit(OpCode.HALT);
        }

    }

    private void WriteClasses()
    {
        Emit(analyzer.Classes.Count);

        SortClassesByID();
        
        for (int i = 0; i < analyzer.Classes.Count; i++)
        {
            var @class = analyzer.Classes[i];
            if (@class.Resolved == null)
                throw new Error($"Class '{@class.Name}' is not resolved", "compiler", @class.Position);

            Emit(Encoding.UTF8.GetByteCount(@class.Name));
            Emit(@class.Name);
            Emit(@class.Resolved.ID);
            Emit(@class.Resolved.Derived?.ID ?? -1);
            Emit(@class.Resolved.VarDefines.Count);
            if (analyzer.Methods.ContainsKey(@class.Resolved))
                WriteFunctions(analyzer.Methods[@class.Resolved]);
            else Emit(0);
        }
    }

    public byte[] Compile(HeaderCompiler headerCompiler)
    {
        compiled.Clear();
        compiled.AddRange(headerCompiler.Compile());
        WriteFunctions(analyzer.Functions);
        WriteClasses();

        
        WriteGlobals();
        CompileStatements(ASTNodes);
        Emit(OpCode.CALL);
        if (Analyzer.MainFunction == null)
            throw new Error("Missing main function (entry point)", "compiler", default);
        Emit(Analyzer.MainFunction.ID);
        Emit(Analyzer.MainFunction.Arity);
        Emit(OpCode.HALT);
        return compiled.ToArray();
    }
}
using System.Buffers;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
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
    ALLOCATE_OBJ = 25,
    CALL_METHOD = 30,
    LOAD_FIELD = 31,
    STORE_FIELD = 32,
    ALLOCATE_ARR = 35,
    ARR_GET = 36,
    ARR_SET = 37,

    INT_EQ = 29,
    INT_NEQ = 38,
    INT_NEG = 39,
    INT_LEQ = 40,
    INT_GEQ = 41,
    INT_LE = 42,
    INT_GE = 43,

    FLOAT_EQ = 44,
    FLOAT_LEQ = 45,
    FLOAT_GEQ = 46,
    FLOAT_LE = 47,
    FLOAT_GE = 48,
    FLOAT_NEQ = 54,
    FLOAT_NEG = 55,

    BOOL_EQ = 49,
    BOOL_NOT = 50,
    BOOL_AND = 51,
    BOOL_OR = 52,
    BOOL_NEQ = 53,
}

public class Compiler
{
    public Compiler(Statement[] AST, Analyzer analyzer)
    {
        ASTNodes = AST;
        Analyzer = analyzer;
    }

    public readonly Analyzer Analyzer;

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

    void CompileVarDef(VarDeclStatement var)
    {
        if (!var.Resolved.IsGlobal)
        {
            if (var.Value != null) CompileExpression(var.Value);
            else Emit(var.Resolved.Type.GetDefaultValueBytes(this, var.Position));
            Emit(OpCode.STORE_LOCAL);
            Emit(var.Resolved.ID);
        }
    }

    void CompileAssign(VarAssignmentStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement.Resolved);
        ArgumentNullException.ThrowIfNull(statement.ResolvedValue);

        if (statement.Operator == "=") 
        {
            CompileExpression(statement.Value);
            if (statement.Resolved.DeclaredIn.Type == ScopeType.Instance)
            {
                CompileIdentifierExpressionWithoutLast(statement.Target);
                Emit(OpCode.STORE_FIELD);
            }
            else 
            {
                Emit(statement.Resolved.IsGlobal ? OpCode.STORE_GLOBAL : OpCode.STORE_LOCAL);
            }

            Emit(statement.Resolved.ID);
        }
        else
        {
            statement.Resolved.Value.CompileEquatingOperator(statement, this);
        }
    }

    public void CompilePushVar(IdentifierExpression target, VarSymbol var)
    {
        if (var.DeclaredIn.Type == ScopeType.Instance)
        {
            CompileIdentifierExpressionWithoutLast(target);
            Emit(OpCode.STORE_FIELD);
        }
        else 
        {
            Emit(var.IsGlobal ? OpCode.STORE_GLOBAL : OpCode.STORE_LOCAL);
        }

        Emit(var.ID);
    }

    public void CompileExpression(Expression ex)
    {
        switch (ex)
        {
            case IntExpression _int : 
                compiled.AddRange(_int.Resolved.GetPushBytes(null));
            break;

            case FloatExpression _float : 
                compiled.AddRange(_float.Resolved.GetPushBytes(null));
            break;

            case BoolExpression _bool : 
                compiled.AddRange(_bool.Resolved.GetPushBytes(null));
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

            case UnaryExpression unary :
                ArgumentNullException.ThrowIfNull(unary.Resolved, nameof(unary.Resolved));
                unary.Resolved.CompileUnaryOperation(unary, this);
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

        compiled.AddRange(sym.GetPushBytes(part));
    }

    public void CompileIdentifierExpression(IdentifierExpression id)
    {
        CompileExpression(id.FirstPart);
        foreach (var part in id.OtherParts) 
        {
            CompileIdentifierExpressionPart(part);
        }
    }

    public void CompileIdentifierExpressionWithoutLast(IdentifierExpression id)
    {
        if (id.OtherParts.Length == 0) return;

        CompileExpression(id.FirstPart);

        for (int i = 0; i < id.OtherParts.Length - 1; i++)
        {
            CompileExpression(id.OtherParts[i]);
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

            case VarAssignmentStatement varAssignment :
                CompileAssign(varAssignment);
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
        Emit(0);
        CompileStatements(statement.Block.Statements);
        SetReservedBytes(oldLen, BitConverter.GetBytes(compiled.Count));
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
        Emit(Analyzer.GlobalVariables.Count);
        foreach (var global in Analyzer.GlobalVariables)
        {
            if (global.Value != null) CompileExpression(global.Value);
            else Emit(global.Resolved.Type.GetDefaultValueBytes(this, global.Position));
            Emit(OpCode.STORE_GLOBAL);
            Emit(global.Resolved.ID);
        }
        Emit(OpCode.HALT);
    }

    private void SortFunctionsByID(List<FunctionOverload> functions)
    {
        functions = [.. functions.OrderBy(x => x.ID)];
    }

    private void SortFunctionsByID(List<FuncDefineStatement> functions)
    {
        functions = functions.OrderBy(x => x.Resolved?.ID 
            ?? throw new Error($"Function {x.Name} is not resolved", "compiler", x.Position)).ToList();
    }

    private void SortClassesByID()
    {
        Analyzer.Classes = Analyzer.Classes.OrderBy(x => x.Resolved?.ID 
            ?? throw new Error($"Class '{x.Name}' is not resolved", "compiler", x.Position)).ToList();
    }


    private void WriteFunctions(List<FunctionOverload> functions) {
        Emit(functions.Count);

        SortFunctionsByID(functions);

        for (int i = 0; i < functions.Count; i++)
        {
            var func = functions[i];
            var oldCount = compiled.Count;
            Emit(0); // reserve bytes for function length
            Emit(OpCode.RESERVE_LOCALS);
            Emit(func.LocalsCount);
            if (func.Statement != null)
                CompileStatements(func.Statement.Block.Statements);
            else
                func.EmitBytes!(this);
            Emit(OpCode.RET);
            var newCount = compiled.Count;
            var funcLength = newCount - oldCount - 4;
            SetReservedBytes(oldCount, BitConverter.GetBytes(funcLength));
        }

    }

    private void SetReservedBytes(int startPos, byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return;
        Span<byte> compiledSpan = CollectionsMarshal.AsSpan(compiled);
        bytes.AsSpan().CopyTo(compiledSpan.Slice(startPos));
    }

    private void WriteFunctions(List<FuncDefineStatement> functions) {
        Emit(functions.Count);

        SortFunctionsByID(functions);

        for (int i = 0; i < functions.Count; i++)
        {
            var func = functions[i];
            var oldCount = compiled.Count;
            Emit(0); // reserve bytes for function length
            Emit(OpCode.RESERVE_LOCALS);
            Emit(func.Resolved!.LocalsCount);
            CompileStatements(func.Block.Statements);
            Emit(OpCode.RET);
            var newCount = compiled.Count;
            var funcLength = newCount - oldCount - 4;
            SetReservedBytes(oldCount, BitConverter.GetBytes(funcLength));
        }

    }

    private void WriteClasses()
    {
        Emit(Analyzer.Classes.Count);

        SortClassesByID();
        
        for (int i = 0; i < Analyzer.Classes.Count; i++)
        {
            var @class = Analyzer.Classes[i];
            if (@class.Resolved == null)
                throw new Error($"Class '{@class.Name}' is not resolved", "compiler", @class.Position);

            Emit(Encoding.UTF8.GetByteCount(@class.Name));
            Emit(@class.Name);
            Emit(@class.Resolved.ID);
            Emit(@class.Resolved.Derived?.ID ?? -1);
            Emit(@class.Resolved.VarDefines.Count);
            if (Analyzer.Methods.ContainsKey(@class.Resolved))
                WriteFunctions(Analyzer.Methods[@class.Resolved]);
            else Emit(0);
        }
    }

    public byte[] Compile(HeaderCompiler headerCompiler)
    {
        compiled.Clear();
        compiled.AddRange(headerCompiler.Compile(Analyzer.ConstantPool));
        WriteFunctions(Analyzer.Functions);
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
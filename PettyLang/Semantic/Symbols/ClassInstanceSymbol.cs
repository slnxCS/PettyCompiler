namespace PettyLang.Semantic;

using PettyLang.AST;
using PettyLang.Compiler;
using PettyLang.Errors;

public class ClassInstanceSymbol : Symbol
{
    public ClassInstanceSymbol(Scope declaredIn, ClassSymbol @class, Position position, Analyzer analyzer)
        : base(@class.GetFullName(), null, @class, declaredIn, position)
    {
        this.analyzer = analyzer;
        Native = @class.Native;
        selfScope = new(ScopeType.Instance, DeclaredIn, this);
        selfScope.CopyFromClass(@class, analyzer);
    }

    protected readonly Native Native;
    protected readonly Analyzer analyzer;
    private Scope selfScope;
    public override Scope Members => selfScope;

    public override byte[] GetPushBytes(IdentifierExpressionPart? idPart)
    {   
        if (idPart?.FuncCallsArguments[0] == null)
        {
            throw new NotImplementedException();
        }
        else return [];
    }
}

public class Float32InstanceSymbol : ClassInstanceSymbol
{
    public float? Value;
    public bool IsConstant => Value != null;

    public Float32InstanceSymbol(Scope globalScope, ClassSymbol? floatClass, float? value, Position position, Analyzer analyzer) 
        : base(globalScope, floatClass ?? analyzer.Native.Float32Class, position, analyzer)
    {
        Value = value;
        if (Value != null)
            analyzer.ConstantPool.Add(new FloatConstant(Value.GetValueOrDefault()));
    }

    public override ClassSymbol ResolveCast(ClassSymbol castType, AsExpression expression)
    {
        if (castType is Int32ClassSymbol)
            return Native.Int32Class;
        
        return base.ResolveCast(castType, expression);
    }

    public override void CompileCastBytes(ClassSymbol castType, AsExpression expression, Compiler compiler)
    {
        if (castType is Int32ClassSymbol)
        {
            compiler.CompileExpression(expression.Value);
            compiler.Emit(OpCode.CAST_FROM_FLOAT32_TO_INT32);
            return;
        }
        base.CompileCastBytes(castType, expression, compiler);
    }

    static readonly Dictionary<string, OpCode> operators = new()
    {
        {"+", OpCode.ADD_FLOAT},
        {"-", OpCode.SUB_FLOAT},
        {"*", OpCode.MUL_FLOAT},
        {"/", OpCode.DIV_FLOAT},
    };

    public override void CompileBinaryOperation(BinaryExpression expression, Compiler compiler)
    {
        var lintExpr = expression.Left;
        var rintExpr = expression.Right;
        
        compiler.CompileExpression(lintExpr);
        compiler.CompileExpression(rintExpr);
        compiler.Emit(operators[expression.Operator]);
    }

    public override byte[] GetPushBytes(IdentifierExpressionPart? idPart)
    {
        return [(byte)OpCode.PUSH_CONSTANT, .. BitConverter.GetBytes(analyzer.ConstantPool.Add(new FloatConstant(Value 
            ?? throw new NullReferenceException("Value"))))];
    }

    public override ClassSymbol VisitBinary(ClassInstanceSymbol right, BinaryExpression expression)
    {
        if (right is not Float32InstanceSymbol)
            throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), expression.Operator, expression.Position);
        
        return expression.Operator switch
        {
            "+" => Native.Float32Class,
            "-" => Native.Float32Class,
            "*" => Native.Float32Class,
            "/" => Native.Float32Class,
            _ =>  throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), expression.Operator, expression.Position)
        };
    }
}

public class Int32InstanceSymbol : ClassInstanceSymbol
{
    public int? Value;
    public bool IsConstant => Value != null;

    public Int32InstanceSymbol(Scope scope, ClassSymbol intClass, int? value, Position position, Analyzer analyzer) 
        : base(scope, intClass, position, analyzer)
    {
        Value = value;
        
        if (Value != null)
            analyzer.ConstantPool.Add(new IntConstant(value.GetValueOrDefault()));
    }

    public override byte[] GetPushBytes(IdentifierExpressionPart? idPart)
    {
        return [(byte)OpCode.PUSH_CONSTANT, .. BitConverter.GetBytes(analyzer.ConstantPool.Add(new IntConstant(Value 
            ?? throw new NullReferenceException("Value"))))];
    }

    public override ClassSymbol ResolveCast(ClassSymbol castType, AsExpression expression)
    {
        if (castType is Float32ClassSymbol)
            return Native.Float32Class;
        
        return base.ResolveCast(castType, expression);
    }

    public override void CompileCastBytes(ClassSymbol castType, AsExpression expression, Compiler compiler)
    {
        if (castType is Float32ClassSymbol)
        {
            compiler.CompileExpression(expression.Value);
            compiler.Emit(OpCode.CAST_FROM_INT32_TO_FLOAT32);
            return;
        }
        base.CompileCastBytes(castType, expression, compiler);
    }

    static Dictionary<string, OpCode> operators = new()
    {
        {"+", OpCode.ADD_INT},
        {"-", OpCode.SUB_INT},
        {"*", OpCode.MUL_INT},
        {"/", OpCode.DIV_INT},
        {"==", OpCode.INT_EQ},
    };

    public override void CompileBinaryOperation(BinaryExpression expression, Compiler compiler)
    {
        var lintExpr = expression.Left;
        var rintExpr = expression.Right;
        
        compiler.CompileExpression(lintExpr);
        compiler.CompileExpression(rintExpr);
        compiler.Emit(operators[expression.Operator]);
    }

    public override ClassSymbol VisitBinary(ClassInstanceSymbol right, BinaryExpression expression)
    {
        if (right is not Int32InstanceSymbol)
            throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), expression.Operator, expression.Position);
        
        return expression.Operator switch
        {
            "+" => Native.Int32Class,
            "-" => Native.Int32Class,
            "*" => Native.Int32Class,
            "/" => Native.Float32Class,
            "==" => Native.BoolClass,
          _ =>  throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), expression.Operator, expression.Position)
        };
    }
}

public class ArrayInstanceSymbol : ClassInstanceSymbol
{
    public ArrayInstanceSymbol(Scope declaredIn, Position position, Analyzer analyzer) 
        : base(declaredIn, analyzer.Native.ArrayClass, position, analyzer) 
    { }
}

public class BoolInstanceSymbol : ClassInstanceSymbol
{
    public bool? Value;

    public BoolInstanceSymbol(bool? value, Scope declaredIn, Position position, Analyzer analyzer) 
        : base(declaredIn, analyzer.Native.BoolClass, position, analyzer)
    {
        Value = value;

        if (Value != null)
            analyzer.ConstantPool.Add(new BoolConstant(Value.GetValueOrDefault()));
    }

    public override byte[] GetPushBytes(IdentifierExpressionPart? idPart)
    {
        return [(byte)OpCode.PUSH_CONSTANT, 
            ..BitConverter.GetBytes(analyzer.ConstantPool.Add(new BoolConstant(Value ?? throw new ArgumentNullException())))];
    }
}
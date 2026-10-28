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
        binaryOperators = new();
        unaryOperators = new();
    }

    protected Dictionary<string, (OpCode, ClassSymbol)> binaryOperators, unaryOperators;

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

        binaryOperators = new Dictionary<string, (OpCode, ClassSymbol)>()
        {
            {"+", (OpCode.ADD_FLOAT, analyzer.Native.Float32Class)},
            {"-", (OpCode.SUB_FLOAT, analyzer.Native.Float32Class)},
            {"*", (OpCode.MUL_FLOAT, analyzer.Native.Float32Class)},
            {"/", (OpCode.DIV_FLOAT, analyzer.Native.Float32Class)},
            {"==", (OpCode.FLOAT_EQ, analyzer.Native.BoolClass)},
            {">=", (OpCode.FLOAT_GEQ, analyzer.Native.BoolClass)},
            {"<=", (OpCode.FLOAT_LEQ, analyzer.Native.BoolClass)},
            {"<", (OpCode.FLOAT_LE, analyzer.Native.BoolClass)},
            {">", (OpCode.FLOAT_GE, analyzer.Native.BoolClass)},  
            {"!=", (OpCode.FLOAT_NEQ, analyzer.Native.BoolClass)},
        };

        unaryOperators = new()
        {
            {"-", (OpCode.FLOAT_NEG, analyzer.Native.Float32Class)},
        };
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

    public override void CompileBinaryOperation(BinaryExpression expression, Compiler compiler)
    {
        var lintExpr = expression.Left;
        var rintExpr = expression.Right;
        
        compiler.CompileExpression(lintExpr);
        compiler.CompileExpression(rintExpr);
        compiler.Emit(binaryOperators[expression.Operator].Item1);
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
        
        if (binaryOperators.TryGetValue(expression.Operator, out var res))
            return res.Item2;

        throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), expression.Operator, expression.Position);
    }

    public override void CompileUnaryOperation(UnaryExpression expression, Compiler compiler)
    {
        var expr = expression.Right;
        
        compiler.CompileExpression(expr);
        compiler.Emit(unaryOperators[expression.Operator].Item1);
    }

    public override ClassSymbol VisitUnary(UnaryExpression expression)
    {        
        if (unaryOperators.TryGetValue(expression.Operator, out var res))
            return res.Item2;

        throw new NotSupportedOperandsError(GetFullName(), expression.Operator, expression.Position);
    }

    public override void VisitEquatingOperator(
        VarAssignmentStatement statement, Analyzer analyzer, Scope scope, ClassInstanceSymbol right)
    {
        if (right.Type != Type)
            throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), statement.Position);
    }

    public override void CompileEquatingOperator(
        VarAssignmentStatement statement, Compiler compiler)
    {
        ArgumentNullException.ThrowIfNull(statement.ResolvedValue);
        ArgumentNullException.ThrowIfNull(statement.Resolved);

        compiler.Emit(statement.Resolved.GetPushBytes(null));
        compiler.CompileExpression(statement.Value);
        compiler.Emit(binaryOperators[statement.Operator.Replace("=", "")].Item1);
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

        binaryOperators = new Dictionary<string, (OpCode, ClassSymbol)>()
        {
            {"+", (OpCode.ADD_INT, analyzer.Native.Int32Class)},
            {"-", (OpCode.SUB_INT, analyzer.Native.Int32Class)},
            {"*", (OpCode.MUL_INT, analyzer.Native.Int32Class)},
            {"/", (OpCode.DIV_INT, analyzer.Native.Int32Class)},
            {"==", (OpCode.INT_EQ, analyzer.Native.BoolClass)},
            {">=", (OpCode.INT_GEQ, analyzer.Native.BoolClass)},
            {"<=", (OpCode.INT_LEQ, analyzer.Native.BoolClass)},
            {"<", (OpCode.INT_LE, analyzer.Native.BoolClass)},
            {">", (OpCode.INT_GE, analyzer.Native.BoolClass)},
            {"!=", (OpCode.INT_NEQ, analyzer.Native.BoolClass)},
        };

        unaryOperators = new()
        {
            {"-", (OpCode.INT_NEG, analyzer.Native.Int32Class)},  
        };
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

    public override void CompileBinaryOperation(BinaryExpression expression, Compiler compiler)
    {
        var lintExpr = expression.Left;
        var rintExpr = expression.Right;
        
        compiler.CompileExpression(lintExpr);
        compiler.CompileExpression(rintExpr);
        compiler.Emit(binaryOperators[expression.Operator].Item1);
    }

    public override ClassSymbol VisitBinary(ClassInstanceSymbol right, BinaryExpression expression)
    {
        if (right is not Int32InstanceSymbol)
            throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), expression.Operator, expression.Position);
        
        if (binaryOperators.TryGetValue(expression.Operator, out var res))
            return res.Item2;

        throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), expression.Operator, expression.Position);
    }

    public override void CompileUnaryOperation(UnaryExpression expression, Compiler compiler)
    {
        var expr = expression.Right;
        
        compiler.CompileExpression(expr);
        compiler.Emit(unaryOperators[expression.Operator].Item1);
    }

    public override ClassSymbol VisitUnary(UnaryExpression expression)
    {        
        if (unaryOperators.TryGetValue(expression.Operator, out var res))
            return res.Item2;

        throw new NotSupportedOperandsError(GetFullName(), expression.Operator, expression.Position);
    }

    public override void VisitEquatingOperator(
        VarAssignmentStatement statement, Analyzer analyzer, Scope scope, ClassInstanceSymbol right)
    {
        if (right.Type != Type)
            throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), statement.Position);
    }

    public override void CompileEquatingOperator(
        VarAssignmentStatement statement, Compiler compiler)
    {
        ArgumentNullException.ThrowIfNull(statement.ResolvedValue);
        ArgumentNullException.ThrowIfNull(statement.Resolved);

        compiler.Emit(statement.Resolved.GetPushBytes(null));
        compiler.CompileExpression(statement.Value);
        compiler.Emit(binaryOperators[statement.Operator.Replace("=", "")].Item1);
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

        binaryOperators = new()
        {
            {"!=", (OpCode.BOOL_NEQ, analyzer.Native.BoolClass)},
            {"==", (OpCode.BOOL_EQ, analyzer.Native.BoolClass)},
            {"and", (OpCode.BOOL_AND, analyzer.Native.BoolClass)},
            {"or", (OpCode.BOOL_OR, analyzer.Native.BoolClass)},
        };

        unaryOperators = new()
        {
            {"not", (OpCode.BOOL_NOT, analyzer.Native.BoolClass)},
        };
    }

    public override byte[] GetPushBytes(IdentifierExpressionPart? idPart)
    {
        return [(byte)OpCode.PUSH_CONSTANT, 
            ..BitConverter.GetBytes(analyzer.ConstantPool.Add(new BoolConstant(Value ?? throw new ArgumentNullException())))];
    }

    public override void CompileBinaryOperation(BinaryExpression expression, Compiler compiler)
    {
        var lintExpr = expression.Left;
        var rintExpr = expression.Right;
        
        compiler.CompileExpression(lintExpr);
        compiler.CompileExpression(rintExpr);
        compiler.Emit(binaryOperators[expression.Operator].Item1);
    }

    public override ClassSymbol VisitBinary(ClassInstanceSymbol right, BinaryExpression expression)
    {
        if (right is not BoolInstanceSymbol)
            throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), expression.Operator, expression.Position);
        
        if (binaryOperators.TryGetValue(expression.Operator, out var res))
            return res.Item2;

        throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), expression.Operator, expression.Position);
    }

    public override void CompileUnaryOperation(UnaryExpression expression, Compiler compiler)
    {
        var expr = expression.Right;
        
        compiler.CompileExpression(expr);
        compiler.Emit(unaryOperators[expression.Operator].Item1);
    }

    public override ClassSymbol VisitUnary(UnaryExpression expression)
    {        
        if (unaryOperators.TryGetValue(expression.Operator, out var res))
            return res.Item2;

        throw new NotSupportedOperandsError(GetFullName(), expression.Operator, expression.Position);
    }

    public override void VisitEquatingOperator(
        VarAssignmentStatement statement, Analyzer analyzer, Scope scope, ClassInstanceSymbol right)
    {
        if (right.Type != Type)
            throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), statement.Position);
    }

    public override void CompileEquatingOperator(
        VarAssignmentStatement statement, Compiler compiler)
    {
        ArgumentNullException.ThrowIfNull(statement.ResolvedValue);
        ArgumentNullException.ThrowIfNull(statement.Resolved);

        compiler.Emit(statement.Resolved.GetPushBytes(null));
        compiler.CompileExpression(statement.Value);
        compiler.Emit(binaryOperators[statement.Operator.Replace("=", "")].Item1);
    }
}
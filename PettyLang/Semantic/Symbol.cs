using System.Runtime.InteropServices;
using PettyLang.AST;
using PettyLang.Compiler;
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

    public string Name {get; protected set; }
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
            return;
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

public class VarSymbol : Symbol
{
    public ClassInstanceSymbol Value;

    public VarSymbol(string name, Position position, Scope declaredIn, ClassSymbol type, ClassInstanceSymbol value) : 
        base(name, declaredIn.GetFreeVarID(), type, declaredIn, position)
    {
        Value = value;
    }

    public bool IsGlobal => DeclaredIn.Type == ScopeType.Global;

    public byte[] GetStoreBytes()
    {
        return [(byte)(IsGlobal ? OpCode.STORE_GLOBAL : OpCode.STORE_LOCAL), .. BitConverter.GetBytes(ID)];
    }

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

    public override void CompileBinaryOperation(BinaryExpression expression, Compiler.Compiler compiler)
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

    public override void CompileCastBytes(ClassSymbol castType, AsExpression expression, Compiler.Compiler compiler)
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

public class ClassInstanceSymbol : Symbol
{
    public ClassInstanceSymbol(Scope declaredIn, ClassSymbol @class, Position position, Analyzer analyzer)
        : base(@class.GetFullName(), null, @class, declaredIn, position)
    {
        selfScope = new(ScopeType.Instance, DeclaredIn, this);
        selfScope.CopyFromClass(@class, analyzer);
    }

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
        : base(globalScope, floatClass ?? Native.Float32Class, position, analyzer)
    {
        Value = value;
        if (Value != null)
            ConstantPool.Add(new FloatConstant(Value.GetValueOrDefault()));
    }

    public override ClassSymbol ResolveCast(ClassSymbol castType, AsExpression expression)
    {
        if (castType is Int32ClassSymbol)
            return Native.Int32Class;
        
        return base.ResolveCast(castType, expression);
    }

    public override void CompileCastBytes(ClassSymbol castType, AsExpression expression, Compiler.Compiler compiler)
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

    public override void CompileBinaryOperation(BinaryExpression expression, Compiler.Compiler compiler)
    {
        var lintExpr = expression.Left;
        var rintExpr = expression.Right;
        
        compiler.CompileExpression(lintExpr);
        compiler.CompileExpression(rintExpr);
        compiler.Emit(operators[expression.Operator]);
    }

    public override byte[] GetPushBytes(IdentifierExpressionPart? idPart)
    {
        return [(byte)OpCode.PUSH_CONSTANT, .. BitConverter.GetBytes(ConstantPool.Add(new FloatConstant(Value 
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

    public Int32InstanceSymbol(Scope globalScope, ClassSymbol intClass, int? value, Position position, Analyzer analyzer) 
        : base(globalScope, intClass, position, analyzer)
    {
        Value = value;
        
        if (Value != null)
            ConstantPool.Add(new IntConstant(value.GetValueOrDefault()));
    }

    public override byte[] GetPushBytes(IdentifierExpressionPart? idPart)
    {
        return [(byte)OpCode.PUSH_CONSTANT, .. BitConverter.GetBytes(ConstantPool.Add(new IntConstant(Value 
            ?? throw new NullReferenceException("Value"))))];
    }

    public override ClassSymbol ResolveCast(ClassSymbol castType, AsExpression expression)
    {
        if (castType is Float32ClassSymbol)
            return Native.Float32Class;
        
        return base.ResolveCast(castType, expression);
    }

    public override void CompileCastBytes(ClassSymbol castType, AsExpression expression, Compiler.Compiler compiler)
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

    public override void CompileBinaryOperation(BinaryExpression expression, Compiler.Compiler compiler)
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

public class ClassSymbol : Symbol
{
    protected Scope ClassScope;
    public ClassSymbol? Derived;
    public override Scope Members => ClassScope;
    public readonly List<VarDeclStatement> VarDefines = new();

    private BasicConstructorSymbol? baseConstructor;
    public ConstructorFunctionSymbol? Constructor;
    private ConstructorFunctionSymbol? autoConstructor = null;

    public byte[] GetBasicConstructorCallBytes()
    {
        if (baseConstructor == null)
            throw new NullReferenceException();
        return [(byte)OpCode.CALL_METHOD, .. BitConverter.GetBytes(baseConstructor.Overload.ID), .. BitConverter.GetBytes(1)];
    }

    public ClassSymbol(string name, Scope declaredIn, Position position, ClassSymbol? derived) : 
        base(name, Native.GlobalScope.GetFreeClassID(), null, declaredIn, position)
    {
        ClassScope = new(ScopeType.Class, declaredIn);
        Derived = derived;
    }

    public virtual ClassInstanceSymbol GetInstance(Scope scope, Position position, Analyzer analyzer)
    {
        return new(scope, this, position, analyzer);
    }

    public (ClassInstanceSymbol, FunctionOverload) ResolveCall(
        FunctionParameter[] arguments, Analyzer analyzer, IdentifierExpressionPart part)
    {
        if (baseConstructor == null)
        {
            baseConstructor = new(this, analyzer);
        }
        if (Constructor == null && autoConstructor == null)
        {
            autoConstructor = new(this, true, analyzer, null);
            autoConstructor.AddOverload(new([new("self", Position, this)], Position, this, null, (_) => {}));
        }
        var constructor = Constructor ?? autoConstructor;
        return constructor!.ResolveCall(arguments, analyzer, part);
    }
}

public class ArrayClassSymbol : ClassSymbol
{
    public ArrayClassSymbol() : base("Array", Native.GlobalScope, default, Native.ObjectClass) { }

    public override ArrayInstanceSymbol GetInstance(Scope scope, Position position, Analyzer analyzer)
    {
        return new ArrayInstanceSymbol(scope, position, analyzer);
    }
}

public class ArrayInstanceSymbol : ClassInstanceSymbol
{
    public ArrayInstanceSymbol(Scope declaredIn, Position position, Analyzer analyzer) 
        : base(declaredIn, Native.ArrayClass, position, analyzer) 
    { }
}

public class Int32ClassSymbol : ClassSymbol
{
    public Int32ClassSymbol() : base("int32", Native.GlobalScope, default, Native.ValueObjectClass) { }

    public override Int32InstanceSymbol GetInstance(Scope scope, Position position, Analyzer analyzer)
    {
        return new Int32InstanceSymbol(scope, this, null, position, analyzer);
    }
}

public class Float32ClassSymbol : ClassSymbol
{
    public Float32ClassSymbol() : base("float32", Native.GlobalScope, default, Native.ValueObjectClass) { }

    public override Float32InstanceSymbol GetInstance(Scope scope, Position position, Analyzer analyzer)
    {
        return new Float32InstanceSymbol(scope, this, null, position, analyzer);
    }
}

public class BoolInstanceSymbol : ClassInstanceSymbol
{
    public bool? Value;

    public BoolInstanceSymbol(bool? value, Scope declaredIn, Position position, Analyzer analyzer) 
        : base(declaredIn, Native.BoolClass, position, analyzer)
    {
        Value = value;

        if (Value != null)
            ConstantPool.Add(new BoolConstant(Value.GetValueOrDefault()));
    }

    public override byte[] GetPushBytes(IdentifierExpressionPart? idPart)
    {
        return [(byte)OpCode.PUSH_CONSTANT, 
            ..BitConverter.GetBytes(ConstantPool.Add(new BoolConstant(Value ?? throw new ArgumentNullException())))];
    }
}

public class BoolClassSymbol : ClassSymbol
{
    public BoolClassSymbol() : base("bool", Native.GlobalScope, default, Native.ValueObjectClass) { }

    public override BoolInstanceSymbol GetInstance(Scope scope, Position position, Analyzer analyzer)
    {
        return new BoolInstanceSymbol(null, scope, position, analyzer);
    }
}

public class BasicConstructorSymbol : ConstructorFunctionSymbol
{
    public readonly FunctionOverload Overload;

    private void getOvBytes(Compiler.Compiler compiler)
    {
        for (int i = 0; i < Class.VarDefines.Count; i++)
        {
            var var = Class.VarDefines[i];
            if (var.Value == null) continue;
            compiler.CompileExpression(var.Value);
            compiler.Emit(OpCode.LOAD_LOCAL);
            compiler.Emit(0);
            compiler.Emit(OpCode.STORE_FIELD);
            compiler.Emit(var.Resolved.ID);
        }

        compiler.Emit(OpCode.LOAD_LOCAL);
        compiler.Emit(0);
        compiler.Emit(OpCode.RET);
    }
    public BasicConstructorSymbol(ClassSymbol klass, Analyzer analyzer) : base(klass, true, analyzer, null)
    {
        Overload = new([new("self", Class.Position, Class)], Class.Position, Class, null, getOvBytes);
        AddOverload(Overload);
        Overload.LocalsCount = 1; // take into account the 'self' parameter
        Class.Members.DefineFunc(this);
        if (!analyzer.Methods.ContainsKey(Class))
            analyzer.Methods.Add(Class, []);
        analyzer.Methods[Class].Add(Overload);
    }
}

public class ArrayConstructorFunctionSymbol : ConstructorFunctionSymbol
{
    public ArrayConstructorFunctionSymbol(Analyzer analyzer) : base(Native.ArrayClass, true, analyzer, null) {}
    

    // Make sure compiler has emited length before call
    public override byte[] GetBytesForCall(FunctionParameter[] arguments, IdentifierExpression id, IdentifierExpressionPart part)
    {        
        return [(byte)OpCode.ALLOCATE_ARR];
    }
}

public class ConstructorFunctionSymbol : MethodSymbol
{
    public readonly bool IsAutoGenerated;
    public readonly ClassSymbol Class;
    public ConstructorFunctionSymbol(ClassSymbol @class, bool isAutoGenerated, Analyzer analyzer, Position? position) 
        : base(@class.Name, @class.Members, position ?? default, analyzer)
    {
        Class = @class;
        IsAutoGenerated = isAutoGenerated;
    }

    public (ClassInstanceSymbol, FunctionOverload) ResolveCall(FunctionParameter[] arguments, Analyzer analyzer, 
        IdentifierExpressionPart part)
    {
        var instance = analyzer.GetInstanceSymbol(Class, Position);
        var overload = GetOverload([new("self", instance.Position, instance.Type) ,.. arguments], true, part.Position);
        return (instance, overload);
    }

    public override byte[] GetBytesForCall(FunctionParameter[] arguments, IdentifierExpression id, IdentifierExpressionPart part)
    {
        if (!IsAutoGenerated)
            return [(byte)OpCode.ALLOCATE_OBJ, .. BitConverter.GetBytes(Class.ID), .. Class.GetBasicConstructorCallBytes(), 
                .. base.GetBytesForCall(arguments, id, part)];
        else
            return [(byte)OpCode.ALLOCATE_OBJ, .. BitConverter.GetBytes(Class.ID), .. Class.GetBasicConstructorCallBytes()];
    }
}

public class MethodSymbol : FunctionSymbol
{
    public MethodSymbol(string name, Scope declaredIn, Position position, Analyzer analyzer) 
        : base(name, declaredIn, position, analyzer)
    { }

    public override byte[] GetBytesForCall(FunctionParameter[] arguments, IdentifierExpression id, IdentifierExpressionPart part)
    {
        if (part.ResolvedOverload == null)
            throw new ArgumentNullException(nameof(part.ResolvedOverload));

        var ov = part.ResolvedOverload;
        //var owner = part.Last();
        //if (owner == null)
        //    throw new NullReferenceException(nameof(owner));
        return [(byte)OpCode.CALL_METHOD, .. BitConverter.GetBytes(ov.ID), .. BitConverter.GetBytes(ov.Arity)];
    }

    public override ClassSymbol ResolveCall(FunctionParameter[] arguments, IdentifierExpression id, IdentifierExpressionPart part)
    {
        if (DeclaredIn.Self != null)
            return base.ResolveCall([new("self", DeclaredIn.Self!.Type.Position, DeclaredIn.Self.Type) ,.. arguments], id, part);
        else
            return base.ResolveCall(arguments, id, part);
    }

    public override FunctionOverload GetOverload(FunctionParameter[] parameters, bool throws, Position position = default)
    {
        if (DeclaredIn.Self != null)
            return base.GetOverload([new("self", DeclaredIn.Self.Type.Position, DeclaredIn.Self.Type) ,.. parameters], throws, position);
        else 
            return base.GetOverload(parameters, throws, position);
    }
}

public class FunctionSymbol : ClassInstanceSymbol
{
    public override ClassSymbol ResolveCall(FunctionParameter[] arguments, IdentifierExpression id, IdentifierExpressionPart part)
    {
        var ov = GetOverload(arguments, true, part.Position);
        return ov.ReturnType;
    }

    public override byte[] GetBytesForCall(FunctionParameter[] arguments, IdentifierExpression id, IdentifierExpressionPart part)
    {
        if (part.ResolvedOverload == null)
            throw new ArgumentNullException(nameof(part.ResolvedOverload));
        
        var ov = part.ResolvedOverload;

        byte[] ar = [(byte)((ov is BuiltInFunctionOverload) ? OpCode.SYS_CALL : OpCode.CALL), .. BitConverter.GetBytes(ov.ID), 
            .. BitConverter.GetBytes(ov.Arity)];
        return ar;
    }

    public FunctionSymbol(string name, Scope declaredIn, Position position, Analyzer analyzer) 
        : base(declaredIn, Native.FunctionClass, position, analyzer)
    {
        Name = name;
    }

    public readonly List<FunctionOverload> Overloads = new();

    public virtual FunctionOverload GetOverload(FunctionParameter[] parameters, bool throws, Position position = default)
    {
        if (!ContainsOverload(parameters, out var ov))
        {
            if (throws)
                throw new Error(
                    $"Function with name '{Name}' does not contains overloads with same parameters({string.Join(',', parameters)})", 
                        "Semantic", position);
            else return null!;
        }

        return ov!;
    }

    private bool ContainsOverload(FunctionParameter[] parameters, out FunctionOverload? matched) 
    {
        for (int i = 0; i < Overloads.Count; i++)
        {
            var candidate = Overloads[i];
            if (candidate.Arity != parameters.Length) continue;

            bool isAllMatched = true;

            for (int j = 0; j < candidate.Arity; j++)
            {
                var candParam = candidate.Parameters[j];
                var param = parameters[j];

                if (candParam.Type != param.Type) 
                {
                    isAllMatched = false;
                    break;
                }
            }

            if (isAllMatched) 
            {
                matched = candidate;
                return true;
            }
        }

        matched = null;

        return false;
    }

    private bool ContainsOverload(FunctionOverload ov) 
    {
        for (int i = 0; i < Overloads.Count; i++)
        {
            var candidate = Overloads[i];
            if (candidate.Arity != ov.Arity) continue;

            bool isAllMatched = true;

            for (int j = 0; j < candidate.Arity; j++)
            {
                var candParam = candidate.Parameters[j];
                var param = ov.Parameters[j];

                if (candParam.Type != param.Type) 
                {
                    isAllMatched = false;
                    break;
                }
            }

            if (isAllMatched) return true;
        }

        return false;
    }

    public void AddOverload(FunctionOverload ov)
    {
        ov.Parent = this;
        if (ov is not BuiltInFunctionOverload)
            if (ov.Parent is not ConstructorFunctionSymbol cf)
                ov.ID = DeclaredIn.GetFreeFuncID();
            else ov.ID = cf.Class.Members.GetFreeFuncID();

        if (ContainsOverload(ov)) 
            throw new Error($"Overload of function '{Name}' with same parameters({string.Join(", ", ov.Parameters)}) is already exist", 
                "Semantic",
                ov.Position);
        
        Overloads.Add(ov);
    }
}

public class FunctionParameter(string name, Position position, ClassSymbol type)
{

    public readonly string Name = name;
    public readonly Position Position = position;
    public readonly ClassSymbol Type = type;

    public override string ToString()
    {
        return $"{Name + (Name != "" ? " :" : "")} {Type.GetFullName()}";
    }
}


public class BuiltInFunctionOverload : FunctionOverload
{
    public BuiltInFunctionOverload(FunctionParameter[] @params, ClassSymbol returnType, int id)
        : base(@params, default, returnType, null, id, null)
    {
        
    }
}

public class FunctionOverload
{
    public FunctionOverload(FunctionParameter[] @params, Position position, ClassSymbol returnType, FuncDefineStatement? statement,
        Action<Compiler.Compiler>? funcBytes)
    {
        Position = position;
        Parameters = @params;
        ReturnType = returnType;
        Statement = statement;
        EmitBytes = funcBytes;
    }

    protected FunctionOverload(FunctionParameter[] @params, Position position, ClassSymbol returnType, FuncDefineStatement? statement, 
        int id, Action<Compiler.Compiler>? funcBytes)
    {
        Position = position;
        Parameters = @params;
        ReturnType = returnType;
        Statement = statement;
        ID = id;
        EmitBytes = funcBytes;
    }

    public int ID;
    public int LocalsCount;
    public readonly Position Position;
    public FunctionSymbol Parent = null!;
    public readonly FunctionParameter[] Parameters;
    public int Arity => Parameters.Length;
    public readonly ClassSymbol ReturnType;
    public readonly FuncDefineStatement? Statement;
    public readonly Action<Compiler.Compiler>? EmitBytes;
}

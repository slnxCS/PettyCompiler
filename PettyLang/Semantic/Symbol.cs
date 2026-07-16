using PettyLang.AST;
using PettyLang.Compiler;
using PettyLang.Errors;

namespace PettyLang.Semantic;

public abstract class Symbol
{
    protected Symbol(string name, int? id, ClassSymbol? type, Scope declaredIn, Position position)
    {
        Name = name;
        if (id is int _id)
            ID = _id;
        Type = type ?? (this as ClassSymbol)!;
        DeclaredIn = declaredIn;
        Position = position;
    }

    public readonly string Name;
    public int ID;
    public ClassSymbol Type;
    public virtual Scope? Members => null;
    public readonly Scope DeclaredIn;
    public readonly Position Position;

    public virtual void CompileBinaryOperation(BinaryExpression expression, Compiler.Compiler compiler, IByteWriter writer)
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

    public virtual void CompileCastBytes(ClassSymbol castType, AsExpression expression, Compiler.Compiler compiler, IByteWriter writer)
    {
        if (castType == Type)
            return;
        throw new NotSupportedCastTypeError(GetFullName(), castType.GetFullName(), expression.Position);
    }

    public virtual byte[] GetBytesForCall(FunctionParameter[] arguments, IdentifierExpression id, IdentifierExpressionPart part)
    {
        throw new NotCallableError(GetFullName(), part.Position);
    }
    
    public abstract byte[] GetPushBytes();

    public virtual string GetFullName()
    {
        return Name;
    }
}

public class VarSymbol : Symbol
{
    public ClassInstanceSymbol Value;

    public VarSymbol(string name, Position position, Scope declaredIn, ClassSymbol type, ClassInstanceSymbol value) : base(name, declaredIn.GetFreeVarID(), type, declaredIn, position)
    {
        Value = value;
    }

    public bool IsGlobal => DeclaredIn.Type == ScopeType.Global;

    public byte[] GetStoreBytes()
    {
        return [(byte)(IsGlobal ? OpCode.STORE_GLOBAL : OpCode.STORE_LOCAL), .. BitConverter.GetBytes(ID)];
    }

    public override byte[] GetPushBytes()
    {
        return [(byte)(IsGlobal ? OpCode.LOAD_GLOBAL : OpCode.LOAD_LOCAL), .. BitConverter.GetBytes(ID)];
    }

    public override void CompileBinaryOperation(BinaryExpression expression, Compiler.Compiler compiler, IByteWriter writer)
    {
        Value.CompileBinaryOperation(expression, compiler, writer);
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

    public override void CompileCastBytes(ClassSymbol castType, AsExpression expression, Compiler.Compiler compiler, IByteWriter writer)
    {
        Value.CompileCastBytes(castType, expression, compiler, writer);
    }
}

public class ClassInstanceSymbol : Symbol
{
    public ClassInstanceSymbol(Scope declaredIn, ClassSymbol @class, Position position, string? name) : base(name ?? @class.GetFullName(), null, @class, declaredIn, position)
    {
        selfScope = new(ScopeType.Instance, DeclaredIn);
    }

    private Scope selfScope;
    public override Scope Members => selfScope;

    public override byte[] GetPushBytes()
    {
        throw new NotImplementedException();
    }
}

public class Float32InstanceSymbol : ClassInstanceSymbol
{
    public float? Value;
    public bool IsConstant => Value != null;

    public Float32InstanceSymbol(Scope globalScope, ClassSymbol? floatClass, float? value, Position position) : base(globalScope, floatClass ?? BuiltIn.Float32Class, position, null)
    {
        Value = value;
    }

    public override ClassSymbol ResolveCast(ClassSymbol castType, AsExpression expression)
    {
        if (castType is Int32ClassSymbol)
            return BuiltIn.Int32Class;
        
        return base.ResolveCast(castType, expression);
    }

    public override void CompileCastBytes(ClassSymbol castType, AsExpression expression, Compiler.Compiler compiler, IByteWriter writer)
    {
        if (castType is Int32ClassSymbol)
        {
            compiler.CompileExpression(expression.Value, writer);
            writer.Emit(OpCode.CAST_FROM_FLOAT32_TO_INT32);
            return;
        }
        base.CompileCastBytes(castType, expression, compiler, writer);
    }

    static Dictionary<string, OpCode> operators = new()
    {
        {"+", OpCode.ADD_FLOAT},
        {"-", OpCode.SUB_FLOAT},
        {"*", OpCode.MUL_FLOAT},
        {"/", OpCode.DIV_FLOAT},
    };

    public override void CompileBinaryOperation(BinaryExpression expression, Compiler.Compiler compiler, IByteWriter writer)
    {
        var lintExpr = expression.Left as FloatExpression;
        var rintExpr = expression.Right as FloatExpression;

        if (lintExpr == null || rintExpr == null)
            throw new NotSupportedOperandsError(GetFullName(), expression.RightSymbol.GetFullName(), expression.Operator, expression.Position);
        
        compiler.CompileExpression(lintExpr, writer);
        compiler.CompileExpression(rintExpr, writer);
        writer.Emit(operators[expression.Operator]);
    }

    public override byte[] GetPushBytes()
    {
        return [(byte)OpCode.PUSH_CONSTANT, .. BitConverter.GetBytes(ConstantPool.Add(new FloatConstant(Value ?? throw new NullReferenceException("Value"))))];
    }

    public override ClassSymbol VisitBinary(ClassInstanceSymbol right, BinaryExpression expression)
    {
        if (right is not Float32InstanceSymbol)
            throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), expression.Operator, expression.Position);
        
        return expression.Operator switch
        {
            "+" => BuiltIn.Float32Class,
            "-" => BuiltIn.Float32Class,
            "*" => BuiltIn.Float32Class,
            "/" => BuiltIn.Float32Class,
            _ =>  throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), expression.Operator, expression.Position)
        };
    }
}

public class Int32InstanceSymbol : ClassInstanceSymbol
{
    public int? Value;
    public bool IsConstant => Value != null;

    public Int32InstanceSymbol(Scope globalScope, ClassSymbol intClass, int? value, Position position) : base(globalScope, intClass, position, null)
    {
        Value = value;
    }

    public override byte[] GetPushBytes()
    {
        return [(byte)OpCode.PUSH_CONSTANT, .. BitConverter.GetBytes(ConstantPool.Add(new IntConstant(Value ?? throw new NullReferenceException("Value"))))];
    }

    static Dictionary<string, OpCode> operators = new()
    {
        {"+", OpCode.ADD_INT},
        {"-", OpCode.SUB_INT},
        {"*", OpCode.MUL_INT},
        {"/", OpCode.DIV_INT},
    };

    public override void CompileBinaryOperation(BinaryExpression expression, Compiler.Compiler compiler, IByteWriter writer)
    {
        var lintExpr = expression.Left;
        var rintExpr = expression.Right;
        
        compiler.CompileExpression(lintExpr, writer);
        compiler.CompileExpression(rintExpr, writer);
        writer.Emit(operators[expression.Operator]);
    }

    public override ClassSymbol VisitBinary(ClassInstanceSymbol right, BinaryExpression expression)
    {
        if (right is not Int32InstanceSymbol)
            throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), expression.Operator, expression.Position);
        
        return expression.Operator switch
        {
            "+" => BuiltIn.Int32Class,
            "-" => BuiltIn.Int32Class,
            "*" => BuiltIn.Int32Class,
            "/" => BuiltIn.Float32Class,
          _ =>  throw new NotSupportedOperandsError(GetFullName(), right.GetFullName(), expression.Operator, expression.Position)
        };
    }
}

public class ClassSymbol : Symbol
{
    public ClassSymbol(string name, Scope declaredIn, Position position) : base(name, declaredIn.GetFreeClassID(), null, declaredIn, position)
    {
        ClassScope = new(ScopeType.Class, declaredIn);
    }

    protected Scope ClassScope;
    public override Scope Members => ClassScope;

    public override byte[] GetPushBytes()
    {
        return [];
    }

    public virtual ClassInstanceSymbol GetInstance(Scope scope, Position position)
    {
        return new(scope, this, position, null);
    }
}

public class Int32ClassSymbol : ClassSymbol
{
    public Int32ClassSymbol() : base("int32", BuiltIn.GlobalScope, default) { }

    public override Int32InstanceSymbol GetInstance(Scope scope, Position position)
    {
        return new Int32InstanceSymbol(scope, this, null, position);
    }
}

public class Float32ClassSymbol : ClassSymbol
{
    public Float32ClassSymbol() : base("float32", BuiltIn.GlobalScope, default) { }

    public override Float32InstanceSymbol GetInstance(Scope scope, Position position)
    {
        return new Float32InstanceSymbol(scope, this, null, position);
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

        byte[] ar = [(byte)((ov is BuiltInFunctionOverload) ? OpCode.SYS_CALL : OpCode.CALL), .. BitConverter.GetBytes(ov.ID), .. BitConverter.GetBytes(ov.Arity)];        
        return ar;
    }

    public FunctionSymbol(string name, Scope declaredIn) : base(declaredIn, BuiltIn.FunctionClass, default, name)
    {

    }

    private readonly List<FunctionOverload> Overloads = new();

    public FunctionOverload GetOverload(FunctionParameter[] parameters, bool throws, Position position = default)
    {
        if (!ContainsOverload(parameters, out var ov))
        {
            if (throws)
                throw new Error(
                    $"Function with name '{Name}' does not contains overloads with same parameters({new string(string.Join(',', parameters).Skip(1).ToArray())})", "Semantic",
                        position);
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
            ov.ID = DeclaredIn.GetFreeFuncID();

        if (ContainsOverload(ov)) 
            throw new Error($"Overload of function '{Name}' with same parameters({new string(string.Join(", ", ov.Parameters).ToArray())}) is already exist", 
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
        : base(@params, default, returnType, null, id)
    {
        
    }
}

public class FunctionOverload
{
    public FunctionOverload(FunctionParameter[] @params, Position position, ClassSymbol returnType, FuncDefineStatement? statement)
    {
        Position = position;
        Parameters = @params;
        ReturnType = returnType;
        Statement = statement;
    }

    protected FunctionOverload(FunctionParameter[] @params, Position position, ClassSymbol returnType, FuncDefineStatement? statement, int id)
    {
        Position = position;
        Parameters = @params;
        ReturnType = returnType;
        Statement = statement;
        ID = id;
    }

    public int ID;
    public int LocalsCount;
    public readonly Position Position;
    public FunctionSymbol Parent = null!;
    public readonly FunctionParameter[] Parameters;
    public int Arity => Parameters.Length;
    public readonly ClassSymbol ReturnType;
    public readonly FuncDefineStatement? Statement;
}
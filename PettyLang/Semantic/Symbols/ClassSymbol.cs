namespace PettyLang.Semantic;

using PettyLang.AST;
using PettyLang.Compiler;
using PettyLang.Errors;

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

    public readonly Native Native;

    public ClassSymbol(string name, Scope declaredIn, Position position, ClassSymbol? derived, Native native) : 
        base(name, native.GlobalScope.GetFreeClassID(), null, declaredIn, position)
    {
        Native = native;
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

    public virtual ClassInstanceSymbol ResolveDefaultValue(Analyzer analyzer, Position position, Scope scope)
    {
        throw new Error($"Type '{Type.GetFullName()}' does not contain a base value", "Semantic", position);
    }

    public virtual byte[] GetDefaultValueBytes(Compiler compiler, Position position)
    {
        throw new Error($"Type '{Type.GetFullName()}' does not contain a base value", "Semantic", position);
    }
}

public class ArrayClassSymbol : ClassSymbol
{
    public ArrayClassSymbol(Native native) 
        : base("Array", native.GlobalScope, default, native.ObjectClass, native) { }

    public override ArrayInstanceSymbol GetInstance(Scope scope, Position position, Analyzer analyzer)
    {
        return new ArrayInstanceSymbol(scope, position, analyzer);
    }

    public override ArrayInstanceSymbol ResolveDefaultValue(Analyzer analyzer, Position position, Scope scope)
    {
        return new ArrayInstanceSymbol(scope, position, analyzer);
    }

    public override byte[] GetDefaultValueBytes(Compiler compiler, Position position)
    {
        return [.. BitConverter.GetBytes(0), (byte)OpCode.ALLOCATE_ARR];
    }
}

public class Int32ClassSymbol : ClassSymbol
{
    public Int32ClassSymbol(Native native) 
        : base("int32", native.GlobalScope, default, native.ValueObjectClass, native) { }

    public override Int32InstanceSymbol GetInstance(Scope scope, Position position, Analyzer analyzer)
    {
        return new Int32InstanceSymbol(scope, this, null, position, analyzer);
    }

    public override Int32InstanceSymbol ResolveDefaultValue(Analyzer analyzer, Position position, Scope scope)
    {
        return new Int32InstanceSymbol(scope, analyzer.Native.Int32Class, 0, position, analyzer);
    }

    public override byte[] GetDefaultValueBytes(Compiler compiler, Position position)
    {
        return [(byte)OpCode.PUSH_CONSTANT, .. BitConverter.GetBytes(compiler.Analyzer.ConstantPool.Add(new IntConstant(0)))];
    }
}

public class Float32ClassSymbol : ClassSymbol
{
    public Float32ClassSymbol(Native native) 
        : base("float32", native.GlobalScope, default, native.ValueObjectClass, native) { }

    public override Float32InstanceSymbol GetInstance(Scope scope, Position position, Analyzer analyzer)
    {
        return new Float32InstanceSymbol(scope, this, null, position, analyzer);
    }

    public override Float32InstanceSymbol ResolveDefaultValue(Analyzer analyzer, Position position, Scope scope)
    {
        return new Float32InstanceSymbol(scope, analyzer.Native.Float32Class, 0f, position, analyzer);
    }

    public override byte[] GetDefaultValueBytes(Compiler compiler, Position position)
    {
        return [(byte)OpCode.PUSH_CONSTANT, .. BitConverter.GetBytes(compiler.Analyzer.ConstantPool.Add(new FloatConstant(0f)))];
    }
}

public class BoolClassSymbol : ClassSymbol
{
    public BoolClassSymbol(Native native) 
        : base("bool", native.GlobalScope, default, native.ValueObjectClass, native) { }

    public override BoolInstanceSymbol GetInstance(Scope scope, Position position, Analyzer analyzer)
    {
        return new BoolInstanceSymbol(null, scope, position, analyzer);
    }

    public override BoolInstanceSymbol ResolveDefaultValue(Analyzer analyzer, Position position, Scope scope)
    {
        return new BoolInstanceSymbol(false, scope, position, analyzer);
    }

    public override byte[] GetDefaultValueBytes(Compiler compiler, Position position)
    {
        return [(byte)OpCode.PUSH_CONSTANT, .. BitConverter.GetBytes(compiler.Analyzer.ConstantPool.Add(new BoolConstant(false)))];
    }
}
using PettyLang.AST;
using PettyLang.Semantic;

namespace PettyLang;

public class Native {

    private class BuiltinFunction
    {
        public readonly FunctionSymbol Function;
        public BuiltInFunctionOverload[] Overloads;

        public BuiltinFunction(string name, BuiltInFunctionOverload[] overloads, Analyzer analyzer, Native native, Scope declaredIn)
        {
            Function = new(name, declaredIn, default, analyzer, native);
            Overloads = overloads;
        }

        public void AddOverloads()
        {
            foreach (var overload in Overloads)
            {
                Function.AddOverload(overload);
            }
        }

        public void Define(Scope scope)
        {
            scope.DefineFunc(Function);
        }
    }

    private BuiltinFunction[] BuiltinFunctions;

    public bool Inited {get; private set; } = false;

    public ClassSymbol VoidClass, ObjectClass, ValueObjectClass, FunctionClass;
    public Int32ClassSymbol Int32Class;
    public Float32ClassSymbol Float32Class;
    public BoolClassSymbol BoolClass;
    public ArrayClassSymbol ArrayClass;
    public ClassSymbol? StringClass = null;
    public readonly Scope GlobalScope = new(ScopeType.Global, null);

    public Native(Analyzer analyzer)
    {
        ObjectClass = new("Object", GlobalScope, default, null, this);
        ValueObjectClass = new("ValueObject", GlobalScope, default, ObjectClass, this);
        VoidClass = new("void", GlobalScope, default, ObjectClass, this);

        ArrayClass = new(this);
        Int32Class = new(this);
        Float32Class = new(this);
        BoolClass = new(this);
        FunctionClass = new("function", GlobalScope, default, ObjectClass, this);

        BuiltinFunctions =
        [
            new(
                "print", 
                [
                    new([new("number", default, Int32Class)], VoidClass, 0), 
                    new([new("number", default, Float32Class)], VoidClass, 1),
                    new([new("value", default, BoolClass)], VoidClass, 2)
                ], analyzer, this, GlobalScope
            ),

            new(
                "read", 
                [
                    new([], Int32Class, 3)
                ], analyzer, this, GlobalScope
            ),

            new(
                "random",
                [
                    new([new("min", default, Int32Class), new("max", default, Int32Class)], Int32Class, 4),
                ], analyzer, this, GlobalScope
            ),

            new (
                "get", [
                    new([new("array", default, ArrayClass), new("index", default, Int32Class)], Int32Class, 5),
                ], analyzer, this, GlobalScope
            ),

            new (
                "set", [
                    new([new("array", default, ArrayClass), new("index", default, Int32Class), new("num", default, Int32Class)], 
                        VoidClass, 6),
                ], analyzer, this, GlobalScope
            ),
        ];

        Define(analyzer);
    }

    private void DefineArray(Analyzer analyzer)
    {
        ArrayClass.Constructor = new ArrayConstructorFunctionSymbol(analyzer, this);
        ArrayClass.Constructor.AddOverload(new([new("self", default, ArrayClass), new("Length", default, Int32Class)], default, 
            VoidClass, null, 
        compiler =>
        {
            
        }));
    }

    private void Define(Analyzer analyzer)
    {
        GlobalScope.DefineClass(ObjectClass);
        GlobalScope.DefineClass(ValueObjectClass);
        GlobalScope.DefineClass(Int32Class);
        GlobalScope.DefineClass(Float32Class);
        GlobalScope.DefineClass(VoidClass);
        GlobalScope.DefineClass(FunctionClass);
        GlobalScope.DefineClass(BoolClass);
        GlobalScope.DefineClass(ArrayClass);
        DefineArray(analyzer);

        foreach (var bf in BuiltinFunctions)
        {
            bf.AddOverloads();
            bf.Define(GlobalScope);
        }
    }
}
using PettyLang.AST;
using PettyLang.Semantic;

namespace PettyLang;

public static class BuiltIn {

    private class BuiltinFunction
    {
        public readonly FunctionSymbol Function;
        public BuiltInFunctionOverload[] Overloads;

        public BuiltinFunction(string name, BuiltInFunctionOverload[] overloads, Scope? declaredIn = null)
        {
            Function = new(name, declaredIn ?? GlobalScope);
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

    private static BuiltinFunction[] BuiltinFunctions = null!;

    public static bool inited {get; private set; } = false;

    public static ClassSymbol VoidClass = null!, ObjectClass = null!, ValueObjectClass = null!, FunctionClass = null!;
    public static Int32ClassSymbol Int32Class = null!;
    public static Float32ClassSymbol Float32Class = null!;
    public static BoolClassSymbol BoolClass = null!;
    public static ClassSymbol? StringClass = null;
    public static readonly Scope GlobalScope = new(ScopeType.Global, null);

    public static void Init()
    {
        if (inited) return;
        else inited = true;

        ObjectClass = new("Object", GlobalScope, default, null);
        ValueObjectClass = new("ValueObject", GlobalScope, default, ObjectClass);
        VoidClass = new("void", GlobalScope, default, ObjectClass);

        Int32Class = new();
        Float32Class = new();
        BoolClass = new();
        FunctionClass = new("function", GlobalScope, default, ObjectClass);

        BuiltinFunctions =
        [
            new(
                "print", 
                [
                    new([new("number", default, Int32Class)], VoidClass, 0), 
                    new([new("number", default, Float32Class)], VoidClass, 1),
                    new([new("value", default, BoolClass)], VoidClass, 2)
                ]
            ),

            new(
                "read", 
                [
                    new([], Int32Class, 3)
                ]
            ),
        ];

        Define();
    }

    public static void Define()
    {
        GlobalScope.DefineClass(Int32Class);
        GlobalScope.DefineClass(Float32Class);
        GlobalScope.DefineClass(VoidClass);
        GlobalScope.DefineClass(FunctionClass);
        GlobalScope.DefineClass(ObjectClass);
        GlobalScope.DefineClass(ValueObjectClass);
        GlobalScope.DefineClass(BoolClass);
        
        if (StringClass != null) GlobalScope.DefineClass(StringClass);

        foreach (var bf in BuiltinFunctions)
        {
            bf.AddOverloads();
            bf.Define(GlobalScope);
        }
    }
}
using PettyLang.Errors;

namespace PettyLang.Semantic;

public enum ScopeType
{
    Global, Function, Class, Instance, Package, Local,
}

public class Scope
{
    public Scope(ScopeType type, Scope? parent, ClassInstanceSymbol? self = null)
    {
        Parent = parent;
        Type = type;
        Self = self;
        if (Type == ScopeType.Instance && Self == null)
            throw new ArgumentNullException("self");

        freeIDSet = Type switch
        {
            ScopeType.Global => GlobalFreeIDSet,
            ScopeType.Package or ScopeType.Function or ScopeType.Instance or ScopeType.Class => new(),
            ScopeType.Local => Parent!.freeIDSet,
            _ => new(),
        };
    }

    public readonly Scope? Parent;
    public readonly ScopeType Type;
    public readonly ClassInstanceSymbol? Self;

    private Dictionary<string, VarSymbol> variables = new();
    public IReadOnlyDictionary<string, VarSymbol> Variables => variables;
    private Dictionary<string, FunctionSymbol> functions = new();
    public IReadOnlyDictionary<string, FunctionSymbol> Functions => functions;
    private Dictionary<string, ClassSymbol> classes = new();
    public IReadOnlyDictionary<string, ClassSymbol> Classes => classes;
    
    public class IDSet
    {
        public int FreeVarID = 0;
        public int FreeFuncID = 0;
        public int FreeClassID = 0;
    }

    private static IDSet GlobalFreeIDSet = new();
    private IDSet freeIDSet;

    public int GetFreeVarID()
    {
        return freeIDSet.FreeVarID++;
    }

    public int GetFreeFuncID()
    {
        if (Type == ScopeType.Local || Type == ScopeType.Function || Type == ScopeType.Package)
            return GlobalFreeIDSet.FreeFuncID++;
        return freeIDSet.FreeFuncID++;
    }

    public int GetFreeClassID()
    {
        return freeIDSet.FreeClassID++;
    }

    public void CopyFromClass(ClassSymbol @class)
    {
        if (Type != ScopeType.Instance || Self == null) 
            throw new InvalidOperationException();

        var classScope = @class.Members;

        foreach (var classFunction in classScope.Functions.Values)
        {
            var method = new MethodSymbol(classFunction.Name, this);
            foreach (var overload in classFunction.Overloads)
            {
                method.AddOverload(overload);
            }
            DefineFunc(method);
        }
        classes = new(classScope.classes);

        foreach (var var in Analyzer.Current.ResolveFieldsDecl(@class.VarDefines, Self))
        {
            DefineVar(var);
        }
    }

    public VarSymbol? GetVar(string name, bool local)
    {
        if (variables.TryGetValue(name, out var res)) return res;
        if (Parent != null && !local) return Parent.GetVar(name, false);
        return null;
    }

    public void DefineVar(VarSymbol var)
    {
        if (GetVar(var.Name!, true) != null) 
            throw new Error($"Variable '{var.Name}' is already exist", "Semantic", var.Position);

        variables.Add(var.Name, var);
    }

    public FunctionSymbol? GetFunc(string name, bool local)
    {
        if (functions.TryGetValue(name, out var res)) return res;
        if (Parent != null && !local) return Parent.GetFunc(name, false);
        return null;
    }

    public void DefineFunc(FunctionSymbol func)
    {
        if (GetFunc(func.Name!, true) != null)
            throw new Error($"Function '{func.Name}' is already exist", "Semantic", func.Position);
        functions.Add(func.Name, func);
    }

    public ClassSymbol? GetClass(string name, bool local)
    {
        if (classes.TryGetValue(name, out var res)) return res;
        if (Parent != null && !local) return Parent.GetClass(name, false);
        return null;
    }

    public void DefineClass(ClassSymbol @class)
    {
        if (GetClass(@class.Name!, true) != null)
            throw new Error($"Class '{@class.Name}' is already exist in current scope", "Semantic", @class.Position);
        
        classes.Add(@class.Name!, @class);
    }
}

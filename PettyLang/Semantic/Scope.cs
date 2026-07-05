using PettyLang.Errors;

namespace PettyLang.Semantic;

public enum ScopeType
{
    Global, Function, Class, Instance, Package, Local,
}

public class Scope
{
    public Scope(ScopeType type, Scope? parent)
    {
        Parent = parent;
        Type = type;
    }

    public readonly Scope? Parent;
    public readonly ScopeType Type;

    private Dictionary<string, VarSymbol> variables = new();
    private Dictionary<string, FunctionSymbol> functions = new();
    private Dictionary<string, ClassSymbol> classes = new();
    
    private static ulong globalFreeID = 0;

    public ulong GetFreeID()
    {
        return globalFreeID++;
    }

    public VarSymbol? GetVar(string name, bool local)
    {
        if (variables.TryGetValue(name, out var res)) return res;
        if (Parent != null && !local) return Parent.GetVar(name, false);
        return null;
    }

    public void DefineVar(VarSymbol var)
    {
        if (GetVar(var.Name, true) != null) 
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
        if (GetFunc(func.Name, true) != null)
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
        if (GetClass(@class.Name, true) != null)
            throw new Error($"Class '{@class.Name}' is already exist in current scope", "Semantic", @class.Position);
        
        classes.Add(@class.Name, @class);
    }
}

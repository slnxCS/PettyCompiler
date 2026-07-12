using System.Security.Cryptography.X509Certificates;
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

        if (Type == ScopeType.Global || Parent == null) freeIDSet = GlobalFreeIDSet;
        else if (Parent.Type == ScopeType.Global) freeIDSet = new();
        else freeIDSet = Parent.freeIDSet;
    }

    public readonly Scope? Parent;
    public readonly ScopeType Type;

    private Dictionary<string, VarSymbol> variables = new();
    private Dictionary<string, FunctionSymbol> functions = new();
    private Dictionary<string, ClassSymbol> classes = new();
    
    public class IDSet
    {
        public int FreeVarID;
        public int FreeFuncID;
        public int FreeClassID;
    }

    public static IDSet GlobalFreeIDSet = new();
    private IDSet freeIDSet;

    public int GetFreeVarID()
    {
        return freeIDSet.FreeVarID++;
    }

    public int GetFreeFuncID()
    {
        return GlobalFreeIDSet.FreeFuncID++;
    }

    public int GetFreeClassID()
    {
        return freeIDSet.FreeClassID++;
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

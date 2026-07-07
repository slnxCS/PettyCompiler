using PettyLang.Errors;

namespace PettyLang.Semantic;

public abstract class Symbol
{
    protected Symbol(string name, Position position, Scope declaredIn, ClassSymbol type, int? id = null)
    {        
        Name = name;
        Position = position;
        DeclaredIn = declaredIn;
        ID = id ?? declaredIn.GetFreeID();
        Type = type;
    }

    public readonly string Name;
    public readonly Position Position;
    public readonly Scope DeclaredIn;
    public readonly int ID;
    public ClassSymbol Type;

    public bool IsGlobal => DeclaredIn.Type == ScopeType.Global;

    public virtual string GetFullName()
    {
        return Name;
    }
}

public class ClassSymbol(string name, Position position, Scope declaredIn) : Symbol(name, position, declaredIn, BuiltIn.ObjectClass)
{
    public readonly Scope Members = /*null!*/ new(ScopeType.Class, declaredIn);
}

public class VarSymbol(string name, Position position, Scope declaredIn, ClassSymbol type) : Symbol(name, position, declaredIn, type)
{
}

public class BuiltInFunctionSymbol(string name, Scope declaredIn, int index) : FunctionSymbol(name, declaredIn, index)
{
}

public class FunctionSymbol : Symbol
{
    public FunctionSymbol(string name, Scope declaredIn) : base(name, default, declaredIn, BuiltIn.FunctionClass)
    {
        
    }

    protected FunctionSymbol(string name, Scope declaredIn, int builtin_index) : base(name, default, declaredIn, BuiltIn.FunctionClass, builtin_index)
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
        if (Overloads.Count == 0) 
        {
            Overloads.Add(ov);
            return;
        }

        if (ContainsOverload(ov)) 
            throw new Error($"Overload of function '{Name}' with same parameters({new string(string.Join(',', ov.Parameters).Skip(1).ToArray())}) is already exist", "Semantic",
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
        return $"{Name + (Name != "" ? ":" : "")} {Type.GetFullName()}";
    }
}

public class FunctionOverload(FunctionParameter[] @params, Position position, ClassSymbol returnType)
{
    public readonly Position Position = position;
    public FunctionSymbol Parent = null!;
    public readonly FunctionParameter[] Parameters = @params;
    public int Arity => Parameters.Length;
    public readonly ClassSymbol ReturnType = returnType;
}
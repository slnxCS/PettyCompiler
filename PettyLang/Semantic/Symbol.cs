using PettyLang.AST;
using PettyLang.Errors;

namespace PettyLang.Semantic;

public abstract class Symbol
{
    protected Symbol(string name, Position position, Scope declaredIn, ClassSymbol? type, int? id = null)
    {        
        Name = name;
        Position = position;
        DeclaredIn = declaredIn;
        ID = id ?? this switch
        {
            VarSymbol => declaredIn.GetFreeVarID(),
            FunctionSymbol => -1,
            ClassSymbol => declaredIn.GetFreeClassID(),
            _ => throw new NotImplementedException(GetType().Name)
        };
        Type = type ?? (this is ClassSymbol c ? c : BuiltIn.ObjectClass);
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

public class ClassSymbol(string name, Position position, Scope declaredIn) : Symbol(name, position, declaredIn, null)
{
    public readonly Scope Members = /*null!*/ new(ScopeType.Class, declaredIn);
}

public class VarSymbol(string name, Position position, Scope declaredIn, ClassSymbol type) : Symbol(name, position, declaredIn, type)
{
    public bool isArg = false;
}

public class FunctionSymbol : Symbol
{
    public FunctionSymbol(string name, Scope declaredIn) : base(name, default, declaredIn, BuiltIn.FunctionClass)
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
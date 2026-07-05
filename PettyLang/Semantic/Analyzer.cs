using PettyLang.AST;
using PettyLang.Errors;

namespace PettyLang.Semantic;

public static class BuiltIn {
    private static bool inited = false;

    public static ClassSymbol Int32Class = null!, Float32Class = null!, VoidClass = null!, ObjectClass = null!, FunctionClass = null!;
    public static ClassSymbol? StringClass = null;
    public static readonly Scope GlobalScope = new(ScopeType.Global, null);

    public static void Init()
    {
        if (inited) return;
        else inited = true;

        ObjectClass = new("Object", default, GlobalScope);

        Int32Class = new("int32", default, GlobalScope);
        Float32Class = new("float32", default, GlobalScope);
        VoidClass = new("void", default, GlobalScope);
        FunctionClass = new("function", default, GlobalScope);


        StringClass = GlobalScope.GetClass("String", true);
        Define();
    }

    static FunctionParameter[] createParams(params (string, ClassSymbol)[] parameters)
    {
        FunctionParameter[] @params = new FunctionParameter[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            @params[i] = new(param.Item1, default, param.Item2);
        }
        return @params;
    }

    static void AddOverload(FunctionSymbol func, FunctionParameter[] @params, ClassSymbol returnType)
    {
        func.AddOverload(new(@params, default, returnType));
    }

    public static void Define()
    {
        GlobalScope.DefineClass(Int32Class);
        GlobalScope.DefineClass(Float32Class);
        GlobalScope.DefineClass(VoidClass);
        if (StringClass != null) GlobalScope.DefineClass(StringClass);

        var printFunc = new FunctionSymbol("print", GlobalScope);
        GlobalScope.DefineFunc(printFunc);
        AddOverload(printFunc, createParams(("text", StringClass!)), VoidClass);
        AddOverload(printFunc, createParams(("num", Int32Class)), VoidClass);
    }
}

public class Analyzer
{
    private Scope currentScope = BuiltIn.GlobalScope;

    public Analyzer()
    {
        BuiltIn.Init();
    }

    public void Analyze(Statement[] AST)
    {
        foreach (var statement in AST)
        {
            VisitStatement(statement);
        }
    }

    void VisitStatement(Statement statement)
    {
        switch (statement)
        {
            case StatementExpression expr :
            {
                VisitStatementExpression(expr);
                break;
            }

            case VarDeclStatement varDecl :
            {
                VisitVarDef(varDecl);
                break;
            }

            default : throw new NotImplementedException($"{statement}");
        }
    }

    FunctionParameter[] ResolveParams(FuncParameter[] parameters)
    {
        var @params = new FunctionParameter[parameters.Length];
        for (int i = 0; i < @params.Length; i++)
        {
            @params[i] = new(parameters[i].Name, parameters[i].Position, GetSymType(ResolveExpression(parameters[i].Type)));
        }

        return @params;
    }

    FunctionOverload ResolveFuncDecl(FuncDefineStatement func)
    {
        var fs = currentScope.GetFunc(func.Name, false);
        if (fs == null) fs = new (func.Name, currentScope);

        var ov = new FunctionOverload(ResolveParams(func.Parameters), func.Position, func.ReturnType == null? BuiltIn.VoidClass : 
                GetSymType(ResolveExpression(func.ReturnType)));

        fs.AddOverload(ov);
        return ov;
    }

    void VisitVarDef(VarDeclStatement varDecl)
    {
        var resolved = ResolveExpression(varDecl.Value);
        var resolvedType = GetSymType(resolved);
        var type = varDecl.Type == null ? resolvedType : GetSymType(varDecl.Type);
        if (varDecl.Type != null)
        {
            if (type != resolvedType) 
                throw new Error($"Cannot convert from '{resolvedType.GetFullName()}' to '{type.GetFullName()}'", "Semantic", resolvedType.Position);
        }
        if (type == BuiltIn.VoidClass) 
            throw new Error($"cannot assign a value of type void to a variable", $"Semantic", varDecl.Value.Position);
        varDecl.Resolved = new(varDecl.Name, varDecl.Position, currentScope, type);
        currentScope.DefineVar(varDecl.Resolved);
    }

    void VisitStatementExpression(StatementExpression expr) => ResolveExpression(expr.Expression);

    Symbol ResolveExpression(Expression expr)
    {
        switch (expr)
        {
            case IntExpression : return BuiltIn.Int32Class;
            case FloatExpression : return BuiltIn.Float32Class;
            case StringExpression : return BuiltIn.StringClass ?? throw new NotImplementedException("string");
            case IdentifierExpression id : return ResolveIdentifierExpression(id);
            case IdentifierExpressionPart idPart : return ResolveIdentifierPart(idPart, currentScope, false, null);
            case BinaryExpression bin : return ResolveBin(bin);
            default : throw new NotImplementedException($"{expr}");
        }
    }

    Symbol ResolveBin(BinaryExpression bin)
    {
        var left = ResolveExpression(bin.Left);
        var right = ResolveExpression(bin.Right);

        if (left.Type != right.Type)
            throw new NotImplementedException();
        bin.LeftSymbol = left;
        bin.RightSymbol = right;
        return left;
    }

    ClassSymbol GetSymType(Symbol s)
    {
        return s is ClassSymbol cl ? cl : (s as VarSymbol)!.Type;
    }

    ClassSymbol GetSymType(IdentifierExpression id)
    {
        var res = ResolveIdentifierExpression(id);
        if (res is not ClassSymbol cl) 
            throw new Error($"Cannot use '{res.GetFullName()}' as type", "Semantic", id.Position);
        return cl;
    }

    FunctionParameter[] ResolveArgs(Expression[] args)
    {
        var res_args = new FunctionParameter[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            var resolved = ResolveExpression(args[i]);
            var type = GetSymType(resolved);
            res_args[i] = new("", args[i].Position, type);
        }
        return res_args;
    }

    Symbol ResolveIdentifierPart(IdentifierExpressionPart part, Scope lookingScope, bool local, Symbol? lastSym)
    {
        var errorMsg = lastSym == null ? $"Name '{part.ID}' does not exist in current context"
            : $"Class '{lastSym.GetFullName()}' does not contains field with name '{part.ID}'";
        if (part.FuncCallsArguments.Length == 0)
        {
            var _v = lookingScope.GetVar(part.ID, local);
            if (_v != null) return _v;
            return lookingScope.GetClass(part.ID, local) ?? throw new Error(errorMsg, "Semantic", part.Position);
        }
        else
        {
            var sym = lookingScope.GetFunc(part.ID, local);
            if (sym == null) 
                throw new Error(errorMsg, "Semantic", part.Position);
            //for (int i = 0; i < part.FuncCallsArguments.Length; i++)
            //{
                var args = part.FuncCallsArguments[0];
                var resolved = ResolveArgs(args);
                var ov = sym.GetOverload(resolved, true, part.Position);
            //}
            return ov.ReturnType;
        }
    }

    Symbol ResolveIdentifierExpression(IdentifierExpression identifier)
    {
        Scope lookingScope = currentScope;
        Symbol sym = ResolveExpression(identifier.FirstPart);

        for (int i = 0; i < identifier.OtherParts.Length; i++)
        {
            if (sym is not ClassSymbol cl) 
                throw new Exception($"Cannot use operator '::' to '{sym.GetFullName()}'");
            lookingScope = cl.Members;
            var part = identifier.OtherParts[i];
            sym = ResolveIdentifierPart(part, lookingScope, true, sym);
        }

        identifier.Resolved = sym;

        return sym;
    }
}
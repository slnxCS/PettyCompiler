using PettyLang.AST;
using PettyLang.Errors;

namespace PettyLang.Semantic;

public static class BuiltIn {
    private static bool inited = false;

    public static ClassSymbol VoidClass = null!, ObjectClass = null!, FunctionClass = null!;
    public static Int32ClassSymbol Int32Class = null!;
    public static Float32ClassSymbol Float32Class = null!;
    public static ClassSymbol? StringClass = null;
    public static readonly Scope GlobalScope = new(ScopeType.Global, null);

    public static void Init()
    {
        if (inited) return;
        else inited = true;

        ObjectClass = new("Object", GlobalScope, default);
        VoidClass = new("void", GlobalScope, default);

        Int32Class = new();
        Float32Class = new();
        FunctionClass = new("function", GlobalScope, default);

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

    static void AddOverload(FunctionSymbol func, FunctionParameter[] @params, ClassSymbol returnType, int id)
    {
        func.AddOverload(new BuiltInFunctionOverload(@params, returnType, id));
    }

    public static void Define()
    {
        GlobalScope.DefineClass(Int32Class);
        GlobalScope.DefineClass(Float32Class);
        GlobalScope.DefineClass(VoidClass);
        if (StringClass != null) GlobalScope.DefineClass(StringClass);

        var printFunc = new FunctionSymbol("print", GlobalScope);
        GlobalScope.DefineFunc(printFunc);
        AddOverload(printFunc, createParams(("num", Int32Class)), VoidClass, 0);
        AddOverload(printFunc, createParams(("num", Float32Class)), VoidClass, 1);
        var toFloatFunc = new FunctionSymbol("ToFloat", Int32Class.Members);
        Int32Class.Members.DefineFunc(toFloatFunc);
        AddOverload(toFloatFunc, createParams(("number", Int32Class)), Float32Class, 2);
        var readFunc = new FunctionSymbol("read", GlobalScope);
        GlobalScope.DefineFunc(readFunc);
        AddOverload(readFunc, Array.Empty<FunctionParameter>(), Int32Class, 3);
    }
}

public class Analyzer
{
    private Scope currentScope = BuiltIn.GlobalScope;
    private FunctionOverload? currentFunction = null;
    public static FunctionOverload? MainFunction;
    public List<VarDeclStatement> GlobalVariables = new();
    public List<FuncDefineStatement> Functions = new();
    private bool returnFinded;

    public Analyzer()
    {
        BuiltIn.Init();
    }

    public void Analyze(Statement[] AST)
    {
        VisitStatements(AST);

        if (MainFunction == null)
        {
            throw new Error("Program should have Main Function", "Semantic", default);
        }
    }

    void VisitStatements(Statement[] statements)
    {
        foreach (var statement in statements) 
            VisitStatement(statement);
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

            case FuncDefineStatement funcDef :
            {
                ResolveFuncDecl(funcDef);
                break;
            }

            case VarDeclStatement varDecl :
            {
                VisitVarDef(varDecl);
                break;
            }

            case ReturnStatement @return :
            {
                VisitReturn(@return);
                break;
            }

            case VarAssigmentStatement varAssigment :
            {
                VisitVarAssigment(varAssigment);
                break;
            }

            default : throw new NotImplementedException($"{statement}");
        }
    }

    void ensureFunc(Position position)
    {
        if (currentFunction == null)
            throw new Error($"Cannot do this operation outside a function", "Semantic", position);
    }

    void VisitVarAssigment(VarAssigmentStatement statement)
    {
        ensureFunc(statement.Position);

        if (statement.Operator != "=")
            throw new NotImplementedException(statement.Operator);

        var target = ResolveExpression(statement.Target);
        if (target is not VarSymbol var)
            throw new Error($"The assignment operation cannot be applied to {target.Type.GetFullName()}", "Semantic", statement.Target.Position);
        var value = ResolveExpression(statement.Value);
        if (target.Type != value.Type)
            throw new Error($"Cannot convert from {value.Type.GetFullName()} to {target.Type.GetFullName()}", "Semantic", statement.Value.Position);

        statement.Resolved = var;
    }

    void VisitReturn(ReturnStatement statement)
    {
        ensureFunc(statement.Position);
        
        ClassSymbol resolved = statement.Value != null ? GetSymType(ResolveExpression(statement.Value)) : BuiltIn.VoidClass;

        if (resolved != currentFunction!.ReturnType)
            throw new Error($"Cannot convert from {resolved.Type.GetFullName()} to {currentFunction.ReturnType.GetFullName()}", 
                "Semantic", statement.Value?.Position ?? statement.Position);

        returnFinded = true;
    }

    FunctionParameter[] ResolveParams(FuncParameter[] parameters)
    {
        var @params = new FunctionParameter[parameters.Length];
        for (int i = 0; i < @params.Length; i++)
        {
            @params[i] = new(parameters[i].Name, parameters[i].Position, GetSymType(ResolveExpression(parameters[i].Type)));
            parameters[i].Resolved = @params[i];
        }

        return @params;
    }

    FunctionOverload ResolveFuncDecl(FuncDefineStatement func)
    {
        var fs = currentScope.GetFunc(func.Name, false);
        if (fs == null) {
            fs = new (func.Name, currentScope);
            currentScope.DefineFunc(fs);
        }

        var ov = new FunctionOverload(ResolveParams(func.Parameters), func.Position, func.ReturnType == null? BuiltIn.VoidClass : 
                GetSymType(ResolveExpression(func.ReturnType)), func);

        func.Resolved = ov;

        fs.AddOverload(ov);

        if (fs.Name == "Main")
        {
            if (MainFunction != null) 
            {
                throw new Error("Main entry point is already exist in this program", "Semantic", ov.Position);
            }
            if (ov.ReturnType != BuiltIn.VoidClass)
            {
                throw new Error("Main function must return void", "Semantic", ov.Position);
            }
            if (ov.Arity != 0)
            {
                throw new Error("Main function should not take any parameters", "Semantic", ov.Position);
            }

            MainFunction = ov;
        }

        currentFunction = ov;
        var lastScope = currentScope;
        currentScope = new Scope(ScopeType.Function, currentScope);
        returnFinded = false;

        for (int i = 0; i < ov.Arity; i++)
        {
            currentScope.DefineVar(new(ov.Parameters[i].Name, ov.Parameters[i].Position, currentScope, ov.Parameters[i].Type, 
                GetInstanceSymbol(ov.Parameters[i].Type, ov.Parameters[i].Position)));
        }

        try
        {
            if (ov.Statement != null)
                VisitStatements(ov.Statement.Block.Statements);
        }
        finally
        {
            ov.LocalsCount = currentScope.GetFreeVarID();
            currentScope = lastScope;
            currentFunction = null;
            if (!returnFinded && ov.ReturnType != BuiltIn.VoidClass) 
                throw new Error($"Function '{ov.Parent.GetFullName()}' : not all code paths return a value", "", func.Position);
        }

        Functions.Add(func);

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
        var value = GetInstanceSymbol(resolved);
        varDecl.Resolved = new(varDecl.Name, varDecl.Position, currentScope, type, value);
        currentScope.DefineVar(varDecl.Resolved);
        if (varDecl.Resolved.IsGlobal)
            GlobalVariables.Add(varDecl);
    }

    void VisitStatementExpression(StatementExpression expr)
    {
        ensureFunc(expr.Position);
        ResolveExpression(expr.Expression);
    }

    Symbol ResolveExpression(Expression expr)
    {
        switch (expr)
        {
            case IntExpression i : return new Int32InstanceSymbol(currentScope, BuiltIn.Int32Class, i.Number, i.Position);
            case FloatExpression f : return new Float32InstanceSymbol(currentScope, BuiltIn.Float32Class, f.Number, f.Position);
            case StringExpression : return BuiltIn.StringClass ?? 
                throw new Error("To use the String type, import the String class from the std module (import String from std)", "Semantic", expr.Position);
            case IdentifierExpression id : return ResolveIdentifierExpression(id);
            case IdentifierExpressionPart idPart : return ResolveIdentifierPart(idPart, currentScope, false, null);
            case BinaryExpression bin : return ResolveBin(bin);
            default : throw new NotImplementedException($"{expr}");
        }
    }

    ClassInstanceSymbol GetInstanceSymbol(Symbol sym)
    {
        if (sym is ClassInstanceSymbol instance)
            return instance;
        if (sym is VarSymbol var)
            return var.Value;
        
        throw new Error($"Value required", "Semantic", sym.Position);
    }

    ClassInstanceSymbol GetInstanceSymbol(ClassSymbol sym, Position position)
    {
        return sym.GetInstance(currentScope, position);
    }

    ClassInstanceSymbol ResolveBin(BinaryExpression bin)
    {
        var left = ResolveExpression(bin.Left);
        var right = ResolveExpression(bin.Right);

        var leftInst = GetInstanceSymbol(left);
        var rightInst = GetInstanceSymbol(right);

        bin.LeftSymbol = left;
        bin.RightSymbol = right;
        
        return GetInstanceSymbol(leftInst.VisitBinary(rightInst, bin), bin.Position);
    }

    ClassSymbol GetSymType(Symbol s)
    {
        if (s is ClassSymbol c)
            return c;
        return s.Type;
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
            if (_v != null) 
            {
                part.Resolved = _v;
                return _v;
            }
            var _c = lookingScope.GetClass(part.ID, local) ?? throw new Error(errorMsg, "Semantic", part.Position);
            part.Resolved = _c;
            return _c;
        }
        else
        {
            var sym = lookingScope.GetFunc(part.ID, local);
            if (sym == null) 
                throw new Error(errorMsg, "Semantic", part.Position);
            part.Resolved = sym;
            //for (int i = 0; i < part.FuncCallsArguments.Length; i++)
            //{
                var args = part.FuncCallsArguments[0];
                var resolved = ResolveArgs(args);
                var ov = sym.GetOverload(resolved, true, part.Position);
                part.ResolvedOverload = ov;
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
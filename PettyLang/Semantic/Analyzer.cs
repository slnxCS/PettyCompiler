using PettyLang.AST;
using PettyLang.Compiler;
using PettyLang.Errors;

namespace PettyLang.Semantic;

public class Analyzer
{
    public readonly Native Native;
    private Scope currentScope;
    private FunctionOverload? currentFunction = null;
    private ClassSymbol? currentClass = null;
    public FunctionOverload? MainFunction;
    public List<VarDeclStatement> GlobalVariables = new();
    public List<FuncDefineStatement> Functions = new();
    public Dictionary<ClassSymbol, List<FunctionOverload>> Methods = new();
    public List<ClassDefineStatement> Classes = new();
    private bool returnFinded;
    public readonly ConstantPool ConstantPool;

    private void AddClassToList(ClassSymbol @class)
    {
        Classes.Add(new ClassDefineStatement(@class.Position, @class.Name, null, null) {Resolved = @class});
    }

    private void InitClassList()
    {
        AddClassToList(Native.ObjectClass);
        AddClassToList(Native.ValueObjectClass);
        AddClassToList(Native.VoidClass);
        AddClassToList(Native.Int32Class);
        AddClassToList(Native.Float32Class);
        AddClassToList(Native.BoolClass);
        AddClassToList(Native.FunctionClass);
        AddClassToList(Native.ArrayClass);
    }

    public Analyzer(ConstantPool constantPool)
    {
        ConstantPool = constantPool;
        Native = new Native(this);
        currentScope = Native.GlobalScope;
        InitClassList();
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

            case VarAssignmentStatement varAssignment :
            {
                VisitVarAssignment(varAssignment);
                break;
            }

            case IfStatement @if :
            {
                VisitIf(@if);
                break;
            }

            case BlockStatement block :
            {
                VisitBlock(block);
                break;
            }

            case ClassDefineStatement @class :
            {
                VisitClass(@class);
                break;
            }

            default : throw new NotImplementedException($"{statement}");
        }
    }

    void VisitClass(ClassDefineStatement @class) 
    {
        ClassSymbol? derived = null;
        if (@class.Derived != null)
        {
            var derivedSym = ResolveIdentifierExpression(@class.Derived);
            derived = derivedSym as ClassSymbol ?? 
                throw new Error($"Class must derived from another class, not {derivedSym.GetType().Name}", "Semantic", @class.Derived.Position);
        }
        var sym = new ClassSymbol(@class.Name, currentScope, @class.Position, derived, Native);
        @class.Resolved = sym;
        currentScope.DefineClass(sym);

        var oldClass = currentClass;
        var oldScope = currentScope;
        currentScope = sym.Members;

        currentClass = sym;
        Classes.Add(@class);

        try
        {
            if (@class.Block != null) 
            {
                var vars = DefineAll<VarDeclStatement>(@class.Block.Statements, VisitVarDef);
                var funcs = DefineAll<FuncDefineStatement>(@class.Block.Statements, VisitFuncDeclWithoutBlock);

                foreach (var func in funcs)
                {
                    VisitFuncBlock(func.Resolved!);
                }
            }
        }
        finally
        {
            
            currentScope = oldScope;
            currentClass = oldClass;
        }
    }

    void VisitBlock(BlockStatement statement)
    {
        var oldScope = currentScope;
        currentScope = new(ScopeType.Local, oldScope);
        try
        {
            VisitStatements(statement.Statements);
        }
        finally
        {
            currentScope = oldScope;
        }
    }

    void VisitIf(IfStatement statement)
    {
        ensureFunc(statement.Position);
        var condition = GetInstanceSymbol(ResolveExpression(statement.Condition));
        if (condition.Type != Native.BoolClass)
        {
            throw new NotImplementedException();
        }

        var oldScope = currentScope;
        currentScope = new(ScopeType.Local, oldScope);

        try
        {
            VisitStatements(statement.Block.Statements);
        }
        finally
        {
            currentScope = oldScope;
        }
    }

    void ensureFunc(Position position)
    {
        if (currentFunction == null)
            throw new Error($"Cannot do this operation outside a function", "Semantic", position);
    }

    void VisitVarAssignment(VarAssignmentStatement statement)
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
        
        ClassSymbol resolved = statement.Value != null ? GetSymType(ResolveExpression(statement.Value)) : Native.VoidClass;

        if (resolved != currentFunction!.ReturnType)
            throw new Error($"Cannot convert from {resolved.Type.GetFullName()} to {currentFunction.ReturnType.GetFullName()}", 
                "Semantic", statement.Value?.Position ?? statement.Position);

        returnFinded = true;
    }

    FunctionParameter[] ResolveParams(FuncParameter[] parameters)
    {
        if (currentClass == null) {
            var @params = new FunctionParameter[parameters.Length];
            for (int i = 0; i < @params.Length; i++)
            {
                @params[i] = new(parameters[i].Name, parameters[i].Position, GetSymType(ResolveExpression(parameters[i].Type)));
                parameters[i].Resolved = @params[i];
            }

            return @params;
        }
        else
        {
            var @params = new FunctionParameter[parameters.Length + 1];
            @params[0] = new("self", currentClass.Position, currentClass);
            for (int i = 1; i < @params.Length; i++)
            {
                var j = i - 1;
                @params[i] = new(parameters[j].Name, parameters[j].Position, GetSymType(ResolveExpression(parameters[j].Type)));
                parameters[j].Resolved = @params[i];
            }
    
            return @params;
        }
    }

    void VisitFuncDeclWithoutBlock(FuncDefineStatement func)
    {
        var fs = currentScope.GetFunc(func.Name, false);
        if (fs == null) {
            if (currentClass == null)
                fs = new FunctionSymbol(func.Name, currentScope, func.Position, this, Native);
            else 
                fs = new MethodSymbol(func.Name, currentClass.Members, func.Position, this, Native);
            currentScope.DefineFunc(fs);
        }

        var ov = new FunctionOverload(ResolveParams(func.Parameters), func.Position, func.ReturnType == null? Native.VoidClass : 
                GetSymType(ResolveExpression(func.ReturnType)), func, null);

        func.Resolved = ov;

        fs.AddOverload(ov);

        if (fs.Name == "Main")
        {
            if (currentClass != null)
            {
                throw new Error("Main entry point should be function, not method", "Semantic", ov.Position);
            }
            if (MainFunction != null) 
            {
                throw new Error("Main entry point is already exist in this program", "Semantic", ov.Position);
            }
            if (ov.ReturnType != Native.VoidClass)
            {
                throw new Error("Main function must return void", "Semantic", ov.Position);
            }
            if (ov.Arity != 0)
            {
                throw new Error("Main function should not take any parameters", "Semantic", ov.Position);
            }

            MainFunction = ov;
        }
        
        if (currentClass == null)
            Functions.Add(func);
        else 
        {
            if (!Methods.ContainsKey(currentClass))
                Methods.Add(currentClass, new());
            Methods[currentClass].Add(ov);
        }
    }

    FunctionOverload ResolveFuncDecl(FuncDefineStatement func)
    {
        var fs = currentScope.GetFunc(func.Name, false);
        if (fs == null) {
            if (currentClass == null)
                fs = new FunctionSymbol(func.Name, currentScope, func.Position, this, Native);
            else 
                fs = new MethodSymbol(func.Name, currentClass.Members, func.Position, this, Native);
            currentScope.DefineFunc(fs);
        }

        var ov = new FunctionOverload(ResolveParams(func.Parameters), func.Position, func.ReturnType == null ? Native.VoidClass : 
                GetSymType(ResolveExpression(func.ReturnType)), func, null);

        func.Resolved = ov;

        fs.AddOverload(ov);

        if (fs.Name == "Main")
        {
            if (currentClass != null)
            {
                throw new Error("Main entry point should be function, not method", "Semantic", ov.Position);
            }
            if (MainFunction != null) 
            {
                throw new Error("Main entry point is already exist in this program", "Semantic", ov.Position);
            }
            if (ov.ReturnType != Native.VoidClass)
            {
                throw new Error("Main function must return void", "Semantic", ov.Position);
            }
            if (ov.Arity != 0)
            {
                throw new Error("Main function should not take any parameters", "Semantic", ov.Position);
            }

            MainFunction = ov;
        }

        VisitFuncBlock(ov);

        if (currentClass == null)
            Functions.Add(func);
        else 
        {
            if (!Methods.ContainsKey(currentClass))
                Methods.Add(currentClass, new());
            Methods[currentClass].Add(ov);
        }

        return ov;
    }

    void VisitFuncBlock(FunctionOverload ov)
    {
        var lastScope = currentScope;
        currentScope = new Scope(ScopeType.Function, currentScope);
        var oldFunc = currentFunction;
        currentFunction = ov;
        returnFinded = false;

        for (int i = 0; i < ov.Arity; i++)
        {
            var instance = GetInstanceSymbol(ov.Parameters[i].Type, ov.Parameters[i].Position);
            var var = new VarSymbol(ov.Parameters[i].Name, ov.Parameters[i].Position, currentScope, ov.Parameters[i].Type, instance);
            currentScope.DefineVar(var);
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
            currentFunction = oldFunc;
            if (!returnFinded && ov.ReturnType != Native.VoidClass) 
                throw new Error($"Function '{ov.Parent.GetFullName()}' : not all code paths return a value", "", ov.Position);
        }
    }

    void VisitVarDef(VarDeclStatement varDecl)
    {
        if (currentClass != null && currentFunction == null)
        {
            currentClass.VarDefines.Add(varDecl);
            return;
        }

        Symbol resolved;
        ClassSymbol resolvedType;

        if (varDecl.Value != null) 
        {
            resolved = ResolveExpression(varDecl.Value);
            resolvedType = GetSymType(resolved);
        }
        else
        {
            resolvedType = GetSymType(ResolveIdentifierExpression(varDecl.Type!));
            resolved = resolvedType.GetInstance(resolvedType.Members, resolvedType.Position, this);
        }


        var type = varDecl.Type == null ? resolvedType : GetSymType(varDecl.Type);
        if (varDecl.Type != null)
        {
            if (type != resolvedType) 
                throw new Error($"Cannot convert from '{resolvedType.GetFullName()}' to '{type.GetFullName()}'", "Semantic", resolvedType.Position);
        }
        if (type == Native.VoidClass) 
            throw new Error($"cannot assign a value of type void to a variable", $"Semantic", varDecl.Value?.Position ?? varDecl.Position);
        var value = GetInstanceSymbol(resolved);
        varDecl.Resolved = new(varDecl.Name, varDecl.Position, currentScope, type, value);
        currentScope.DefineVar(varDecl.Resolved);
        if (varDecl.Resolved.IsGlobal)
            GlobalVariables.Add(varDecl);

        return;
    }

    public VarSymbol[] ResolveFieldsDecl(List<VarDeclStatement> fields, ClassInstanceSymbol instance)
    {
        List<VarSymbol> vars = new();
        foreach (var field in fields) {
            Symbol resolved;
            ClassSymbol resolvedType;
            if (field.Value != null) 
            {
                resolved = ResolveExpression(field.Value);
                resolvedType = GetSymType(resolved);
            }
            else
            {
                resolvedType = GetSymType(ResolveIdentifierExpression(field.Type!));
                resolved = resolvedType.GetInstance(instance.Members, instance.Position, this);
            }
            var type = field.Type == null ? resolvedType : GetSymType(field.Type);
            if (field.Type != null)
            {
                if (type != resolvedType) 
                    throw new Error($"Cannot convert from '{resolvedType.GetFullName()}' to '{type.GetFullName()}'", "Semantic", resolvedType.Position);
            }
            if (type == Native.VoidClass) 
                throw new Error($"cannot assign a value of type void to a variable", $"Semantic", field.Value?.Position ?? field.Position);
            var value = GetInstanceSymbol(resolved);
            field.Resolved = new(field.Name, field.Position, instance.Members, type, value);
            vars.Add(field.Resolved);
        }

        return vars.ToArray();
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
            case IntExpression i : 
            {
                i.Resolved = new Int32InstanceSymbol(currentScope, Native.Int32Class, i.Number, i.Position, this);
                return i.Resolved;
            }
            case FloatExpression f : 
            {
                f.Resolved = new Float32InstanceSymbol(currentScope, Native.Float32Class, f.Number, f.Position, this);
                return f.Resolved;
            }

            case BoolExpression b :
            {
                b.Resolved = new(b.Value, currentScope, b.Position, this);
                return b.Resolved;
            }

            case StringExpression : return Native.StringClass ?? 
                throw new Error("To use the String type, import the String class from the std module (import String from std)", "Semantic", expr.Position);
            case IdentifierExpression id : return ResolveIdentifierExpression(id);
            case IdentifierExpressionPart idPart : {
                idPart.Parent.FirstPartResolved = ResolveIdentifierPart(idPart, currentScope, false, null);
                return idPart.Parent.FirstPartResolved;
            }
            case BinaryExpression bin : return ResolveBin(bin);
            case AsExpression @as : return ResolveCastExpr(@as);
            default : throw new NotImplementedException($"{expr}");
        }
    }

    T[] DefineAll<T, TFuncResult>(Statement[] statements, Func<T, TFuncResult> defAction)
        where T : Statement
    {
        List<T> TStatements = [];

        foreach (var statement in statements)
        {
            if (statement is T TStatement)
            {
                TStatements.Add(TStatement);
                defAction(TStatement);
            }
        }

        return TStatements.ToArray();
    }

    T[] DefineAll<T>(Statement[] statements, Action<T> defAction)
        where T : Statement
    {
        List<T> TStatements = [];

        foreach (var statement in statements)
        {
            if (statement is T TStatement)
            {
                TStatements.Add(TStatement);
                defAction(TStatement);
            }
        }

        return TStatements.ToArray();
    }

    ClassInstanceSymbol GetInstanceSymbol(Symbol sym)
    {
        if (sym is ClassInstanceSymbol instance)
            return instance;
        if (sym is VarSymbol var)
            return var.Value;
        
        throw new Error($"Value required", "Semantic", sym.Position);
    }

    ClassSymbol ExpectClass(Expression expr)
    {
        var s = ResolveExpression(expr);
        if (s is not ClassSymbol cl)
            throw new Error($"Expected type, but its {s.GetFullName()}", "Semantic", expr.Position);
        return cl;
    }

    ClassInstanceSymbol ResolveCastExpr(AsExpression expression)
    {
        var sym = ResolveExpression(expression.Value);
        var type = ExpectClass(expression.AsType);
        expression.ResolvedValue = sym;
        expression.ResolvedAsType = type;
        return GetInstanceSymbol(sym.ResolveCast(type, expression), expression.Position);
    }

    public ClassInstanceSymbol GetInstanceSymbol(ClassSymbol sym, Position position)
    {
        return sym.GetInstance(currentScope, position, this);
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
            : $"'{lastSym.Type.GetFullName()}' does not contains field with name '{part.ID}'";
        if (part.FuncCallsArguments.Length == 0)
        {
            var _v = lookingScope.GetVar(part.ID, local);
            if (_v != null) 
            {
                part.Resolved = _v;
                return _v;
            }
            var _c = lookingScope.GetClass(part.ID, local) ?? lookingScope.GetFunc(part.ID, local)?.Type ?? throw new Error(errorMsg, "Semantic", part.Position);
            part.Resolved = _c;
            return _c;
        }
        else
        {
            var func = lookingScope.GetFunc(part.ID, local);
            var args = part.FuncCallsArguments[0];
            var resolved = ResolveArgs(args);
            if (func != null) {
                if (func is not ConstructorFunctionSymbol constructor) {
                    part.Resolved = func;
                    //for (int i = 0; i < part.FuncCallsArguments.Length; i++)
                    //{
                        
                        var ov = func.GetOverload(resolved, true, part.Position);
                        part.ResolvedOverload = ov;
                        part.ResolvedParameters = resolved;
                    //}
                    part.Resolved = GetInstanceSymbol(ov.ReturnType, part.Position);
                    return part.Resolved;
                }
                else
                {
                    var res = constructor.ResolveCall(resolved, this, part);
                    part.ResolvedOverload = res.Item2;
                    part.ResolvedParameters = resolved;
                    part.Resolved = res.Item1;
                    return res.Item1;
                }
            }
            var var = lookingScope.GetVar(part.ID, local);
            if (var != null)
            {
                return GetInstanceSymbol(var.ResolveCall(resolved, part.Parent, part), part.Position);
            }
            var klass = lookingScope.GetClass(part.ID, local);
            if (klass != null)
            {
                var res = klass.ResolveCall(resolved, this, part);
                part.ResolvedOverload = res.Item2;
                part.ResolvedParameters = resolved;
                part.Resolved = res.Item1;
                return res.Item1;
            }

            throw new Error(errorMsg, "Semantic", part.Position);
        }
    }

    Symbol ResolveIdentifierExpression(IdentifierExpression identifier)
    {
        Symbol sym = ResolveExpression(identifier.FirstPart);

        for (int i = 0; i < identifier.OtherParts.Length; i++)
        {
            Scope lookingScope = sym.Members ?? throw new Exception($"Cannot use operator '.' to '{sym.GetFullName()}'");;
            var part = identifier.OtherParts[i];
            sym = ResolveIdentifierPart(part, lookingScope, true, sym);
        }

        identifier.Resolved = sym;

        return sym;
    }
}
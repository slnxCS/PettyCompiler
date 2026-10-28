using NUnit.Framework;
using PettyLang.AST;
using PettyLang.Errors;
using PettyLang.Lexer;
using PettyLang.Parser;

[TestFixture]
public class ParserTests
{
    [TestCase("x", false)]
    [TestCase("var", true)]
    public void VarAssignTest(string name, bool isNameKeyWord)
    {
        var source = $"var {name} = 10;";
        var parser = new Parser(source);
        if (!isNameKeyWord) 
        {
            var ast = parser.Parse();
            Assert.AreEqual(ast.Length, 1);
            Assert.True(ast[0] is VarDeclStatement);
            Assert.True((ast[0] as VarDeclStatement)!.Name == name);
            Assert.True((ast[0] as VarDeclStatement)!.Value is not null);
            Assert.True((ast[0] as VarDeclStatement)!.Value is IntExpression);
            Assert.True(((ast[0] as VarDeclStatement)!.Value as IntExpression)!.Number == 10);
        }
        else
        {
            Assert.Throws<Error>(() => parser.Parse());
        }
    }

    [TestCase("var x = 10")]
    [TestCase("print()")]
    [TestCase("printer.print()")]
    public void SemicolonTest(string source)
    {
        var parser = new Parser(source);
        Assert.True(Assert.Throws<Error>(() => parser.Parse())?.Message.ToLower().Contains("expected ';'") ?? true);
    }


    [TestCase("x", "y()")]
    public void IdentifierTest(params string[] parts)
    {
        var full = string.Join('.', parts) + ";";
        var parser = new Parser(full);
        var ast = parser.Parse();
        Assert.AreEqual(ast.Length, 1);
        Assert.True(ast[0] is StatementExpression);
        Assert.True((ast[0] as StatementExpression)!.Expression is IdentifierExpression);
        Assert.AreEqual(((ast[0] as StatementExpression)!.Expression as IdentifierExpression)!.OtherParts.Length + 1, parts.Length);
    }

    static Statement[] parse(string source) => new Parser(source).Parse();

    static T single<T>(string source) where T : Statement
    {
        var ast = parse(source);
        Assert.That(ast, Has.Length.EqualTo(1));
        Assert.That(ast[0], Is.InstanceOf<T>());
        return (T)ast[0];
    }

    // Parses `var t = <expression>;` and returns the expression
    static Expression expression(string source) => single<VarDeclStatement>($"var t = {source};").Value!;

    // Renders an expression tree in fully parenthesized form, e.g. "(1 + (2 * 3))"
    static string render(Expression ex) => ex switch
    {
        IntExpression i => i.Number.ToString(),
        BoolExpression b => b.Value ? "true" : "false",
        StringExpression s => $"\"{s.String}\"",
        BinaryExpression bin => $"({render(bin.Left)} {bin.Operator} {render(bin.Right)})",
        AsExpression @as => $"({render(@as.Value)} as {render(@as.AsType)})",
        IdentifierExpression id => string.Join('.',
            new[] {(IdentifierExpressionPart)id.FirstPart}.Concat(id.OtherParts).Select(renderPart)),
        _ => throw new NotSupportedException(ex.GetType().Name),
    };

    static string renderPart(IdentifierExpressionPart part) =>
        part.ID + string.Concat(part.FuncCallsArguments.Select(args => $"({string.Join(", ", args.Select(render))})"));

    [TestCase("1 + 2 * 3", "(1 + (2 * 3))")]
    [TestCase("1 * 2 + 3", "((1 * 2) + 3)")]
    [TestCase("(1 + 2) * 3", "((1 + 2) * 3)")]
    [TestCase("1 - 2 - 3", "((1 - 2) - 3)")]
    [TestCase("8 / 4 / 2", "((8 / 4) / 2)")]
    [TestCase("1 + 2 == 3", "((1 + 2) == 3)")]
    [TestCase("1 == 2 == true", "((1 == 2) == true)")]
    [TestCase("2 * 3 as float32", "(2 * (3 as float32))")]
    [TestCase("1 as float32 as int32", "((1 as float32) as int32)")]
    [TestCase("(((1)))", "1")]
    [TestCase("-1 * 2", "(-1 * 2)")]
    public void OperatorPrecedenceAndAssociativity(string source, string expected)
    {
        Assert.That(render(expression(source)), Is.EqualTo(expected));
    }

    [TestCase("x", "x")]
    [TestCase("f()", "f()")]
    [TestCase("f(1, 2 + 3)", "f(1, (2 + 3))")]
    [TestCase("a.b.c", "a.b.c")]
    [TestCase("a.b(1).c", "a.b(1).c")]
    [TestCase("f(g(h()))", "f(g(h()))")]
    [TestCase("\"text\"", "\"text\"")]
    public void PathsAndCalls(string source, string expected)
    {
        Assert.That(render(expression(source)), Is.EqualTo(expected));
    }

    [Test]
    public void VarWithTypeAndValue()
    {
        var decl = single<VarDeclStatement>("var x : int32 = 1;");
        Assert.That(decl.Name, Is.EqualTo("x"));
        Assert.That(render(decl.Type!), Is.EqualTo("int32"));
        Assert.That(decl.Value, Is.InstanceOf<IntExpression>());
    }

    [Test]
    public void VarWithTypeOnly()
    {
        var decl = single<VarDeclStatement>("var x : int32;");
        Assert.That(render(decl.Type!), Is.EqualTo("int32"));
        Assert.That(decl.Value, Is.Null);
    }

    [Test]
    public void FunctionWithParametersAndReturnType()
    {
        var func = single<FuncDefineStatement>("func add(a : int32, b : float32) : int32 { return a; }");
        Assert.That(func.Name, Is.EqualTo("add"));
        Assert.That(func.Parameters.Select(x => x.Name), Is.EqualTo(new[] {"a", "b"}));
        Assert.That(func.Parameters.Select(x => render(x.Type)), Is.EqualTo(new[] {"int32", "float32"}));
        Assert.That(render(func.ReturnType!), Is.EqualTo("int32"));
        Assert.That(func.Block.Statements, Has.Length.EqualTo(1));
        Assert.That(func.Block.Statements[0], Is.InstanceOf<ReturnStatement>());
    }

    [TestCase("func f() { }")]
    [TestCase("func f() : { }")]
    public void FunctionWithoutReturnType(string source)
    {
        var func = single<FuncDefineStatement>(source);
        Assert.That(func.ReturnType, Is.Null);
        Assert.That(func.Parameters, Is.Empty);
        Assert.That(func.Block.Statements, Is.Empty);
    }

    [Test]
    public void ClassWithMembers()
    {
        var @class = single<ClassDefineStatement>("class A : { var x = 1; func m() { } }");
        Assert.That(@class.Name, Is.EqualTo("A"));
        Assert.That(@class.Derived, Is.Null);
        Assert.That(@class.Block!.Statements[0], Is.InstanceOf<VarDeclStatement>());
        Assert.That(@class.Block!.Statements[1], Is.InstanceOf<FuncDefineStatement>());
    }

    [Test]
    public void ClassWithBase()
    {
        var @class = single<ClassDefineStatement>("class B : A { }");
        Assert.That(render(@class.Derived!), Is.EqualTo("A"));
    }

    [Test]
    public void IfStatement()
    {
        var func = single<FuncDefineStatement>("func f() { if x == 1 : { y(); } }");
        var @if = (IfStatement)func.Block.Statements[0];
        Assert.That(render(@if.Condition), Is.EqualTo("(x == 1)"));
        Assert.That(@if.Block.Statements, Has.Length.EqualTo(1));
    }

    [Test]
    public void IfWithoutBraces()
    {
        var func = single<FuncDefineStatement>("func f() { if x == 1 : y(); z(); }");
        Assert.That(func.Block.Statements, Has.Length.EqualTo(2));
        var @if = (IfStatement)func.Block.Statements[0];
        Assert.That(@if.Block.Statements, Has.Length.EqualTo(1));
        Assert.That(render(((StatementExpression)@if.Block.Statements[0]).Expression), Is.EqualTo("y()"));
    }

    [Test]
    public void NestedIfWithoutBraces()
    {
        var func = single<FuncDefineStatement>("func f() { if a == 1 : if b == 1 : c(); }");
        var outer = (IfStatement)func.Block.Statements[0];
        var inner = (IfStatement)outer.Block.Statements[0];
        Assert.That(render(inner.Condition), Is.EqualTo("(b == 1)"));
        Assert.That(inner.Block.Statements, Has.Length.EqualTo(1));
    }

    [TestCase("return;", false)]
    [TestCase("return 1 + 2;", true)]
    public void ReturnStatement(string statement, bool hasValue)
    {
        var func = single<FuncDefineStatement>($"func f() {{ {statement} }}");
        var ret = (ReturnStatement)func.Block.Statements[0];
        Assert.That(ret.Value, hasValue ? Is.Not.Null : Is.Null);
    }

    [Test]
    public void NestedBlocks()
    {
        var func = single<FuncDefineStatement>("func f() { { { } } }");
        var outer = (BlockStatement)func.Block.Statements[0];
        Assert.That(outer.Statements[0], Is.InstanceOf<BlockStatement>());
    }

    [TestCase("=")]
    [TestCase("+=")]
    [TestCase("-=")]
    [TestCase("*=")]
    [TestCase("/=")]
    public void AssignmentOperators(string op)
    {
        var assignment = single<VarAssignmentStatement>($"a.b {op} 1;");
        Assert.That(assignment.Operator, Is.EqualTo(op));
        Assert.That(render(assignment.Target), Is.EqualTo("a.b"));
        Assert.That(render(assignment.Value), Is.EqualTo("1"));
    }

    [Test]
    public void MultipleTopLevelStatements()
    {
        var ast = parse("var a = 1; func f() { } class C : { }");
        Assert.That(ast.Select(x => x.GetType()),
            Is.EqualTo(new[] {typeof(VarDeclStatement), typeof(FuncDefineStatement), typeof(ClassDefineStatement)}));
    }

    [TestCase("var x;", "Expected variable type or value")]
    [TestCase("var = 1;", "Expected variable name")]
    [TestCase("var x : int32 1;", "Expected '=' after variable type")]
    [TestCase("var x 1;", "Expected '=' after variable name")]
    [TestCase("func () { }", "Expected function name")]
    [TestCase("func f { }", "Expected '(' after function name")]
    [TestCase("func f(a) { }", "Expected ':' before parameter type")]
    [TestCase("func f(a : int32 { }", "Expected ')' after function parameters")]
    [TestCase("func f() : int32", "Expected '{' for start block")]
    [TestCase("func f() print(1);", "Expected '{' for start block")]
    [TestCase("func f() : int32 return 1;", "Expected '{' for start block")]
    [TestCase("class A : B var x = 1;", "Expected '{' for start block")]
    [TestCase("class { }", "Expected class name")]
    [TestCase("class A { }", "Expected ':' after class name")]
    [TestCase("func f() { if x { } }", "Expected ':' after expression")]
    [TestCase("func f() { return 1 }", "Expected ';' after return value")]
    [TestCase("var x = (1 + 2;", "Expected ')'")]
    [TestCase("var x = f(1;", "Expected ) after function arguments")]
    [TestCase("var x = ;", "Unexpected ';' in expression")]
    [TestCase("1;", "Unexpected 'IntNumber'")]
    [TestCase("func f() { else { } }", "Unexpected 'Else'")]
    public void SyntaxErrors(string source, string messagePart)
    {
        Assert.That(() => parse(source), Throws.InstanceOf<Error>().With.Message.Contains(messagePart));
    }

    [TestCase("func f() {")]
    [TestCase("func f() { var x = 1;")]
    public void UnclosedBlock(string source)
    {
        Assert.That(() => parse(source), Throws.InstanceOf<Error>());
    }
}
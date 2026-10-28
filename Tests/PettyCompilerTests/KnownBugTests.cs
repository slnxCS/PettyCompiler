using NUnit.Framework;
using PettyLang.AST;
using PettyLang.Errors;
using PettyLang.Lexer;
using PettyLang.Parser;
using static PettyCompilerTests.Support.TestCompiler;

/// <summary>
/// Tests describing the expected behavior for bugs that are known but not fixed yet.
/// They fail until the bug is fixed. Exclude them with: dotnet test --filter "TestCategory!=KnownBug"
/// </summary>
[TestFixture, Category("KnownBug")]
public class KnownBugTests
{
    // Compiler.CompileIf patches the jump address with InsertRange, which shifts already computed
    // absolute addresses of inner jumps by 4 bytes.
    [Test]
    public void NestedIfJumpTargets()
    {
        Assert.That(MainListing(InMain("if true : { if true : { print(1); } print(2); }")), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 0",
            "PUSH_CONSTANT true",
            "JMP_IF_FALSE @9",
            "PUSH_CONSTANT true",
            "JMP_IF_FALSE @7",
            "PUSH_CONSTANT 1",
            "SYS_CALL 0 1",
            "PUSH_CONSTANT 2",
            "SYS_CALL 0 1",
            "RET",
        }));
    }

    // The lexer replaces '.' with ',' and the parser uses float.Parse with the current culture.
    [Test, SetCulture("en-US")]
    public void FloatLiteralDoesNotDependOnCulture()
    {
        Assert.That(Compile(InMain("var a = 2.5;")).Constants, Is.EqualTo(new object[] {2.5f}));
    }

    // Symbol.CompileCastBytes returns without compiling the value when casting to the same type.
    [Test]
    public void CastToSameTypeKeepsValue()
    {
        Assert.That(MainListing(InMain("var a = 1 as int32;")), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 1",
            "PUSH_CONSTANT 1",
            "STORE_LOCAL 0",
            "RET",
        }));
    }

    // '-' directly before a digit is lexed as part of a number literal.
    [Test]
    public void SubtractionWithoutSpaces()
    {
        var decl = (VarDeclStatement)new Parser("var y = x-1;").Parse()[0];
        Assert.That(decl.Value, Is.InstanceOf<BinaryExpression>());
        Assert.That(((BinaryExpression)decl.Value!).Operator, Is.EqualTo("-"));
    }

    // Identifiers start one column too far on the first line, symbols one column too early on the others.
    [Test]
    public void TokenColumnsAreConsistentAcrossLines()
    {
        var tokens = new Lexer("var x = 1;\nvar y = 2;").Tokenize();
        var identifiers = tokens.Where(t => t.Lexeme is "x" or "y").Select(t => t.Position.Start.X);
        var equates = tokens.Where(t => t.Lexeme == "=").Select(t => t.Position.Start.X);
        Assert.That(identifiers, Is.EqualTo(new[] {5, 5}));
        Assert.That(equates, Is.EqualTo(new[] {7, 7}));
    }

    // Compiler.SortFunctionsByID sorts a local copy, so the functions section keeps the order in which the
    // analyzer finished the functions: a function declared inside Main ends up before Main.
    [Test]
    public void FunctionsSectionIsSortedById()
    {
        var bc = Compile("func Main() { func inner() { } inner(); }");
        Assert.That(bc.Listing(bc.Main), Does.Contain("CALL 1 0"));
    }

    // Analyzer.VisitFuncBlock checks for a missing return in a finally block, so any error inside the body
    // of a non-void function is replaced by "not all code paths return a value".
    [TestCase("func f() : int32 { return true; } func Main() { }", "Cannot convert")]
    [TestCase("func f() : int32 { return; } func Main() { }", "Cannot convert")]
    [TestCase("func f() : int32 { print(x); return 1; } func Main() { }", "Name 'x' does not exist")]
    public void ErrorInsideNonVoidFunctionIsNotMasked(string source, string messagePart)
    {
        AssertCompileError(source, messagePart);
    }

    // Source accepted by the parser must either compile or fail with a compiler Error,
    // never with an internal exception (NullReferenceException, NotImplementedException, ...).
    [TestCase("func Main() { var x : int32; }")]
    [TestCase("var g : int32; func Main() { }")]
    [TestCase("func Main() { var x = 1; x += 1; }")]
    [TestCase("func Main() { if 1 : { } }")]
    public void NoInternalExceptions(string source)
    {
        try
        {
            Bytes(source);
        }
        catch (Error) { }
    }
}

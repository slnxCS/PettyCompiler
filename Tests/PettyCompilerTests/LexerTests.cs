using NUnit.Framework;
using PettyLang.Errors;
using PettyLang.Lexer;
using PettyLang.Tokens;

[TestFixture] 
public class LexerTests
{
    [TestCase("var x = 10;", TokenType.Var, TokenType.Identifier, TokenType.Equate, TokenType.IntNumber, TokenType.Semicolon)]
    [TestCase("func\n    Main\t\n      ()\n{\n\t\t\t\t}", 
        TokenType.Func, TokenType.Identifier, TokenType.LParen, TokenType.RParen, TokenType.LCurlyBrace, TokenType.RCurlyBrace)]
    public void EqualsTest(string source, params TokenType[] types)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize().Reverse().Skip(1).Reverse().ToArray();
        Assert.AreEqual(tokens.Length, types.Length);
        for (int i = 0; i < tokens.Length; i++)
        {
            Assert.AreEqual(tokens[i].Type, types[i]);
        }
    }


    [TestCase("#<")]
    [TestCase("\"")]
    public void ThrowsTest(string source)
    {
        var lexer = new Lexer(source);
        Assert.Throws<Error>(() => lexer.Tokenize());
    }

    static Token[] tokenize(string source) => new Lexer(source).Tokenize();

    // All tokens except the trailing EOF
    static Token[] tokens(string source) => tokenize(source)[..^1];

    [TestCase("")]
    [TestCase("   \n\t  ")]
    [TestCase("# only a comment")]
    public void EmptyInputProducesOnlyEOF(string source)
    {
        var result = tokenize(source);
        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].Type, Is.EqualTo(TokenType.EOF));
    }

    [Test]
    public void EOFIsAlwaysLast()
    {
        Assert.That(tokenize("var x = 1;").Last().Type, Is.EqualTo(TokenType.EOF));
    }

    [TestCase("func", TokenType.Func)]
    [TestCase("var", TokenType.Var)]
    [TestCase("import", TokenType.Import)]
    [TestCase("package", TokenType.Package)]
    [TestCase("return", TokenType.Return)]
    [TestCase("as", TokenType.As)]
    [TestCase("false", TokenType.False)]
    [TestCase("true", TokenType.True)]
    [TestCase("if", TokenType.If)]
    [TestCase("else", TokenType.Else)]
    [TestCase("class", TokenType.Class)]
    public void Keywords(string source, TokenType expected)
    {
        var result = tokens(source);
        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].Type, Is.EqualTo(expected));
        Assert.That(result[0].Lexeme, Is.EqualTo(source));
    }

    [TestCase("x")]
    [TestCase("_private")]
    [TestCase("snake_case_2")]
    [TestCase("переменная")]
    [TestCase("variable")]   // starts with keyword 'var'
    [TestCase("Func")]       // keywords are case sensitive
    [TestCase("iff")]
    public void Identifiers(string source)
    {
        var result = tokens(source);
        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].Type, Is.EqualTo(TokenType.Identifier));
        Assert.That(result[0].Lexeme, Is.EqualTo(source));
    }

    [TestCase("+", TokenType.Plus)]
    [TestCase("-", TokenType.Minus)]
    [TestCase("*", TokenType.Asterisk)]
    [TestCase("/", TokenType.Slash)]
    [TestCase("=", TokenType.Equate)]
    [TestCase("==", TokenType.Equals)]
    [TestCase("+=", TokenType.PlusEquate)]
    [TestCase("-=", TokenType.MinusEquate)]
    [TestCase("*=", TokenType.MultiplyEquate)]
    [TestCase("/=", TokenType.DivideEquate)]
    [TestCase("(", TokenType.LParen)]
    [TestCase(")", TokenType.RParen)]
    [TestCase("[", TokenType.LSquareBracket)]
    [TestCase("]", TokenType.RSquareBracket)]
    [TestCase("{", TokenType.LCurlyBrace)]
    [TestCase("}", TokenType.RCurlyBrace)]
    [TestCase("<", TokenType.LAngleBrace)]
    [TestCase(">", TokenType.RAngleBrace)]
    [TestCase(":", TokenType.Colon)]
    [TestCase(";", TokenType.Semicolon)]
    [TestCase(".", TokenType.Dot)]
    [TestCase(",", TokenType.Comma)]
    public void Symbols(string source, TokenType expected)
    {
        var result = tokens(source);
        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].Type, Is.EqualTo(expected));
        Assert.That(result[0].Lexeme, Is.EqualTo(source));
    }

    [TestCase("===", TokenType.Equals, TokenType.Equate)]
    [TestCase("= =", TokenType.Equate, TokenType.Equate)]
    [TestCase("+==", TokenType.PlusEquate, TokenType.Equate)]
    [TestCase("a.b", TokenType.Identifier, TokenType.Dot, TokenType.Identifier)]
    [TestCase("f(x,y)", TokenType.Identifier, TokenType.LParen, TokenType.Identifier, TokenType.Comma,
        TokenType.Identifier, TokenType.RParen)]
    public void LongestSymbolMatch(string source, params TokenType[] expected)
    {
        Assert.That(tokens(source).Select(x => x.Type), Is.EqualTo(expected));
    }

    [TestCase("0", "0")]
    [TestCase("42", "42")]
    [TestCase("2147483647", "2147483647")]
    public void IntNumbers(string source, string lexeme)
    {
        var result = tokens(source);
        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].Type, Is.EqualTo(TokenType.IntNumber));
        Assert.That(result[0].Lexeme, Is.EqualTo(lexeme));
    }

    [TestCase("3.14")]
    [TestCase("10.0")]
    public void FloatNumbers(string source)
    {
        var result = tokens(source);
        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].Type, Is.EqualTo(TokenType.FloatNumber));
    }

    [Test]
    public void MinusWithSpaceIsOperator()
    {
        Assert.That(tokens("x - 1").Select(x => x.Type),
            Is.EqualTo(new[] {TokenType.Identifier, TokenType.Minus, TokenType.IntNumber}));
    }

    [TestCase("\"hello\"", "hello")]
    [TestCase("\"\"", "")]
    [TestCase("\"with spaces and # not a comment\"", "with spaces and # not a comment")]
    [TestCase("\"multi\nline\"", "multi\nline")]
    public void Strings(string source, string content)
    {
        var result = tokens(source);
        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].Type, Is.EqualTo(TokenType.String));
        Assert.That(result[0].Lexeme, Is.EqualTo(content));
    }

    [TestCase("var # comment\nx", TokenType.Var, TokenType.Identifier)]
    [TestCase("var#comment", TokenType.Var)]
    [TestCase("var #< multi\nline\ncomment ># x", TokenType.Var, TokenType.Identifier)]
    [TestCase("#<>#var", TokenType.Var)]
    [TestCase("#< # nested-looking >#var", TokenType.Var)]
    public void CommentsAreSkipped(string source, params TokenType[] expected)
    {
        Assert.That(tokens(source).Select(x => x.Type), Is.EqualTo(expected));
    }

    [TestCase("@", "Unknown symbol")]
    [TestCase("var $x", "Unknown symbol")]
    [TestCase("\"never closed", "String termination required")]
    [TestCase("#< never closed", "Multi-line comment termination required")]
    public void LexicalErrors(string source, string messagePart)
    {
        Assert.That(() => tokenize(source), Throws.InstanceOf<Error>().With.Message.Contains(messagePart));
    }

    [Test]
    public void LexicalErrorsAreSyntaxErrors()
    {
        Assert.That(() => tokenize("@"), Throws.InstanceOf<Error>().With.Message.StartsWith("Syntax error"));
    }

    [Test]
    public void LinesAreCountedFromOne()
    {
        var result = tokens("var\n\nx");
        Assert.That(result[0].Position.Start.Y, Is.EqualTo(1));
        Assert.That(result[1].Position.Start.Y, Is.EqualTo(3));
    }

    [Test]
    public void TokenizeIsRepeatable()
    {
        var lexer = new Lexer("var x = 1;");
        var first = lexer.Tokenize().Select(x => (x.Type, x.Lexeme));
        var second = lexer.Tokenize().Select(x => (x.Type, x.Lexeme));
        Assert.That(second, Is.EqualTo(first));
    }
}
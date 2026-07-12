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
}
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
}
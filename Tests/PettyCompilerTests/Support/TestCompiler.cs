using NUnit.Framework;
using PettyLang.Errors;

namespace PettyCompilerTests.Support;

public static class TestCompiler
{
    public static byte[] Bytes(string source) => PettyLang.Program.Start(source);

    public static Bytecode Compile(string source) => Bytecode.Parse(Bytes(source));

    /// <summary>Wraps statements into an entry point: <c>func Main() { body }</c>.</summary>
    public static string InMain(string body) => $"func Main() {{ {body} }}";

    public static string[] MainListing(string source)
    {
        var bc = Compile(source);
        return bc.Listing(bc.Main);
    }

    /// <summary>Asserts that compilation fails with a compiler <see cref="Error"/> whose message contains the text.</summary>
    public static void AssertCompileError(string source, string messagePart)
    {
        Assert.That(() => Bytes(source), Throws.InstanceOf<Error>().With.Message.Contains(messagePart));
    }
}

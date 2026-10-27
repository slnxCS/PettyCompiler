using PettyLang.AST;
using PettyLang.Compiler;
using PettyLang.Semantic;

namespace PettyLang.Compiler;

public class CompileFabric(Statement[] ast)
{   
    private readonly Statement[] ast = ast;

    public byte[] Build(Analyzer analyzer)
    {
        var compiler = new Compiler(ast, analyzer);
        var byteCode = compiler.Compile(new HeaderCompiler());
        return byteCode;
    }
}
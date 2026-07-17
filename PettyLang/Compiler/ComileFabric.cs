using PettyLang.AST;
using PettyLang.Compiler;
using PettyLang.Semantic;

namespace PettyLang.Compiler;

public class CompileFabric(Statement[] ast)
{   
    private readonly Statement[] ast = ast;

    /*
    [PTVM]
    BC_VER
    CONSTANTS_COUNT
    CONSTANTS_TABLE
    GLOBALS_COUNT
    GLOBAL_TABLE
    HALT
    FUNCTIONS_COUNT
    FUNCTIONS
    BYTE_CODE
    (HALT)
    */

    public byte[] Build(Analyzer analyzer)
    {
        var compiler = new Compiler(ast, analyzer);
        var byteCode = compiler.Comiple(new HeaderCompiler());
        return byteCode;
    }
}
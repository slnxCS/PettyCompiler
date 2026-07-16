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
        var list = new List<byte>();
        var compiler = new Compiler(ast, analyzer);
        var byteCode = compiler.Comiple();
        list.AddRange(new HeaderCompiler().Compile());
        list.AddRange(BitConverter.GetBytes(analyzer.GlobalVariables.Count));
        list.AddRange(compiler.GlobalWriter.GetWritedBytesArray());
        list.Add((byte)OpCode.HALT);
        list.AddRange(BitConverter.GetBytes(analyzer.Functions.Count));
        list.AddRange(compiler.FunctionsWriter.GetWritedBytesArray());
        list.AddRange(byteCode);
        list.Add((byte)OpCode.HALT);
            return list.ToArray();
    }
}
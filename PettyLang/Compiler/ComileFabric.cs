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
    BYTE_CODE
    (HALT)
    */

    public byte[] Build()
    {
        var list = new List<byte>();
        var compiler = new Compiler(ast);
        var byteCode = compiler.Comiple();
        list.AddRange(new HeaderCompiler(compiler.ConstantPool).Compile());
        list.AddRange(BitConverter.GetBytes(BuiltIn.GlobalScope.GetFreeID()));
        list.AddRange(compiler.GlobalWriter.GetWritedBytesArray());
        list.Add((byte)OpCode.HALT);
        list.AddRange(byteCode);
        list.Add((byte)OpCode.HALT);
            return list.ToArray();
    }
}
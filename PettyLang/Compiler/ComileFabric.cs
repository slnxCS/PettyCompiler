using PettyLang.AST;
using PettyLang.Compiler;
using PettyLang.Semantic;

namespace PettyLang.Compiler;

public class CompileFabric(Statement[] ast)
{   
    private readonly Statement[] ast = ast;

    public byte[] Build()
    {
        var list = new List<byte>();
        var compiler = new Compiler(ast);
        var byteCode = compiler.Comiple();
        list.AddRange(new HeaderCompiler((int)BuiltIn.GlobalScope.GetFreeID(), compiler.ConstantPool).Compile());
        list.AddRange(byteCode);
        return list.ToArray();
    }
}
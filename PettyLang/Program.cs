using PettyLang.AST;
using PettyLang.Compiler;
using PettyLang.Errors;
using PettyLang.Semantic;

namespace PettyLang;

public class Program
{
    public const double VERSION = 0.1f;

    public static string FilePath = "";

    public static void Main(string[] args)
    {
#if DEBUG
    args = new string[] {"/home/slnx/PettyLang/Compiler/PettyLang/bin/Debug/net10.0/code.pt"};
#endif
        if (args.Length < 1) 
            throw new ArgumentException("Excepted file name");
        
        var filePath = args[0];

        if (!File.Exists(filePath)) 
            throw new FileNotFoundException($"File '{filePath}' does not exsist");

        FilePath = filePath;

        var outputName = "compiled.pt";

        var source = File.ReadAllText(filePath);

        //try 
        //{
            
            var parser = new Parser.Parser(source);
            var AST = parser.Parse();
            var analyzer = new Analyzer();
            analyzer.Analyze(AST);
            var compiler = new CompileFabric(AST);
            var compiled = compiler.Build(analyzer);
            File.WriteAllBytes(outputName, compiled);
        //}
        //catch (Error error)
        //{
        //    Console.WriteLine(error.Message);
        //    return;
        //}
    }
}

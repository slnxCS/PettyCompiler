using PettyLang.Compiler;
using PettyLang.Errors;
using PettyLang.Semantic;
using System.CommandLine;
using System.CommandLine.Help;
using System.Globalization;

namespace PettyLang;

public class Program
{
    public const string VERSION = "0.1";

    public static byte[] Start(string sourceCode)
    {
        var parser = new Parser.Parser(sourceCode);
        var AST = parser.Parse();
        var constantPool = new ConstantPool();
        var analyzer = new Analyzer(constantPool);
        analyzer.Analyze(AST);
        var compiler = new CompileFabric(AST);
        var compiled = compiler.Build(analyzer);
        return compiled;
    }

    private static async Task<string> GetFileText(string path)
    {
        if (!File.Exists(path)) 
            throw new FileNotFoundException($"File '{path}' does not exist");

        return await File.ReadAllTextAsync(path);
    }

    public static async Task<int> Main(string[] args)
    {
        var sourceOption = new Option<string>("--input", "-i")
             { Description = "Path to file"};
        var outputOption = new Option<string>("--output", "-o")
            { Description = "Path to compiled bytecode"};
        var versionOption = new Option<bool>("--version", "-v") 
            { Description = "Print the current version of compiler"};
        var debugOption = new Option<bool>("--debug", "-d") 
            { Description = "Enable debug"};

        var rootCommand = new RootCommand($"Pettylang compiler v{VERSION}")
            {sourceOption, versionOption, debugOption, outputOption};

        rootCommand.Options.Remove(rootCommand.Options.OfType<VersionOption>().Single());

        rootCommand.SetAction(async (res) =>
        {
            var version = res.GetRequiredValue(versionOption);
            if (version)
            {
                Console.WriteLine($"Compiler version : {VERSION}");
                Console.WriteLine(
                    $"Bytecode version : {HeaderCompiler.BYTECODE_VERSION.ToString(CultureInfo.InvariantCulture)}");
                return;
            }

            var path = res.GetValue(sourceOption);
            var isDebug = res.GetValue(debugOption);
            var outPath = res.GetValue(outputOption);

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(outPath))
            {
                new HelpAction().Invoke(res);
                return;
            }
            
            string src;

            try { src = await GetFileText(path); }
            catch(FileNotFoundException) 
            { 
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"File '{path}' does not exist. Terminating compiler..."); 
                Console.ResetColor();
                return; 
            }

            byte[] compiled;

            if (!isDebug) 
            {
                try 
                {
                    compiled = Start(src);
                }
                catch(Error error)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(error.ToString());
                    Console.ResetColor();
                    return;
                }
                catch (Exception exception)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Compiler exception : {exception}");
                    Console.ResetColor();
                    return;
                }
            }
            else compiled = Start(src);

            await File.WriteAllBytesAsync(outPath, compiled);
        });

        return await rootCommand.Parse(args).InvokeAsync();
    }
}

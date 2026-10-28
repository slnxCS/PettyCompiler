using System.Text;

namespace PettyLang.Compiler;

public class HeaderCompiler
{
    public const float BYTECODE_VERSION = 1.392f;

    public static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("[PTVM]");

    public byte[] Compile(ConstantPool constantPool)
    {
        var list = new List<byte>();
        list.AddRange(MAGIC);
        list.AddRange(BitConverter.GetBytes(BYTECODE_VERSION));
        list.AddRange(BitConverter.GetBytes(constantPool.ConstantsCount));
        list.AddRange(constantPool.GetBytes());

        return list.ToArray();
    }
}
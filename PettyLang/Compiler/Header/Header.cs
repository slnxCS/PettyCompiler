using System.Text;

namespace PettyLang.Compiler;

public class HeaderCompiler
{
    public const float BYTE_CODE_VERSION = 1.2f;

    public static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("[PTVM]");

    public byte[] Compile()
    {
        var list = new List<byte>();
        list.AddRange(MAGIC);
        list.AddRange(BitConverter.GetBytes(BYTE_CODE_VERSION));
        list.AddRange(BitConverter.GetBytes(ConstantPool.ConstantsCount));
        list.AddRange(ConstantPool.GetBytes());

        return list.ToArray();
    }
}
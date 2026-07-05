using System.Text;

namespace PettyLang.Compiler;

public class HeaderCompiler(int globalsLenght, ConstantPool constantPool)
{
    private readonly int GlobalsLenght = globalsLenght;
    private readonly ConstantPool ConstantPool = constantPool;
    public const int BYTE_CODE_VERSION = 1;

    public static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("[PTVM]");

    public byte[] CompileWithByteCode(byte[] byteCode)
    {
        var list = new List<byte>();
        list.AddRange(MAGIC);
        list.AddRange(BitConverter.GetBytes(BYTE_CODE_VERSION));
        list.AddRange(BitConverter.GetBytes(GlobalsLenght));
        list.AddRange(BitConverter.GetBytes(ConstantPool.ConstantsCount));
        list.AddRange(ConstantPool.GetBytes());
        list.AddRange(byteCode);

        return list.ToArray();
    }
}
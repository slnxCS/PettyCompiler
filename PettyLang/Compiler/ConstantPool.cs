using PettyLang.Semantic;
namespace PettyLang.Compiler;

public enum ConstantType : byte
{
    CONSTANT_INT = 0,
    CONSTANT_FLOAT = 1,
}

public abstract record Constant(ConstantType type)
{
    public abstract byte[] GetBytes();
}
public record IntConstant(int value) : Constant(ConstantType.CONSTANT_INT)
{
    public override byte[] GetBytes()
    {
        return BitConverter.GetBytes(value);
    }
}

public record FloatConstant(float value) : Constant(ConstantType.CONSTANT_FLOAT)
{
    public override byte[] GetBytes()
    {
        return BitConverter.GetBytes(value);
    }
}

public class ConstantPool
{
    private readonly Dictionary<Constant, int> lookup = new();
    private readonly List<Constant> constants = new();

    public int Add(Constant constant)
    {
        if (lookup.TryGetValue(constant, out var index))
            return index;

        index = constants.Count;

        constants.Add(constant);
        lookup[constant] = index;

        return index;
    }

    public Constant this[int index] => constants[index];

    public int ConstantsCount => constants.Count;

    public byte[] GetBytes()
    {
        var list = new List<byte>();
        foreach (var constant in constants)
        {
            list.Add((byte)constant.type);
            list.AddRange(constant.GetBytes());
        }

        return list.ToArray();
    }
}
namespace PettyLang.Compiler;

public interface IBinaryWriter
{
    public void WriteByte(byte b);
    public void Emit(OpCode opCode);
    public void Emit(int number);

    public IReadOnlyList<byte> GetWritedBytes();
    public byte[] GetWritedBytesArray();
}

public class BinaryWriter : IBinaryWriter
{
    private List<byte> writed = new();

    public IReadOnlyList<byte> GetWritedBytes() => writed;

    public void WriteByte(byte b)
    {
        writed.Add(b);
    }

    public void Emit(int number)
    {
        writed.AddRange(BitConverter.GetBytes(number));
    }

    public void Emit(OpCode opCode)
    {
        writed.AddRange((byte)opCode);
    }

    public byte[] GetWritedBytesArray()
    {
        return writed.ToArray();
    }
}
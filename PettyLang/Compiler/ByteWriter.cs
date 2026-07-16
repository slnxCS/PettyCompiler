namespace PettyLang.Compiler;

public interface IByteWriter
{
    public void WriteByte(byte _byte);
    public void WriteBytes(byte[] bytes);
    public void Emit(OpCode opCode);
    public void Emit(int number);
    public void Emit(float number);

    public IReadOnlyList<byte> GetWritedBytes();
    public byte[] GetWritedBytesArray();
}

public class ByteWriter : IByteWriter
{
    private List<byte> writed = new();

    public IReadOnlyList<byte> GetWritedBytes() => writed;

    public void WriteByte(byte b)
    {
        writed.Add(b);
    }

    public void WriteBytes(byte[] b)
    {
        writed.AddRange(b);
    }

    public void Emit(int number)
    {
        writed.AddRange(BitConverter.GetBytes(number));
    }

    public void Emit(float number)
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
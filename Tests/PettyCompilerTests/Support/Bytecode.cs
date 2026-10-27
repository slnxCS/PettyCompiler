using System.Globalization;
using System.Text;
using PettyLang.Compiler;

namespace PettyCompilerTests.Support;

public record Instruction(int Offset, OpCode Op, int[] Operands);

public record ClassEntry(string Name, int Id, int ParentId, int FieldCount, List<List<Instruction>> Methods);

/// <summary>
/// Test-side reader of the PTVM file format. Parses the whole file and fails on any structural mismatch,
/// so every test that goes through it also checks the overall layout of the output.
/// </summary>
public class Bytecode
{
    // Number of i32 operands of every instruction the compiler can emit
    private static readonly Dictionary<OpCode, int> operandCounts = new()
    {
        {OpCode.PUSH_CONSTANT, 1},
        {OpCode.STORE_LOCAL, 1},
        {OpCode.STORE_GLOBAL, 1},
        {OpCode.LOAD_LOCAL, 1},
        {OpCode.LOAD_GLOBAL, 1},
        {OpCode.SYS_CALL, 2},
        {OpCode.RET, 0},
        {OpCode.CALL, 2},
        {OpCode.RESERVE_LOCALS, 1},
        {OpCode.ADD_INT, 0},
        {OpCode.SUB_INT, 0},
        {OpCode.DIV_INT, 0},
        {OpCode.MUL_INT, 0},
        {OpCode.ADD_FLOAT, 0},
        {OpCode.SUB_FLOAT, 0},
        {OpCode.MUL_FLOAT, 0},
        {OpCode.DIV_FLOAT, 0},
        {OpCode.HALT, 0},
        {OpCode.CAST_FROM_FLOAT32_TO_INT32, 0},
        {OpCode.CAST_FROM_INT32_TO_FLOAT32, 0},
        {OpCode.JMP_IF_FALSE, 1},
        {OpCode.JMP_IF_TRUE, 1},
        {OpCode.JMP, 1},
        {OpCode.INT_EQ, 0},
        {OpCode.ALLOCATE_OBJ, 1},
        {OpCode.CALL_METHOD, 2},
        {OpCode.LOAD_FIELD, 1},
        {OpCode.STORE_FIELD, 1},
        {OpCode.ALLOCATE_ARR, 0},
        {OpCode.ARR_GET, 0},
        {OpCode.ARR_SET, 0},
    };

    private static readonly HashSet<OpCode> jumps = [OpCode.JMP_IF_FALSE, OpCode.JMP_IF_TRUE, OpCode.JMP];

    public string Magic = "";
    public float Version;
    public readonly List<object> Constants = new();
    public readonly List<List<Instruction>> Functions = new();
    public readonly List<ClassEntry> Classes = new();
    public int GlobalCount;
    public List<Instruction> GlobalInit = new();
    public List<Instruction> Entry = new();

    public List<Instruction> Main
    {
        get
        {
            var call = Entry[0];
            if (call.Op != OpCode.CALL)
                throw new InvalidDataException($"Entry section must start with CALL, not {call.Op}");
            return Functions[call.Operands[0]];
        }
    }

    public ClassEntry Class(string name) => Classes.Single(x => x.Name == name);

    private readonly byte[] bytes;
    private int position;

    private Bytecode(byte[] bytes)
    {
        this.bytes = bytes;
    }

    public static Bytecode Parse(byte[] bytes)
    {
        var bc = new Bytecode(bytes);
        bc.parse();
        return bc;
    }

    private void parse()
    {
        Magic = Encoding.ASCII.GetString(take(6));
        Version = BitConverter.ToSingle(take(4));

        var constCount = readInt();
        for (int i = 0; i < constCount; i++)
            Constants.Add(readConstant());

        var funcCount = readInt();
        for (int i = 0; i < funcCount; i++)
            Functions.Add(readCode());

        var classCount = readInt();
        for (int i = 0; i < classCount; i++)
        {
            var name = Encoding.UTF8.GetString(take(readInt()));
            var id = readInt();
            var parentId = readInt();
            var fieldCount = readInt();
            var methodCount = readInt();
            var methods = new List<List<Instruction>>();
            for (int j = 0; j < methodCount; j++)
                methods.Add(readCode());
            Classes.Add(new(name, id, parentId, fieldCount, methods));
        }

        GlobalCount = readInt();
        GlobalInit = readCode();
        Entry = readCode();

        if (position != bytes.Length)
            throw new InvalidDataException($"{bytes.Length - position} unexpected trailing bytes at 0x{position:X}");
    }

    private object readConstant()
    {
        var type = (ConstantType)take(1)[0];
        return type switch
        {
            ConstantType.CONSTANT_INT => readInt(),
            ConstantType.CONSTANT_FLOAT => BitConverter.ToSingle(take(4)),
            ConstantType.CONSTANT_BOOL => take(1)[0] switch
            {
                0 => false,
                1 => true,
                var b => throw new InvalidDataException($"Invalid bool constant {b}"),
            },
            _ => throw new InvalidDataException($"Unknown constant type {(byte)type} at 0x{position - 1:X}"),
        };
    }

    // Reads instructions up to and including HALT
    private List<Instruction> readCode()
    {
        var list = new List<Instruction>();
        while (true)
        {
            var offset = position;
            var op = (OpCode)take(1)[0];
            if (!operandCounts.TryGetValue(op, out var count))
                throw new InvalidDataException($"Unknown opcode {(byte)op} at 0x{offset:X}");

            var operands = new int[count];
            for (int i = 0; i < count; i++)
                operands[i] = readInt();

            list.Add(new(offset, op, operands));
            if (op == OpCode.HALT) return list;
        }
    }

    private int readInt() => BitConverter.ToInt32(take(4));

    private byte[] take(int count)
    {
        if (position + count > bytes.Length)
            throw new InvalidDataException($"Unexpected end of file at 0x{position:X} (needed {count} bytes)");
        var res = bytes[position..(position + count)];
        position += count;
        return res;
    }

    /// <summary>
    /// Human readable listing: constants are shown by value (int "2", float "2.5f", bool "true"),
    /// jump targets as "@index" of the target instruction inside the same block ("@?0x41" if the
    /// target is not an instruction boundary of the block).
    /// </summary>
    public string[] Listing(List<Instruction> code)
    {
        var indexByOffset = code.Select((x, i) => (x.Offset, i)).ToDictionary(x => x.Offset, x => x.i);
        return code.Select(x =>
        {
            if (x.Op == OpCode.PUSH_CONSTANT)
                return $"PUSH_CONSTANT {formatConstant(Constants[x.Operands[0]])}";
            if (jumps.Contains(x.Op))
                return indexByOffset.TryGetValue(x.Operands[0], out var target)
                    ? $"{x.Op} @{target}"
                    : $"{x.Op} @?0x{x.Operands[0]:X}";
            return string.Join(' ', [x.Op.ToString(), .. x.Operands.Select(o => o.ToString())]);
        }).ToArray();
    }

    private static string formatConstant(object constant) => constant switch
    {
        int i => i.ToString(CultureInfo.InvariantCulture),
        float f => f.ToString(CultureInfo.InvariantCulture) + "f",
        bool b => b ? "true" : "false",
        _ => throw new InvalidOperationException(),
    };
}

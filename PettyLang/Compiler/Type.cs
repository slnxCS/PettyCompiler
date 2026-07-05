using PettyLang.Compiler;
using PettyLang.Semantic;

namespace PettyLang.Types;

public enum TypeKind
{
    Primitive,
    Class
}

public class PettyType
{
    public readonly TypeKind Kind;
    public readonly string Name;
    public readonly int ByteSize;
    public readonly Symbol Resolved;

    public PettyType(TypeKind kind, string name, int byteSize, Symbol resolved)
    {
        Kind = kind;
        Name = name;
        ByteSize = byteSize;
        Resolved = resolved;
    }

    public static PettyType Int32 => new(TypeKind.Primitive, "int32", 4, BuiltIn.Int32Class);
}
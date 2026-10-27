using NUnit.Framework;
using PettyLang.Compiler;
using static PettyCompilerTests.Support.TestCompiler;

[TestFixture]
public class CodegenTests
{
    // ---------- File layout ----------

    [Test]
    public void Header()
    {
        var bc = Compile("func Main() { }");
        Assert.That(bc.Magic, Is.EqualTo("[PTVM]"));
        Assert.That(bc.Version, Is.EqualTo(HeaderCompiler.BYTECODE_VERSION));
    }

    [Test]
    public void MinimalProgram()
    {
        var bc = Compile("func Main() { }");
        Assert.That(bc.Constants, Is.Empty);
        Assert.That(bc.Functions, Has.Count.EqualTo(1));
        Assert.That(bc.Listing(bc.Functions[0]), Is.EqualTo(new[] {"RESERVE_LOCALS 0", "RET", "HALT"}));
        Assert.That(bc.GlobalCount, Is.EqualTo(0));
        Assert.That(bc.Listing(bc.GlobalInit), Is.EqualTo(new[] {"HALT"}));
        Assert.That(bc.Listing(bc.Entry), Is.EqualTo(new[] {"CALL 0 0", "HALT"}));
    }

    [Test]
    public void EntryCallsMain()
    {
        var bc = Compile("func a() { } func b() { } func Main() { } func c() { }");
        Assert.That(bc.Functions, Has.Count.EqualTo(4));
        Assert.That(bc.Listing(bc.Entry), Is.EqualTo(new[] {"CALL 2 0", "HALT"}));
    }

    [Test]
    public void FunctionsAreStoredInIdOrder()
    {
        var bc = Compile("func a() { print(1); } func b() { print(2); } func Main() { b(); a(); }");
        Assert.That(bc.Listing(bc.Functions[0]), Does.Contain("PUSH_CONSTANT 1"));
        Assert.That(bc.Listing(bc.Functions[1]), Does.Contain("PUSH_CONSTANT 2"));
        Assert.That(bc.Listing(bc.Main), Is.EqualTo(new[] {"RESERVE_LOCALS 0", "CALL 1 0", "CALL 0 0", "RET", "HALT"}));
    }

    [Test]
    public void CompilationIsDeterministic()
    {
        const string a = "class P : { var x = 1; func m() : int32 { return self.x; } } func Main() { var p = P(); print(p.m()); }";
        const string b = "var g = 3; func f(x : int32) : int32 { return x * g; } func Main() { print(f(2)); }";

        var first = Bytes(a);
        Bytes(b);
        Assert.That(Bytes(a), Is.EqualTo(first));
    }

    // ---------- Constants ----------

    [Test]
    public void ConstantsAreDeduplicated()
    {
        var bc = Compile(InMain("var a = 7; var b = 7; var c = 7;"));
        Assert.That(bc.Constants, Is.EqualTo(new object[] {7}));
    }

    [Test]
    public void ConstantKinds()
    {
        var bc = Compile(InMain("var a = 1; var b = true; var c = false; var d = 1;"));
        Assert.That(bc.Constants, Is.EqualTo(new object[] {1, true, false}));
    }

    // Float literals depend on the current culture (see KnownBugTests.FloatLiteralDoesNotDependOnCulture)
    [Test, SetCulture("ru-RU")]
    public void FloatConstant()
    {
        var bc = Compile(InMain("var a = 2.5; var b = -0.25;"));
        Assert.That(bc.Constants, Is.EqualTo(new object[] {2.5f, -0.25f}));
    }

    // ---------- Variables ----------

    [Test]
    public void LocalVariable()
    {
        Assert.That(MainListing(InMain("var x = 2; print(x);")), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 1",
            "PUSH_CONSTANT 2",
            "STORE_LOCAL 0",
            "LOAD_LOCAL 0",
            "SYS_CALL 0 1",
            "RET",
            "HALT",
        }));
    }

    [Test]
    public void Assignment()
    {
        Assert.That(MainListing(InMain("var x = 1; x = x + 2;")), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 1",
            "PUSH_CONSTANT 1",
            "STORE_LOCAL 0",
            "LOAD_LOCAL 0",
            "PUSH_CONSTANT 2",
            "ADD_INT",
            "STORE_LOCAL 0",
            "RET",
            "HALT",
        }));
    }

    [Test]
    public void LocalsOfNestedBlocksShareFunctionSlots()
    {
        var listing = MainListing(InMain("var a = 1; { var b = 2; } if true : { var c = 3; }"));
        Assert.That(listing[0], Is.EqualTo("RESERVE_LOCALS 3"));
        Assert.That(listing.Where(x => x.StartsWith("STORE_LOCAL")),
            Is.EqualTo(new[] {"STORE_LOCAL 0", "STORE_LOCAL 1", "STORE_LOCAL 2"}));
    }

    [Test]
    public void ParametersOccupyFirstSlots()
    {
        var bc = Compile("func f(a : int32, b : int32) { var c = a; } func Main() { }");
        Assert.That(bc.Listing(bc.Functions[0]), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 3",
            "LOAD_LOCAL 0",
            "STORE_LOCAL 2",
            "RET",
            "HALT",
        }));
    }

    [Test]
    public void GlobalVariables()
    {
        var bc = Compile("var g = 5; var h = true; func Main() { print(g); g = 6; print(h); }");
        Assert.That(bc.GlobalCount, Is.EqualTo(2));
        Assert.That(bc.Listing(bc.GlobalInit), Is.EqualTo(new[]
        {
            "PUSH_CONSTANT 5",
            "STORE_GLOBAL 0",
            "PUSH_CONSTANT true",
            "STORE_GLOBAL 1",
            "HALT",
        }));
        Assert.That(bc.Listing(bc.Main), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 0",
            "LOAD_GLOBAL 0",
            "SYS_CALL 0 1",
            "PUSH_CONSTANT 6",
            "STORE_GLOBAL 0",
            "LOAD_GLOBAL 1",
            "SYS_CALL 2 1",
            "RET",
            "HALT",
        }));
    }

    // ---------- Expressions ----------

    [TestCase("+", "ADD_INT")]
    [TestCase("-", "SUB_INT")]
    [TestCase("*", "MUL_INT")]
    [TestCase("/", "DIV_INT")]
    [TestCase("==", "INT_EQ")]
    public void IntOperators(string op, string opcode)
    {
        Assert.That(MainListing(InMain($"var r = 6 {op} 3;")), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 1",
            "PUSH_CONSTANT 6",
            "PUSH_CONSTANT 3",
            opcode,
            "STORE_LOCAL 0",
            "RET",
            "HALT",
        }));
    }

    [TestCase("+", "ADD_FLOAT")]
    [TestCase("-", "SUB_FLOAT")]
    [TestCase("*", "MUL_FLOAT")]
    [TestCase("/", "DIV_FLOAT")]
    public void FloatOperators(string op, string opcode)
    {
        Assert.That(MainListing(InMain($"var a = 6 as float32; var r = a {op} a;")), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 2",
            "PUSH_CONSTANT 6",
            "CAST_FROM_INT32_TO_FLOAT32",
            "STORE_LOCAL 0",
            "LOAD_LOCAL 0",
            "LOAD_LOCAL 0",
            opcode,
            "STORE_LOCAL 1",
            "RET",
            "HALT",
        }));
    }

    [Test]
    public void ExpressionsAreCompiledInPostfixOrder()
    {
        Assert.That(MainListing(InMain("var r = 1 + 2 * 3 - 4;")), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 1",
            "PUSH_CONSTANT 1",
            "PUSH_CONSTANT 2",
            "PUSH_CONSTANT 3",
            "MUL_INT",
            "ADD_INT",
            "PUSH_CONSTANT 4",
            "SUB_INT",
            "STORE_LOCAL 0",
            "RET",
            "HALT",
        }));
    }

    [Test]
    public void Casts()
    {
        Assert.That(MainListing(InMain("var i = (2 as float32) as int32;")), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 1",
            "PUSH_CONSTANT 2",
            "CAST_FROM_INT32_TO_FLOAT32",
            "CAST_FROM_FLOAT32_TO_INT32",
            "STORE_LOCAL 0",
            "RET",
            "HALT",
        }));
    }

    // ---------- Calls ----------

    [TestCase("print(1);", "SYS_CALL 0 1")]
    [TestCase("print(1 as float32);", "SYS_CALL 1 1")]
    [TestCase("print(true);", "SYS_CALL 2 1")]
    [TestCase("read();", "SYS_CALL 3 0")]
    [TestCase("random(1, 2);", "SYS_CALL 4 2")]
    [TestCase("var a = Array(1); print(get(a, 0));", "SYS_CALL 5 2")]
    [TestCase("var a = Array(1); set(a, 0, 1);", "SYS_CALL 6 3")]
    public void BuiltinFunctionIds(string statements, string expected)
    {
        Assert.That(MainListing(InMain(statements)), Does.Contain(expected));
    }

    [Test]
    public void ArraysAndBuiltins()
    {
        Assert.That(MainListing(InMain("var a = Array(3); set(a, 0, 7); print(get(a, 0));")), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 1",
            "PUSH_CONSTANT 3",
            "ALLOCATE_ARR",
            "STORE_LOCAL 0",
            "LOAD_LOCAL 0",
            "PUSH_CONSTANT 0",
            "PUSH_CONSTANT 7",
            "SYS_CALL 6 3",
            "LOAD_LOCAL 0",
            "PUSH_CONSTANT 0",
            "SYS_CALL 5 2",
            "SYS_CALL 0 1",
            "RET",
            "HALT",
        }));
    }

    [Test]
    public void UserFunctionCall()
    {
        var bc = Compile("func add(a : int32, b : int32) : int32 { return a + b; } func Main() { print(add(1, 2)); }");
        Assert.That(bc.Listing(bc.Functions[0]), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 2",
            "LOAD_LOCAL 0",
            "LOAD_LOCAL 1",
            "ADD_INT",
            "RET",
            "RET",
            "HALT",
        }));
        Assert.That(bc.Listing(bc.Main), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 0",
            "PUSH_CONSTANT 1",
            "PUSH_CONSTANT 2",
            "CALL 0 2",
            "SYS_CALL 0 1",
            "RET",
            "HALT",
        }));
    }

    [Test]
    public void OverloadResolution()
    {
        var bc = Compile("func f(a : int32) { } func f(a : float32) { } func f(a : int32, b : int32) { } " +
            "func Main() { f(1 as float32); f(1); f(1, 1); }");
        Assert.That(bc.Listing(bc.Main).Where(x => x.StartsWith("CALL")),
            Is.EqualTo(new[] {"CALL 1 1", "CALL 0 1", "CALL 2 2"}));
    }

    [Test]
    public void UserOverloadOfBuiltin()
    {
        var bc = Compile("func print(a : int32, b : int32) { } func Main() { print(1, 2); print(3); }");
        Assert.That(bc.Listing(bc.Main).Where(x => x.Contains("CALL")), Is.EqualTo(new[] {"CALL 0 2", "SYS_CALL 0 1"}));
    }

    [Test]
    public void Recursion()
    {
        var bc = Compile("func f(n : int32) : int32 { return f(n); } func Main() { print(f(1)); }");
        Assert.That(bc.Listing(bc.Functions[0]), Does.Contain("CALL 0 1"));
    }

    // ---------- Control flow ----------

    [Test]
    public void If()
    {
        Assert.That(MainListing(InMain("var x = 2; if x == 2 : { print(x); } print(1);")), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 1",
            "PUSH_CONSTANT 2",
            "STORE_LOCAL 0",
            "LOAD_LOCAL 0",
            "PUSH_CONSTANT 2",
            "INT_EQ",
            "JMP_IF_FALSE @9",
            "LOAD_LOCAL 0",
            "SYS_CALL 0 1",
            "PUSH_CONSTANT 1",
            "SYS_CALL 0 1",
            "RET",
            "HALT",
        }));
    }

    [Test]
    public void SequentialIfs()
    {
        Assert.That(MainListing(InMain("if true : { print(1); } if false : { print(2); }")), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 0",
            "PUSH_CONSTANT true",
            "JMP_IF_FALSE @5",
            "PUSH_CONSTANT 1",
            "SYS_CALL 0 1",
            "PUSH_CONSTANT false",
            "JMP_IF_FALSE @9",
            "PUSH_CONSTANT 2",
            "SYS_CALL 0 1",
            "RET",
            "HALT",
        }));
    }

    [Test]
    public void IfWithEmptyBody()
    {
        Assert.That(MainListing(InMain("if true : { }")), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 0",
            "PUSH_CONSTANT true",
            "JMP_IF_FALSE @3",
            "RET",
            "HALT",
        }));
    }

    [Test]
    public void JumpTargetsAreAbsoluteFileOffsets()
    {
        // Jumps in a function other than the first one must still point inside that function
        var bc = Compile("func f() { print(1); } func Main() { if true : { print(2); } }");
        var jump = bc.Main.Single(x => x.Op == OpCode.JMP_IF_FALSE);
        Assert.That(bc.Main.Select(x => x.Offset), Does.Contain(jump.Operands[0]));
    }

    [Test]
    public void ReturnInsideIf()
    {
        var bc = Compile("func f(a : int32) : int32 { if a == 0 : { return 1; } return a; } func Main() { }");
        Assert.That(bc.Listing(bc.Functions[0]), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 1",
            "LOAD_LOCAL 0",
            "PUSH_CONSTANT 0",
            "INT_EQ",
            "JMP_IF_FALSE @7",
            "PUSH_CONSTANT 1",
            "RET",
            "LOAD_LOCAL 0",
            "RET",
            "RET",
            "HALT",
        }));
    }

    [Test]
    public void EmptyReturn()
    {
        Assert.That(MainListing(InMain("return;")), Is.EqualTo(new[] {"RESERVE_LOCALS 0", "RET", "RET", "HALT"}));
    }

    // ---------- Classes ----------

    [Test]
    public void BuiltinClassTable()
    {
        var bc = Compile("func Main() { }");
        Assert.That(bc.Classes.Select(x => (x.Name, x.Id, x.ParentId, x.FieldCount, x.Methods.Count)), Is.EqualTo(new[]
        {
            ("Object", 0, -1, 0, 0),
            ("ValueObject", 1, 0, 0, 0),
            ("void", 2, 0, 0, 0),
            ("Array", 3, 0, 0, 0),
            ("int32", 4, 1, 0, 0),
            ("float32", 5, 1, 0, 0),
            ("bool", 6, 1, 0, 0),
            ("function", 7, 0, 0, 0),
        }));
    }

    [Test]
    public void UserClassesFollowBuiltins()
    {
        var bc = Compile("class A : { var x = 1; var y : int32; } class B : A { } func Main() { }");
        Assert.That(bc.Classes.Select(x => x.Id), Is.Ordered);
        Assert.That(bc.Class("A"), Is.EqualTo(bc.Class("A") with {Id = 8, ParentId = -1, FieldCount = 2}));
        Assert.That(bc.Class("B"), Is.EqualTo(bc.Class("B") with {Id = 9, ParentId = 8, FieldCount = 0}));
    }

    [Test]
    public void UninstantiatedClassHasNoConstructor()
    {
        var bc = Compile("class A : { var x = 1; func m() { } } func Main() { }");
        Assert.That(bc.Class("A").Methods, Has.Count.EqualTo(1));
    }

    const string point = """
        class Point : {
            var x = 0;
            var y : int32;

            func sum() : int32 {
                return self.x + self.y;
            }

            func move(dx : int32) {
                self.x = self.x + dx;
            }
        }

        func Main() {
            var p = Point();
            p.y = 4;
            p.move(3);
            print(p.sum());
        }
        """;

    [Test]
    public void Methods()
    {
        var bc = Compile(point);
        var methods = bc.Class("Point").Methods;
        Assert.That(bc.Listing(methods[0]), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 1",
            "LOAD_LOCAL 0",
            "LOAD_FIELD 0",
            "LOAD_LOCAL 0",
            "LOAD_FIELD 1",
            "ADD_INT",
            "RET",
            "RET",
            "HALT",
        }));
        Assert.That(bc.Listing(methods[1]), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 2",
            "LOAD_LOCAL 0",
            "LOAD_FIELD 0",
            "LOAD_LOCAL 1",
            "ADD_INT",
            "LOAD_LOCAL 0",
            "STORE_FIELD 0",
            "RET",
            "HALT",
        }));
    }

    [Test]
    public void BaseConstructorInitializesFields()
    {
        var bc = Compile(point);
        var methods = bc.Class("Point").Methods;
        Assert.That(methods, Has.Count.EqualTo(3));
        Assert.That(bc.Listing(methods[2]), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 1",
            "PUSH_CONSTANT 0",
            "LOAD_LOCAL 0",
            "STORE_FIELD 0",
            "LOAD_LOCAL 0",
            "RET",
            "RET",
            "HALT",
        }));
    }

    [Test]
    public void ObjectCreationFieldsAndMethodCalls()
    {
        Assert.That(MainListing(point), Is.EqualTo(new[]
        {
            "RESERVE_LOCALS 1",
            "ALLOCATE_OBJ 8",
            "CALL_METHOD 2 1",
            "STORE_LOCAL 0",
            "PUSH_CONSTANT 4",
            "LOAD_LOCAL 0",
            "STORE_FIELD 1",
            "LOAD_LOCAL 0",
            "PUSH_CONSTANT 3",
            "CALL_METHOD 1 2",
            "LOAD_LOCAL 0",
            "CALL_METHOD 0 1",
            "SYS_CALL 0 1",
            "RET",
            "HALT",
        }));
    }

    [Test]
    public void NestedFieldAccess()
    {
        var bc = Compile("class In : { var v = 1; } class Out : { var i : In; } " +
            "func Main() { var o = Out(); o.i = In(); print(o.i.v); }");
        var listing = bc.Listing(bc.Main);
        Assert.That(listing[^6..^1], Is.EqualTo(new[] {"LOAD_LOCAL 0", "LOAD_FIELD 0", "LOAD_FIELD 0", "SYS_CALL 0 1", "RET"}));
    }

    // ---------- Whole programs ----------

    [Test]
    public void ArrayRecursionExample()
    {
        const string source = """
            func array_write(ar : Array, index : int32, stopIndex : int32) : {
                set(ar, index, read());
                index = index + 1;
                if index == stopIndex : {
                    return;
                }
                array_write(ar, index, stopIndex);
            }

            func print_element(ar : Array, index : int32, stopIndex : int32) : {
                print(get(ar, index));
                index = index + 1;
                if index == stopIndex : {
                    return;
                }
                print_element(ar, index, stopIndex);
            }

            func print(ar : Array, len : int32) : {
                print_element(ar, 0, len - 1);
            }

            func Main() : {
                var len = 10;
                var ar = Array(len);
                array_write(ar, 0, len - 1);
                print(ar, len);
            }
            """;
        var bc = Compile(source);
        Assert.That(bc.Functions, Has.Count.EqualTo(4));
        Assert.That(bc.Listing(bc.Entry), Is.EqualTo(new[] {"CALL 3 0", "HALT"}));
        Assert.That(bc.Listing(bc.Main), Does.Contain("CALL 2 2"));
    }
}

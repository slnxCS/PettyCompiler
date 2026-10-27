using NUnit.Framework;
using PettyLang.Errors;
using static PettyCompilerTests.Support.TestCompiler;

[TestFixture]
public class SemanticTests
{
    // ---------- Entry point ----------

    [Test]
    public void ProgramWithoutMain()
    {
        AssertCompileError("func f() { }", "Program should have Main Function");
    }

    [Test]
    public void EmptyProgram()
    {
        AssertCompileError("", "Program should have Main Function");
    }

    [Test]
    public void MainWithParameters()
    {
        AssertCompileError("func Main(a : int32) { }", "Main function should not take any parameters");
    }

    [Test]
    public void MainReturningValue()
    {
        AssertCompileError("func Main() : int32 { return 1; }", "Main function must return void");
    }

    [Test]
    public void MainAsMethod()
    {
        AssertCompileError("class A : { func Main() { } } func Main() { }", "Main entry point should be function, not method");
    }

    [Test]
    public void SecondMain()
    {
        AssertCompileError("func Main() { } func Main() { }", "already exist");
    }

    // ---------- Names ----------

    [Test]
    public void UnknownName()
    {
        AssertCompileError(InMain("print(x);"), "Name 'x' does not exist in current context");
    }

    [Test]
    public void FunctionUsedBeforeDeclaration()
    {
        AssertCompileError("func Main() { f(); } func f() { }", "Name 'f' does not exist in current context");
    }

    [Test]
    public void VariableOutOfScope()
    {
        AssertCompileError(InMain("{ var a = 1; } print(a);"), "Name 'a' does not exist");
    }

    [Test]
    public void VariableFromOtherFunction()
    {
        AssertCompileError("func f() { var a = 1; } func Main() { print(a); }", "Name 'a' does not exist");
    }

    [Test]
    public void DuplicateVariableInSameScope()
    {
        AssertCompileError(InMain("var a = 1; var a = 2;"), "Variable 'a' is already exist");
    }

    [Test]
    public void DuplicateGlobalVariable()
    {
        AssertCompileError("var a = 1; var a = 2; func Main() { }", "Variable 'a' is already exist");
    }

    [Test]
    public void ParameterAndLocalWithSameName()
    {
        AssertCompileError("func f(a : int32) { var a = 1; } func Main() { }", "Variable 'a' is already exist");
    }

    [Test]
    public void ShadowingInNestedBlockIsAllowed()
    {
        Assert.That(() => Bytes(InMain("var a = 1; { var a = true; print(a); } print(a);")), Throws.Nothing);
    }

    [Test]
    public void LocalShadowsGlobal()
    {
        Assert.That(() => Bytes("var a = 1; func Main() { var a = true; print(a); }"), Throws.Nothing);
    }

    [Test]
    public void StatementOutsideFunction()
    {
        AssertCompileError("print(1); func Main() { }", "Cannot do this operation outside a function");
    }

    [Test]
    public void AssignmentOutsideFunction()
    {
        AssertCompileError("var a = 1; a = 2; func Main() { }", "Cannot do this operation outside a function");
    }

    [Test]
    public void ReturnOutsideFunction()
    {
        AssertCompileError("return; func Main() { }", "Cannot do this operation outside a function");
    }

    // ---------- Types ----------

    [Test]
    public void DeclaredTypeMismatch()
    {
        AssertCompileError(InMain("var x : bool = 1;"), "Cannot convert from 'int32' to 'bool'");
    }

    [Test]
    public void NoImplicitIntToFloat()
    {
        AssertCompileError(InMain("var x : float32 = 1;"), "Cannot convert from 'int32' to 'float32'");
    }

    [Test]
    public void AssignmentTypeMismatch()
    {
        AssertCompileError(InMain("var x = 1; x = true;"), "Cannot convert from bool to int32");
    }

    [Test]
    public void AssignmentToFunction()
    {
        AssertCompileError(InMain("print = 1;"), "The assignment operation cannot be applied");
    }

    [Test]
    public void VoidVariable()
    {
        AssertCompileError("func f() { } func Main() { var x = f(); }", "cannot assign a value of type void to a variable");
    }

    [Test]
    public void ValueUsedAsType()
    {
        AssertCompileError("var t = 1; func Main() { var x : t = 1; }", "Cannot use 't' as type");
    }

    [Test]
    public void StringLiteralWithoutStringClass()
    {
        AssertCompileError(InMain("var s = \"hi\";"), "import the String class");
    }

    [TestCase("1 + true", "+")]
    [TestCase("true + true", "+")]
    [TestCase("true == true", "==")]
    [TestCase("1 + (1 as float32)", "+")]
    [TestCase("(1 as float32) == (1 as float32)", "==")]
    public void UnsupportedOperands(string expression, string op)
    {
        Assert.That(() => Bytes(InMain($"var x = {expression};")),
            Throws.InstanceOf<NotSupportedOperandsError>().With.Message.Contains($"Cannot use operator {op}"));
    }

    [TestCase("true as int32")]
    [TestCase("1 as bool")]
    [TestCase("true as float32")]
    public void UnsupportedCasts(string expression)
    {
        Assert.That(() => Bytes(InMain($"var x = {expression};")),
            Throws.InstanceOf<NotSupportedCastTypeError>().With.Message.Contains("Cannot cast"));
    }

    [Test]
    public void CastToValueInsteadOfType()
    {
        AssertCompileError("var t = 1; func Main() { var x = 1 as t; }", "Expected type");
    }

    [TestCase("var a = 1 + 2;")]
    [TestCase("var a = 6 / 3;")]
    [TestCase("var a : bool = 1 == 2;")]
    [TestCase("var a : float32 = 1 as float32;")]
    [TestCase("var a : int32 = (1 as float32) as int32;")]
    [TestCase("var a : int32 = 1 as int32;")]
    [TestCase("var a : float32 = (1 as float32) * (2 as float32);")]
    public void ValidExpressions(string statement)
    {
        Assert.That(() => Bytes(InMain(statement)), Throws.Nothing);
    }

    [Test]
    public void IntDivisionHasFloatType()
    {
        Assert.That(() => Bytes(InMain("var a : float32 = 6 / 3;")), Throws.Nothing);
        AssertCompileError(InMain("var a : int32 = 6 / 3;"), "Cannot convert from 'float32' to 'int32'");
    }

    [Test]
    public void IfAcceptsBoolCondition()
    {
        Assert.That(() => Bytes(InMain("if 1 == 1 : { }")), Throws.Nothing);
        Assert.That(() => Bytes(InMain("if true : { }")), Throws.Nothing);
    }

    // ---------- Functions ----------

    [Test]
    public void MissingReturn()
    {
        AssertCompileError("func f() : int32 { } func Main() { }", "not all code paths return a value");
    }

    [Test]
    public void ReturnValueFromVoidFunction()
    {
        AssertCompileError(InMain("return 1;"), "Cannot convert");
    }

    [Test]
    public void NoMatchingOverload()
    {
        AssertCompileError(InMain("print(1, 2);"), "does not contains overloads");
    }

    [Test]
    public void WrongArgumentType()
    {
        AssertCompileError("func f(a : int32) { } func Main() { f(true); }", "does not contains overloads");
    }

    [Test]
    public void DuplicateOverload()
    {
        AssertCompileError("func f(a : int32) { } func f(b : int32) { } func Main() { }", "already exist");
    }

    [Test]
    public void OverloadsDifferingOnlyInReturnType()
    {
        AssertCompileError("func f() { } func f() : int32 { return 1; } func Main() { }", "already exist");
    }

    [Test]
    public void OverloadingBuiltinFunction()
    {
        Assert.That(() => Bytes("func print(a : int32, b : int32) { } func Main() { print(1, 2); print(3); }"),
            Throws.Nothing);
    }

    [Test]
    public void Recursion()
    {
        Assert.That(() => Bytes("func f(n : int32) : int32 { return f(n); } func Main() { print(f(1)); }"), Throws.Nothing);
    }

    [Test]
    public void FunctionIsNotAValue()
    {
        AssertCompileError("func f() { } func Main() { var g = f; }", "Value required");
    }

    // ---------- Classes ----------

    [Test]
    public void FieldsAreAccessedThroughSelf()
    {
        Assert.That(() => Bytes("class A : { var x = 1; func m() : int32 { return self.x; } } func Main() { }"),
            Throws.Nothing);
        AssertCompileError("class A : { var x = 1; func m() { print(x); } } func Main() { }",
            "Name 'x' does not exist");
    }

    [Test]
    public void MethodsSeeEachOtherRegardlessOfOrder()
    {
        Assert.That(() => Bytes(
            "class A : { func a() : int32 { return self.b(); } func b() : int32 { return 1; } } func Main() { }"),
            Throws.Nothing);
    }

    [Test]
    public void UnknownMember()
    {
        AssertCompileError("class A : { } func Main() { var a = A(); print(a.y); }", "does not contains field with name 'y'");
    }

    [Test]
    public void FieldTypeIsChecked()
    {
        AssertCompileError("class A : { var x = 1; } func Main() { var a = A(); a.x = true; }", "Cannot convert");
    }

    [Test]
    public void DeriveFromValue()
    {
        AssertCompileError("var x = 1; class A : x { } func Main() { }", "Class must derived from another class");
    }

    [Test]
    public void DuplicateClass()
    {
        AssertCompileError("class A : { } class A : { } func Main() { }", "Class 'A' is already exist");
    }

    [Test]
    public void ClassInstancesAreTyped()
    {
        Assert.That(() => Bytes("class A : { } func Main() { var a : A = A(); }"), Throws.Nothing);
        AssertCompileError("class A : { } class B : { } func Main() { var a : A = B(); }", "Cannot convert from 'B' to 'A'");
    }

    [Test]
    public void ObjectsAsParameters()
    {
        Assert.That(() => Bytes(
            "class A : { var x = 1; } func get(a : A) : int32 { return a.x; } func Main() { print(get(A())); }"),
            Throws.Nothing);
    }

    // ---------- Built-ins ----------

    [TestCase("print(1);")]
    [TestCase("print(1 as float32);")]
    [TestCase("print(true);")]
    [TestCase("var x : int32 = read();")]
    [TestCase("var x : int32 = random(1, 10);")]
    [TestCase("var a = Array(3); set(a, 0, 1); var x : int32 = get(a, 0);")]
    public void BuiltinFunctions(string statements)
    {
        Assert.That(() => Bytes(InMain(statements)), Throws.Nothing);
    }

    [TestCase("int32")]
    [TestCase("float32")]
    [TestCase("bool")]
    [TestCase("Array")]
    [TestCase("Object")]
    public void BuiltinTypesAreVisible(string type)
    {
        Assert.That(() => Bytes($"func f(a : {type}) {{ }} func Main() {{ }}"), Throws.Nothing);
    }

    // ---------- Errors ----------

    [Test]
    public void ErrorMessageContainsTypeAndPosition()
    {
        // 'x' is on line 2, column 5
        Assert.That(() => Bytes("func Main() {\n    x();\n}"),
            Throws.InstanceOf<Error>().With.Message.StartsWith("Semantic error on (5;2)"));
    }
}

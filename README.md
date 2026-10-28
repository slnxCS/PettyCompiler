<div align="center">

# PettyLang

**A small statically typed, object-oriented language and its compiler to PTVM bytecode**

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![C#](https://img.shields.io/badge/C%23-13-239120)
![Bytecode](https://img.shields.io/badge/bytecode-1.392-blue)
![Status](https://img.shields.io/badge/status-early%20development-orange)

English | [Русский](README.ru.md)

</div>

---

This repository contains the **PettyLang compiler**, written in C#. It turns PettyLang source files into
bytecode for **[PettyVM](https://github.com/slnxCS/PettyVM)**, a stack-based virtual machine written in C.

```petty
class Counter : {
    var value = 0;

    func next() : int32 {
        self.value = self.value + 1;
        return self.value;
    }
}

func fact(n : int32) : int32 {
    if n == 0 : return 1;
    return n * fact(n - 1);
}

func Main() {
    var c = Counter();
    print(c.next());   # 1
    print(c.next());   # 2
    print(fact(5));    # 120
    print(7 / 2);      # 3.5
}
```

## Features

- **Static typing** with type inference for variables: `var x = 1;` or `var x : int32 = 1;`
- **Built-in types** `int32`, `float32`, `bool` and explicit conversions with `as`
- **Classes** with fields, field initializers and methods (`self` is passed implicitly)
- **Functions** with overloading by parameter types and recursion
- **Arrays** of `int32` via `Array(n)`, `get` and `set`
- **`if`** with block or single-statement bodies
- **Built-in functions**: `print`, `read`, `random`, `get`, `set`
- **Compact binary output**: constant pool, function table, class table and entry point in one file
- Compile-time errors with line and column

> PettyLang is a hobby project in early development. Loops, `else`, strings, comparison operators other
> than `==` and inheritance of members are not implemented yet — see
> [Known issues](docs/06-known-issues.md).

## Getting started

### Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or newer — to build the compiler
- `gcc` and `make` — to build [PettyVM](https://github.com/slnxCS/PettyVM) and run programs

### Build

```bash
git clone https://github.com/slnxCS/PettyCompiler.git
cd PettyCompiler
dotnet build PettyLang/PettyLang.csproj -c Release
```

### Compile and run a program

```bash
# compile
dotnet PettyLang/bin/Release/net10.0/PettyLang.dll -i hello.pt -o compiled.pt

# run on PettyVM
git clone https://github.com/slnxCS/PettyVM.git ../PettyVM
make -C ../PettyVM
cp compiled.pt ../PettyVM/ && cd ../PettyVM && ./build/main compiled.pt
```

The bytecode version in the file must match the version PettyVM expects (currently **1.392**).

## Usage

```
PettyLang -i <source> -o <output> [options]
```

| Option | Description |
|---|---|
| `-i`, `--input <path>` | source file |
| `-o`, `--output <path>` | where to write the bytecode |
| `-d`, `--debug` | don't catch compiler exceptions (for debugging the compiler itself) |
| `-v`, `--version` | print the compiler and bytecode versions |
| `-h`, `--help` | show help |

Running without `-i` or `-o` prints the help.

## How it works

```
source ─▶ Lexer ─▶ Parser ─▶ AST ─▶ Analyzer ─▶ Compiler ─▶ PTVM bytecode
 *.pt     tokens          tree     symbols,      bytes
                                   types, IDs,
                                   constant pool
```

1. **Lexer** turns source text into tokens.
2. **Parser** — a hand-written recursive descent parser — builds the AST.
3. **Analyzer** resolves names, checks types, picks overloads and assigns IDs to variables, functions and classes.
4. **Compiler** emits bytecode: header, constant pool, functions, class table, globals and the call to `Main`.

The whole pipeline is available as one call: `Program.Start(string source) : byte[]`.

## Project layout

```
PettyLang/            compiler (console app)
  Lexer/              tokens and lexer
  AST/                syntax tree and parser
  Semantic/           scopes, symbols, semantic analyzer
  Compiler/           code generation, constant pool, file header
  Builtin.cs          built-in types and functions
Tests/                NUnit tests
docs/                 technical documentation
```

## Testing

```bash
dotnet test PettyLang.slnx                                     # all tests
dotnet test PettyLang.slnx --filter "TestCategory!=KnownBug"   # skip known bugs
```

The test suite covers the lexer, parser, semantic analysis and exact bytecode listings. Tests in the
`KnownBug` category describe the expected behavior for bugs that are not fixed yet and fail until they are.

## Documentation

The technical documentation is in Russian:

| | |
|---|---|
| [Getting started](docs/01-getting-started.md) | building, CLI, tests |
| [Language reference](docs/02-language.md) | syntax (EBNF), types, classes, built-in functions |
| [Compiler architecture](docs/03-architecture.md) | every stage of the compiler in detail |
| [Bytecode format](docs/04-bytecode.md) | file layout and instruction set of PTVM |
| [Extending the compiler](docs/05-extending.md) | adding operators, built-ins, types, instructions |
| [Known issues](docs/06-known-issues.md) | limitations and known bugs |

## Contributing

Bug reports are welcome in [Issues](https://github.com/slnxCS/PettyCompiler/issues). Please include a minimal
PettyLang program that reproduces the problem and the compiler output.

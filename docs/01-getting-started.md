# 01 — Быстрый старт

## Требования

- .NET SDK 10.0 или новее (`dotnet --version` → `10.x`).
- Виртуальная машина PTVM — для запуска скомпилированного байткода (в этот репозиторий не входит).

## Структура репозитория

```
Compiler/
├── PettyLang.slnx                 # решение (компилятор + тесты)
├── PettyLang/
│   ├── PettyLang.csproj           # консольное приложение, net10.0
│   ├── Program.cs                 # точка входа: чтение файла → конвейер → compiled.pt
│   ├── Position.cs                # Point2D / Position — координаты в исходнике
│   ├── Error.cs                   # иерархия ошибок компиляции
│   ├── Builtin.cs                 # встроенные типы и функции, глобальная область видимости
│   ├── Lexer/
│   │   ├── Token.cs               # TokenType, Token
│   │   └── Lexer.cs               # лексический анализатор
│   ├── AST/
│   │   ├── ASTNode.cs             # базовый узел
│   │   ├── Statement.cs           # узлы-инструкции
│   │   ├── Expression.cs          # узлы-выражения
│   │   └── Parser.cs              # рекурсивный нисходящий парсер
│   ├── Semantic/
│   │   ├── Scope.cs               # области видимости и выдача ID
│   │   ├── Symbol.cs              # таблица символов, типы, перегрузки, правила кодогенерации
│   │   └── Analyzer.cs            # семантический анализ (разрешение имён, проверка типов)
│   └── Compiler/
│       ├── ComileFabric.cs        # CompileFabric — фасад сборки байткода
│       ├── Compiler.cs            # OpCode, генерация кода
│       ├── ConstantPool.cs        # пул констант
│       └── Header/Header.cs       # заголовок файла PTVM
└── Tests/PettyCompilerTests/      # NUnit-тесты лексера и парсера
```

## Сборка

```bash
dotnet build PettyLang.slnx              # Debug
dotnet build PettyLang/PettyLang.csproj -c Release
```

## Запуск компилятора

```bash
dotnet run --project PettyLang -c Release -- path/to/program.pt
# или
dotnet PettyLang/bin/Release/net10.0/PettyLang.dll path/to/program.pt
```

- Единственный аргумент — путь к исходному файлу.
- Результат всегда записывается в **`compiled.pt` в текущем рабочем каталоге** (имя жёстко задано
  в `Program.cs`).
- Если аргумента нет — `ArgumentException("Excepted file name")`; если файла нет — `FileNotFoundException`.

> **Внимание — Debug-сборка.** В конфигурации `DEBUG` `Program.Main` *игнорирует* аргументы командной строки
> и всегда компилирует `/home/slnx/PettyLang/Compiler/PettyLang/bin/Debug/net10.0/code.pt`
> (`#if DEBUG` в `Program.cs`). Для компиляции произвольных файлов используйте `-c Release`.

## Сообщения об ошибках

Ошибки компиляции — исключения типа `PettyLang.Errors.Error`. Сейчас они **не перехватываются**
(блок `try/catch` в `Program.cs` закомментирован), поэтому процесс завершается с необработанным
исключением и стеком вызовов. Первая строка содержит полезную информацию:

```
Unhandled exception. PettyLang.Errors.Error: Semantic error on (33;4) :
	Name 'x' does not exist in current context
```

Формат: `<Тип> error on (<столбец>;<строка>) : <сообщение>`. Обратите внимание: **сначала столбец, потом
строка**; нумерация с 1. Подробнее — в [03 — Архитектура → Ошибки](03-architecture.md#ошибки).

## Тесты

```bash
dotnet test PettyLang.slnx
```

Тестовый проект (`NUnit 4.6.1`) содержит 10 случаев:

| Файл | Что проверяется |
|---|---|
| `LexerTests.cs` | последовательности типов токенов; ошибки на незакрытом `#<` и `"` |
| `ParserTests.cs` | разбор `var x = 10;`; ошибка при имени-ключевом слове; ошибка при отсутствии `;`; цепочка `x.y()` |

Семантический анализатор и генератор кода тестами пока не покрыты.

## Пример: от исходника до байткода

`hello.pt`:

```petty
func Main() : {
    var x = 2;
    if x == 2 : {
        print(x + 1);
    }
}
```

```bash
dotnet PettyLang/bin/Release/net10.0/PettyLang.dll hello.pt
xxd compiled.pt | head
```

Полный побайтовый разбор этого файла приведён в [04 — Формат байткода → Разобранный пример](04-bytecode.md#7-разобранный-пример).

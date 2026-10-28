# 01 — Быстрый старт

## Требования

- .NET SDK 10.0 или новее (`dotnet --version` → `10.x`).
- Виртуальная машина PTVM — для запуска скомпилированного байткода (в этот репозиторий не входит).
  Версия байткода в файле должна совпадать с версией, которую ожидает VM.

## Структура репозитория

```
Compiler/
├── PettyLang.slnx                 # решение (компилятор + тесты)
├── PettyLang/
│   ├── PettyLang.csproj           # консольное приложение, net10.0; зависимость System.CommandLine
│   ├── Properties/
│   │   └── launchSettings.json    # аргументы по умолчанию для запуска из VS Code / dotnet run
│   ├── Program.cs                 # командная строка и Program.Start — весь конвейер компиляции
│   ├── Position.cs                # Point2D / Position — координаты в исходнике
│   ├── Error.cs                   # иерархия ошибок компиляции
│   ├── Builtin.cs                 # класс Native: встроенные типы, функции и глобальная область видимости
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
│       ├── CompileFabric.cs       # CompileFabric — фасад сборки байткода
│       ├── Compiler.cs            # OpCode, генерация кода
│       ├── ConstantPool.cs        # пул констант
│       └── Header/Header.cs       # заголовок файла PTVM
└── Tests/PettyCompilerTests/
    ├── LexerTests.cs              # лексер
    ├── ParserTests.cs             # парсер
    ├── SemanticTests.cs           # семантический анализ
    ├── CodegenTests.cs            # генерация байткода
    ├── KnownBugTests.cs           # известные ошибки (категория KnownBug)
    └── Support/
        ├── Bytecode.cs            # чтение и дизассемблирование файла PTVM для тестов
        └── TestCompiler.cs        # вспомогательные методы: компиляция строки, проверка ошибок
```

Каталоги `bin/` и `obj/` не хранятся в git (`.gitignore`).

## Сборка

```bash
dotnet build PettyLang.slnx                          # Debug, вместе с тестами
dotnet build PettyLang/PettyLang.csproj -c Release
```

## Запуск компилятора

```bash
dotnet PettyLang/bin/Release/net10.0/PettyLang.dll -i program.pt -o program.ptc
# или
dotnet run --project PettyLang -c Release -- -i program.pt -o program.ptc
```

| Опция | Описание |
|---|---|
| `-i`, `--input <путь>` | исходный файл |
| `-o`, `--output <путь>` | куда записать байткод |
| `-d`, `--debug` | не перехватывать исключения компилятора (для отладки самого компилятора) |
| `-v`, `--version` | вывести версии компилятора и байткода |
| `-?`, `-h`, `--help` | справка |

- `-i` и `-o` обязательны для компиляции. Если хотя бы одной нет, выводится справка (как при `--help`).
- `-v` выводит:
  ```
  Compiler version : 0.1
  Bytecode version : 1.392
  ```
  Встроенная опция `--version` библиотеки `System.CommandLine` отключена, поэтому `--version` и `-v`
  работают одинаково.
- Если входного файла нет, процесс завершается необработанным `FileNotFoundException` (код выхода 1).

### Запуск из VS Code

`PettyLang/Properties/launchSettings.json` задаёт аргументы по умолчанию. VS Code с C# Dev Kit использует их
при запуске проекта (F5 / «Run»), если нет `.vscode/launch.json`; `dotnet run` — тоже:

```json
{
  "profiles": {
    "PettyLang": {
      "commandName": "Project",
      "commandLineArgs": "-i /home/slnx/PettyLang/Compiler/PettyLang/bin/Debug/net10.0/code.pt -o /home/slnx/PettyLang/Compiler/PettyLang/bin/Debug/net10.0/compiled.pt"
    }
  }
}
```

Пути абсолютные и привязаны к машине разработчика — поменяйте их под себя.

## Сообщения об ошибках

Ошибки компиляции — исключения типа `PettyLang.Errors.Error`. Без `-d` компилятор перехватывает их, выводит
красным цветом и не записывает выходной файл. Выводится `Exception.ToString()`, поэтому после сообщения
идёт стек вызовов компилятора; полезна первая часть:

```
PettyLang.Errors.Error: Semantic error on (5;2) :
	Name 'x' does not exist in current context
   at PettyLang.Semantic.Analyzer.ResolveIdentifierPart(...)
   ...
```

Формат: `<Тип> error on (<столбец>;<строка>) : <сообщение>`. Обратите внимание: **сначала столбец, потом
строка**; нумерация с 1. Подробнее — в [03 — Архитектура → Ошибки](03-architecture.md#ошибки).

Прочие исключения (внутренние ошибки компилятора) выводятся как `Compiler exception : ...`.

Код выхода при ошибке компиляции сейчас **0** — его нельзя использовать в скриптах для проверки успеха
(см. [известные проблемы](06-known-issues.md)). С `-d` исключения не перехватываются, и процесс завершается
с необработанным исключением.

## Тесты

```bash
dotnet test PettyLang.slnx                                     # все тесты
dotnet test PettyLang.slnx --filter "TestCategory!=KnownBug"   # без известных ошибок
```

| Файл | Что проверяется |
|---|---|
| `LexerTests.cs` | ключевые слова, идентификаторы, символы и выбор самого длинного совпадения, числа, строки, комментарии, лексические ошибки |
| `ParserTests.cs` | приоритет и ассоциативность операторов, пути и вызовы, объявления, `if`/`return`/блоки, синтаксические ошибки с текстом сообщения |
| `SemanticTests.cs` | все диагностики анализатора (правила `Main`, имена, области видимости, типы, приведения, перегрузки, классы) и корректные программы |
| `CodegenTests.cs` | точный листинг байткода для каждой конструкции, таблица классов, детерминированность компиляции |
| `KnownBugTests.cs` | ожидаемое правильное поведение для известных ошибок |

Тесты генерации кода читают выходной файл через `Support/Bytecode.cs`. Он разбирает весь файл по формату
PTVM и выдаёт читаемый листинг: константы показаны значениями (`PUSH_CONSTANT 2`, `2.5f`, `true`), адреса
переходов — номером целевой инструкции (`JMP_IF_FALSE @9`, или `@?0x41`, если адрес попадает в середину
инструкции). **При изменении формата байткода `Bytecode.cs` нужно обновлять вместе с компилятором.**

Тесты категории `KnownBug` описывают ожидаемое поведение для ещё не исправленных ошибок и падают, пока
ошибка не исправлена. После исправления тест можно перенести в обычные.

Тест `CodegenTests.FloatConstant` выставляет культуру `ru-RU`: без этого вещественные литералы
разбираются неверно (см. [известные проблемы](06-known-issues.md)).

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
dotnet PettyLang/bin/Release/net10.0/PettyLang.dll -i hello.pt -o hello.ptc
xxd hello.ptc | head
```

Полный побайтовый разбор этого файла приведён в [04 — Формат байткода → Разобранный пример](04-bytecode.md#7-разобранный-пример).

# 03 — Архитектура компилятора

## 1. Обзор конвейера

Компилятор — классический многопроходный конвейер. Каждый этап — отдельный класс; данные между этапами
передаются через массив токенов, AST и аннотации, которые семантический анализатор записывает прямо в узлы AST.

Весь конвейер собран в `Program.Start(string sourceCode) : byte[]`. Его же вызывают тесты.

```
Program.Main                      разбор командной строки (System.CommandLine)
 └─ Program.Start(source)
     ├─ new Parser(source)            Parser.cs       ← внутри вызывает Lexer.Tokenize()
     │     └─ Lexer.Tokenize()        Lexer.cs        → Token[]
     ├─ parser.Parse()                                → Statement[] (AST)
     ├─ new ConstantPool()            ConstantPool.cs
     ├─ new Analyzer(constantPool)    Analyzer.cs     ← создаёт Native (встроенные типы и функции)
     ├─ analyzer.Analyze(AST)                         → аннотированный AST + списки функций/классов/глобалов
     │                                                  + заполненный пул констант
     └─ new CompileFabric(AST)        CompileFabric.cs
           .Build(analyzer)
             └─ new Compiler(AST, analyzer)
                  .Compile(new HeaderCompiler())      → byte[]
 └─ File.WriteAllBytesAsync(<-o>, bytes)
```

| Пространство имён | Содержимое |
|---|---|
| `PettyLang` | `Program`, `Point2D`, `Position`, `Native` |
| `PettyLang.Errors` | `Error` и производные |
| `PettyLang.Tokens` | `TokenType`, `Token` |
| `PettyLang.Lexer` | `Lexer` |
| `PettyLang.Parser` | `Parser` |
| `PettyLang.AST` | узлы дерева |
| `PettyLang.Semantic` | `Analyzer`, `Scope`, символы |
| `PettyLang.Compiler` | `CompileFabric`, `Compiler`, `OpCode`, `ConstantPool`, `HeaderCompiler` |

### Состояние компиляции

Глобального статического состояния нет. Всё, что относится к одной компиляции, принадлежит её объектам:

| Что | Владелец |
|---|---|
| встроенные типы и функции, глобальная область видимости | `Native` — создаётся в конструкторе `Analyzer` |
| пул констант | `ConstantPool` — передаётся в конструктор `Analyzer`, доступен как `analyzer.ConstantPool` |
| счётчики ID глобальных переменных, функций и классов | `IDSet` глобальной области (`Native.GlobalScope`) |
| точка входа, списки функций/методов/классов/глобалов | поля `Analyzer` |

Поэтому в одном процессе можно скомпилировать сколько угодно программ, и результаты не влияют друг на друга
(это проверяет тест `CodegenTests.CompilationIsDeterministic`).

## 2. Позиции и ошибки

### Позиции

```csharp
public readonly record struct Point2D(int X = 0, int Y = 0);   // X — столбец, Y — строка (с 1)
public readonly record struct Position(Point2D Start, Point2D End);
```

Каждый токен, узел AST и символ хранит `Position`. Позиция `default` (`(0;0)`) означает «нет позиции» —
используется для встроенных сущностей и глобальных ошибок.

### Ошибки

```csharp
class Error(string message, string errorType, Position position) : Exception
// Message = $"{errorType} error on ({Start.X};{Start.Y}) : \n\t{message}"
```

| Класс | Тип | Когда |
|---|---|---|
| `Error` | `Syntax` | лексические и синтаксические ошибки |
| `Error` | `Semantic` | ошибки разрешения имён и типов |
| `Error` | `parser`, `compiler`, `""` | отдельные случаи (нет типа или значения у `var`, неразрешённый класс, отсутствие `return`) |
| `NotSupportedOperandsError` | `Semantic` | оператор неприменим к типам операндов |
| `NotSupportedCastTypeError` | `Semantic` | недопустимое приведение `as` |
| `NotCallableError` | `Semantic` | вызов не вызываемого объекта |

Помимо `Error`, на нереализованных путях выбрасываются системные исключения `NotImplementedException`,
`NullReferenceException`, `NotSupportedException` — их нужно воспринимать как «конструкция пока не
поддерживается». Компилятор останавливается на **первой** ошибке; восстановления после ошибок нет.

`Program.Main` без опции `-d` перехватывает `Error` и прочие исключения и печатает их; с `-d` пропускает
наружу (см. [01 — Быстрый старт](01-getting-started.md#сообщения-об-ошибках)).

## 3. Лексер (`Lexer/Lexer.cs`)

**Вход:** строка исходника. **Выход:** `Token[]`, последний элемент — `EOF`.

```csharp
public class Token(string lexeme, TokenType type, Position position)
```

### Алгоритм `Tokenize()`

Посимвольный проход с указателем `_currentPosition` и счётчиками столбца/строки (`advance()` обрабатывает `\n`).
На каждом шаге по текущему символу выбирается ветка:

| Условие | Действие |
|---|---|
| `#` и следующий `<` | `_skipMultiLineComment()` — пропуск до `>#` |
| `#` | `_skipSingleLineComment()` — пропуск до `\n` |
| пробельный символ | пропуск |
| буква или `_` | `_addID()` — идентификатор; если строка есть в таблице `Keywords`, тип меняется на ключевое слово |
| `"` | `_addString()` |
| `'` | `_addChar()` |
| цифра, или `-` перед цифрой | `_addNumber()` — `IntNumber` или `FloatNumber` |
| иначе | `_addSymbol()` — жадный поиск в таблице `Symbols` (сначала 2 символа, затем 1) |

Особенности реализации:

- В лексеме вещественного числа точка заменяется на запятую (`num += ','`), а затем парсер вызывает
  `float.Parse` **с текущей культурой**. Это работает только в локалях с десятичной запятой
  (например, `ru-RU`) — см. [известные проблемы](06-known-issues.md).
- Методы `_process*`, `_process()` и `_getWhile()` дублируют `_add*`, но возвращают токен вместо добавления
  в список. Сейчас они нигде не используются (задел под потоковую токенизацию).

### Таблицы

- `Keywords`: `func var import package return as false true if else class`.
- `Symbols`: `+ - / * = == += -= /= *= ( ) [ ] { } < > : ; . ,`.

## 4. Парсер (`AST/Parser.cs`)

Рекурсивный нисходящий парсер (LL(1) с одним случаем просмотра вперёд). Конструктор сразу вызывает лексер.

### Вспомогательные методы

| Метод | Назначение |
|---|---|
| `current`, `next`, `last` | текущий, следующий, предыдущий токен |
| `match(type, needAdvance = true)` | если текущий токен нужного типа — (опционально) сдвинуться и вернуть `true` |
| `consume(type, message, needAdvance = true)` | то же, но при несовпадении бросает `Error(message, "Syntax")`; возвращает поглощённый токен |

### Правила

| Метод | Правило грамматики |
|---|---|
| `Parse()` | `program` |
| `parseStatement()` | `statement`: выбор по типу текущего токена (`Identifier`, `Var`, `{`, `Func`, `Class`, `Return`, `If`) |
| `parseBlock(canOneLine)` | `block` / `ifBody`: если текущий токен `{` — инструкции до `}`; иначе при `canOneLine` — ровно одна инструкция, без него — ошибка `Expected '{' for start block` |
| `parseVarAssign()` | `varDecl` (несмотря на имя, разбирает объявление) |
| `parseVarAssignment(target)` | присваивание после уже разобранного `path` |
| `parseFuncDef()`, `parseParameters()`, `parseParameter()` | `funcDef`, `param` |
| `parseClass()` | `classDef` |
| `parseReturn()` | `returnStmt` |
| `parseIf()` | `ifStmt` |
| `parseExpression()` → `parseEquals()` → `parseAdditive()` → `parseMultiplicative()` → `parseAsExpression()` → `parsePrimaryBasicExpression()` | каскад приоритетов |
| `parseIdentifier()` / `parseIdentifierPart()` / `ParseFuncCalls()` / `parseFuncCall()` | `path`, `pathPart`, списки аргументов |

`parseBlock(true)` вызывается только для тела `if`; тела функций, классов и блок `{ … }` как инструкция
разбираются через `parseBlock(false)` и требуют фигурных скобок.

Полная грамматика — в [02 — Справочник языка](02-language.md#2-грамматика).

Мёртвый код: перегрузка `parseIdentifier(Expression firstExpr)` и `parseAfterIdentifierExpression()`
(логика доступа к членам произвольного выражения закомментирована).

## 5. AST (`AST/*.cs`)

Все узлы наследуют `ASTNode { Position Position }`. Поля с суффиксом `Resolved*` изначально пусты и
заполняются семантическим анализатором — это **аннотации**, которые затем читает генератор кода.

### Инструкции (`Statement`)

| Узел | Поля синтаксиса | Аннотации анализатора |
|---|---|---|
| `StatementExpression` | `Expression` | — |
| `VarDeclStatement` | `Name`, `Type?` (`IdentifierExpression`), `Value?` | `Resolved : VarSymbol` |
| `VarAssignmentStatement` | `Target` (`IdentifierExpression`), `Operator`, `Value` | `Resolved : VarSymbol?` |
| `BlockStatement` | `Statements[]` | — |
| `FuncDefineStatement` | `Name`, `Parameters[]` (`FuncParameter`), `ReturnType?`, `Block` | `Resolved : FunctionOverload?` |
| `ClassDefineStatement` | `Name`, `Block?`, `Derived?` | `Resolved : ClassSymbol?` |
| `ReturnStatement` | `Value?` | — |
| `IfStatement` | `Condition`, `Block` | — |

Тело `if` без скобок тоже представлено `BlockStatement` — с одной инструкцией.

`FuncParameter` (параметр в AST) — не узел, а простой класс: `Name`, `Type`, `Resolved : FunctionParameter?`.

### Выражения (`Expression`)

| Узел | Поля синтаксиса | Аннотации анализатора |
|---|---|---|
| `IntExpression` | `Number : int` | `Resolved : Int32InstanceSymbol` |
| `FloatExpression` | `Number : float` | `Resolved : Float32InstanceSymbol` |
| `BoolExpression` | `Value : bool` | `Resolved : BoolInstanceSymbol` |
| `StringExpression` | `String` | — |
| `BinaryExpression` | `Left`, `Operator`, `Right` | `LeftSymbol`, `RightSymbol` |
| `AsExpression` | `Value`, `AsType` | `ResolvedValue`, `ResolvedAsType` |
| `IdentifierExpression` | `FirstPart`, `OtherParts[]` | `Resolved`, `FirstPartResolved` |
| `IdentifierExpressionPart` | `ID`, `FuncCallsArguments[][]` | `Resolved`, `ResolvedOverload`, `ResolvedParameters` |

#### Пути (`IdentifierExpression`)

Выражение `a.b(1).c` представлено так:

```
IdentifierExpression
├── FirstPart  = IdentifierExpressionPart { ID="a", FuncCallsArguments=[],     Index=0 }
└── OtherParts = [
      IdentifierExpressionPart { ID="b", FuncCallsArguments=[[1]], Index=1 },
      IdentifierExpressionPart { ID="c", FuncCallsArguments=[],    Index=2 } ]
```

Каждая часть знает родителя (`Parent`) и свой индекс; `Last()` возвращает предыдущую часть.
Поля `ArrayAppealArgumentsBeforeFuncCall/AfterFuncCall` зарезервированы под индексаторы `a[i]` и сейчас
всегда `null`.

## 6. Семантический анализ

### 6.1. Области видимости (`Semantic/Scope.cs`)

```csharp
enum ScopeType { Global, Function, Class, Instance, Package, Local }
```

`Scope` хранит три независимых словаря — переменные, функции, классы — и ссылку на родителя.
Поиск (`GetVar/GetFunc/GetClass(name, local)`) идёт вверх по цепочке родителей, если `local == false`.
`Define*` проверяет конфликт только в текущей области (это даёт shadowing во вложенных блоках).

У каждой области есть ссылка на `IDSet` своей глобальной области (`GlobalFreeIDSet`): глобальная область
создаёт новый набор, а все остальные берут его у родителя. Так счётчики общие в пределах одной компиляции и
независимы между компиляциями.

| Тип области | Где создаётся | Собственный набор счётчиков (`freeIDSet`) | `Self` |
|---|---|---|---|
| `Global` | `Native.GlobalScope` | совпадает с глобальным набором | — |
| `Function` | `VisitFuncBlock` — тело функции/метода | новый | — |
| `Local` | `VisitBlock`, `VisitIf` | **общий с родителем** | — |
| `Class` | `ClassSymbol` — статические члены класса | новый | — |
| `Instance` | `ClassInstanceSymbol` — члены конкретного экземпляра | новый | экземпляр |
| `Package` | не используется | новый | — |

Выдача ID:

- `GetFreeVarID()` — следующий номер переменной в `freeIDSet`. Поскольку `Local` разделяет набор
  с родительской `Function`, все локальные переменные функции (включая параметры и переменные вложенных
  блоков) нумеруются сквозным образом `0..N-1`, и `N` становится `LocalsCount` функции.
- `GetFreeFuncID()` — для областей `Global/Function/Local/Package` берёт **глобальный** счётчик функций;
  для `Class/Instance` — собственный счётчик. Поэтому у функций верхнего уровня сквозная нумерация, а у методов —
  своя внутри каждого класса.
- `GetFreeClassID()` — `ClassSymbol` всегда вызывает его у `Native.GlobalScope`, так что ID классов сквозные.

`CopyFromClass(ClassSymbol, Analyzer)` наполняет `Instance`-область: создаёт `MethodSymbol` для каждой функции
класса (разделяя с ним объекты `FunctionOverload`), копирует вложенные классы и вызывает
`analyzer.ResolveFieldsDecl` — так каждый экземпляр получает собственные `VarSymbol` полей с ID `0..K-1`.

### 6.2. Символы (`Semantic/Symbol.cs`)

Символы — это одновременно таблица символов и **объектная модель кодогенерации**: виртуальные методы
символов возвращают байты для своих операций. Иерархия:

```
Symbol (abstract)
├── VarSymbol                     переменная/параметр/поле: Value → ClassInstanceSymbol
├── ClassSymbol                   тип; Members = Class-scope; Derived; VarDefines; конструкторы; Native
│   ├── Int32ClassSymbol          "int32"
│   ├── Float32ClassSymbol        "float32"
│   ├── BoolClassSymbol           "bool"
│   └── ArrayClassSymbol          "Array"
└── ClassInstanceSymbol           значение типа; Members = Instance-scope; Native, analyzer
    ├── Int32InstanceSymbol       Value: int?   (константа, если задано)
    ├── Float32InstanceSymbol     Value: float?
    ├── BoolInstanceSymbol        Value: bool?
    ├── ArrayInstanceSymbol
    └── FunctionSymbol            функция: список Overloads
        └── MethodSymbol          метод: подставляет self при поиске перегрузки
            └── ConstructorFunctionSymbol   конструктор: ALLOCATE_OBJ + вызов базового конструктора
                ├── BasicConstructorSymbol          сгенерированный инициализатор полей
                └── ArrayConstructorFunctionSymbol  ALLOCATE_ARR

FunctionOverload                  одна перегрузка: ID, Parameters, ReturnType, LocalsCount,
└── BuiltInFunctionOverload       Statement (AST) или EmitBytes (генератор тела)
FunctionParameter                 Name, Position, Type
```

Общие поля `Symbol`: `Name`, `ID`, `Type` (для `ClassSymbol` — он сам), `DeclaredIn` (область), `Position`,
`Members` (область членов или `null`).

**Доступ к встроенным типам и пулу констант.** Символы не используют глобальных объектов:

- `ClassSymbol` получает `Native` в конструкторе и хранит его в поле `Native`;
- `ClassInstanceSymbol` (и все наследники, включая функции) получает `Analyzer` в конструкторе, хранит его в
  поле `analyzer`, а `Native` берёт у своего класса (`@class.Native`);
- `FunctionSymbol`, `MethodSymbol`, `ArrayConstructorFunctionSymbol` дополнительно принимают `Native` явно:
  они создаются и внутри конструктора `Native`, когда `analyzer.Native` ещё не присвоен.

Виртуальные методы `Symbol` и их роль:

| Метод | Этап | Назначение |
|---|---|---|
| `VisitBinary(right, expr)` | анализ | тип результата бинарной операции или ошибка |
| `ResolveCast(type, expr)` | анализ | тип результата `as` или ошибка |
| `ResolveCall(args, id, part)` | анализ | тип результата вызова |
| `CompileBinaryOperation(expr, compiler)` | генерация | код бинарной операции |
| `CompileCastBytes(type, expr, compiler)` | генерация | код приведения |
| `GetBytesForCall(args, id, part)` | генерация | байты инструкции вызова |
| `GetPushBytes(part)` | генерация | байты загрузки значения на стек |

`VarSymbol` делегирует все операции своему `Value`, а `GetPushBytes` реализует сам:
`LOAD_FIELD id`, если переменная — поле экземпляра (`DeclaredIn.Self != null`), иначе
`LOAD_GLOBAL id` / `LOAD_LOCAL id`.

Литеральные экземпляры (`Int32InstanceSymbol` и т. п. с `Value != null`) **при создании добавляют своё
значение в `analyzer.ConstantPool`**; их `GetPushBytes` возвращает `PUSH_CONSTANT <индекс>`.

#### Перегрузки

`FunctionSymbol.AddOverload` назначает перегрузке `ID` (кроме встроенных — у них ID фиксирован) и проверяет
уникальность списка типов параметров. `GetOverload(args)` ищет перегрузку с той же арностью и
**попарно идентичными** типами параметров.

`MethodSymbol.GetOverload/ResolveCall`: если метод принадлежит экземпляру (`DeclaredIn.Self != null`),
в начало списка аргументов добавляется `self` с типом класса — так вызов `obj.m(x)` сопоставляется
с перегрузкой `(self : C, x : T)`.

#### Конструкторы

При первом выражении `C(...)` вызывается `ClassSymbol.ResolveCall`:

1. Лениво создаётся `BasicConstructorSymbol`: перегрузка `(self : C) : C` с `LocalsCount = 1`, телом
   из `EmitBytes` — для каждого поля с инициализатором `<value>; LOAD_LOCAL 0; STORE_FIELD id`, затем
   `LOAD_LOCAL 0; RET`. Он регистрируется в `Members` класса под именем класса и добавляется в
   `analyzer.Methods[C]`.
2. Если у класса нет `Constructor`, лениво создаётся автоконструктор (`IsAutoGenerated = true`) с той же сигнатурой.
3. Выбирается перегрузка конструктора с аргументами `(self, ...args)`.

`ConstructorFunctionSymbol.GetBytesForCall` генерирует `ALLOCATE_OBJ classId; CALL_METHOD baseCtorId 1`
(для пользовательского конструктора далее добавился бы вызов самого конструктора).

### 6.3. Встроенные сущности (`Builtin.cs`, класс `Native`)

`Native` создаётся в конструкторе `Analyzer` (`new Native(this)`) и содержит:

- `GlobalScope` — глобальную область видимости этой компиляции;
- встроенные классы в порядке, определяющем их ID:
  `Object`=0, `ValueObject`=1, `void`=2, `Array`=3, `int32`=4, `float32`=5, `bool`=6, `function`=7;
- встроенные функции с фиксированными native-ID 0–6 (`print` ×3, `read`, `random`, `get`, `set`);
- конструктор `Array` (`ArrayConstructorFunctionSymbol`).

Всё это регистрируется в `GlobalScope` в `Native.Define()`. Затем `Analyzer.InitClassList()` добавляет все
встроенные классы (включая `Array`) в список классов, который попадёт в таблицу классов байткода.

`StringClass` объявлен, но всегда `null` — строковый литерал даёт ошибку с подсказкой про
`import String from std` (импорт пока не реализован).

### 6.4. Анализатор (`Semantic/Analyzer.cs`)

Однопроходный обход AST с текущим контекстом:

| Поле | Значение |
|---|---|
| `currentScope` | текущая область (начинается с `Native.GlobalScope`) |
| `currentFunction` | анализируемая перегрузка или `null` на верхнем уровне |
| `currentClass` | анализируемый класс или `null` |
| `returnFinded` | встречен ли `return` в текущей функции |

Результаты, которые потребляет генератор кода:

| Поле | Содержимое |
|---|---|
| `Native` | встроенные типы и функции этой компиляции |
| `ConstantPool` | пул констант |
| `GlobalVariables` | объявления глобальных переменных в порядке объявления |
| `Functions` | `FuncDefineStatement` функций верхнего уровня (и вложенных в функции) |
| `Methods` | `ClassSymbol → List<FunctionOverload>`: методы и базовый конструктор каждого класса |
| `Classes` | `ClassDefineStatement` всех классов, включая синтетические записи для встроенных |
| `MainFunction` | перегрузка `Main` |

#### Обработка инструкций

| Инструкция | Метод | Что делает |
|---|---|---|
| `func` | `ResolveFuncDecl` | находит/создаёт `FunctionSymbol` (или `MethodSymbol`), создаёт `FunctionOverload` (типы параметров и возврата), проверяет правила `Main`, анализирует тело (`VisitFuncBlock`), добавляет в `Functions`/`Methods` |
| — | `VisitFuncBlock` | новая `Function`-область, параметры объявляются как переменные (ID `0..arity-1`), обход тела, затем в `finally`: `LocalsCount = FreeVarID` и проверка наличия `return` |
| `class` | `VisitClass` | разрешает базовый класс, создаёт `ClassSymbol`, входит в его область; **сначала** регистрирует все поля (`VisitVarDef` → `VarDefines`) и сигнатуры всех методов (`VisitFuncDeclWithoutBlock`), **затем** анализирует тела методов |
| `var` | `VisitVarDef` | в теле класса — только запоминает в `VarDefines`; иначе выводит тип, сверяет с аннотацией, создаёт `VarSymbol`, глобальные добавляет в `GlobalVariables` |
| присваивание | `VisitVarAssignment` | только `=`; цель — `VarSymbol`; точное совпадение типов |
| `return` | `VisitReturn` | тип значения (или `void`) должен совпасть с типом возврата функции |
| `if` | `VisitIf` | условие типа `bool`, тело в новой `Local`-области |
| `{ }` | `VisitBlock` | новая `Local`-область |
| выражение | `VisitStatementExpression` | разрешает выражение |

Выражения, присваивания, `if` и `return` требуют `currentFunction != null` (`ensureFunc`).

Проверка `return` в блоке `finally` подменяет любую ошибку из тела не-`void`-функции
([#8](https://github.com/slnxCS/PettyCompiler/issues/8)).

#### Разрешение выражений (`ResolveExpression`)

Возвращает `Symbol` — либо экземпляр (значение), либо класс (тип), либо переменную.

- Литералы → новые `*InstanceSymbol` с константой.
- `BinaryExpression` → разрешает обе стороны, приводит их к экземплярам (`GetInstanceSymbol`) и вызывает
  `left.VisitBinary(right)`, результат — экземпляр типа результата.
- `AsExpression` → `ResolveCast`.
- `IdentifierExpression` → `ResolveIdentifierExpression`: первая часть ищется в текущей области (с подъёмом),
  каждая следующая — **только** в `Members` предыдущего символа. Правила поиска частей описаны в
  [справочнике языка](02-language.md#10-разрешение-имён).

#### Поля экземпляров

Для каждого `ClassInstanceSymbol` (каждой переменной/параметра/выражения типа класса) создаётся своя
`Instance`-область, и `ResolveFieldsDecl` повторно разрешает объявления полей класса. Поэтому символы
полей принадлежат конкретному экземпляру, а их ID совпадают у всех экземпляров одного класса.

## 7. Генерация кода

### 7.1. `CompileFabric` и `HeaderCompiler`

`CompileFabric.Build(analyzer)` создаёт `Compiler` и вызывает `Compile(new HeaderCompiler())`.
`HeaderCompiler.Compile(constantPool)` записывает сигнатуру, версию байткода и **весь пул констант** — поэтому
пул должен быть полностью собран *до* генерации кода (что выполняется: константы добавляются при создании
символов-литералов во время анализа).

### 7.2. `Compiler`

Код накапливается в `List<byte> compiled`. Методы `Emit(int|float|string|OpCode|byte[])` дописывают
байты (числа — little-endian через `BitConverter`, строки — UTF-8 без длины).
`SetReservedBytes(startPos, bytes)` перезаписывает уже выданные байты на месте (через
`CollectionsMarshal.AsSpan`) — так заполняется зарезервированная длина функции.

`Compile()` формирует файл в порядке:

1. заголовок и пул констант (`HeaderCompiler`);
2. `WriteFunctions(analyzer.Functions)` — функции верхнего уровня;
3. `WriteClasses()` — таблица классов с методами;
4. `WriteGlobals()` — код инициализации глобальных переменных;
5. `CompileStatements(AST)` — инструкции верхнего уровня (фактически пусто, см. ниже);
6. `CALL <Main.ID> <Main.Arity>; HALT`.

Детальный формат — в [04 — Формат байткода](04-bytecode.md).

### 7.3. Генерация инструкций

| Инструкция | Генерация |
|---|---|
| `var` (локальная) | `<value>; STORE_LOCAL id`. Глобальные пропускаются — их код в секции глобалов |
| `x = v` | `<v>; STORE_LOCAL/STORE_GLOBAL id` |
| `a.b.f = v` | `<v>; <a.b>; STORE_FIELD id(f)` — `CompileIdentifierExpressionWithoutLast` загружает объект-владелец |
| `return v` | `<v>; RET` |
| `if c : …` | `<c>; JMP_IF_FALSE <addr>; <тело>` — адрес вставляется задним числом (`InsertRange`) и равен **абсолютному смещению в файле** первого байта после тела |
| `{ … }` | инструкции блока подряд |
| `func`, `class` | ничего (тела выводятся в своих секциях) |
| выражение | код выражения |

### 7.4. Генерация выражений

| Выражение | Генерация |
|---|---|
| литерал | `PUSH_CONSTANT idx` |
| переменная | `LOAD_LOCAL id` / `LOAD_GLOBAL id` / `LOAD_FIELD id` (объект уже на стеке) |
| `a op b` | `<a>; <b>; <OP>` — opcode выбирает `CompileBinaryOperation` типа левого операнда |
| `v as T` | `<v>; CAST_…`; при приведении к тому же типу сейчас не генерируется ничего, даже `<v>` ([#7](https://github.com/slnxCS/PettyCompiler/issues/7)) |
| `f(a, b)` | `<a>; <b>; CALL id arity` (или `SYS_CALL` для встроенных) |
| `obj.m(a)` | `<obj>; <a>; CALL_METHOD id arity+1` — `obj` становится аргументом `self` |
| `C()` | `ALLOCATE_OBJ classId; CALL_METHOD baseCtorId 1` |
| `Array(n)` | `<n>; ALLOCATE_ARR` |
| `a.b.c` | `<a>; LOAD_FIELD id(b); LOAD_FIELD id(c)` |

Для пути `CompileIdentifierExpression` последовательно генерирует код каждой части; для части-вызова сначала
вычисляются аргументы первого списка, затем добавляются байты `GetBytesForCall` найденной перегрузки.

### 7.5. Тела функций

`WriteFunctions` (обе перегрузки: для функций верхнего уровня и для методов) для каждой функции выводит:

```
<длина тела: i32>          ; резервируется Emit(0), заполняется SetReservedBytes в конце
RESERVE_LOCALS <LocalsCount>
<тело: инструкции AST или EmitBytes(compiler)>
RET
```

Длина — число байт **после** поля длины (от `RESERVE_LOCALS` до `RET` включительно). `RET` в конце добавляется
всегда, даже если тело уже закончилось `return`.

### 7.6. Таблица классов

`WriteClasses` сортирует `analyzer.Classes` по ID и для каждого класса пишет имя, ID, ID базового класса,
число полей и методы (через `WriteFunctions(analyzer.Methods[class])`).

## 8. Тесты

`Tests/PettyCompilerTests` ссылается на проект компилятора. Лексер и парсер тестируются напрямую, семантика
и генерация кода — через `Program.Start`, то есть весь конвейер целиком. Поскольку состояние компиляции не
статическое, тесты независимы друг от друга. Подробнее — в [01 — Быстрый старт → Тесты](01-getting-started.md#тесты).

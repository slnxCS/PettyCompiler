# 05 — Расширение компилятора

Пошаговые рецепты для типичных доработок. Общий принцип: **анализ** (тип результата, проверки) и
**генерация** (байты) для конкретного типа живут в его символе (`Semantic/Symbol.cs`), а `Analyzer` и
`Compiler` лишь диспетчеризуют.

## 1. Новая встроенная функция

Пример: `abs(x : int32) : int32`.

1. В `Builtin.cs` добавьте элемент в `BuiltinFunctions` со **следующим свободным** native-ID (сейчас 7):

   ```csharp
   new(
       "abs",
       [
           new([new("x", default, Int32Class)], Int32Class, 7),
       ]
   ),
   ```

   Перегрузки одной функции — несколько `BuiltInFunctionOverload` в одном массиве, каждая со своим ID.
2. Реализуйте `SYS_CALL 7` в VM.
3. Обновите таблицы в [02-language.md](02-language.md#9-встроенные-функции) и [04-bytecode.md](04-bytecode.md#вызовы).

Генерация кода не требует изменений: `FunctionSymbol.GetBytesForCall` выдаёт `SYS_CALL`, если перегрузка —
`BuiltInFunctionOverload`.

## 2. Новый бинарный оператор

Пример: `<` для `int32`.

1. **Лексер** — токен `<` (`LAngleBrace`) уже существует. Для двухсимвольных операторов (`<=`, `!=`)
   добавьте их в `TokenType` и таблицу `Symbols` (максимальная длина символа — 2, `maxSymbolLen`).
2. **Парсер** — добавьте уровень приоритета или расширьте существующий. Для сравнений удобно расширить
   `parseEquals()`:

   ```csharp
   while (match(TokenType.Equals) || match(TokenType.LAngleBrace))
   {
       var op = last.Lexeme;
       var right = parseAdditive();
       ex = new BinaryExpression(new(ex.Position.Start, right.Position.End), ex, op, right);
   }
   ```
3. **OpCode** — добавьте инструкцию в `enum OpCode` (`Compiler.cs`) со свободным кодом, например `INT_LT = 38`.
4. **Семантика** — в `Int32InstanceSymbol.VisitBinary` добавьте `"<" => BuiltIn.BoolClass`.
5. **Генерация** — в словарь `Int32InstanceSymbol.operators` добавьте `{"<", OpCode.INT_LT}`.
6. Поднимите `BYTE_CODE_VERSION` и реализуйте инструкцию в VM.

## 3. Новая инструкция языка

Пример: `while cond : { … }`.

1. `Token.cs` — `TokenType.While`; `Lexer.cs` — `{"while", TokenType.While}` в `Keywords`.
2. `Statement.cs` — узел:

   ```csharp
   public class WhileStatement(Expression condition, BlockStatement block, Position position) : Statement(position)
   {
       public readonly Expression Condition = condition;
       public readonly BlockStatement Block = block;
   }
   ```
3. `Parser.cs` — метод `parseWhile()` по образцу `parseIf()` и ветка `case TokenType.While` в `parseStatement()`.
4. `Analyzer.cs` — ветка в `VisitStatement` по образцу `VisitIf` (проверка `bool`, новая `Local`-область).
5. `Compiler.cs` — ветка в `CompileStatement`. Адреса абсолютные, поэтому начало цикла — это `compiled.Count`:

   ```csharp
   void CompileWhile(WhileStatement st)
   {
       var start = compiled.Count;
       CompileExpression(st.Condition);
       Emit(OpCode.JMP_IF_FALSE);
       var patchAt = compiled.Count;
       Emit(0);                                   // заглушка
       CompileStatements(st.Block.Statements);
       Emit(OpCode.JMP);
       Emit(start);
       var end = BitConverter.GetBytes(compiled.Count);
       for (int i = 0; i < 4; i++) compiled[patchAt + i] = end[i];
   }
   ```

   Подход «заглушка + патч» предпочтительнее, чем `InsertRange` в `CompileIf`: вставка байтов сдвигает
   уже вычисленные абсолютные адреса вложенных переходов (см. [известные проблемы](06-known-issues.md)).

## 4. Новый встроенный тип

Пример: `int64`.

1. `Symbol.cs` — `Int64ClassSymbol : ClassSymbol` (имя, базовый класс `ValueObjectClass`, переопределённый
   `GetInstance`) и `Int64InstanceSymbol : ClassInstanceSymbol` (`Value`, добавление в `ConstantPool`,
   `GetPushBytes`, `VisitBinary`, `CompileBinaryOperation`, `ResolveCast`, `CompileCastBytes`).
2. `ConstantPool.cs` — `ConstantType.CONSTANT_LONG` и `record LongConstant`.
3. `Builtin.cs` — поле `Int64Class`, создание в `Init()` (**порядок создания задаёт ID класса** — добавляйте
   в конец, чтобы не сдвинуть ID существующих классов), регистрация в `Define()`.
4. `Analyzer.InitClassList()` — `AddClassToList(BuiltIn.Int64Class)`, иначе класс не попадёт в таблицу
   классов байткода.
5. Лексер/парсер — если нужен собственный литерал.

## 5. Новая инструкция VM

1. Добавьте значение в `enum OpCode` — только из свободных кодов; не меняйте существующие.
2. Генерируйте её через `compiler.Emit(OpCode.X)` и `Emit(int)` для операндов.
3. Поднимите `HeaderCompiler.BYTE_CODE_VERSION`.
4. Опишите её в [04-bytecode.md](04-bytecode.md#6-набор-инструкций): операнды, стековый эффект.

## 6. Чек-лист изменения

- [ ] Лексер/парсер покрыты тестами в `Tests/PettyCompilerTests`.
- [ ] Новые ошибки — через `Error(..., "Semantic"|"Syntax", position)`, а не `NotImplementedException`.
- [ ] Поднята версия байткода, если менялся формат.
- [ ] Обновлена документация.

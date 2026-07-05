namespace PettyLang.Parser;

using System.Runtime.Intrinsics.Arm;
using PettyLang.AST;
using PettyLang.Errors;
using PettyLang.Tokens;

public class Parser
{
    public Parser(string source)
    {
        var lexer = new Lexer.Lexer(source);
        tokens = lexer.Tokenize();
    }

    private List<Statement> nodes = new();
    static List<string> AssigmentOperators = new()
    {
        "=", "+=", "-=", "/=", "*="
    };

    private readonly Token[] tokens;
    private int position;

    private Token current => tokens[position];
    private Token next => tokens[position + 1];
    private Token last => tokens[position - 1];
    private bool isEnd => current.Type == TokenType.EOF;

    private void advance() => position++;
    private bool match(TokenType type, bool needAdvance = true)
    {
        if (current.Type == type)
        {
            if (needAdvance) advance();
            return true;
        }
        else return false;
    }

    private Token consume(TokenType type, string message, bool needAdvance = true)
    {
        if (current.Type == type)
        {
            if (needAdvance) advance();
            return last;
        }

        throw new Error(message, "Syntax", current.Position);
    }

    private BlockStatement parseBlock()
    {
        var startPos = consume(TokenType.LCurlyBrace, "Expected '{' before block").Position.Start;
        var statements = new List<Statement>();
        while (!match(TokenType.RCurlyBrace)) 
        {
            statements.Add(parseStatement());
            if (isEnd) throw new Error("Expected '}'", "Syntax", current.Position);
        }
        var endPos = last.Position.End;

        return new(new(startPos, endPos), statements.ToArray());
    }

    private FuncParameter parseParameter()
    {
        var name = consume(TokenType.Identifier, "Expected parameter name").Lexeme;
        var startPos = last.Position.Start;
        consume(TokenType.Colon, "Expected ':' before parameter type");
        var type = parseIdentifier();
        var endPos = type.Position.End;
        return new(new(startPos, endPos), name, type);
    }

    private FuncParameter[] parseParameters()
    {
        consume(TokenType.LParen, "Expected '(' after function name");
        var list = new List<FuncParameter>();
        if (match(TokenType.RParen)) return list.ToArray();

        do list.Add(parseParameter());
        while(match(TokenType.Comma));

        consume(TokenType.RParen, "Expected ')' after function parameters");

        return list.ToArray();
    }

    private FuncDefineStatement parseFuncDef()
    {
        var startPos = consume(TokenType.Func, "Expected 'func' keyword before function name").Position.Start;
        var name = consume(TokenType.Identifier, "Expected function name").Lexeme;
        var parameters = parseParameters();
        var endPos = last.Position.End;
        IdentifierExpression? retType = null;
        if (match(TokenType.Colon))
        {
            if (!match(TokenType.LCurlyBrace, false)) retType = parseIdentifier();
        }

        if (retType != null) endPos = retType.Position.End;

        var block = parseBlock();

        return new(new(startPos, endPos), name, parameters, retType);
    }

    private VarAssigmentStatement parseVarAssigment(IdentifierExpression target)
    {
        var @operator = current.Lexeme;
        advance();
        var value = parseExpression();
        var endPos = consume(TokenType.Semicolon, "Expected ';' after expression").Position.End;
        return new(new(target.Position.Start, endPos), target, @operator, value);
    }

    private Statement parseStatement()
    {
        switch (current.Type)
        {
            case TokenType.Identifier :
            {
                var id = parseIdentifier();
                if (AssigmentOperators.Any(x => current.Lexeme == x)) return parseVarAssigment(id);
                consume(TokenType.Semicolon, "Expected ';'");
                return new StatementExpression(id);
            }
            case TokenType.Var : return parseVarAssign();
            case TokenType.LCurlyBrace : return parseBlock();
            case TokenType.Func : return parseFuncDef();
            default : throw new Error($"Unexpected '{current.Type}'", "Syntax", current.Position);
        }
    }

    private Expression[] parseFuncCall()
    {
        consume(TokenType.LParen, "Expected ( before function arguments");
        var list = new List<Expression>();
        if (match(TokenType.RParen)) return list.ToArray();
        do
        {
            list.Add(parseExpression());
        } while(match(TokenType.Comma));

        consume(TokenType.RParen, "Expected ) after function arguments");

        return list.ToArray();
    }

    private Expression[][] ParseFuncCalls()
    {
        var calls = new List<Expression[]>();
        while (match(TokenType.LParen, false)) 
        {
            calls.Add(parseFuncCall());
        }

        return calls.ToArray();
    }

    private IdentifierExpressionPart parseIdentifierPart() 
    {
        var id = consume(TokenType.Identifier, "Expected identifier");
        return new(id.Position, id.Lexeme, ParseFuncCalls(), null, null);
    }

    private IdentifierExpression parseIdentifier()
    {
        var startPos = current.Position.Start;
        var firstExpr = parseExpression();
        var otherParts = new List<IdentifierExpressionPart>();

        while (match(TokenType.Appeal)) otherParts.Add(parseIdentifierPart());

        var endPos = last.Position.End;

        return new(new(startPos, endPos), firstExpr, otherParts.ToArray());
    }

    private IdentifierExpression parseIdentifier(Expression firstExpr)
    {
        var startPos = current.Position.Start;

        var otherParts = new List<IdentifierExpressionPart>();

        while (match(TokenType.Appeal))
        {
            var id = consume(TokenType.Identifier, "Expected identifier");
            otherParts.Add(new(id.Position, id.Lexeme, ParseFuncCalls(), null, null));
        }

        var endPos = last.Position.End;

        return new(new(startPos, endPos), firstExpr, otherParts.ToArray());
    }

    private VarDeclStatement parseVarAssign()
    {
        var startPos = consume(TokenType.Var, "Expected var keyword").Position.Start;
        var name = consume(TokenType.Identifier, "Expected variable name").Lexeme;
        IdentifierExpression? type = null;

        if (match(TokenType.Colon)) type = parseIdentifier();
        
        consume(TokenType.Equate, "Expected '=' before variable value");

        var value = parseExpression();
        var endPos = consume(TokenType.Semicolon, "Expected ';' after variable assign").Position.End;
        
        return new(new(startPos, endPos), name, type, value);
    }

    private Expression parseExpression()
    {
        return parseAdditive();
    }

    private Expression parseAdditive()
    {
        var ex = parseMultiplicative();
        
        while (match(TokenType.Plus) || match(TokenType.Minus))
        {
            var op = last.Lexeme;
            var right = parseMultiplicative();
            ex = new BinaryExpression(new(ex.Position.Start, right.Position.End), ex, op, right);
        }

        return ex;
    }

    private Expression parseMultiplicative()
    {
        var ex = parsePrimaryExpression();
        
        while (match(TokenType.Slash) || match(TokenType.Asteric))
        {
            var op = last.Lexeme;
            var right = parsePrimaryExpression();
            ex = new BinaryExpression(new(ex.Position.Start, right.Position.End), ex, op, right);
        }

        return ex;
    }

    private Expression parsePrimaryExpression()
    {
        var ex = parsePrimaryBasicExpression();
        switch (current.Type)
        {
            case TokenType.Appeal : return parseIdentifier(ex);
        }

        if (ex is IdentifierExpressionPart idPart) ex = new IdentifierExpression(idPart.Position, idPart, Array.Empty<IdentifierExpressionPart>());

        return ex;
    }

    private Expression parsePrimaryBasicExpression()
    {
        switch (current.Type)
        {
            case TokenType.IntNumber :
            {
                advance();
                return new IntExpression(int.Parse(last.Lexeme), last.Position);
            }

            case TokenType.FloatNumber :
            {
                advance();
                return new FloatExpression(double.Parse(last.Lexeme), last.Position);
            }

            case TokenType.String :
            {
                advance();
                return new StringExpression(last.Lexeme, last.Position);
            }

            case TokenType.Identifier :
            {
                return parseIdentifierPart();
            }

            default : throw new Error("Expected expression", "Syntax", current.Position);
        }
    }

    public Statement[] Parse()
    {
        nodes.Clear();
        position = 0;

        while (!isEnd)
        {
            nodes.Add(parseStatement());
        }

        return nodes.ToArray();
    }
}
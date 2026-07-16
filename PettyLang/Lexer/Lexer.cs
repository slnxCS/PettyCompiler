namespace PettyLang.Lexer;

using PettyLang.Errors;
using PettyLang.Tokens;

public class Lexer(string source)
{
    private readonly string _source = source;
    private int _currentPosition;

    private int _currentPositionX, _currentPositionY;

    private Point2D _currentPosition2D => new(_currentPositionX, _currentPositionY);
    private Point2D _currentPosition2DIncrement => new(_currentPositionX + 1, _currentPositionY);

    private void advance() 
    {
        if (current == '\n')
        {
            _currentPositionX = 0;
            _currentPositionY++;
        }
        else _currentPositionX++;
        _currentPosition++;
    }
    private bool isEnd => _currentPosition >= _source.Length;
    private bool hasNextSymbol => _currentPosition + 1 < _source.Length;
    private char next => _source[_currentPosition + 1];
    private char last => _source[_currentPosition - 1];
    private char current => _source[_currentPosition];
    private List<Token> _tokens = new();

    private Dictionary<string, TokenType> Keywords = new()
    {
        {"func", TokenType.Func},
        {"var", TokenType.Var},
        {"import", TokenType.Import},
        {"package", TokenType.Package},
        {"return", TokenType.Return},
    }, 
    Symbols = new()
    {
        {"+", TokenType.Plus},
        {"-", TokenType.Minus},
        {"/", TokenType.Slash},
        {"*", TokenType.Asteric},
        {"=", TokenType.Equate},
        {"==", TokenType.Equals},
        {"+=", TokenType.PlusEquate},
        {"-=", TokenType.MinusEquate},
        {"/=", TokenType.DivideEquate},
        {"*=", TokenType.MultiplyEquate},
        {"(", TokenType.LParen},
        {")", TokenType.RParen},
        {"[", TokenType.LSquareBracket},
        {"]", TokenType.RSquareBracket},
        {"{", TokenType.LCurlyBrace},
        {"}", TokenType.RCurlyBrace},
        {"<", TokenType.LAngleBrace},
        {">", TokenType.RAngleBrace},
        {":", TokenType.Colon},
        //{"::", TokenType.Appeal},
        {";", TokenType.Semicolon},
        {".", TokenType.Dot},
        {",", TokenType.Comma},
    };

    const char SingleCommentSymbol = '#';

    private void _skipSingleLineComment()
    {
        while (!isEnd && current != '\n') advance();
    }

    private void _skipMultiLineComment()
    {
        advance();
        advance();

        while (!isEnd)
        {
            if (current == '>' && hasNextSymbol && next == '#') break;
            advance();
        }

        if (isEnd)
            throw new Error("Multi-line comment termination required (>#)", "Syntax", new(_currentPosition2D, _currentPosition2D));

        advance();
        advance();
    }

    private void _addID()
    {
        var str = "";
        var startPos = _currentPosition2DIncrement;
        while (!isEnd && (char.IsLetterOrDigit(current) || current == '_'))
        {
            str += current;
            advance();
        }

        var endPos = _currentPosition2D;

        var type = TokenType.Identifier;

        if (Keywords.TryGetValue(str, out var _type)) type = _type;

        _tokens.Add(new(str, type, new(startPos, endPos)));
    }

    private Token _processID()
    {
        var str = "";
        var startPos = _currentPosition2DIncrement;
        while (!isEnd && (char.IsLetterOrDigit(current) || current == '_'))
        {
            str += current;
            advance();
        }

        var endPos = _currentPosition2D;

        var type = TokenType.Identifier;

        if (Keywords.TryGetValue(str, out var _type)) type = _type;

        return new(str, type, new(startPos, endPos));
    }

    private void _addNumber()
    {
        var num = "";
        var isFloat = false;
        var startPos = _currentPosition2DIncrement;
        var isNegative = current == '-';

        if (isNegative)
        {
            num += '-';
            advance();
        }

        while (!isEnd && char.IsDigit(current))
        {
            num += current;
            advance();
            if (!isEnd && current == '.' && !(hasNextSymbol && !char.IsDigit(next)))
            {
                if (isFloat) break;
                isFloat = true;
                num += ',';
                advance();
            }
        }

        var endPos = _currentPosition2D;

        _tokens.Add(new(num, isFloat ? TokenType.FloatNumber : TokenType.IntNumber, new (startPos, endPos)));
    }

    private Token _processNumber()
    {
        var num = "";
        var isFloat = false;
        var startPos = _currentPosition2DIncrement;
        var isNegative = current == '-';

        if (isNegative)
        {
            num += '-';
            advance();
        }

        while (!isEnd && char.IsDigit(current))
        {
            num += current;
            advance();
            if (!isEnd && current == '.' && !(hasNextSymbol && !char.IsDigit(next)))
            {
                if (isFloat) break;
                isFloat = true;
                num += ',';
                advance();
            }
        }

        var endPos = _currentPosition2D;

        return new(num, isFloat ? TokenType.FloatNumber : TokenType.IntNumber, new (startPos, endPos));
    }

    public void _addString()
    {
        var str = "";
        var startPos = _currentPosition2DIncrement;

        Point2D? endl = null;

        advance();

        while (!isEnd && current != '\"')
        {
            if (current == '\n') endl = _currentPosition2D;
            str += current;
            advance();
        }

        if (isEnd) 
            throw new Error("String termination required", "Syntax", new Position(startPos, endl ?? _currentPosition2D));

        advance();

        var endPos = _currentPosition2D;

        _tokens.Add(new(str, TokenType.String, new(startPos, endPos)));
    }

    public Token _processString()
    {
        var str = "";
        var startPos = _currentPosition2DIncrement;

        Point2D? endl = null;

        advance();

        while (!isEnd && current != '\"')
        {
            if (current == '\n') endl = _currentPosition2D;
            str += current;
            advance();
        }

        if (current != '\"' && isEnd) 
            throw new Error("String termination required", "Syntax", new Position(startPos, endl ?? _currentPosition2D));

        advance();

        var endPos = _currentPosition2D;

        return new(str, TokenType.String, new(startPos, endPos));
    }

    public void _addChar()
    {
        advance();

        var startPos = _currentPosition2D;

        var str = current.ToString();

        advance();

        if (isEnd || current != '\'') 
            throw new Error("Char termination required", "Syntax", new Position(startPos, _currentPosition2D));

        var endPos = _currentPosition2D;

        _tokens.Add(new(str, TokenType.Char, new(startPos, endPos)));
    }

    public Token _processChar()
    {
        advance();

        var startPos = _currentPosition2D;

        var str = current.ToString();

        advance();

        if (isEnd || current != '\'') 
            throw new Error("Char termination required", "Syntax", new Position(startPos, _currentPosition2D));

        var endPos = _currentPosition2D;

        return new(str, TokenType.Char, new(startPos, endPos));
    }

    public void _addSymbol()
    {
        var startPos = _currentPosition2D;
        const int maxSymbolLen = 2;

        string? matched = null;
        var matchedType = TokenType._Unknown;

        var maxCheck = Math.Min(maxSymbolLen, _source.Length - _currentPosition);

        for (var len = maxCheck; len > 0; len--)
        {
            var candidate = _source.Substring(_currentPosition, len);

            if (Symbols.TryGetValue(candidate, out var type))
            {
                matchedType = type;
                matched = candidate;
                break;
            }
        }

        if (matched == null)
            throw new Error($"Unknown symbol : {current}",
                "Syntax", new(startPos, _currentPosition2DIncrement));
        
        for (var i = 0; i < matched.Length; i++) 
            advance();

        _tokens.Add(new(matched, matchedType, new(startPos, _currentPosition2D)));
    }

    public Token _processSymbol()
    {
        var startPos = _currentPosition2D;
        const int maxSymbolLen = 2;

        string? matched = null;
        var matchedType = TokenType._Unknown;

        var maxCheck = Math.Min(maxSymbolLen, _source.Length - _currentPosition);

        for (var len = maxCheck; len > 0; len--)
        {
            var candidate = _source.Substring(_currentPosition, len);

            if (Symbols.TryGetValue(candidate, out var type))
            {
                matchedType = type;
                matched = candidate;
                break;
            }
        }

        if (matched == null)
            throw new Error($"Unknown symbol : {current}",
                "Syntax", new(startPos, _currentPosition2DIncrement));
        
        for (var i = 0; i < matched.Length; i++) 
            advance();

        return new(matched, matchedType, new(startPos, _currentPosition2D));
    }
    
    private Token? _process()
    {
        if (current == SingleCommentSymbol && hasNextSymbol && next == '<')
        {
            _skipMultiLineComment();
            return null;
        }
        if (char.IsWhiteSpace(current))
        {
            advance();
            return null;
        }

        if (char.IsLetter(current) || current == '_') return _processID();
        else if (current == '\"') return _processString();
        else if (current == '\'') return _processChar();
        else if (char.IsDigit(current) || current == '-' && hasNextSymbol && char.IsDigit(next)) return _processNumber();
        else return _processSymbol();
    }

    private Token[] _getWhile(char c)
    {
        var list = new List<Token>();
        while (!isEnd && current != c)
        {
            var res = _process();
            if (res == null) continue;
            list.Add(res);
        }

        return list.ToArray();
    }

    public Token[] Tokenize()
    {
        _currentPosition = 0;
        _currentPositionX = _currentPositionY = 1;
        _tokens.Clear();

        while (!isEnd)
        {
            if (current == SingleCommentSymbol && hasNextSymbol && next == '<')
            {
                _skipMultiLineComment();
                continue;
            }

            if (current == SingleCommentSymbol)
            {
                _skipSingleLineComment();
                continue;
            }

            if (char.IsWhiteSpace(current))
            {
                advance();
                continue;
            }

            if (char.IsLetter(current) || current == '_') _addID();
            else if (current == '\"') _addString();
            else if (current == '\'') _addChar();
            else if (char.IsDigit(current) || current == '-' && hasNextSymbol && char.IsDigit(next)) _addNumber();
            else _addSymbol();
        }

        _tokens.Add(new("", TokenType.EOF, new Position()));

        return _tokens.ToArray();
    }
}
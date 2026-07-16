namespace PettyLang.Tokens;

public enum TokenType
{
    // Standart : 
    _Unknown,
    EOF,
    Identifier,
    String,
    Char,
    IntNumber,
    FloatNumber,
    
    // Keywords :
    Func, Var, Import, Package, Return,

    // Symbols :

    Plus, Minus, Slash, Asteric, Equate, Equals, 
    PlusEquate, MinusEquate, DivideEquate, MultiplyEquate,
    LParen, RParen, LSquareBracket, RSquareBracket, LCurlyBrace, RCurlyBrace,
    Colon, Semicolon, LAngleBrace, RAngleBrace, Dot, Comma,
}

public class Token(string lexeme, TokenType type, Position position) : object()
{
    public readonly string Lexeme = lexeme;
    public readonly TokenType Type = type;
    public readonly Position Position = position;

    public override string ToString()
    {
        return $"lexeme = '{Lexeme}'; type = '{Type}'";
    }
}
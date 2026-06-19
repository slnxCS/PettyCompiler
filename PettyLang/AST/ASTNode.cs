namespace PettyLang.AST;

public abstract class ASTNode(Position position)
{
    public Position Position = position;
}
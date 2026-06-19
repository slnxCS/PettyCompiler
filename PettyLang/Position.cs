namespace PettyLang;

public readonly record struct Point2D(int X = 0, int Y = 0);
public readonly record struct Position(Point2D Start, Point2D End); 
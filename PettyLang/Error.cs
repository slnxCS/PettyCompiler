namespace PettyLang.Errors;

public class Error(string message, string errorType, Position position) : Exception($"{errorType} error on ({position.Start.X};{position.Start.Y}) : \n\t{message}");
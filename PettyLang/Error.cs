namespace PettyLang.Errors;

public class Error(string message, string errorType, Position position) : Exception($"{errorType} error on ({position.Start.X};{position.Start.Y}) : \n\t{message}");
public class NotSupportedOperandsError(string left, string right, string @operator, Position position) : 
    Error($"Cannot use operator {@operator} on operands of types {left} and {right}", "Semantic", position) {}

public class NotSupportedCastTypeError(string left, string right, Position position) : 
    Error($"Cannot cast object {left} to type {right}", "Semantic", position) {}

public class NotCallableError(string name, Position position) : 
    Error($"Cannot call {name} : object is not callable", "Semantic", position) {}
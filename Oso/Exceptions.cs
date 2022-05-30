using System.Diagnostics;
using System.Reflection;
using System.Resources;
using System.Text.Json;

namespace Oso;

public class OsoException : Exception
{
    public Dictionary<string, object>? Details;
    public OsoException() { }

    public OsoException(string? message) : base(message) { }

    public OsoException(string? message, Dictionary<string, object> details) : base(message)
    {
        Details = details;
    }

    public OsoException(string? message, Exception? inner) : base(message, inner) { }

    internal static OsoException ParseError(string? polarError)
    {
        if (polarError == null) return new OsoException("FFI error not found");

        var errorJson = JsonDocument.Parse(polarError).RootElement;
        var msg = errorJson.GetProperty("formatted").GetString();
        var property = errorJson.GetProperty("kind").EnumerateObject().First();
        var kind = property.Name;
        var data = property.Value;
        string subkind;
        Dictionary<string, object>? details = null;
        if (data.ValueKind == JsonValueKind.Object)
        {
            var subkindProp = data.EnumerateObject().First();
            subkind = subkindProp.Name;
            details = subkindProp.Value.Deserialize<Dictionary<string, object>>();
        }
        else
        {
            subkind = errorJson.GetProperty("kind").GetProperty(kind).GetString();
        }

        switch (kind)
        {
            case "Parse":
                switch (subkind)
                {
                    case "ExtraToken":
                        // return new ExtraToken(msg, details);
                        return new OsoException($"Extra token: {msg}", details);
                    case "IntegerOverflow":
                        // return new IntegerOverflow(msg, details);
                        return new OsoException($"Integer overflow: {msg}", details);
                    case "InvalidToken":
                        // return new InvalidToken(msg, details);
                        return new OsoException($"Invalid token: {msg}", details);
                    case "InvalidTokenCharacter":
                        // return new InvalidTokenCharacter(msg, details);
                        return new OsoException($"Invalid token character: {msg}", details);
                    case "UnrecognizedEOF":
                        // return new UnrecognizedEOF(msg, details);
                        return new OsoException($"Unrecognized EOF: {msg}", details);
                    case "UnrecognizedToken":
                        // return new UnrecognizedToken(msg, details);
                        return new OsoException($"Unrecognized token: {msg}", details);
                    default:
                        // return new ParseError(msg, details);
                        return new OsoException($"Parse error: {msg}", details);
                }
            /*
        case "Runtime":
            return;
        case "Operational":
            return;
        case "Validation":
            return;
            */
            default:
                return new OsoException(msg, details);
        }

    }
}

public class PolarRuntimeException : OsoException
{
    public PolarRuntimeException() { }

    public PolarRuntimeException(string? message) : base(message) { }

    public PolarRuntimeException(string? message, Exception? inner) : base(message, inner) { }
}

// TODO: Do we need so many exception types?
public class InvalidAttributeException : PolarRuntimeException
{
    public InvalidAttributeException(string? message) : base(message) { }

    public InvalidAttributeException(string className, string attrName) : base($"Invalid attribute `{attrName}` on class {className}") { }

}

public class InvalidCallException : PolarRuntimeException
{
    public InvalidCallException() { }
    public InvalidCallException(string? message) : base(message) { }
    public InvalidCallException(string? message, Exception? inner) : base(message, inner) { }
    public InvalidCallException(string className, string callName, params Type[] argTypes) : base($"Invalid call {callName} on class {className}, with argument types `{string.Join(", ", argTypes.Select(t => t.Name))}`") { }
}

public class InvalidConstructorException : PolarRuntimeException
{
    public InvalidConstructorException(string? message) : base(message) { }
}

public class UnregisteredInstanceException : PolarRuntimeException
{
    internal UnregisteredInstanceException(ulong instanceId) : base(Exceptions.GetExceptionMessage("UnregisteredInstance", instanceId)) { }
}

public class UnregisteredClassException : PolarRuntimeException
{
    public string ClassName { get; }
    internal UnregisteredClassException(string className) : base(Exceptions.GetExceptionMessage("UnregisteredClass", className))
    {
        ClassName = className;
    }
}

public class UnexpectedPolarTypeException : PolarRuntimeException
{
    internal UnexpectedPolarTypeException() : base(Exceptions.GetExceptionMessage("UnexpectedPolarType")) { }

    internal UnexpectedPolarTypeException(string polarType) : base(Exceptions.GetExceptionMessage("UnexpectedPolarType", polarType)) { }
}

public class MissingConstructorException : PolarRuntimeException
{
    public string ClassName { get; }
    internal MissingConstructorException(string className) : base(Exceptions.GetExceptionMessage("MissingConstructor", className))
    {
        ClassName = className;
    }
}

public class DuplicateClassAliasException : PolarRuntimeException
{
    internal DuplicateClassAliasException(string alias, Type oldClass, Type newClass) : base(Exceptions.GetExceptionMessage("DuplicateClassAlias", alias, oldClass, newClass))
    {
    }
}

public class InstantiationException : PolarRuntimeException
{
    public string ClassName { get; }
    internal InstantiationException(string className, Exception e) : base(Exceptions.GetExceptionMessage("Instantiation", className), e)
    {
        ClassName = className;
    }
}
public class AuthorizationException : OsoException
{
    public AuthorizationException(string message) : base(message) { }
}

public class ForbiddenException : AuthorizationException
{
    public ForbiddenException() : base(Exceptions.GetExceptionMessage("Forbidden")) { }
}
public class NotFoundException : AuthorizationException
{
    public NotFoundException() : base(Exceptions.GetExceptionMessage("NotFound")) { }
}

internal static class Exceptions
{
    static ResourceManager _resourceManager = new ResourceManager("Oso.Resources.ExceptionMessages", Assembly.GetExecutingAssembly());
    internal static string GetExceptionMessage(string messageName)
    {
        string? message = _resourceManager.GetString(messageName);
        Debug.Assert(message != null);
        return message;
    }
    internal static string GetExceptionMessage(string messageName, params object[] args)
    {
        return string.Format(GetExceptionMessage(messageName), args);
    }
}
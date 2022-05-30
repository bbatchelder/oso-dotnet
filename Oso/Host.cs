using System.Reflection;
using System.Resources;
using System.Text.Json;
using Oso.Ffi;

namespace Oso;

public class Host
{
    private readonly PolarHandle _handle;
    private readonly Dictionary<string, Type> _classes = new();
    private readonly Dictionary<Type, ulong> _classIds = new();
    private readonly Dictionary<ulong, object?> _instances = new();
    private readonly bool _acceptExpression;

    internal Host(PolarHandle handle)
    {
        _handle = handle;
    }

    /// <summary>Copies
    private Host(PolarHandle handle, Dictionary<string, Type> classes, Dictionary<Type, ulong> classIds, Dictionary<ulong, object?> instances, bool acceptExpression)
    {
        _handle = handle;
        _classes = new(classes);
        _classIds = new(classIds);
        _instances = new(instances);
        _acceptExpression = acceptExpression;
    }

    internal Host Clone() => new Host(_handle, _classes, _classIds, _instances, _acceptExpression);

    public bool AcceptExpression { get; set; }
    public Dictionary<string, object> DeserializePolarDictionary(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new OsoException($"Expected a JSON object element, received {element.ValueKind}");

        return element.EnumerateObject()
                        .Select(property => new KeyValuePair<string, object>(property.Name, ParsePolarTerm(property.Value)))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public List<object> DeserializePolarList(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new OsoException($"Expected a JSON array element, received {element.ValueKind}");

        return element.EnumerateArray().Select(ParsePolarTerm).ToList();
    }

    /// <summary>
    /// Make an instance of a class from a <see cref="List&lt;object&gt;" /> of fields. 
    /// </summary>
    internal object MakeInstance(string className, List<object> constructorArgs, ulong instanceId)
    {

        if (!_classes.TryGetValue(className, out Type? t)) throw new UnregisteredClassException(className);
        var argTypes = constructorArgs.Select(o => o.GetType()).ToArray();

        var constructor = t.GetConstructors().FirstOrDefault(c =>
        {
            var paramTypes = c.GetParameters().Select(p => p.ParameterType).ToArray();
            if (argTypes.Count() == paramTypes.Count())
            {
                for (int i = 0; i < paramTypes.Count(); i++)
                {
                    if (!paramTypes[i].IsAssignableFrom(argTypes[i])) return false;
                }
                return true;
            }
            return false;
        });

        if (constructor == null) throw new MissingConstructorException(className);
        object instance;
        try
        {
            instance = constructor.Invoke(constructorArgs.ToArray());
        }
        catch (Exception e)
        {
            throw new InstantiationException(className, e);
        }
        CacheInstance(instance, instanceId);
        return instance;
    }

    /// <summary>
    /// </summary>
    internal bool IsA(JsonElement instance, string className)
    {
        if (_classes.TryGetValue(className, out Type? t))
        {
            return ParsePolarTerm(instance)?.GetType() == t;
        }
        else throw new UnregisteredClassException(className);
    }

    /// <summary>
    /// </summary>
    internal bool IsSubclass(string leftTag, string rightTag)
    {
        if (!_classes.TryGetValue(leftTag, out Type? leftClass)) throw new OsoException($"Unregistered class exception: {leftTag}");
        if (!_classes.TryGetValue(rightTag, out Type? rightClass)) throw new OsoException($"Unregistered class exception: {leftTag}");
        return rightClass.IsAssignableFrom(leftClass);
    }

    /// <summary>
    /// </summary>
    internal bool Subspecializer(ulong instanceId, string leftTag, string rightTag)
    {
        throw new NotImplementedException();
    }
    /// <summary>
    /// Turn a Polar term passed across the FFI boundary into an <see cref="object" />.
    /// </summary>
    public object? ParsePolarTerm(JsonElement term)
    {
        /*
            {
                "value": {"String": "someValue" }
            }

         */
        // TODO: Would this be better as a JsonConverter?
        JsonElement value = term.GetProperty("value");
        var property = value.EnumerateObject().First();
        string tag = property.Name;
        switch (tag)
        {
            case "String":
                return property.Value.GetString();
            case "Boolean":
                return property.Value.GetBoolean();
            case "Number":
                JsonProperty numProperty = property.Value.EnumerateObject().First();
                string numType = numProperty.Name;
                switch (numType)
                {
                    case "Integer":
                        return numProperty.Value.GetInt32();
                    case "Float":
                        if (numProperty.Value.ValueKind == JsonValueKind.String)
                        {
                            return numProperty.Value.GetString() switch
                            {
                                "Infinity" => double.PositiveInfinity,
                                "-Infinity" => double.NegativeInfinity,
                                "NaN" => double.NaN,
                                var f => throw new OsoException($"Expected a floating point number, got `{f}`"),
                            };
                        }
                        return numProperty.Value.GetDouble();
                }
                throw new OsoException("Unexpected Number type: {numType}");
            case "List":
                return DeserializePolarList(property.Value);
            case "Dictionary":
                return DeserializePolarDictionary(property.Value.GetProperty("fields"));
            case "ExternalInstance":
                return GetInstance(property.Value.GetProperty("instance_id").GetUInt64());
            case "Call":
                string name = property.Value.GetProperty("name").GetString();
                List<object> args = DeserializePolarList(property.Value.GetProperty("args"));
                return new Predicate(name, args);
            case "Variable":
                return new Variable(property.Value.GetString());
            case "Expression":
                if (!AcceptExpression) throw new UnexpectedPolarTypeException(Exceptions.GetExceptionMessage("UnexpectedPolarExpression"));
                return new Expression(
                    Enum.Parse<Operator>(property.Value.GetProperty("operator").GetString()),
                    DeserializePolarList(property.Value.GetProperty("args")));
            case "Pattern":
                JsonProperty pattern = property.Value.EnumerateObject().First();
                string patternTag = pattern.Name;
                return patternTag switch
                {
                    "Instance" => new Pattern(
                                                pattern.Value.GetProperty("tag").GetString(),
                                                DeserializePolarDictionary(pattern.Value.GetProperty("fields").GetProperty("fields"))),
                    "Dictionary" => new Pattern(null, DeserializePolarDictionary(pattern.Value)),
                    _ => throw new UnexpectedPolarTypeException(Exceptions.GetExceptionMessage("UnexpectedPolarType", $"Pattern: {patternTag}")),
                };
            default:
                throw new UnexpectedPolarTypeException(Exceptions.GetExceptionMessage("UnexpectedPolarType", tag));
        }
    }

    public JsonElement SerializePolarTerm(object value)
    {
        // Build Polar value
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream);
        writer.WriteStartObject();
        writer.WriteNumber("id", 0);
        writer.WriteNumber("offset", 0);
        writer.WriteStartObject("value");
        if (value is bool b)
        {
            writer.WriteBoolean("Boolean", b);
        }
        else if (value is int i)
        {
            writer.WriteStartObject("Number");
            writer.WriteNumber("Integer", i);
            writer.WriteEndObject();
        }
        else if (value is double or float)
        {
            writer.WriteStartObject("Number");
            writer.WritePropertyName("Float");
            double doubleValue = (double)value;
            if (double.IsPositiveInfinity(doubleValue))
            {
                writer.WriteStringValue("Infinity");
            }
            else if (double.IsNegativeInfinity(doubleValue))
            {
                writer.WriteStringValue("-Infinity");
            }
            else if (double.IsNaN(doubleValue))
            {
                writer.WriteStringValue("NaN");
            }
            else
            {
                writer.WriteNumberValue(doubleValue);
            }
            writer.WriteEndObject();
        }
        else if (value is string stringValue)
        {
            writer.WriteString("String", stringValue);
        }
        else if (value != null && (value.GetType().IsArray || (value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(List<>))))
        {
            writer.WritePropertyName("List");
            SerializePolarList(writer, value);
        }
        else if (value != null && value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            writer.WritePropertyName("Dictionary");
            SerializePolarDictionary(writer, value);
        }
        else if (value is Predicate pred)
        {
            writer.WriteStartObject("Call");
            writer.WriteString("name", pred.Name);
            writer.WritePropertyName("args");
            SerializePolarList(writer, pred.Arguments ?? new List<object>());
            writer.WriteEndObject();
        }
        else if (value is Variable variable)
        {
            writer.WriteString("Variable", variable.Name);
        }
        else if (value is Expression expression)
        {
            writer.WriteStartObject("Expression");
            writer.WriteString("operator", expression.Operator.ToString());
            writer.WritePropertyName("args");
            SerializePolarList(writer, expression.Args);
            writer.WriteEndObject();
        }
        else if (value is Pattern pattern)
        {
            writer.WriteStartObject("Pattern");
            if (pattern.Tag == null)
            {
                writer.WriteRawValue(SerializePolarTerm(pattern.Fields).ToString());
            }
            else
            {
                writer.WritePropertyName("Instance");
                writer.WriteStartObject();
                writer.WritePropertyName("fields");
                SerializePolarDictionary(writer, pattern.Fields);
                writer.WriteString("tag", pattern.Tag);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteStartObject("ExternalInstance");
            ulong? instanceId = null;

            // if the object is a Class, then it will already have an instance ID
            if (value is Type t)
            {
                instanceId = _classIds[t];
            }

            writer.WriteNumber("instance_id", CacheInstance(value, instanceId)); // TODO
            writer.WriteString("repr", value?.ToString() ?? "null");

            // pass a class_repr string *for registered types only*
            if (value != null)
            {
                Type valueType = value.GetType();
                string valueTypeRepr = _classIds.ContainsKey(valueType)
                    ? valueType.ToString()
                    : "null";
                writer.WriteString("class_repr", valueTypeRepr);
            }
            else
            {
                writer.WriteNull("class_repr");
            }

            writer.WriteEndObject();
        }

        // Build Polar term
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();
        var reader = new Utf8JsonReader(stream.ToArray());
        return JsonElement.ParseValue(ref reader);
    }

    public ulong CacheInstance(object? instance, ulong? id)
    {
        ulong i = id ?? Native.polar_get_external_id(_handle);
        _instances[i] = instance;
        return i;
    }

    public string CacheClass(Type t, string name)
    {
        if (_classes.TryGetValue(name, out Type? oldType))
        {
            throw new DuplicateClassAliasException(name, oldType, t);
        }

        _classes[name] = t;
        _classIds[t] = CacheInstance(t, null);
        return name;
    }

    void SerializePolarList(Utf8JsonWriter writer, object listLikeObject)
    {
        // We support int, double, float, bool, and string
        writer.WriteStartArray();
        if (listLikeObject is IEnumerable<int> intList)
        {
            foreach (var element in intList)
            {
                writer.WriteRawValue(SerializePolarTerm(element).ToString());
            }
        }
        else if (listLikeObject is IEnumerable<double> doubleList)
        {
            foreach (var element in doubleList)
            {
                writer.WriteRawValue(SerializePolarTerm(element).ToString());
            }
        }
        else if (listLikeObject is IEnumerable<float> floatList)
        {
            foreach (var element in floatList)
            {
                writer.WriteRawValue(SerializePolarTerm(element).ToString());
            }
        }
        else if (listLikeObject is IEnumerable<bool> boolList)
        {
            foreach (var element in boolList)
            {
                writer.WriteRawValue(SerializePolarTerm(element).ToString());
            }
        }
        else if (listLikeObject is IEnumerable<string> stringList)
        {
            foreach (var element in stringList)
            {
                writer.WriteRawValue(SerializePolarTerm(element).ToString());
            }
        }
        else if (listLikeObject is IEnumerable<object> objList)
        {
            foreach (var element in objList)
            {
                writer.WriteRawValue(SerializePolarTerm(element).ToString());
            }
        }
        else
        {
            throw new OsoException($"Cannot support list of type {listLikeObject.GetType()}.");
        }

        writer.WriteEndArray();
    }

    void SerializePolarDictionary(Utf8JsonWriter writer, object dictObject)
    {
        writer.WriteStartObject();
        writer.WriteStartObject("fields");
        // Polar only supports dictionaries with string keys. Convert a map to a map of
        // string keys.
        if (dictObject is Dictionary<string, int> intMap)
        {
            foreach (var (k, v) in intMap)
            {
                writer.WritePropertyName(k);
                writer.WriteRawValue(SerializePolarTerm(v).ToString());
            }
        }
        else if (dictObject is Dictionary<string, double> doubleMap)
        {
            foreach (var (k, v) in doubleMap)
            {
                writer.WritePropertyName(k);
                writer.WriteRawValue(SerializePolarTerm(v).ToString());
            }
        }
        else if (dictObject is Dictionary<string, float> floatMap)
        {
            foreach (var (k, v) in floatMap)
            {
                writer.WritePropertyName(k);
                writer.WriteRawValue(SerializePolarTerm(v).ToString());
            }
        }
        else if (dictObject is Dictionary<string, bool> boolMap)
        {
            foreach (var (k, v) in boolMap)
            {
                writer.WritePropertyName(k);
                writer.WriteRawValue(SerializePolarTerm(v).ToString());
            }
        }
        else if (dictObject is Dictionary<string, string> stringMap)
        {
            foreach (var (k, v) in stringMap)
            {
                writer.WritePropertyName(k);
                writer.WriteRawValue(SerializePolarTerm(v).ToString());
            }
        }
        else if (dictObject is Dictionary<string, object> objMap)
        {
            foreach (var (k, v) in objMap)
            {
                writer.WritePropertyName(k);
                writer.WriteRawValue(SerializePolarTerm(v).ToString());
            }
        }
        else
        {
            throw new UnexpectedPolarTypeException("Cannot convert map with non-string keys to Polar");
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    // TODO: Does this need to be public?
    public bool Operator(string op, List<object> args)
    {
        Object left = args[0], right = args[1];
        if ("Eq".Equals(op, StringComparison.InvariantCulture))
        {
            if (left == null) return left == right;
            else return left.Equals(right);
        }
        throw new OsoException($"{op} are unimplemented in the oso .NET library");
    }

    /// <summary>
    /// Determine if an instance has been cached.
    /// </summary>
    public bool HasInstance(ulong instanceId) => _instances.ContainsKey(instanceId);

    internal void RegisterMros()
    {
        foreach (var (key, value) in _classes)
        {
            var superType = value.BaseType;
            var mro = new List<ulong>();
            while (superType != null)
            {
                if (_classIds.TryGetValue(superType, out ulong id))
                {
                    mro.Add(id);
                }
                superType = superType.BaseType;
            }
            Native.RegisterMro(_handle, key, JsonSerializer.Serialize(mro));
        }
    }

    private object? GetInstance(ulong instanceId)
    {
        return _instances.TryGetValue(instanceId, out object? value)
            ? value
            : throw new UnregisteredInstanceException(instanceId);
    }
}
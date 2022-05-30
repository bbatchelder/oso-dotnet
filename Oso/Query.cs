using System.Reflection;
using System.Text.Json;
using Oso.Ffi;

namespace Oso;

public class Query : IDisposable
{

    private readonly QueryHandle _handle;
    private readonly Host _host;
    private Dictionary<ulong, IEnumerator<object>> _calls = new();

    private Dictionary<string, object>? _currentResult;

    internal Query(QueryHandle handle, Host host)
    {
        _handle = handle;
        _host = host;
        NextResult();
    }

    // struct polar_CResult_c_char *polar_next_query_event(struct polar_Query *query_ptr);
    // TODO: 
    /*
    public IEnumerator<string> QueryEvents
    {
        get
        {
            // Add error handling to check for error and throw PolarException
        }
    }
    */
    private bool _resultsEnumerated;
    private List<Dictionary<string, object>> _results = new();

    /// <remarks>Not thread-safe, and does not support concurrent enumerations. Fully enumerate once before calling again.</remarks>
    public IEnumerable<Dictionary<string, object>> Results
    {
        get
        {
            return _resultsEnumerated ? _results : EnumerateResults();
        }
    }
    private IEnumerable<Dictionary<string, object>> EnumerateResults()
    {
        while (_currentResult != null)
        {
            _results.Add(_currentResult);
            yield return _currentResult;
            NextResult();
        }
    }

    /// Generate the next Query result
    private void NextResult()
    {
        while (true)
        {
            // TODO: Check if this can actually be null
            string eventStr = Native.NextQueryEvent(_handle)!;
            string kind, className;
            JsonElement data, instance;
            ulong callId;

            try
            {
                JsonElement queryEvent = JsonDocument.Parse(eventStr).RootElement;
                var property = queryEvent.EnumerateObject().First();
                kind = property.Name;
                data = property.Value;
            }
            catch (JsonException)
            {
                // TODO: we should have a consistent serialization format
                kind = eventStr.Replace("\"", "");
                throw new PolarRuntimeException("Unhandled event type: " + kind);
            }

            switch (kind)
            {
                case "Done":
                    _currentResult = null;
                    _resultsEnumerated = true;
                    return;
                case "Result":
                    _currentResult = _host.DeserializePolarDictionary(data.GetProperty("bindings"));
                    return;
                case "MakeExternal":
                    ulong id = data.GetProperty("instance_id").GetUInt64();
                    if (_host.HasInstance(id))
                    {
                        // TODO: More specific exceptions?
                        // throw new DuplicateInstanceRegistrationError(id);
                        throw new OsoException($"Duplicate instance registration: {id}");
                    }

                    JsonElement constructor = data.GetProperty("constructor").GetProperty("value");
                    if (constructor.TryGetProperty("Call", out JsonElement call))
                    {
                        className = call.GetProperty("name").GetString();
                        var initargs = call.GetProperty("args");

                        // kwargs should are not supported in .NET and should always be null
                        if (call.GetProperty("kwargs").ValueKind != JsonValueKind.Null)
                        {
                            // TODO: More specific exceptions?
                            // throw new InstantiationError(className);
                            throw new OsoException($"Failed to instantiate external class {className}; named arguments are not supported in .NET");
                        }
                        _host.MakeInstance(className, _host.DeserializePolarList(initargs), id);
                        break;
                    }
                    else
                    {
                        // TODO: should this be an ArgumentException?
                        throw new InvalidConstructorException("Bad constructor");
                    }
                case "ExternalCall":
                    {

                        instance = data.GetProperty("instance");
                        callId = data.GetProperty("call_id").GetUInt64();
                        string attrName = data.GetProperty("attribute").GetString();

                        JsonElement? jArgs;
                        if (data.TryGetProperty("args", out JsonElement args) && args.ValueKind != JsonValueKind.Null)
                        {
                            jArgs = args;
                        }
                        else
                        {
                            jArgs = null;
                        }
                        if (data.GetProperty("kwargs").ValueKind != JsonValueKind.Null)
                        {
                            // TODO: _Could_ we support this with named arguments?
                            throw new InvalidCallException("The .NET Oso library does not support keyword arguments");
                        }
                        HandleCall(attrName, jArgs, instance, callId);
                        break;
                    }
                case "ExternalIsa":
                    instance = data.GetProperty("instance");
                    callId = data.GetProperty("call_id").GetUInt64();
                    className = data.GetProperty("class_tag").GetString();
                    int answer = _host.IsA(instance, className) ? 1 : 0;
                    Native.QuestionResult(_handle, callId, answer);
                    break;
                case "ExternalIsSubSpecializer":
                    ulong instanceId = data.GetProperty("instance_id").GetUInt64();
                    callId = data.GetProperty("call_id").GetUInt64();
                    string leftTag = data.GetProperty("left_class_tag").GetString();
                    string rightTag = data.GetProperty("right_class_tag").GetString();
                    answer = _host.Subspecializer(instanceId, leftTag, rightTag) ? 1 : 0;
                    Native.QuestionResult(_handle, callId, answer);
                    break;
                case "ExternalIsSubclass":
                    callId = data.GetProperty("call_id").GetUInt64();
                    answer =
                        _host.IsSubclass(data.GetProperty("left_class_tag").GetString(), data.GetProperty("right_class_tag").GetString())
                            ? 1
                            : 0;
                    Native.QuestionResult(_handle, callId, answer);
                    break;
                case "ExternalOp":
                    {
                        callId = data.GetProperty("call_id").GetUInt64();
                        var args = data.GetProperty("args");
                        answer = _host.Operator(data.GetProperty("operator").GetString(), _host.DeserializePolarList(args)) ? 1 : 0;
                        Native.QuestionResult(_handle, callId, answer);

                        break;
                    }
                case "NextExternal":
                    callId = data.GetProperty("call_id").GetUInt64();
                    JsonElement iterable = data.GetProperty("iterable");
                    HandleNextExternal(callId, iterable);
                    break;
                case "Debug":
                    if (data.TryGetProperty("message", out JsonElement messageElement))
                    {
                        string message = messageElement.GetString();
                        // TODO: Replace with ILogger or text stream writer
                        Console.WriteLine(message);
                    }

                    Console.Write("debug> ");
                    try
                    {
                        string? input = Console.ReadLine();
                        if (input == null) break;
                        string command = _host.SerializePolarTerm(input).ToString();
                        Native.DebugCommand(_handle, command);
                    }
                    catch (IOException e)
                    {
                        throw new PolarRuntimeException("Caused by: " + e.Message);
                    }
                    break;
                default:
                    throw new PolarRuntimeException("Unhandled event type: " + kind);
            }
        }
    }

    private void HandleCall(string attrName, JsonElement? jArgs, JsonElement polarInstance, ulong callId)
    {
        List<object>? args = null;
        if (jArgs != null)
        {
            args = _host.DeserializePolarList((JsonElement)jArgs!);
        }
        try
        {
            object instance = _host.ParsePolarTerm(polarInstance);
            // Select a method to call based on the types of the arguments.
            object result = null;
            try
            {
                // Class<?> cls = instance instanceof Class ? (Class<?>) instance : instance.getClass();
                Type type = instance is Type t ? t : instance.GetType();
                if (args != null)
                {
                    Type[] argTypes = args.Select(a => a.GetType()).ToArray();
                    // TODO: Determine whether this is a performance bottleneck
                    // TODO: This can be limited to search just for MethodInfo, I think
                    MethodInfo? method = type.GetMembers()
                        .Where(mi => mi.MemberType == MemberTypes.Method && mi.Name == attrName)
                        .Cast<MethodInfo>()
                        .FirstOrDefault(mi => mi.GetParameters().Select(pi => pi.ParameterType).ToArray().SequenceEqual(argTypes));
                    if (method == null)
                    {
                        throw new InvalidCallException(type.Name, attrName, argTypes);
                    }

                    result = method.Invoke(instance, args.ToArray());
                }
                else
                {
                    // Look for a field with the given name.
                    PropertyInfo property = type.GetProperty(attrName) ?? throw new InvalidAttributeException(type.Name, attrName);
                    result = property.GetValue(instance);
                }
                string term = _host.SerializePolarTerm(result).ToString();
                Native.CallResult(_handle, callId, term);

            }
            catch (MemberAccessException e)
            {
                throw new InvalidCallException("Failed to handle call", e);
            }
            catch (TargetInvocationException tie)
            {
                throw new InvalidCallException("Failed to handle call", tie);
            }
        }
        catch (InvalidCallException e)
        {
            Native.ReturnApplicationError(_handle, e.Message);
            Native.CallResult(_handle, callId, "null");
        }
        catch (InvalidAttributeException e)
        {
            Native.ReturnApplicationError(_handle, e.Message);
            Native.CallResult(_handle, callId, "null");
        }
    }

    private void HandleNextExternal(ulong callId, JsonElement iterable)
    {
        if (!_calls.ContainsKey(callId))
        {
            var result = _host.ParsePolarTerm(iterable);
            _calls[callId] = result switch
            {
                IEnumerable<object> oList => oList.GetEnumerator(),
                IEnumerable<int> intList => intList.Cast<object>().GetEnumerator(),
                IEnumerable<double> doubleList => doubleList.Cast<object>().GetEnumerator(),
                IEnumerable<float> floatList => floatList.Cast<object>().GetEnumerator(),
                IEnumerable<bool> boolList => boolList.Cast<object>().GetEnumerator(),
                _ => throw new OsoException($"Invalid iterator: value {result.ToString()} of type {result.GetType()} is not iterable"),
            };
        }
        if (_calls.TryGetValue(callId, out IEnumerator<object>? e))
        {
            e.MoveNext();
            var call = e.Current;
            var cachedResult = _host.SerializePolarTerm(call).ToString();
            Native.CallResult(_handle, callId, cachedResult);
        }
        else throw new Exception($"Unregistered call ID: {callId}");
    }

    /**
     * Execute one debugger command for the given query.
     *
     * ## Returns
     * - `0` on error.
     * - `1` on success.
     *
     * ## Errors
     * - Provided value is NULL.
     * - Provided value contains malformed JSON.
     * - Provided value cannot be parsed to a Term wrapping a Value::String.
     * - Query.debug_command returns an error.
     * - Anything panics during the parsing/execution of the provided command.
     */
    // struct polar_CResult_c_void *polar_debug_command(struct polar_Query *query_ptr, const char *value);
    public void DebugCommand(string value)
    {
        Native.DebugCommand(_handle, value);
    }

    public bool TryDebugCommand(string value)
    {
        try
        {
            DebugCommand(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /*
    struct polar_CResult_c_void *polar_call_result(struct polar_Query *query_ptr,
                                                   uint64_t call_id,
                                                   const char *term);
   */
    public void CallResult(ulong callId, string term)
    {
        Native.CallResult(_handle, callId, term);
    }

    /*
    struct polar_CResult_c_void *polar_question_result(struct polar_Query *query_ptr,
                                                       uint64_t call_id,
                                                       int32_t result);
    */
    public void QuestionResult(ulong callId, int result)
    {
        Native.QuestionResult(_handle, callId, result);
    }

    // struct polar_CResult_c_void *polar_application_error(struct polar_Query *query_ptr, char *message);
    public void ReturnApplicationError(string message)
    {
        Native.ReturnApplicationError(_handle, message);
    }

    // struct polar_CResult_c_char *polar_next_query_message(struct polar_Query *query_ptr);
    // TODO: Turn this into an iterator?
    /*
    public IEnumerator<string> Messages
    {
        get
        {
            // Add error handling to check for error and throw PolarException
        }
    }
    */
    private string? NextMessage()
    {
        return Native.NextQueryMessage(_handle);
    }

    private void ProcessMessages()
    {
        string? message = NextMessage();
        while (message != null)
        {
            ProcessMessage(message);
            message = NextMessage();
        }
    }

    private void ProcessMessage(string queryMessage)
    {
        try
        {
            JsonElement doc = JsonDocument.Parse(queryMessage).RootElement;
            string? kind = doc.GetProperty("kind").GetString();
            string? msg = doc.GetProperty("msg").GetString();
            if (kind == "Print")
            {
                // TODO: Replace this with a text stream writer or ILogger
                Console.WriteLine(msg);
            }
            else if (kind == "Warning")
            {
                Console.WriteLine($"[warning] {msg}");
            }
        }
        catch (JsonException)
        {
            throw new OsoException($"Invalid JSON Message: {queryMessage}");
        }

    }

    // struct polar_CResult_c_char *polar_query_source_info(struct polar_Query *query_ptr);
    internal string? Source
    {
        get => Native.QuerySourceInfo(_handle);
    }

    public void Dispose()
    {
        _handle.Dispose();
    }
}
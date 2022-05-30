using System.Text.Json;
using Oso.Ffi;

namespace Oso;

public class Polar : IDisposable
{
    private readonly PolarHandle _handle;
    public Host Host { get; }

    // struct polar_Polar *polar_new(void);
    public Polar()
    {
        _handle = Native.polar_new();
        Host = new Host(_handle);

        // Register global constants.
        RegisterConstant(null, "nil");
        // Register built-in classes.
        RegisterClass(typeof(bool), "Boolean");
        RegisterClass(typeof(int), "Integer");
        RegisterClass(typeof(double), "Float");
        RegisterClass(typeof(List<>), "List");
        RegisterClass(typeof(Dictionary<,>), "Dictionary");
        RegisterClass(typeof(string), "String");
    }

    // struct polar_CResult_c_void *polar_load(struct polar_Polar *polar_ptr, const char *sources);
    public void Load(List<string> sources)
    {
        string sourcesJson = JsonSerializer.Serialize(sources.Select(source => new KeyValuePair<string, string>("src", source)));
        Native.Load(_handle, sourcesJson);
    }

    public void LoadStr(string source)
    {
        Load(new Source[] { new(source) });
    }

    public void LoadFiles(IEnumerable<string> filenames)
    {
        if (!filenames.Any()) return;

        var errors = new List<OsoException>();
        var sources = new List<Source>();
        foreach (var filename in filenames)
        {
            var ext = Path.GetExtension(filename);
            if (ext == null || ext != ".polar")
            {
                errors.Add(new OsoException($"Polar file extension missing: {filename}"));
                continue;
            }

            try
            {
                string contents = File.ReadAllText(filename);
                sources.Add(new Source(contents, filename));
            }
            catch (Exception ex) when (ex is DirectoryNotFoundException || ex is FileNotFoundException)
            {
                errors.Add(new OsoException($"Polar file not found: {filename}", ex));
            }
            catch (Exception ex)
            {
                throw new OsoException($"Failed to load Polar file {filename}", ex);
            }
        }
        if (errors.Any()) throw new OsoException("Failed to load specified files", new AggregateException(errors));
        Load(sources);
    }

    public void LoadFiles(params string[] filenames)
    {
        LoadFiles((IEnumerable<string>)filenames);
    }

    private void Load(IEnumerable<Source> sources)
    {
        string sourcesJson = JsonSerializer.Serialize(sources);
        Host.RegisterMros();
        Native.Load(_handle, sourcesJson);
        // TODO: Move this to Native
        var nextQuery = Native.NextInlineQuery(_handle, Host);
        while (nextQuery != null)
        {
            if (!nextQuery.Results.Any())
            {
                string? querySource = nextQuery.Source;
                throw new OsoException($"Inline query failed: {querySource}");
            }
            nextQuery.Dispose();
            nextQuery = Native.NextInlineQuery(_handle, Host);
        }
    }

    // struct polar_CResult_c_void *polar_clear_rules(struct polar_Polar *polar_ptr);
    public void ClearRules()
    {
        Native.ClearRules(_handle);
    }

    /*
     *  struct polar_CResult_c_void *polar_register_constant(struct polar_Polar *polar_ptr,
     *                                                      const char *name,
     *                                                      const char *value);
     */
    public void RegisterConstant(object? value, string name)
    {
        Native.RegisterConstant(_handle, name, Host.SerializePolarTerm(value).ToString());
    }

    public void RegisterClass(Type t) => RegisterClass(t, t.Name);

    public void RegisterClass(Type t, string name)
    {
        Host.CacheClass(t, name);
        RegisterConstant(t, name);
    }


    /*
    struct polar_CResult_Query *polar_new_query_from_term(struct polar_Polar *polar_ptr,
                                                          const char *query_term,
                                                          uint32_t trace);
    */
    public Query NewQueryFromTerm(string queryTerm, uint trace)
    {
        return Native.NewQueryFromTerm(_handle, Host, queryTerm, trace);
    }

    /*
    struct polar_CResult_Query *polar_new_query(struct polar_Polar *polar_ptr,
                                                const char *query_str,
                                                uint32_t trace);
    */
    public Query NewQuery(string query, uint trace)
    {
        return Native.NewQuery(_handle, Host, query, trace);
    }

    public Query NewQuery(string query, bool acceptExpression, uint trace)
    {
        var host = Host.Clone();
        host.AcceptExpression = acceptExpression;
        return Native.NewQuery(_handle, host, query, trace);
    }

    public Query NewQuery(Predicate predicate, bool acceptExpression)
    {
        var host = Host.Clone();
        host.AcceptExpression = acceptExpression;
        var query = host.SerializePolarTerm(predicate).ToString();
        return Native.NewQueryFromTerm(_handle, host, query, 0);
    }

    public Query NewQuery(Predicate predicate, Dictionary<string, object> bindings, bool acceptExpression)
    {
        var host = Host.Clone();
        host.AcceptExpression = acceptExpression;
        var query = host.SerializePolarTerm(predicate).ToString();
        return Native.NewQueryFromTerm(_handle, host, query, bindings, 0);
    }

    public Query QueryRule(string rule, params object?[] args) => QueryRule(rule, null, args);
    public Query QueryRule(string rule, Dictionary<string, object>? bindings = null, params object?[] args)
    {
        var host = Host.Clone();
        string predicate = host.SerializePolarTerm(new Predicate(rule, args)).ToString();
        return (bindings == null)
            ? Native.NewQueryFromTerm(_handle, host, predicate, 0)
            : Native.NewQueryFromTerm(_handle, host, predicate, bindings, 0);
    }

    /// <summary>
    /// Query for a rule, and check if it has any results. Returns true if there are results, and false
    /// if not.
    /// </summary>
    /// 
    /// <param name="rule">Rule name, e.g. <c>f</c> for rule <c>f(x)</c>.</param>
    /// <param name="args">Variable list of rule arguments.</param>
    public bool QueryRuleOnce(string rule, params object[] args) => QueryRule(rule, new(), args).Results.Any();

    // struct polar_CResult_c_char *polar_next_polar_message(struct polar_Polar *polar_ptr);
    // TODO: Turn this into an IEnumerator?
    /*
    public IEnumerator<string> Messages
    {
        get
        {
            // Add error handling to check for error and throw PolarException
        }
    }
    */
    public string? NextMessage()
    {
        return Native.NextPolarMessage(_handle);
    }

    // uint64_t polar_get_external_id(struct polar_Polar *polar_ptr);
    public ulong ExternalId
    {
        get => Native.polar_get_external_id(_handle);
    }

    public void Dispose()
    {
        _handle.Dispose();
    }

    /*
    struct polar_CResult_c_char *polar_build_data_filter(struct polar_Polar *polar_ptr,
                                                         const char *types,
                                                         const char *results,
                                                         const char *variable,
                                                         const char *class_tag);
    */
    public string? BuildDataFilter(string types, string results, string variable, string classTag)
    {
        return Native.BuildDataFilter(_handle, types, results, variable, classTag);
    }

    /*
    struct polar_CResult_c_char *polar_build_filter_plan(struct polar_Polar *polar_ptr,
                                                         const char *types,
                                                         const char *results,
                                                         const char *variable,
                                                         const char *class_tag);
    */
    public string? BuildFilterPlan(string types, string results, string variable, string classTag)
    {
        return Native.BuildFilterPlan(_handle, types, results, variable, classTag);
    }
}
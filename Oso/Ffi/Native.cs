using System.Runtime.InteropServices;
using System.Text.Json;

namespace Oso.Ffi;
internal static class Native
{
    internal const string Polar = "polar";
    /**
    * We use the convention of zero as an error term,
    * since we also use `null_ptr()` to indicate an error.
    * So for consistency, a zero term is an error in both cases.
    */
    const int POLAR_FAILURE = 0;

    const int POLAR_SUCCESS = 1;


    [DllImport(Polar)]
    // struct polar_Polar *polar_new(void);
    internal extern static PolarHandle polar_new();

    // struct polar_CResult_c_void *polar_load(struct polar_Polar *polar_ptr, const char *sources);
    [DllImport(Polar, CharSet = CharSet.Ansi)]
    private extern static unsafe VoidResult* polar_load(PolarHandle polar_ptr, string sources);

    internal static void Load(PolarHandle polar_ptr, string sources)
    {
        unsafe
        {
            GetVoidResult(polar_load(polar_ptr, sources));
        }
    }

    // struct polar_CResult_c_void *polar_clear_rules(struct polar_Polar *polar_ptr);
    [DllImport(Polar)]
    private extern static unsafe VoidResult* polar_clear_rules(PolarHandle polar_ptr);

    internal static void ClearRules(PolarHandle polar)
    {
        unsafe
        {
            GetVoidResult(polar_clear_rules(polar));
            ProcessMessages(polar);
        }
    }


    /**
      * struct polar_CResult_c_void *polar_register_constant(struct polar_Polar *polar_ptr,
      *                                                     const char *name,
      *                                                     const char *value);
      */
    [DllImport(Polar, CharSet = CharSet.Ansi)]
    private extern static unsafe VoidResult* polar_register_constant(PolarHandle polar_ptr, string name, string value);

    internal static void RegisterConstant(PolarHandle polar, string name, string value)
    {
        unsafe
        {
            GetVoidResult(polar_register_constant(polar, name, value));
        }
    }

    // struct polar_CResult_c_void *polar_register_mro(struct polar_Polar *polar_ptr,
    //                                                 const char *name,
    //                                                 const char *mro);
    [DllImport(Polar, CharSet = CharSet.Ansi)]
    private extern static unsafe VoidResult* polar_register_mro(PolarHandle polar_ptr, string name, string mro);

    internal static void RegisterMro(PolarHandle polar, string name, string mro)
    {
        unsafe
        {
            GetVoidResult(polar_register_mro(polar, name, mro));
        }
    }


    // struct polar_Query *polar_next_inline_query(struct polar_Polar *polar_ptr, uint32_t trace);
    [DllImport(Polar)]
    private extern static QueryHandle polar_next_inline_query(PolarHandle polar_ptr, uint trace);

    internal static Query? NextInlineQuery(PolarHandle polarHandle, Host host)
    {
        var queryHandle = polar_next_inline_query(polarHandle, 0);
        // TODO: processMessages();
        return (!queryHandle.IsInvalid) ? new Query(queryHandle, host) : null;
    }

    /**
      *  struct polar_CResult_Query *polar_new_query_from_term(struct polar_Polar *polar_ptr,
      *                                                      const char *query_term,
      *                                                      uint32_t trace);
      */
    [DllImport(Polar, CharSet = CharSet.Ansi)]
    private extern static unsafe QueryResult* polar_new_query_from_term(PolarHandle polar_ptr, string query_term, uint trace);

    internal static Query NewQueryFromTerm(PolarHandle polar, Host host, string queryTerm, uint trace)
    {
        unsafe
        {
            var queryPtr = polar_new_query_from_term(polar, queryTerm, trace);
            ProcessMessages(polar);
            return GetQueryResult(queryPtr, host);
        }
    }

    internal static Query NewQueryFromTerm(PolarHandle polar, Host host, string queryTerm, Dictionary<string, object> bindings, uint trace)
    {
        Query query;
        unsafe
        {
            var queryPtr = polar_new_query_from_term(polar, queryTerm, trace);
            ProcessMessages(polar);
            query = GetQueryResult(queryPtr, host, bindings);
        }
        return query;
    }

    /**
      *  struct polar_CResult_Query *polar_new_query(struct polar_Polar *polar_ptr,
      *                                              const char *query_str,
      *                                              uint32_t trace);
      */
    [DllImport(Polar, CharSet = CharSet.Ansi)]
    private extern static unsafe QueryResult* polar_new_query(PolarHandle polar_ptr, string query_str, uint trace);

    internal static Query NewQuery(PolarHandle polar, Host host, string queryString, uint trace)
    {
        unsafe
        {
            var queryPtr = polar_new_query(polar, queryString, trace);
            ProcessMessages(polar);
            return GetQueryResult(queryPtr, host);
        }
    }

    private unsafe static void ProcessMessages(PolarHandle handle)
    {
        var message = GetStringResultOrDefault(polar_next_polar_message(handle));
        while (message != null)
        {
            ProcessMessage(message);
            message = GetStringResultOrDefault(polar_next_polar_message(handle));
        }
    }

    private unsafe static void ProcessMessage(string messageStr)
    {
        try
        {
            var message = JsonDocument.Parse(messageStr).RootElement;
            string kind = message.GetProperty("kind").GetString();
            var msg = message.GetProperty("msg").GetString();
            if ("Print".Equals(kind))
            {
                // TODO: Make this into a text stream or something.
                Console.WriteLine(msg);
            }
            else if ("Warning".Equals(kind))
            {
                Console.WriteLine("[warning] {0}", msg);
            }
        }
        catch (JsonException)
        {
            throw new OsoException(string.Format("Invalid JSON Message: {0}", messageStr));
        }
    }


    // struct polar_CResult_c_char *polar_next_polar_message(struct polar_Polar *polar_ptr);
    [DllImport(Polar)]
    private extern static unsafe StringResult* polar_next_polar_message(PolarHandle polar_ptr);

    internal static string NextPolarMessage(PolarHandle polar)
    {
        unsafe
        {
            return GetStringResult(polar_next_polar_message(polar));
        }
    }

    // struct polar_CResult_c_char *polar_next_query_event(struct polar_Query *query_ptr);
    [DllImport(Polar)]
    private extern static unsafe StringResult* polar_next_query_event(QueryHandle query_ptr);

    internal static string NextQueryEvent(QueryHandle query)
    {
        unsafe
        {
            return GetStringResult(polar_next_query_event(query));
        }
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
    [DllImport(Polar, CharSet = CharSet.Ansi)]
    internal extern static unsafe VoidResult* polar_debug_command(QueryHandle query_ptr, string value);

    internal static void DebugCommand(QueryHandle query, string value)
    {
        unsafe
        {
            GetVoidResult(polar_debug_command(query, value));
        }
    }

    /**
      *  struct polar_CResult_c_void *polar_call_result(struct polar_Query *query_ptr,
      *                                              uint64_t call_id,
      *                                              const char *term);
      */
    [DllImport(Polar, CharSet = CharSet.Ansi)]
    private extern static unsafe VoidResult* polar_call_result(QueryHandle query_ptr, ulong call_id, string term);

    internal static void CallResult(QueryHandle query, ulong callId, string term)
    {
        unsafe
        {
            GetVoidResult(polar_call_result(query, callId, term));
        }
    }


    /**
      *  struct polar_CResult_c_void *polar_question_result(struct polar_Query *query_ptr,
      *                                                  uint64_t call_id,
      *                                                  int32_t result);
      */
    [DllImport(Polar)]
    private extern static unsafe VoidResult* polar_question_result(QueryHandle query_ptr, ulong call_id, int result);

    internal static void QuestionResult(QueryHandle query, ulong callId, int result)
    {
        unsafe
        {
            GetVoidResult(polar_question_result(query, callId, result));
        }
    }

    // struct polar_CResult_c_void *polar_application_error(struct polar_Query *query_ptr, char *message);
    [DllImport(Polar, CharSet = CharSet.Ansi)]
    private extern static unsafe VoidResult* polar_application_error(QueryHandle query_ptr, string message);

    internal static void ReturnApplicationError(QueryHandle query, string message)
    {
        unsafe
        {
            GetVoidResult(polar_application_error(query, message));
        }
    }

    // struct polar_CResult_c_char *polar_next_query_message(struct polar_Query *query_ptr);
    [DllImport(Polar)]
    private extern static unsafe StringResult* polar_next_query_message(QueryHandle query_ptr);

    internal static string NextQueryMessage(QueryHandle query)
    {
        unsafe
        {
            return GetStringResult(polar_next_query_message(query));
        }
    }

    // struct polar_CResult_c_char *polar_query_source_info(struct polar_Query *query_ptr);
    [DllImport(Polar)]
    private extern static unsafe StringResult* polar_query_source_info(QueryHandle query_ptr);

    internal static string QuerySourceInfo(QueryHandle query)
    {
        unsafe
        {
            return GetStringResult(polar_query_source_info(query));
        }
    }

    /**
      *  struct polar_CResult_c_void *polar_bind(struct polar_Query *query_ptr,
      *                                          const char *name,
      *                                          const char *value);
      */
    [DllImport(Polar, CharSet = CharSet.Ansi)]
    private extern static unsafe VoidResult* polar_bind(QueryHandle query_ptr, string name, string value);

    internal static void QueryBind(QueryHandle query, string name, string value)
    {
        unsafe
        {
            GetVoidResult(polar_bind(query, name, value));
        }
    }

    // uint64_t polar_get_external_id(struct polar_Polar *polar_ptr);
    [DllImport(Polar)]
    internal extern static ulong polar_get_external_id(PolarHandle polar_ptr);


    /**
    * Required to free strings properly
    */
    // int32_t string_free(char *s);
    [DllImport(Polar, CharSet = CharSet.Ansi)]
    internal extern static int string_free(IntPtr s);

    /**
    * Recovers the original boxed version of `polar` so that
    * it can be properly freed
    */
    // int32_t polar_free(struct polar_Polar *polar);
    [DllImport(Polar)]
    internal extern static int polar_free(IntPtr polar);

    /**
    * Recovers the original boxed version of `query` so that
    * it can be properly freed
    */
    // int32_t query_free(struct polar_Query *query);
    [DllImport(Polar)]
    internal extern static int query_free(IntPtr query);

    /**
    * Recovers the original boxed version of `result` so that
    * it can be properly freed
    */
    // int32_t result_free(struct polar_CResult_c_void *result);
    [DllImport(Polar)]
    internal extern static unsafe int result_free(VoidResult* result);

    [DllImport(Polar)]
    internal extern static unsafe int result_free(QueryResult* result);

    [DllImport(Polar)]
    internal extern static unsafe int result_free(StringResult* result);

    /**
      *  struct polar_CResult_c_char *polar_build_data_filter(struct polar_Polar *polar_ptr,
      *                                                      const char *types,
      *                                                      const char *results,
      *                                                      const char *variable,
      *                                                      const char *class_tag);
      */
    [DllImport(Polar, CharSet = CharSet.Ansi)]
    private extern static unsafe StringResult* polar_build_data_filter(PolarHandle polar_ptr, string types, string results, string variable, string class_tag);

    internal static string BuildDataFilter(PolarHandle polar, string types, string results, string variable, string classTag)
    {
        unsafe
        {
            return GetStringResult(polar_build_data_filter(polar, types, results, variable, classTag));
        }
    }

    /**
      *  struct polar_CResult_c_char *polar_build_filter_plan(struct polar_Polar *polar_ptr,
      *                                                      const char *types,
      *                                                      const char *results,
      *                                                      const char *variable,
      *                                                      const char *class_tag);
      */

    [DllImport(Polar, CharSet = CharSet.Ansi)]
    private extern static unsafe StringResult* polar_build_filter_plan(PolarHandle polar_ptr, string types, string results, string variable, string class_tag);

    internal static string BuildFilterPlan(PolarHandle polar, string types, string results, string variable, string classTag)
    {
        unsafe
        {
            return GetStringResult(polar_build_filter_plan(polar, types, results, variable, classTag));
        }
    }

    private static unsafe void GetVoidResult(VoidResult* ptr)
    {
        unsafe
        {
            try
            {
                if (ptr->error == IntPtr.Zero)
                {
                    return;
                }
                else
                {
                    string error = Marshal.PtrToStringAnsi(ptr->error)!;
                    _ = string_free(ptr->error);
                    throw OsoException.ParseError(error);
                }
            }
            finally
            {
                _ = result_free(ptr);
            }
        }

    }

    private static unsafe Query GetQueryResult(QueryResult* ptr, Host host, Dictionary<string, object>? bindings = null)
    {
        unsafe
        {
            try
            {
                if (ptr->error == IntPtr.Zero)
                {
                    var queryHandle = new QueryHandle(ptr->result);

                    if (bindings != null)
                    {
                        foreach (var (k, v) in bindings)
                        {
                            QueryBind(queryHandle, k, host.SerializePolarTerm(v).ToString());
                        }
                    }
                    // TODO: when is the query string freed?
                    return new Query(queryHandle, host);
                }
                else
                {
                    if (ptr->result != IntPtr.Zero)
                    {
                        var r = Marshal.PtrToStringAnsi(ptr->result);
                        var e = Marshal.PtrToStringAnsi(ptr->error);
                        throw new OsoException($"Internal error: both result and error pointers are non-null: Result: {r}. Error: {e}");
                    }

                    string error = Marshal.PtrToStringAnsi(ptr->error)!;
                    _ = string_free(ptr->error);
                    throw OsoException.ParseError(error);
                }
            }
            finally
            {
                _ = result_free(ptr);
            }
        }

    }
    private static unsafe string GetStringResult(StringResult* ptr)
    {
        unsafe
        {
            try
            {
                if (ptr->error == IntPtr.Zero)
                {
                    // TODO: Is this efficient? This is often returning UTF-8 JSON, so we're
                    // copying and encoding to UTF-16, then re-encoding backk to UTF-8 to parse the JSON.
                    // Maybe this should be kept as a byte[] instead.
                    try
                    {
                        return Marshal.PtrToStringAnsi(ptr->result) ?? throw new OsoException("Received null string from native API.");
                    }
                    finally
                    {
                        _ = string_free(ptr->result);
                    }
                }
                else
                {
                    if (ptr->result != IntPtr.Zero)
                    {
                        var r = Marshal.PtrToStringAnsi(ptr->result);
                        var e = Marshal.PtrToStringAnsi(ptr->error);
                        throw new OsoException($"Internal error: both result and error pointers are non-null: Result: {r}. Error: {e}");
                    }

                    string error = Marshal.PtrToStringAnsi(ptr->error)!;
                    _ = string_free(ptr->error);
                    throw OsoException.ParseError(error);
                }
            }
            finally
            {
                _ = result_free(ptr);
            }
        }
    }

    private static unsafe string? GetStringResultOrDefault(StringResult* ptr)
    {
        unsafe
        {
            try
            {
                if (ptr->error == IntPtr.Zero)
                {
                    // TODO: Is this efficient? This is often returning UTF-8 JSON, so we're
                    // copying and encoding to UTF-16, then re-encoding backk to UTF-8 to parse the JSON.
                    // Maybe this should be kept as a byte[] instead.
                    try
                    {
                        return Marshal.PtrToStringAnsi(ptr->result);
                    }
                    finally
                    {
                        _ = string_free(ptr->result);
                    }
                }
                else
                {
                    if (ptr->result != IntPtr.Zero)
                    {
                        var r = Marshal.PtrToStringAnsi(ptr->result);
                        var e = Marshal.PtrToStringAnsi(ptr->error);
                        throw new OsoException($"Internal error: both result and error pointers are non-null: Result: {r}. Error: {e}");
                    }

                    string error = Marshal.PtrToStringAnsi(ptr->error)!;
                    _ = string_free(ptr->error);
                    throw OsoException.ParseError(error);
                }
            }
            finally
            {
                _ = result_free(ptr);
            }
        }
    }
}
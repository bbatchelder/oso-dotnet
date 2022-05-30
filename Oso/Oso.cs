namespace Oso;

public class Oso : Polar
{
    /// <summary> Used to differentiate between a <see cref="NotFoundException" /> and a
    /// <see cref="ForbiddenException" /> on authorization failures.
    /// </summary>
    public object ReadAction { get; set; } = "read";

    public Oso() : base() { }

    public bool IsAllowed(object actor, object action, object resource) => QueryRuleOnce("allow", actor, action, resource);

    /// <summary>
    /// Return the allowed actions for the given actor and resource, if any.
    /// </summary>
    /// 
    /// <code>
    /// Oso oso = new Oso();
    /// o.loadStr("allow(\"guest\", \"get\", \"widget\");");
    /// HashSet actions = o.getAllowedActions("guest", "widget");
    /// assert actions.contains("get");
    /// </code>
    /// 
    /// <param name="actor">The actor performing the request</param>
    /// <param name="resource">The resource being accessed</param>
    /// <returns cref="HashSet&lt;object&gt;" />
    /// <throws cref="OsoException" />
    public HashSet<object> GetAllowedActions(object actor, object resource)
    {
        return AuthorizedActions(actor, resource, false);
    }

    /// <summary>
    /// Determine the actions <paramref name="actor" /> is allowed to take on <paramref name="resource" />.
    /// 
    /// Collects all actions allowed by allow rules in the Polar policy for the given combination of
    /// actor and resource.
    /// </summary>
    /// 
    /// <param name="actor">The actor for whom to collect allowed actions</param>
    /// <param name="resource">The resource being accessed</param>
    /// <param name="allowWildcard">
    ///   Flag to determine behavior if the policy includes a wildcard action.
    ///   E.g., given a rule allowing any action:
    ///   <code>
    ///     allow(_actor, _action, _resource)
    ///   </code>
    ///   If <c>true</c>, the method will return <c>["*"]</c>.
    ///   if <c>false</c>, the method will raise an exception.
    /// </param>
    /// <returns> A list of the unique allowed actions.</returns>
    /// <throws cref="OsoException" />
    public HashSet<object> AuthorizedActions(object actor, object resource, bool allowWildcard = false)
    {
        return QueryRule("allow", actor, new Variable("action"), resource).Results
            .Select(action =>
            {
                if (action["action"] is not Variable) return action["action"];
                return allowWildcard
                    ? "*"
                    : throw new OsoException(Exceptions.GetExceptionMessage("UnconstrainedAction"));
            }).ToHashSet();
    }

    /// <summary>
    /// Ensure that <paramref name="actor" /> is allowed to perform <paramref name="action" /> on <paramref name="resource" />.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>
    /// If the action is permitted with an <c>allow</c> rule in the policy, then this method returns
    /// without error. If the action is not permitted by the policy, this method will raise an error.
    /// </para>
    /// 
    /// <para>
    /// The error raised by this method depends on whether the actor can perform the <c>"read"</c> action
    /// on the resource. If they cannot read the resource, then a <see cref="NotFoundException" /> is raised.
    /// Otherwise, a <see cref="ForbiddenException" /> is raised.
    /// </para>
    /// </remarks>
    /// 
    /// <param name="actor">The actor performing the request.</param>
    /// <param name="action">The action the actor is attempting to perform.</param>
    /// <param name="resource">The resource being accessed.</param>
    /// <param name="checkRead">
    ///     If set to <c>false</c>, a <see cref="ForbiddenException" /> is always thrown on authorization
    ///     failures, regardless of whether the actor can read the resource. Default is <c>true</c>.</param>
    /// <throws name="OsoException" />
    public void Authorize(object actor, object action, object resource, bool checkRead = true)
    {
        bool authorized = QueryRuleOnce("allow", actor, action, resource);
        if (authorized)
        {
            return;
        }
        // Authorization failure. Determine whether to throw a NotFoundException or
        // a ForbiddenException.
        if (checkRead)
        {
            if (action == ReadAction || !QueryRuleOnce("allow", actor, ReadAction, resource))
            {
                throw new NotFoundException();
            }
        }
        throw new ForbiddenException();
    }

    /// <summary>
    /// Ensure that <paramref name="actor" /> is allowed to send <paramref name="request" /> to the server.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>
    /// Checks the <c>allow_request</c> rule of a policy.
    /// </para>
    /// 
    /// <para>
    /// If the request is permitted with an <c>allow_request</c> rule in the policy, then this method
    /// returns nothing. Otherwise, this method raises a <see cref="ForbiddenException" />.
    /// </para>
    /// </remarks>
    /// 
    /// <param name="actor">The actor performing the request.</param>
    /// <param name="request">An object representing the request that was sent by the actor.</param>
    /// <throws cref="OsoException" />
    public void AuthorizeRequest(object actor, object request)
    {
        bool authorized = QueryRuleOnce("allow_request", actor, request);
        if (!authorized) throw new ForbiddenException();
    }

    /// <summary>
    /// Ensure that <paramref name="actor" /> is allowed to perform <paramref name="action" /> on a given <paramref name="resource" />'s <paramref name="field" />.
    /// </summary>
    /// 
    /// <remarks>
    /// If the action is permitted by an <c>allow_field</c> rule in the policy, then this method returns
    /// nothing. If the action is not permitted by the policy, this method will raise a
    /// <see cref="ForbiddenException" />.
    /// </remarks>
    /// 
    /// <param name="actor">The actor performing the request.</param>
    /// <param name="action">The action the actor is attempting to perform on the field.</param>
    /// <param name="resource">The resource being accessed.</param>
    /// <param name="field">The name of the field being accessed.</param>
    /// <throws cref="OsoException" />
    public void AuthorizeField(object actor, object action, object resource, object field)
    {
        bool authorized = QueryRuleOnce("allow_field", actor, action, resource, field);
        if (!authorized) throw new ForbiddenException();
    }

    /// <summary>
    /// Determine the fields of <paramref name="resource" /> on which <paramref name="actor" /> is allowed to perform <paramref name="action" />.
    /// </summary>
    /// 
    /// <remarks>
    /// Uses <c>allow_field</c> rules in the policy to find all allowed fields.
    /// </remarks>
    /// 
    /// <param name="actor">The actor for whom to collect allowed fields.</param>
    /// <param name="action">The action being taken on the field.</param>
    /// <param name="resource">The resource being accessed.</param>
    /// <param name="allowWildcard">
    /// Flag to determine behavior if the policy includes a wildcard field.
    /// <para>
    /// <example>
    ///     E.g., given a rule allowing any field:
    ///     <code>
    ///     allow_field(_actor, _action, _resource, _field)
    ///     </code>
    ///     If <c>true</c>, the method will return <c>["*"]</c>, if <c>false</c>, the method will raise an exception.
    /// </example>
    /// </para>
    /// </param>
    /// <returns> A set of the unique allowed fields.</returns>
    /// <throws cref="OsoException" />
    public HashSet<object> AuthorizedFields(object actor, object action, object resource, bool allowWildcard = false)
    {
        return QueryRule("allow_field", actor, action, resource, new Variable("field"))
            .Results.Select(
                field =>
                {
                    if (field["field"] is not Variable) return field["field"];
                    else
                    {
                        return (allowWildcard)
                            ? "*"
                            : throw new OsoException(Exceptions.GetExceptionMessage("UnconstrainedAction"));
                    }
                }).ToHashSet();
    }
}
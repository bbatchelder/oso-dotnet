namespace Oso;

internal class TypeConstraint : Expression
{
    public TypeConstraint(object left, string typeName) :
        base(Operator.And, new List<object> { new Expression(Operator.Isa, new List<object> { left, new Pattern(typeName, new Dictionary<string, object>()) }) })
    {

    }
}
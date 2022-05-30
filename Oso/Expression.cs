namespace Oso;

internal class Expression
{
    public Operator Operator { get; set; }
    public List<object> Args { get; set; }

    public Expression(Operator op, List<object> args)
    {
        Operator = op;
        Args = args;
    }

    public override bool Equals(object? o)
    {
        if (this == o) return true;
        return (o is Expression e)
            ? Operator == e.Operator && Args.SequenceEqual(e.Args)
            : false;
    }

    public override int GetHashCode()
    {
        // TODO:
        throw new NotImplementedException();
    }
}
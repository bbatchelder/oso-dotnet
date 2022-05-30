namespace Oso;

internal record class Variable(string Name)
{
    public override string ToString() => Name;
}

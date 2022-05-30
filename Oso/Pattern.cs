namespace Oso;

public class Pattern
{
    internal string Tag { get; set; }
    internal Dictionary<string, object> Fields { get; set; }

    public Pattern(string tag, Dictionary<string, object> fields)
    {
        Tag = tag;
        Fields = fields;
    }

    public override bool Equals(object? o)
    {
        if (this == o) return true;
        return (o is Pattern p)
            ? Tag == p.Tag && Fields.SequenceEqual(p.Fields)
            : false;
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}
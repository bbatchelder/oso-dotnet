namespace Oso.DataFiltering;

public class Filter
{
    public Dictionary<string,Type> Types { get; set; }
    public Filter(string model, object relations, object conditions, Dictionary<string,Type> types)
    {
        this.Types = types;
    }

    public static void Parse(Polar polar, object blob)
    {
        var types = polar.Host._classes;
    }
}
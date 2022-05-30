namespace Oso.DataFiltering;
public interface IDataFilterAdapter
{
    void BuildQuery(object Filter);
    void ExecuteQuery(object query);
    Dictionary<string,object> SerializeType(Type type);
}

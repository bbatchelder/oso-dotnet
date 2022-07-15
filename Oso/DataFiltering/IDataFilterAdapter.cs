namespace Oso.DataFiltering;
public interface IDataFilterAdapter
{
    IQueryable BuildQuery(string plan);
    IQueryable ExecuteQuery(IQueryable query);
    Dictionary<string,object> SerializeType(Type type);
}

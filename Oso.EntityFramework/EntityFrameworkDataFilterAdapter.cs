using System.Collections;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Oso.DataFiltering.EntityFramework;

public class EntityFrameworkDataFilterAdapter<T> : IDataFilterAdapter where T : DbContext
{
    T _dbContext;
    public EntityFrameworkDataFilterAdapter(T dbContext)
    {
        _dbContext = dbContext;
    }
    public IQueryable BuildQuery(string plan)
    {
        Type dbContextType = _dbContext.GetType();

        foreach(var propInfo in dbContextType.GetProperties())
        {
            string blah = propInfo.Name;
        }

        return null;
    }

    public IQueryable ExecuteQuery(IQueryable query)
    {
        return query;
    }

    public Dictionary<string,object> SerializeType(Type type)
    {
        var rv = new Dictionary<string,object>();

        if(type == null || type.FullName == null)
            return rv;

        var entityType = _dbContext.Model.FindEntityType(type.FullName!);

        if(entityType == null)
            return rv;

        var properties = entityType.GetProperties();

        foreach(var property in properties)
        {
            string propertyName = property.Name;
            var payload = new { Base = new { class_tag = type.Name } };
            rv.Add(propertyName, payload);
        }

        var navProperties = entityType.GetNavigations();

        foreach(var navProperty in navProperties)
        {
            string cardinality = "one";

            if(typeof(IEnumerable).IsAssignableFrom(navProperty.ClrType))
                cardinality = "many";
                
            string otherType = string.Empty;
            string otherPropertyName = string.Empty;
            string myPropertyName = string.Empty;

            bool isDependentToPrincipal = navProperty.IsDependentToPrincipal();

            if(isDependentToPrincipal)
            {
                otherType = navProperty.TargetEntityType.Name;
                otherPropertyName = navProperty.ForeignKey.PrincipalKey.Properties[0].Name;
                myPropertyName = navProperty.ForeignKey.Properties[0].Name;
            }
            else
            {
                otherType = navProperty.TargetEntityType.Name;
                otherPropertyName = navProperty.ForeignKey.Properties[0].Name;
                myPropertyName = navProperty.ForeignKey.PrincipalKey.Properties[0].Name;
            }

            var payload = new { Relation = new { 
                    kind = cardinality,
                    other_class_tag = otherType, //maybe not pass in?  See python and ruby implementations
                    other_type = otherType,
                    other_field = otherPropertyName,
                    my_field = myPropertyName
                }
            };

            if(!rv.ContainsValue(payload))
                rv.Add(navProperty.Name, payload);
        }

        return rv;
    }
}

//For future reference on building linq queries at runtime
// private Expression<Func<Goods, bool>> LambdaConstructor (string propertyName, string inputText, Condition condition)
//     {

//             var item = Expression.Parameter(typeof(Goods), "item");
//             var prop = Expression.Property(item, propertyName);
//             var propertyInfo = typeof(Goods).GetProperty(propertyName);
//             var value = Expression.Constant(Convert.ChangeType(inputText, propertyInfo.PropertyType));
//             BinaryExpression equal;
//             switch (condition)
//             {
//                 case Condition.eq:
//                     equal = Expression.Equal(prop, value);
//                     break;
//                 case Condition.gt:
//                     equal = Expression.GreaterThan(prop, value);
//                     break;
//                 case Condition.gte:
//                     equal = Expression.GreaterThanOrEqual(prop, value);
//                     break;
//                 case Condition.lt:
//                     equal = Expression.LessThan(prop, value);
//                     break;
//                 case Condition.lte:
//                     equal = Expression.LessThanOrEqual(prop, value);
//                     break;
//                 default:
//                     equal = Expression.Equal(prop, value);
//                     break;
//             }
//             var lambda = Expression.Lambda<Func<Goods, bool>>(equal, item);
//             return lambda;
//         }
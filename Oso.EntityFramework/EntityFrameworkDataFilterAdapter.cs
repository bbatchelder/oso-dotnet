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
    public void BuildQuery(object Filter)
    {
        throw new NotImplementedException();
    }

    public void ExecuteQuery(object query)
    {
        throw new NotImplementedException();
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

            if(property.IsForeignKey())
            {
                //TODO: Handle multiple foreign keys
                var foreignKey = property.GetContainingForeignKeys().First();

                var payload = new { Relation = new { 
                    kind = "one",
                    other_class_tag = foreignKey.DependentToPrincipal?.Name,
                    my_field = property.Name,
                    other_field = foreignKey.PrincipalKey.Properties[0].Name } };

                rv.Add(propertyName, payload);
            }
            else
            {
                // var payload = new JObject(new JProperty("Base",
                //                     new JObject(new JProperty("class_tag", ""))));

                var payload = new { Base = new { class_tag = type.Name } };

                rv.Add(propertyName, payload);
            }
        }

        var navProperties = entityType.GetNavigations();

        foreach(var navProperty in navProperties)
        {
            var foreignKey = navProperty.ForeignKey;
            string otherClassName;

            if(foreignKey.PrincipalEntityType.ClrType == entityType.ClrType)
            {
                otherClassName = foreignKey.DependentToPrincipal?.DeclaringEntityType.ClrType.Name ?? "";
            }
            else
            {
                otherClassName = foreignKey.DependentToPrincipal?.Name ?? "";
            }
            
            string cardinality = "one";

            if(typeof(IEnumerable).IsAssignableFrom(navProperty.ClrType))
                cardinality = "many";

            var payload = new { Relation = new { 
                    kind = cardinality,
                    other_class_tag = otherClassName,
                    my_field = navProperty.Name,
                    other_field = foreignKey.PrincipalKey.Properties[0].Name } };

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
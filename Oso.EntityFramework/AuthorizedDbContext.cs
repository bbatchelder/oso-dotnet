using System.Collections;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Oso.DataFiltering.EntityFramework;

public class AuthorizedDbContext : DbContext
{
    protected Oso osoContext;

    public AuthorizedDbContext(DbContextOptions options, Oso oso) : base(options)
    {
        this.osoContext = oso;
    }

    public IQueryable<TEntity> AuthorizedQuery<TEntity>(object actor, string action)
    {
        string resource = typeof(TEntity).Name;
        return osoContext.AuthorizedQuery(actor, action, resource) as IQueryable<TEntity>;
    }
}
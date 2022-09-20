using System.Collections;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Oso.DataFiltering.EntityFramework;

public class AuthorizedDbContext : DbContext
{
    public Oso OsoContext { get; private set; }

    public AuthorizedDbContext(DbContextOptions options) : base(options)
    {
    }

    public void SetOsoContext(Oso osoContext)
    {
        this.OsoContext = osoContext;
        this.OsoContext.SetDataFilteringAdapter(new EntityFrameworkDataFilterAdapter(this));
    }

    public IQueryable<TEntity> AuthorizedQuery<TEntity>(object actor, string action)
    {
        string resource = typeof(TEntity).Name;
        return OsoContext.AuthorizedQuery(actor, action, resource) as IQueryable<TEntity>;
    }
}
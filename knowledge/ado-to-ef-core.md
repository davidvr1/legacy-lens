# ADO Recordset to EF Core

Tags: ADODB.Recordset, Recordset, rs.GetRows, Connection.Execute, Server.CreateObject, DbContext, Entity Framework, EF Core, LINQ, AsNoTracking, ToListAsync

## Legacy pattern

Classic ASP hand-writes the SQL and copies recordset fields into HTML. Each field is read by name while the page walks the cursor.

```asp
<%
Set rs = conn.Execute("SELECT o.Id, o.Total FROM Orders o WHERE o.CustomerId = " & customerId)
Do While Not rs.EOF
  Response.Write "<tr><td>" & rs("Id") & "</td><td>" & rs("Total") & "</td></tr>"
  rs.MoveNext
Loop
%>
```

## ASP.NET Core equivalent

An EF Core `DbContext` replaces the connection and the SQL text. LINQ builds the query, and a projection returns only the columns the page needs.

```csharp
public sealed class ShopDbContext(DbContextOptions<ShopDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
}

var orders = await db.Orders
    .AsNoTracking()
    .Where(o => o.CustomerId == customerId)
    .Select(o => new OrderSummary(o.Id, o.Total, o.Customer.Name))
    .ToListAsync(cancellationToken);
```

## Notes

- Use `AsNoTracking()` for read-only pages. Change tracking costs memory and is not needed to render HTML.
- Project into a record with `Select` so only the needed columns are loaded, not whole entities.
- `AddDbContext` registers the context as scoped by default. Never share one context across requests or make it a singleton.
- `UseSqlServer` needs the Microsoft.EntityFrameworkCore.SqlServer package. Add it only when the stage needs it. For tests, prefer SQLite in-memory over the EF InMemory provider, which does not enforce relational rules.

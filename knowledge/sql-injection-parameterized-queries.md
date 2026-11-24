# SQL Injection and Parameterized Queries

Tags: Request.QueryString, Request.Form, Request("id"), Request.Params, string concatenation, SQL concatenation, conn.Execute, SqlCommand, SqlParameter, DbDataSource, DbParameter, parameterized query, SQL injection

## Legacy pattern

Classic ASP builds SQL by concatenating request values, so a crafted value changes the query. Input such as `1 OR 1=1` returns every row.

```asp
<%
id = Request.QueryString("id")
sql = "SELECT Id, Name, Price FROM Products WHERE Id = " & id
Set rs = conn.Execute(sql)
%>
```

## ASP.NET Core equivalent

Send the value as a typed parameter, never as SQL text. The database receives `@Id` as a parameter, so it never parses the input as SQL. Declare the row record in the same file.

```csharp
using System.Data.Common;

public sealed record ProductRow(int Id, string Name, decimal Price);

public sealed class ProductQueries(DbDataSource dataSource)
{
    public async Task<ProductRow?> GetProductAsync(int id, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT Id, Name, Price FROM Products WHERE Id = @Id");
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@Id";
        parameter.Value = id;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProductRow(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2))
            : null;
    }
}
```

## Notes

- Parse and validate input at the edge (`int.TryParse`, allow-listed values) before it reaches data code. A parameter cannot replace a table or column name.
- Table names, column names and `ORDER BY` direction cannot be parameters. Map them through a fixed allow-list (a `switch` expression) instead.
- With ADO.NET, add one parameter per value with `command.CreateParameter()`. With EF Core, use LINQ or `FromSqlInterpolated`, not `FromSqlRaw` with concatenated strings.
- Add a test that passes `1 OR 1=1` and expects a not-found or validation result, not all rows. Run the database user with the fewest rights it needs.

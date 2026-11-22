# SQL Injection and Parameterized Queries

Tags: Request.QueryString, Request.Form, Request("id"), Request.Params, string concatenation, SQL concatenation, conn.Execute, SqlCommand, SqlParameter, Dapper, parameterized query, SQL injection

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

Send the value as a typed parameter, never as SQL text. Dapper sends `@Id` as a parameter, so the database never parses the input as SQL.

```csharp
public async Task<ProductRow?> GetProductAsync(int id, CancellationToken cancellationToken)
{
    await using var connection = new SqlConnection(connectionString);
    return await connection.QueryFirstOrDefaultAsync<ProductRow>(
        new CommandDefinition(
            "SELECT Id, Name, Price FROM Products WHERE Id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken));
}
```

## Notes

- Parse and validate input at the edge (`int.TryParse`, allow-listed values) before it reaches data code. A parameter cannot replace a table or column name.
- Table names, column names and `ORDER BY` direction cannot be parameters. Map them through a fixed allow-list (a `switch` expression) instead.
- With EF Core, use LINQ or `FromSqlInterpolated`, not `FromSqlRaw` with concatenated strings. With ADO.NET, use `SqlParameter` objects.
- Add a test that passes `1 OR 1=1` and expects a not-found or validation result, not all rows. Run the database user with the fewest rights it needs.

# ADO Recordset to Dapper

Tags: ADODB.Connection, ADODB.Recordset, Recordset, rs.Open, rs.EOF, rs.MoveNext, rs.Close, Server.CreateObject, Connection.Execute, Dapper, QueryAsync, SqlConnection

## Legacy pattern

Classic ASP opens an ADODB connection, runs a query into a recordset, then walks it row by row until `rs.EOF`, calling `rs.MoveNext` at the end of each loop.

```asp
<%
Set conn = Server.CreateObject("ADODB.Connection")
conn.Open "Provider=SQLOLEDB;Data Source=DBSRV01;Initial Catalog=Shop;Integrated Security=SSPI;"
Set rs = Server.CreateObject("ADODB.Recordset")
rs.Open "SELECT Id, Name FROM Users WHERE Active = 1", conn
Do While Not rs.EOF
  Response.Write rs("Name") & "<br>"
  rs.MoveNext
Loop
rs.Close
conn.Close
%>
```

## ASP.NET Core equivalent

Dapper maps each row to a record, so there is no cursor to move. Open the connection with `await using`, pass parameters as an anonymous object, and return a typed list.

```csharp
public sealed record UserRow(int Id, string Name);

public sealed class UserQueries(string connectionString)
{
    public async Task<IReadOnlyList<UserRow>> GetActiveUsersAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        var rows = await connection.QueryAsync<UserRow>(
            new CommandDefinition(
                "SELECT Id, Name FROM Users WHERE Active = @Active",
                new { Active = true },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }
}
```

## Notes

- Use `SqlConnection` from Microsoft.Data.SqlClient and let ADO.NET pooling reuse physical connections. Do not keep a connection open in a singleton.
- Dapper maps columns to record constructor parameters by name, so alias columns in SQL when the names differ.
- Always pass values as parameters (`@Active`). String concatenation reopens SQL injection; see the SQL injection note.
- Test the query class behind an interface in unit tests. Run SQL checks against a disposable test database, never production.

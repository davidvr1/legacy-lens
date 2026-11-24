# ADO Recordset to Typed Rows

Tags: ADODB.Connection, ADODB.Recordset, Recordset, rs.Open, rs.EOF, rs.MoveNext, rs.Close, Server.CreateObject, Connection.Execute, SqlConnection, DbDataSource, ExecuteReaderAsync

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

Read the rows into a list of records, so no cursor leaves the data code. Create the command from an injected `DbDataSource`, pass values as parameters, and return a typed list. Declare the row record in the same file.

```csharp
using System.Data.Common;

public sealed record UserRow(int Id, string Name);

public sealed class UserQueries(DbDataSource dataSource)
{
    public async Task<IReadOnlyList<UserRow>> GetActiveUsersAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT Id, Name FROM Users WHERE Active = @Active");
        var active = command.CreateParameter();
        active.ParameterName = "@Active";
        active.Value = true;
        command.Parameters.Add(active);

        var rows = new List<UserRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new UserRow(reader.GetInt32(0), reader.GetString(1)));
        }

        return rows;
    }
}
```

## Notes

- Inject `System.Data.Common.DbDataSource`, registered once at startup for the database provider. It pools connections; do not keep a connection open in a singleton.
- Read columns by position in the order of the `SELECT` list, and alias columns in SQL when the names differ from the record.
- Always pass values as parameters (`@Active`). String concatenation reopens SQL injection; see the SQL injection note.
- Test the query class behind an interface in unit tests. Run SQL checks against a disposable test database, never production.

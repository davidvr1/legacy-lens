# Error Handling

Tags: On Error Resume Next, On Error GoTo 0, Err.Number, Err.Description, Err.Clear, Err.Raise, try/catch, UseExceptionHandler, ILogger, LogError, SqlException

## Legacy pattern

Classic ASP has no structured exceptions. `On Error Resume Next` lets the script continue after a failure, so the page checks `Err.Number` after each risky call and often prints `Err.Description` to the browser.

```asp
<%
On Error Resume Next
Set rs = conn.Execute("SELECT Id FROM Users")
If Err.Number <> 0 Then
  Response.Write "Database error: " & Err.Description
  Err.Clear
End If
On Error GoTo 0
%>
```

## ASP.NET Core equivalent

Use `try`/`catch` only where the code can recover, and log with `ILogger`. Let other exceptions reach `UseExceptionHandler`, which shows a generic error page.

```csharp
public async Task<IReadOnlyList<UserRow>> LoadUsersAsync(CancellationToken cancellationToken)
{
    try
    {
        return await _queries.GetActiveUsersAsync(cancellationToken);
    }
    catch (SqlException ex)
    {
        _logger.LogError(ex, "Failed to load users");
        throw;
    }
}

// Program.cs
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
```

## Notes

- Never show `Err.Description` or exception messages to users. They reveal table names and server details. Log the exception and return a generic message.
- Rethrow with `throw;`, not `throw ex;`, so the original stack trace is kept.
- Do not log `OperationCanceledException` as an error. A client that disconnects is normal.
- Test that a failing query is logged and the exception still propagates, using a fake that throws and a fake logger such as `NullLogger`.

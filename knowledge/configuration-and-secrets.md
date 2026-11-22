# Configuration and Secrets

Tags: hard-coded connection string, Provider=SQLOLEDB, User ID, Password, Application("ConnStr"), Global.asa, web.config, connectionStrings, appSettings, ConfigurationManager.AppSettings, IOptions, User Secrets, environment variables, appsettings.json

## Legacy pattern

Classic ASP often hard-codes the connection string and password in the page, or stores them in `Global.asa` application variables. Anyone who can read the source or the repository gets the credentials.

```asp
<%
Const CONN_STR = "Provider=SQLOLEDB;Data Source=DBSRV01;Initial Catalog=Shop;User ID=shopuser;Password=Example!Pass1;"
Set conn = Server.CreateObject("ADODB.Connection")
conn.Open CONN_STR
%>
```

## ASP.NET Core equivalent

Keep non-secret settings in `appsettings.json` and bind them to a typed options class with `IOptions<T>`. Supply the secret from User Secrets locally or from environment variables on the server.

```json
{
  "Shop": {
    "ConnectionString": "Server=DBSRV01;Database=Shop;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

```csharp
public sealed class ShopOptions
{
    public const string SectionName = "Shop";
    public string ConnectionString { get; set; } = "";
}

// Program.cs
builder.Services.Configure<ShopOptions>(builder.Configuration.GetSection(ShopOptions.SectionName));
```

## Notes

- Never commit passwords. Store them with `dotnet user-secrets set "Shop:ConnectionString" "..."` (after `dotnet user-secrets init`) or as the environment variable `Shop__ConnectionString`. Check `.gitignore` before each commit.
- If a password was ever committed, treat it as leaked: rotate it, even after removing it from the file.
- Prefer integrated or managed identity in production so no password exists at all.
- Test with `ConfigurationBuilder().AddInMemoryCollection(...)` and bind the options, so tests never need a real secret.

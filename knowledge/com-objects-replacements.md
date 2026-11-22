# COM Objects and .NET Replacements

Tags: Server.CreateObject, Scripting.FileSystemObject, OpenTextFile, CDO.Message, MSXML2.ServerXMLHTTP, Scripting.Dictionary, CreateObject, System.IO, SmtpClient, MailKit, IHttpClientFactory

## Legacy pattern

Classic ASP reaches for COM components through `Server.CreateObject`. Each object is created per request and must be released, and the components depend on Windows installs.

```asp
<%
Set fso = Server.CreateObject("Scripting.FileSystemObject")
Set f = fso.OpenTextFile(Server.MapPath("data/log.txt"), 8, True)
f.WriteLine Now & " order saved"
f.Close

Set http = Server.CreateObject("MSXML2.ServerXMLHTTP")
http.Open "GET", "https://rates.example.com/usd", False
http.Send

Set d = Server.CreateObject("Scripting.Dictionary")
d.Add "USD", 1
%>
```

## ASP.NET Core equivalent

Each COM object has a managed replacement in the base class library or a standard NuGet client. Inject the services and pass the request's cancellation token to async calls.

```csharp
await File.AppendAllTextAsync(
    Path.Combine(_env.ContentRootPath, "data", "log.txt"),
    $"{DateTime.UtcNow:O} order saved{Environment.NewLine}",
    cancellationToken);

var client = _httpClientFactory.CreateClient("Rates");
var rate = await client.GetStringAsync("usd", cancellationToken);

var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["USD"] = 1m };
```

## Notes

- `Scripting.FileSystemObject` maps to `System.IO`. Keep data files out of `wwwroot` and wrap file access behind an interface so tests can fake it.
- `MSXML2.ServerXMLHTTP` is synchronous. `IHttpClientFactory` gives pooled handlers and async calls, and avoids socket exhaustion from creating `HttpClient` per request.
- `CDO.Message` maps to MailKit, or to `System.Net.Mail.SmtpClient`, which is legacy and discouraged for new code. Add a mail package only when a stage needs mail.
- `Scripting.Dictionary` is case-sensitive by default. Pass `StringComparer.OrdinalIgnoreCase` when the legacy code compared keys case-insensitively. Test HTTP calls with a fake `HttpMessageHandler`.

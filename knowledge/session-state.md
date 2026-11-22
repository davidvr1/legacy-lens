# Session State

Tags: Session("UserId"), Session.Contents, Session.Abandon, Session.Timeout, ASP Session, HttpContext.Session, ISession, GetString, SetString, AddSession, UseSession, TempData, ClaimsPrincipal

## Legacy pattern

Classic ASP keeps per-user values in the `Session` object, keyed by name, and reads them on later requests. The values stay in server memory for the whole visit.

```asp
<%
Session("UserId") = Request.Form("userId")
Session("CartIds") = "3,7,9"
If Session("UserId") = "" Then Response.Write "Please log in"
Session.Abandon
%>
```

## ASP.NET Core equivalent

Register a distributed cache and session in `Program.cs`, then use `HttpContext.Session` for string values. Keep the logged-in identity in the authentication cookie (claims), not in session.

```csharp
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();
app.UseSession(); // after UseRouting, before MapRazorPages or MapControllers
```

```csharp
HttpContext.Session.SetString("CartIds", "3,7,9");
var cartIds = HttpContext.Session.GetString("CartIds");
```

## Notes

- Keep session small: store IDs or short strings, not whole entities or lists. Load the rest from the database on each request.
- `AddDistributedMemoryCache` keeps sessions in one server's memory. A web farm needs a shared store such as Redis.
- Use claims for identity and `TempData` for a message that must survive one redirect.
- Test the logic through an interface over the session values, or check the full flow with `WebApplicationFactory`.

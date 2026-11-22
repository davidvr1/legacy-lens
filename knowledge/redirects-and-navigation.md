# Redirects and Navigation

Tags: Response.Redirect, Response.Location, Server.Transfer, returnUrl, Request.QueryString("returnUrl"), open redirect, RedirectToPage, Redirect, LocalRedirect, RedirectToAction, Url.IsLocalUrl

## Legacy pattern

Classic ASP sends a 302 with `Response.Redirect` so the browser makes a new request. It can also use `Server.Transfer` to run another page on the server without telling the browser. Often the target comes straight from the query string.

```asp
<%
If Session("UserId") = "" Then Response.Redirect "login.asp"
returnUrl = Request.QueryString("returnUrl")
If returnUrl <> "" Then Response.Redirect returnUrl
Server.Transfer "errors/notfound.asp"
%>
```

## ASP.NET Core equivalent

Use `RedirectToPage` for Razor Pages targets. For a user-supplied return address, check it with `Url.IsLocalUrl` and then call `LocalRedirect`, which accepts only local URLs.

```csharp
public IActionResult OnPost(string? returnUrl)
{
    // sign-in logic
    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
    {
        return LocalRedirect(returnUrl);
    }
    return RedirectToPage("/Index");
}
```

## Notes

- Open redirect: an attacker can send `?returnUrl=https://evil.example`. `LocalRedirect` throws for non-local URLs, so check `Url.IsLocalUrl` first and fall back to a fixed page.
- `Response.Redirect` maps to `Redirect(url)` (302), or `RedirectPermanent` (301). `RedirectToPage` and `RedirectToAction` build the URL from routes instead of strings.
- `Server.Transfer` has no direct equivalent. For error pages, use `app.UseStatusCodePagesWithReExecute("/Errors/{0}")` or return a view from the action.
- Test with a `WebApplicationFactory` integration test. Assert the `Location` header for a local `returnUrl`, and the fallback for `https://evil.example`.

# Response.Write to Razor Output

Tags: Response.Write, <%= %>, <% %>, Server.HTMLEncode, Server.URLEncode, Response.Flush, Html.Raw, @Html.Raw, Razor, HtmlEncoder, cross-site scripting, XSS

## Legacy pattern

Classic ASP writes HTML by mixing code blocks with `Response.Write` or `<%= %>`. Values go into the page as-is unless the developer remembers to call `Server.HTMLEncode`.

```asp
<% For Each item In items %>
  <li><%= item("Name") %></li>
<% Next %>
<% Response.Write "<p>Hello " & Request.QueryString("name") & "</p>" %>
```

## ASP.NET Core equivalent

Razor writes HTML in the template, and `@` values are HTML-encoded by default. The view needs no explicit encoding call.

```cshtml
@foreach (var item in Model.Items)
{
    <li>@item.Name</li>
}
<p>Hello @Model.Name</p>
```

## Notes

- `Html.Raw(value)` skips encoding. Use it only for HTML you produced or sanitized. A `<script>` tag from a comment field would run in the browser if passed to `Html.Raw`.
- `Server.HTMLEncode` maps to automatic encoding in Razor. `Server.URLEncode` maps to `Url.Action` or `UrlEncoder` for URL parts.
- Write markup in the view, not as strings built in C#. A string passed to `@` is encoded, so its tags show as plain text.
- Test with `<script>alert(1)</script>` as input and assert the output contains `&lt;script&gt;`. Use `WebApplicationFactory` for full-page checks.

# Request Values to Model Binding

Tags: Request.QueryString, Request.Form, Request("sort"), Request.Params, Request.Cookies, Request.ServerVariables, ModelState, [FromQuery], [FromForm], [BindProperty], Required, Range, DataAnnotations

## Legacy pattern

Classic ASP reads each value from the `Request` collections by name, converts it by hand, and supplies a default when it is missing. `Request("x")` searches several collections, so the source is unclear.

```asp
<%
page = CInt(Request.QueryString("page"))
If page < 1 Then page = 1
sort = Request("sort")
email = Request.Form("email")
%>
```

## ASP.NET Core equivalent

Declare the inputs as a record and let model binding fill it from a named source. Validation attributes run before the action body.

```csharp
public sealed record ProductFilter(
    [property: Range(1, 10_000)] int Page = 1,
    [property: MaxLength(20)] string? Sort = null);

public IActionResult Index([FromQuery] ProductFilter filter)
{
    if (!ModelState.IsValid) return BadRequest(ModelState);
    return View(filter);
}
```

## Notes

- Name the source explicitly (`[FromQuery]`, `[FromForm]`, `[FromRoute]`). `Request("x")` hid where a value came from.
- In Razor Pages, put `[BindProperty]` on a PageModel property for POST data. Use `[BindProperty(SupportsGet = true)]` if the same property also comes from the query string.
- Validation attributes (`[Required]`, `[Range]`, `[MaxLength]`) replace hand-written checks. Always check `ModelState.IsValid` first.
- Test with a `WebApplicationFactory` request such as `?page=0`, and unit-test attributes with `Validator.TryValidateObject`.

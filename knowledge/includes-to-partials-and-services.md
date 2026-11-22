# Includes to Partials and Services

Tags: <!--#include file=, <!--#include virtual=, #include, header.inc, footer.inc, fn_format.asp, Server.MapPath, Razor partial, partial tag helper, _Layout, RenderBody, dependency injection

## Legacy pattern

Classic ASP pastes files into the page at compile time with server-side include directives. Markup and shared helper functions travel together, so one change in an include affects every page that uses it.

```asp
<!--#include file="includes/header.inc" -->
<h1>Orders</h1>
<!--#include virtual="/includes/fn_format.asp" -->
<%= FormatPrice(order("Total")) %>
<!--#include file="includes/footer.inc" -->
```

## ASP.NET Core equivalent

Split the markup into a layout and partial views. Move shared logic into a service that is injected where it is needed.

```cshtml
@* Views/Shared/_Layout.cshtml *@
<partial name="_Header" />
@RenderBody()
<partial name="_Footer" />
```

```csharp
public interface IPriceFormatter { string Format(decimal amount); }

public sealed class PriceFormatter : IPriceFormatter
{
    public string Format(decimal amount) => amount.ToString("C", CultureInfo.CurrentCulture);
}

// Program.cs
builder.Services.AddSingleton<IPriceFormatter, PriceFormatter>();
// In a view: @inject IPriceFormatter Formatter
```

## Notes

- Use `<partial>` or `Html.PartialAsync` for markup only. Keep data access out of partials; pass them the model instead.
- Shared functions belong in a service with an interface, registered with DI, so unit tests can replace it with a fake.
- One `_Layout.cshtml` with `@RenderBody()` and sections replaces copy-pasted headers, so every page gets the same navigation.
- Partials keep Razor HTML encoding. The formatter should return plain text, not HTML.

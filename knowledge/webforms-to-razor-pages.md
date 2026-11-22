# WebForms to Razor Pages

Tags: Page_Load, IsPostBack, ViewState, runat="server", asp:TextBox, asp:Button, OnClick, btnSave_Click, Page.IsValid, __doPostBack, PageModel, OnGet, OnPost, Razor Pages, asp-for, tag helpers

## Legacy pattern

ASP.NET WebForms runs `Page_Load` on every request and uses `IsPostBack` to skip first-load work. Server controls keep their state in the hidden `ViewState` field, and button clicks fire server-side handlers.

```aspx
<asp:TextBox ID="txtName" runat="server" />
<asp:Button ID="btnSave" runat="server" Text="Save" OnClick="btnSave_Click" />
```

```csharp
protected void Page_Load(object sender, EventArgs e)
{
    if (!IsPostBack)
    {
        ViewState["LoadedAt"] = DateTime.UtcNow;
    }
}

protected void btnSave_Click(object sender, EventArgs e)
{
    lblStatus.Text = "Saved " + txtName.Text;
}
```

## ASP.NET Core equivalent

A Razor Page is a `.cshtml` file with a `PageModel`. `OnGet` and `OnPost` replace `Page_Load` and the click event. A new model is created per request, and tag helpers render the form.

```csharp
public sealed class EditModel(IUserService users) : PageModel
{
    [BindProperty]
    public UserInput Input { get; set; } = new();

    public async Task OnGetAsync(int id, CancellationToken cancellationToken)
    {
        Input = await users.GetForEditAsync(id, cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        await users.SaveAsync(Input, cancellationToken);
        return RedirectToPage("Index");
    }
}
```

```cshtml
<form method="post">
  <input asp-for="Input.Name" class="form-control" />
  <span asp-validation-for="Input.Name"></span>
  <button type="submit">Save</button>
</form>
```

## Notes

- There is no ViewState. Keep values in the form, the database, or `TempData` for one request after a redirect, not in hidden fields managed by hand.
- Razor Pages validates anti-forgery tokens on POST forms built with tag helpers. WebForms relied on ViewState MAC and event validation instead.
- Check `ModelState.IsValid`, the modern replacement for `Page.IsValid`, before saving. Redirect after a successful POST (Post/Redirect/Get).
- Test handlers by building the PageModel with fakes and asserting the result type, or use `WebApplicationFactory` for a full form post.

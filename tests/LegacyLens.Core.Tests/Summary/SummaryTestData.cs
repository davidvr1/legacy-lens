using LegacyLens.Core.Model;

namespace LegacyLens.Core.Tests.Summary;

internal static class SummaryTestData
{
    public static CodeSummary Valid() => new("Shows a page.", ["Form: a"], [], ["SELECT"], [], ["ADODB.Connection"]);

    public static SourceFile File(SourceLanguage lang = SourceLanguage.ClassicAsp, string code = "<%\nResponse.Write 1\n%>")
        => new("page.asp", lang, code);
}

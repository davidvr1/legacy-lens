using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;

namespace LegacyLens.Core.Tests;

/// <summary>Runs all rules on the hand-written pages in samples/ and checks the exact findings.</summary>
public sealed class SampleFilesTests
{
    public static TheoryData<string, string[]> Samples => new()
    {
        {
            "product-details.asp",
            ["INC001:2", "COM001:8", "SEC001:9", "SQL001:11", "XSS001:17", "XSS001:18", "INC001:23"]
        },
        {
            "login.asp",
            ["ERR001:4", "SEC001:6", "SES001:13", "COM001:16", "SQL001:20", "SQL001:21", "XSS001:40"]
        },
        {
            "search.asp",
            ["INC001:2", "SQL001:9", "XSS001:21"]
        },
        {
            // Parameterized query, encoded output, checked errors, commented-out old code.
            "safe-orders.asp",
            ["INC001:2", "COM001:10", "COM001:18"]
        },
        {
            "CustomerSearch.aspx.cs",
            ["SEC001:10", "XSS001:15", "SQL001:20"]
        },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void Sample_produces_expected_findings(string fileName, string[] expected)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "samples", fileName);
        var language = fileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            ? SourceLanguage.WebForms
            : SourceLanguage.ClassicAsp;
        var file = new SourceFile(fileName, language, File.ReadAllText(path));

        var findings = CreateEngine().Run(file);

        Assert.Equal(expected, findings.Select(finding => $"{finding.RuleId}:{finding.Line}"));
    }

    private static RuleEngine CreateEngine() => new(
    [
        new SqlConcatenationRule(),
        new XssOutputRule(),
        new HardCodedSecretRule(),
        new OnErrorResumeNextRule(),
        new ComObjectRule(),
        new SessionUsageRule(),
        new IncludeRule(),
    ]);
}

using LegacyLens.Core.Model;
using LegacyLens.Core.Rules;

namespace LegacyLens.Core.Tests.Rules;

public class HardCodedSecretRuleTests
{
    private static IReadOnlyList<Finding> Analyze(string code, SourceLanguage language = SourceLanguage.ClassicAsp)
    {
        var file = new SourceFile("test.asp", language, code);
        return new HardCodedSecretRule().Analyze(file);
    }

    [Fact]
    public void Flags_ConnectionString_WithPassword_SecretNotLeaked()
    {
        const string code =
            """connStr = "Provider=SQLOLEDB;Data Source=myServer;User Id=sa;Password=P@ss123;" """;

        var finding = Assert.Single(Analyze(code));

        Assert.Equal("SEC001", finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Equal(1, finding.Line);
        Assert.Contains("password", finding.Explanation);
        Assert.DoesNotContain("P@ss123", finding.Explanation);
    }

    [Fact]
    public void Flags_ConnectionString_WithoutPassword()
    {
        const string code = """connStr = "Data Source=myServer;Initial Catalog=MyDb;" """;

        var finding = Assert.Single(Analyze(code));

        Assert.Equal("SEC001", finding.RuleId);
        Assert.Contains("cannot change per environment", finding.Explanation);
    }

    [Fact]
    public void Flags_HardCodedApiKey_ValueNotInExplanation()
    {
        const string code = """ApiKey = "sk-1234567890abcdef" """;

        var finding = Assert.Single(Analyze(code));

        Assert.Equal("SEC001", finding.RuleId);
        Assert.Contains("ApiKey", finding.Explanation);
        Assert.DoesNotContain("sk-1234567890abcdef", finding.Explanation);
    }

    [Fact]
    public void Flags_HardCodedPassword_CSharp()
    {
        const string code = """string password = "Sup3rSecret!";""";

        var finding = Assert.Single(Analyze(code, SourceLanguage.WebForms));

        Assert.Equal("SEC001", finding.RuleId);
        Assert.DoesNotContain("Sup3rSecret!", finding.Explanation);
    }

    [Fact]
    public void DoesNotFlag_EmptyPasswordInitialization()
    {
        const string code = """password = "" """;

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_NonSecretNameComparison()
    {
        const string code = """If username = "admin" Then""";

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_CommentedOutVbScript()
    {
        const string code = """' password = "hunter2" """;

        Assert.Empty(Analyze(code));
    }

    [Fact]
    public void DoesNotFlag_CommentedOutCSharp()
    {
        const string code = """// string apiKey = "abc123";""";

        Assert.Empty(Analyze(code, SourceLanguage.WebForms));
    }
}

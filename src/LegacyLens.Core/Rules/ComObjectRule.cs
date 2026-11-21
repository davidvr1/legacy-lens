using System.Text.RegularExpressions;
using LegacyLens.Core.Model;

namespace LegacyLens.Core.Rules;

/// <summary>COM001: lists COM dependencies created with Server.CreateObject("ProgId").</summary>
public sealed partial class ComObjectRule : IRule
{
    public string Id => "COM001";

    public IReadOnlyList<Finding> Analyze(SourceFile file)
    {
        var findings = new List<Finding>();

        for (var i = 0; i < file.CodeLines.Count; i++)
        {
            foreach (Match match in CreateObject().Matches(file.CodeLines[i]))
            {
                var progId = match.Groups["progId"].Value;
                findings.Add(new Finding(
                    Id,
                    Severity.Info,
                    i + 1,
                    $"COM dependency: {progId}",
                    $"The page creates the COM object '{progId}'. COM components do not run in ASP.NET Core on Linux and must be replaced during migration.",
                    FixFor(progId)));
            }
        }

        return findings;
    }

    private static string FixFor(string progId)
    {
        if (progId.StartsWith("ADODB.", StringComparison.OrdinalIgnoreCase))
        {
            return "Replace ADO with ADO.NET (SqlConnection and SqlCommand with parameters), Dapper or EF Core.";
        }

        if (progId.StartsWith("Scripting.FileSystemObject", StringComparison.OrdinalIgnoreCase))
        {
            return "Use System.IO (File, Directory, Path) behind an injected service.";
        }

        if (progId.StartsWith("CDO.", StringComparison.OrdinalIgnoreCase)
            || progId.StartsWith("CDONTS.", StringComparison.OrdinalIgnoreCase))
        {
            return "Send email through an injected email service (for example with MailKit).";
        }

        if (progId.StartsWith("MSXML2.", StringComparison.OrdinalIgnoreCase)
            || progId.StartsWith("Microsoft.XMLHTTP", StringComparison.OrdinalIgnoreCase))
        {
            return "Use System.Xml or System.Text.Json for documents and HttpClient (via IHttpClientFactory) for HTTP calls.";
        }

        return "Find a .NET library or service that does the same job and wrap it behind an interface.";
    }

    [GeneratedRegex(@"(?<![\w.])(?:Server\s*\.\s*)?CreateObject\s*\(\s*""(?<progId>[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex CreateObject();
}

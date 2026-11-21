using System.Text.RegularExpressions;
using LegacyLens.Core.Model;
using LegacyLens.Core.Rules.Parsing;

namespace LegacyLens.Core.Rules;

/// <summary>SEC001: connection strings, passwords or keys written in the source code.</summary>
public sealed partial class HardCodedSecretRule : IRule
{
    private const string Fix =
        "Move the value to configuration outside source control (environment variables, user secrets or a secret store) and rotate the exposed value.";

    public string Id => "SEC001";

    public IReadOnlyList<Finding> Analyze(SourceFile file)
    {
        var findings = new List<Finding>();

        for (var i = 0; i < file.CodeLines.Count; i++)
        {
            var line = file.CodeLines[i];
            var (masked, literals) = StringLiterals.Split(line, file.Language);

            var connectionString = literals.FirstOrDefault(IsConnectionString);
            if (connectionString is not null)
            {
                findings.Add(ConnectionStringFinding(i + 1, ConnectionPassword().IsMatch(connectionString)));
                continue;
            }

            // Matched on the masked line so names inside strings are ignored;
            // the value is read from the original line at the same position.
            var secretName = SecretValue().Matches(masked)
                .Where(match => line.Substring(match.Groups["value"].Index, match.Groups["value"].Length).Trim().Length > 0)
                .Select(match => match.Groups["name"].Value)
                .FirstOrDefault();

            if (secretName is not null)
            {
                findings.Add(SecretFinding(i + 1, secretName));
            }
        }

        return findings;
    }

    // At least two "key=value;" pairs with connection string keys.
    private static bool IsConnectionString(string literal) => ConnectionStringPart().Matches(literal).Count >= 2;

    private Finding ConnectionStringFinding(int line, bool hasPassword) => new(
        Id,
        Severity.High,
        line,
        "Hard-coded connection string",
        hasPassword
            ? "A database connection string, including a password, is written in the source code. Anyone who can read the code can connect to the database."
            : "A database connection string is written in the source code, so it cannot change per environment and exposes server details.",
        Fix);

    // The secret value itself is never repeated in the finding.
    private Finding SecretFinding(int line, string name) => new(
        Id,
        Severity.High,
        line,
        "Hard-coded secret",
        $"'{name}' is set to or compared with a fixed value in the source code. Anyone who can read the code knows the secret.",
        Fix);

    [GeneratedRegex(
        @"\b(?:provider|data\s+source|server|initial\s+catalog|database|dsn|user\s+id|uid|password|pwd)\s*=[^;]*;",
        RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringPart();

    [GeneratedRegex(@"\b(?:password|pwd)\s*=\s*[^;\s]", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionPassword();

    // A secret-looking name assigned or compared to a string literal: ADMIN_PASSWORD = "...", If pwd = "..." Then.
    [GeneratedRegex(
        @"\b(?<name>\w*(?:password|passwd|pwd|secret|api_?key|access_?key|private_?key|encryption_?key|auth_?token)\w*)\s*(?:==|=|<>|!=)\s*""(?<value>[^""]*)""",
        RegexOptions.IgnoreCase)]
    private static partial Regex SecretValue();
}

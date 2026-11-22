using LegacyLens.Core.Migration;
using LegacyLens.Core.Model;
using LegacyLens.Core.Validation;

namespace LegacyLens.Core.Tests.Migration;

internal static class MigrationTestData
{
    public static readonly KnowledgeChunk ChunkA = new("sql-injection", "SQL Injection", "Use parameters.");
    public static readonly KnowledgeChunk ChunkB = new("xss-output", "XSS Output", "Encode output.");

    public static MigrationReply Reply(string code = "class A {}", params string[] sources)
        => new(code, ["note"], sources);

    public static CodeValidationResult Ok() => new(true, []);

    public static CodeValidationResult Fail(int line = 3, string id = "CS0103", string message = "The name 'x' does not exist")
        => new(false, [new CodeDiagnostic(id, message, line)]);

    public static Finding Finding(string title = "SQL built by string concatenation", int line = 2)
        => new("SQL001", Severity.High, line, title, "e", "f");

    public static SourceFile File(string code = "<%\nResponse.Write 1\n%>")
        => new("page.asp", SourceLanguage.ClassicAsp, code);
}

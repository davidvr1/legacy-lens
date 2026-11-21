using LegacyLens.Core.Rules.Parsing;

namespace LegacyLens.Core.Model;

/// <summary>A legacy source file to analyze.</summary>
public sealed class SourceFile
{
    public SourceFile(string fileName, SourceLanguage language, string code)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(code);

        FileName = fileName;
        Language = language;
        Code = code;
        Lines = code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        CodeLines = CodeScanner.StripComments(Lines, language);
    }

    public string FileName { get; }

    public SourceLanguage Language { get; }

    public string Code { get; }

    /// <summary>The raw lines. Index 0 is line 1.</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>
    /// The same lines with only server-side code kept: comments are removed, so rules do not flag
    /// commented-out code, and HTML outside <c>&lt;% %&gt;</c> blocks is replaced with spaces.
    /// </summary>
    public IReadOnlyList<string> CodeLines { get; }
}

using LegacyLens.Core.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LegacyLens.Infrastructure.Validation;

/// <summary>
/// Compiles C# code in memory and reports the errors. Nothing is emitted or run. The references are the
/// running framework's assemblies (ASP.NET Core and Extensions included), so Razor Page and controller code compiles.
/// </summary>
public sealed class RoslynCodeValidator : ICodeValidator
{
    public const int MaxDiagnostics = 20;

    /// <summary>The same global usings as the ASP.NET Core Web SDK implicit usings.</summary>
    private const string GlobalUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Net.Http;
        global using System.Threading;
        global using System.Threading.Tasks;
        global using Microsoft.AspNetCore.Builder;
        global using Microsoft.AspNetCore.Hosting;
        global using Microsoft.AspNetCore.Http;
        global using Microsoft.AspNetCore.Routing;
        global using Microsoft.Extensions.Configuration;
        global using Microsoft.Extensions.DependencyInjection;
        global using Microsoft.Extensions.Hosting;
        global using Microsoft.Extensions.Logging;
        """;

    private static readonly string[] ReferencePrefixes =
    [
        "System.",
        "Microsoft.AspNetCore.",
        "Microsoft.Extensions.",
        "Microsoft.Net.Http.Headers",
        "Microsoft.Win32.",
    ];

    private static readonly string[] ReferenceFileNames = ["netstandard.dll", "mscorlib.dll", "Microsoft.CSharp.dll"];

    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(LoadReferences);

    public Task<CodeValidationResult> ValidateAsync(string code, CancellationToken cancellationToken)
        => Task.Run(() => Validate(code, cancellationToken), cancellationToken);

    private static CodeValidationResult Validate(string code, CancellationToken cancellationToken)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp14);
        var snippetTree = CSharpSyntaxTree.ParseText(code, parseOptions, path: "Migrated.cs", cancellationToken: cancellationToken);
        var usingsTree = CSharpSyntaxTree.ParseText(GlobalUsings, parseOptions, path: "GlobalUsings.cs", cancellationToken: cancellationToken);

        var compilation = CSharpCompilation.Create(
            "LegacyLens.Migrated",
            [snippetTree, usingsTree],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation.GetDiagnostics(cancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error && d.Location.SourceTree == snippetTree)
            .OrderBy(d => d.Location.GetLineSpan().StartLinePosition.Line)
            .Take(MaxDiagnostics)
            .Select(d => new CodeDiagnostic(d.Id, d.GetMessage(), d.Location.GetLineSpan().StartLinePosition.Line + 1))
            .ToList();

        return new CodeValidationResult(errors.Count == 0, errors);
    }

    private static IReadOnlyList<MetadataReference> LoadReferences()
    {
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;

        return trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(path => IsReferenceAssembly(Path.GetFileName(path)))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
    }

    private static bool IsReferenceAssembly(string fileName)
    {
        return ReferenceFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase)
            || ReferencePrefixes.Any(prefix => fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}

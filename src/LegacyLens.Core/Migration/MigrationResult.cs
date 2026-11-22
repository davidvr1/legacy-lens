using LegacyLens.Core.Model;

namespace LegacyLens.Core.Migration;

/// <summary>The outcome of a migration request: either a proposal or a user-facing error, never both.</summary>
public sealed record MigrationResult(MigrationProposal? Proposal, string? Error);

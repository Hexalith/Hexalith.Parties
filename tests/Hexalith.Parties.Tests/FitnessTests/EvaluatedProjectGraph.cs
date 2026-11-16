namespace Hexalith.Parties.Tests.FitnessTests;

/// <summary>Records the independently evaluated dependency graph for one project.</summary>
/// <param name="Properties">Effective build properties.</param>
/// <param name="PackageReferences">Declared package identities.</param>
/// <param name="ProjectReferences">Resolved project paths.</param>
/// <param name="PackageMetadata">Effective version metadata by package.</param>
/// <param name="ProjectMetadata">Reference context metadata.</param>
internal sealed record EvaluatedProjectGraph(
    IReadOnlyDictionary<string, string> Properties,
    string[] PackageReferences,
    string[] ProjectReferences,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> PackageMetadata,
    IReadOnlyDictionary<string, string>[] ProjectMetadata);

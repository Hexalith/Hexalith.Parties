namespace Hexalith.Parties.Contracts.Tests.Package;

/// <summary>Locates the isolated contract package probe and its selected family identities.</summary>
/// <param name="WorkingDirectory">The task-owned probe directory.</param>
/// <param name="PackageFeed">The isolated package feed.</param>
/// <param name="PartiesPackagePath">The packed Parties contract archive.</param>
/// <param name="EventStoreContractsPackagePath">The packed EventStore contract archive.</param>
/// <param name="PartiesVersion">The Parties probe version.</param>
/// <param name="EventStoreVersion">The selected EventStore version.</param>
/// <param name="McpCliVersion">The selected MCP contract version.</param>
internal sealed record ContractsPackageArtifacts(string WorkingDirectory,
    string PackageFeed,
    string PartiesPackagePath,
    string EventStoreContractsPackagePath,
    string PartiesVersion,
    string EventStoreVersion,
    string McpCliVersion);

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Hexalith.EventStore.Aspire;

namespace Hexalith.Parties.Aspire;

/// <summary>Locates the separately built Parties server in the consuming root workspace.</summary>
internal sealed class PartiesServerProjectMetadata : IProjectMetadata
{
    /// <inheritdoc/>
    public string ProjectPath => RepositoryProjectPaths.GetReferencedModuleProjectPath(
        "Hexalith.Parties", "src", "Hexalith.Parties", "Hexalith.Parties.csproj");

    /// <inheritdoc/>
    public bool SuppressBuild => true;
}

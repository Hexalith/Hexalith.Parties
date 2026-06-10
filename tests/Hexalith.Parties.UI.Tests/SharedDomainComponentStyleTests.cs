using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class SharedDomainComponentStyleTests
{
    private static readonly string[] ComponentFiles =
    [
        "src/Hexalith.Parties.UI/Components/Shared/PartyStateBadge.razor",
        "src/Hexalith.Parties.UI/Components/Shared/PartyStateBadge.razor.css",
        "src/Hexalith.Parties.UI/Components/Shared/DataFreshnessIndicator.razor",
        "src/Hexalith.Parties.UI/Components/Shared/DataFreshnessIndicator.razor.css",
        "src/Hexalith.Parties.UI/Components/Shared/GdprDestructiveButton.razor",
        "src/Hexalith.Parties.UI/Components/Shared/GdprDestructiveButton.razor.css",
    ];

    private static readonly string[] ForbiddenColorTokens =
    [
        "#",
        "rgb(",
        "rgba(",
        "hsl(",
        "hsla(",
    ];

    [Fact]
    public void Shared_domain_component_files_do_not_use_hard_coded_color_literals()
    {
        string repositoryRoot = FindRepositoryRoot();

        foreach (string relativePath in ComponentFiles)
        {
            string path = Path.Combine(repositoryRoot, relativePath);
            if (!File.Exists(path))
            {
                continue;
            }

            string content = File.ReadAllText(path);
            foreach (string forbidden in ForbiddenColorTokens)
            {
                content.ShouldNotContain(forbidden, Case.Insensitive, $"Forbidden color literal '{forbidden}' found in {relativePath}.");
            }

            content.ShouldNotContain("#0097A7", Case.Insensitive, $"Raw brand teal found in {relativePath}.");
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Parties.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}

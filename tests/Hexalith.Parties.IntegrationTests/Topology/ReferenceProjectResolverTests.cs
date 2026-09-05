extern alias apphost;

using Shouldly;

using ReferenceProjectResolver = apphost::Hexalith.Parties.AppHost.ReferenceProjectResolver;

namespace Hexalith.Parties.IntegrationTests.Topology;

/// <summary>Exercises topology-project resolution across supported repository layouts.</summary>
[Collection("Non-parallel")]
public sealed class ReferenceProjectResolverTests
{
    /// <summary>Verifies that a root-declared umbrella dependency wins over stale nested content.</summary>
    [Fact]
    public void FindPrefersUmbrellaDeclaredSiblingOverStaleNestedCheckout()
    {
        string workspace = CreateTemporaryDirectory();
        try
        {
            DirectoryInfo partiesRoot = Directory.CreateDirectory(Path.Combine(workspace, "references", "Hexalith.Parties"));
            string sibling = CreateProject(workspace, "references", "Hexalith.EventStore", "src", "Host.csproj");
            _ = CreateProject(partiesRoot.FullName, "references", "Hexalith.EventStore", "src", "Host.csproj");

            string? result = ReferenceProjectResolver.Find(
                partiesRoot,
                "Hexalith.EventStore",
                Path.Combine("src", "Host.csproj"));

            result.ShouldBe(sibling);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    /// <summary>Verifies standalone nested, sibling, and sibling-references repository layouts.</summary>
    [Fact]
    public void FindSupportsStandaloneNestedSiblingAndSiblingReferencesLayouts()
    {
        foreach (string layout in new[] { "nested", "sibling", "sibling-references" })
        {
            string workspace = CreateTemporaryDirectory();
            try
            {
                DirectoryInfo partiesRoot = Directory.CreateDirectory(Path.Combine(workspace, "modules", "Hexalith.Parties"));
                string expected = layout switch
                {
                    "nested" => CreateProject(partiesRoot.FullName, "references", "Hexalith.EventStore", "src", "Host.csproj"),
                    "sibling" => CreateProject(workspace, "modules", "Hexalith.EventStore", "src", "Host.csproj"),
                    _ => CreateProject(workspace, "modules", "references", "Hexalith.EventStore", "src", "Host.csproj"),
                };

                string? result = ReferenceProjectResolver.Find(
                    partiesRoot,
                    "Hexalith.EventStore",
                    Path.Combine("src", "Host.csproj"));

                result.ShouldBe(expected, layout);
            }
            finally
            {
                Directory.Delete(workspace, recursive: true);
            }
        }
    }

    /// <summary>Verifies fail-closed diagnostics and root-aware initialization guidance.</summary>
    [Fact]
    public void ResolveRequiredReportsProbesAndUmbrellaRootInitializationCommand()
    {
        string workspace = CreateTemporaryDirectory();
        try
        {
            DirectoryInfo partiesRoot = Directory.CreateDirectory(Path.Combine(workspace, "references", "Hexalith.Parties"));

            FileNotFoundException exception = Should.Throw<FileNotFoundException>(() =>
                ReferenceProjectResolver.ResolveRequired(
                    partiesRoot,
                    "Hexalith.Tenants",
                    Path.Combine("src", "Hexalith.Tenants", "Hexalith.Tenants.csproj")));

            exception.Message.ShouldContain("Probed:");
            exception.Message.ShouldContain(Path.Combine(workspace, "references", "Hexalith.Tenants"));
            exception.Message.ShouldContain($"git -C \"{workspace}\" submodule update --init references/Hexalith.Tenants");
            exception.Message.ShouldContain("Do not use recursive submodule initialization.");
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static string CreateProject(params string[] segments)
    {
        string path = Path.Combine(segments);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<Project />");
        return Path.GetFullPath(path);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "parties-reference-resolver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

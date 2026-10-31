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

    /// <summary>Explicit package selection uses the fixed owned host; every other profile retains the native owner.</summary>
    [Fact]
    public void GatewaySelectionUsesOnlyTheExplicitRunProfile()
    {
        string workspace = CreateTemporaryDirectory();
        try
        {
            DirectoryInfo root = Directory.CreateDirectory(Path.Combine(workspace, "parties"));
            string native = CreateProject(root.FullName, "references", "Hexalith.EventStore", "src", "Hexalith.EventStore", "Hexalith.EventStore.csproj");
            string packaged = CreateProject(root.FullName, "tests", "Hexalith.Parties.EventStoreGateway.TestHost", "Hexalith.Parties.EventStoreGateway.TestHost.csproj");
            ReferenceProjectResolver.ResolveGateway(root, false, false).ShouldBe(native);
            ReferenceProjectResolver.ResolveGateway(root, false, true).ShouldBe(native);
            ReferenceProjectResolver.ResolveGateway(root, true, false).ShouldBe(packaged);
            Should.Throw<InvalidOperationException>(() => ReferenceProjectResolver.ResolveGateway(root, true, true))
                .Message.ShouldContain("cannot be published");
        }
        finally { Directory.Delete(workspace, recursive: true); }
    }

    /// <summary>A missing package host cannot silently fall back to the newer native gateway.</summary>
    [Fact]
    public void PackageGatewaySelectionRefusesMissingOwnedHost()
    {
        string workspace = CreateTemporaryDirectory();
        try
        {
            DirectoryInfo root = new(workspace);
            _ = CreateProject(workspace, "references", "Hexalith.EventStore", "src", "Hexalith.EventStore", "Hexalith.EventStore.csproj");
            var error = Should.Throw<FileNotFoundException>(() => ReferenceProjectResolver.ResolveGateway(root, true, false));
            error.FileName.ShouldBe(Path.Combine(workspace, "tests", "Hexalith.Parties.EventStoreGateway.TestHost", "Hexalith.Parties.EventStoreGateway.TestHost.csproj"));
            error.Message.ShouldContain("package compatibility");
        }
        finally { Directory.Delete(workspace, recursive: true); }
    }

    /// <summary>The sample follows the same explicit package profile and refuses missing hosts or publication.</summary>
    [Fact]
    public void SampleSelectionUsesOnlyTheExplicitRunProfileAndRefusesMissingHost()
    {
        string workspace = CreateTemporaryDirectory();
        try
        {
            DirectoryInfo root = new(workspace);
            string native = CreateProject(workspace, "references", "Hexalith.EventStore", "samples", "Hexalith.EventStore.Sample", "Hexalith.EventStore.Sample.csproj");
            ReferenceProjectResolver.ResolveSample(root, false, false).ShouldBe(native);
            ReferenceProjectResolver.ResolveSample(root, false, true).ShouldBe(native);
            Should.Throw<FileNotFoundException>(() => ReferenceProjectResolver.ResolveSample(root, true, false))
                .Message.ShouldContain("package compatibility sample");
            string packaged = CreateProject(workspace, "tests", "Hexalith.Parties.EventStoreSample.TestHost", "Hexalith.Parties.EventStoreSample.TestHost.csproj");
            ReferenceProjectResolver.ResolveSample(root, true, false).ShouldBe(packaged);
            Should.Throw<InvalidOperationException>(() => ReferenceProjectResolver.ResolveSample(root, true, true))
                .Message.ShouldContain("cannot be published");
        }
        finally { Directory.Delete(workspace, recursive: true); }
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

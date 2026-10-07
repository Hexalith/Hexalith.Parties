using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Parties.UI.Tests;

public sealed class IdentityBindingBoundaryTests
{
    [Fact]
    public void ConsumerLoginAliases_StayOutOfPartiesContractsAndEventStreamPaths()
    {
        string[] forbiddenRoots =
        [
            "src/Hexalith.Parties.Contracts/Commands",
            "src/Hexalith.Parties.Contracts/Events",
            "src/Hexalith.Parties.Projections",
            "src/Hexalith.Parties/Domain",
            "src/Hexalith.Parties/Actors",
        ];

        foreach (string root in forbiddenRoots)
        {
            string absoluteRoot = ProjectRoot(root);
            if (!Directory.Exists(absoluteRoot))
            {
                continue;
            }

            IReadOnlyCollection<string> offenders = Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories)
                .Where(static path => new[] { "IdentityBindingRecord", "IdentityBindingKey", "IdentityBindingAuditEntry", "IIdentityBindingStore", "Issuer", "Subject" }
                    .Any(term => File.ReadAllText(path).Contains(term, StringComparison.Ordinal)))
                .Select(path => Path.GetRelativePath(ProjectRoot(), path))
                .ToList();

            offenders.ShouldBeEmpty($"Consumer login alias mapping must remain UI/BFF-owned, not in {root}.");
        }
    }

    [Fact]
    public void UiHost_DoesNotExposeIdentityBindingPublicHttpEndpoint()
    {
        string source = File.ReadAllText(ProjectRoot("src/Hexalith.Parties.UI/Program.cs"));

        source.ShouldNotContain("MapPost(\"/identity-binding", Case.Insensitive);
        source.ShouldNotContain("MapGet(\"/identity-binding", Case.Insensitive);
        source.ShouldNotContain("MapControllers(");
    }

    [Fact]
    public void PartiesDaprAccessControl_DoesNotExpandForIdentityBinding()
    {
        string appHostAccessControl = File.ReadAllText(
            ProjectRoot("src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml"));

        appHostAccessControl.ShouldNotContain("identity", Case.Insensitive);
    }

    [Fact]
    public void IdentityBindingImplementation_DoesNotReferenceSecretsOrDecodedJwtPayloads()
    {
        string root = ProjectRoot("src/Hexalith.Parties.UI/IdentityBinding");
        string[] forbiddenTerms = ["bearer", "clientsecret", "password", "decoded jwt", "jwt payload", "email"];

        foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(path);
            foreach (string term in forbiddenTerms)
            {
                source.ShouldNotContain(term, Case.Insensitive);
            }
        }
    }

    [Fact]
    public void UiProject_DoesNotGainDaprOrServerReferencesForIdentityBinding()
    {
        XDocument project = XDocument.Load(ProjectRoot("src/Hexalith.Parties.UI/Hexalith.Parties.UI.csproj"));

        IReadOnlyCollection<string> references = project.Descendants("ProjectReference")
            .Select(static reference => reference.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

        references.ShouldNotContain(reference => reference.Contains(@"Hexalith.Parties.Server", StringComparison.Ordinal));
        references.ShouldNotContain(reference => reference.Contains(@"Hexalith.Parties.Projections", StringComparison.Ordinal));
        references.ShouldNotContain(reference => reference.Contains(@"Dapr", StringComparison.Ordinal));
    }

    private static string ProjectRoot(string? relativePath = null)
    {
        // xUnit runs from the assembly directory, which may be outside the source tree.
        // The owner verifier supplies its exact repository; ordinary in-tree builds retain discovery.
        string current = Environment.GetEnvironmentVariable("HEXALITH_PARTIES_SOURCE_ROOT") ?? AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(current, "Hexalith.Parties.slnx")))
        {
            DirectoryInfo? parent = Directory.GetParent(current);
            parent.ShouldNotBeNull();
            current = parent.FullName;
        }

        return relativePath is null ? current : Path.Combine(current, relativePath);
    }
}

using System.Xml.Linq;

using Hexalith.Parties.PackageTests;

using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

/// <summary>Exercises real Parties consumer evaluation across catalog and family fallback boundaries.</summary>
public sealed class PackageCatalogFallbackTests
{
    private static readonly (string Property, string Package, string Caller, string Override, string Fallback)[] Families =
    [
        ("HexalithEventStoreVersion", "Hexalith.EventStore.Contracts", "90.1.1", "91.1.1", "4.2.0"),
        ("HexalithTenantsVersion", "Hexalith.Tenants.Contracts", "90.2.1", "91.2.1", "7.0.5"),
        ("HexalithFrontComposerVersion", "Hexalith.FrontComposer.Shell", "90.3.1", "91.3.1", "4.7.6"),
        ("HexalithMemoriesVersion", "Hexalith.Memories.Client.Rest", "90.4.1", "91.4.1", "3.0.25"),
    ];

    /// <summary>The default catalog, caller-selected catalog and explicit family versions retain their precedence.</summary>
    [Fact]
    public void CallerCatalogDefaultsAndExplicitFamilyOverridesFollowMSBuildPrecedence()
    {
        string root = RepositoryRoot.Locate();
        string directory = Path.Combine(Path.GetTempPath(), $"parties-caller-catalog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string catalog = WriteCatalog(directory);
            IReadOnlyDictionary<string, string> standalone = EffectivePackageVersions.ReadPackages(root, isolateEnvironment: true);
            IReadOnlyDictionary<string, string> defaults = EffectivePackageVersions.ReadPackages(root,
                "references/Hexalith.Builds/Props/Directory.Packages.props", isolateEnvironment: true);
            var properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Hexalith1BuildPackageProps"] = catalog,
            };
            IReadOnlyDictionary<string, string> caller = EffectivePackageVersions.ReadPackages(root,
                isolateEnvironment: true, globalProperties: properties);
            foreach (var family in Families)
            {
                standalone[family.Package].ShouldBe(defaults[family.Package], family.Property);
                caller[family.Package].ShouldBe(family.Caller, family.Property);
                properties[family.Property] = family.Override;
            }

            IReadOnlyDictionary<string, string> explicitVersions = EffectivePackageVersions.ReadPackages(root,
                isolateEnvironment: true, globalProperties: properties);
            foreach (var family in Families)
            {
                explicitVersions[family.Package].ShouldBe(family.Override, family.Property);
            }

            // Query state belongs to the child process; a caller catalog must not
            // change subsequent standalone evaluations or the shared restore files.
            IReadOnlyDictionary<string, string> afterCaller = EffectivePackageVersions.ReadPackages(root, isolateEnvironment: true);
            foreach (var family in Families)
            {
                afterCaller[family.Package].ShouldBe(standalone[family.Package], family.Property);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A missing first catalog activates root fallbacks before the next existing catalog supplies items.</summary>
    [Fact]
    public void MissingFirstCatalogUsesRootFallbacksBeforeTheNextAvailableCatalog()
    {
        string root = RepositoryRoot.Locate();
        string directory = Path.Combine(Path.GetTempPath(), $"parties-missing-catalog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string catalog = WriteCatalog(directory);
            var properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Hexalith1BuildPackageProps"] = Path.Combine(directory, "absent.props"),
                ["Hexalith2BuildPackageProps"] = catalog,
            };
            IReadOnlyDictionary<string, string> versions = EffectivePackageVersions.ReadPackages(root,
                isolateEnvironment: true, globalProperties: properties);
            foreach (var family in Families)
            {
                versions[family.Package].ShouldBe(family.Fallback, family.Property);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string WriteCatalog(string directory)
    {
        // Keep a space in the path to exercise ArgumentList rather than shell quoting.
        string catalog = Path.Combine(directory, "caller catalog.props");
        new XDocument(new XElement("Project",
            new XElement("PropertyGroup",
                new XElement("HexalithVersionsLoaded", "true"),
                Families.Select(family => new XElement(family.Property,
                    new XAttribute("Condition", $"'$({family.Property})' == ''"), family.Caller))),
            new XElement("ItemGroup", Families.Select(family => new XElement("PackageVersion",
                new XAttribute("Include", family.Package),
                new XAttribute("Version", $"$({family.Property})")))))).Save(catalog);
        return catalog;
    }
}

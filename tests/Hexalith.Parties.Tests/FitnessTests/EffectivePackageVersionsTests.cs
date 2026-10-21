using Hexalith.Parties.PackageTests;

using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

/// <summary>Exercises the same MSBuild evaluator used by the live dependency readers.</summary>
public sealed class EffectivePackageVersionsTests
{
    [Fact]
    public void RootPinsAndConditionalImportOrderGovernEveryVersionReader()
    {
        string root = CreateFixture();
        try
        {
            File.WriteAllText(Path.Combine(root, "catalog.props"), """
                <Project><PropertyGroup>
                  <HexalithEventStoreVersion Condition="'$(HexalithEventStoreVersion)' == ''">3.0.0</HexalithEventStoreVersion>
                  <HexalithCommonsVersion Condition="'$(HexalithCommonsVersion)' == ''">2.0.0</HexalithCommonsVersion>
                  <HexalithFrontComposerVersion Condition="'$(HexalithFrontComposerVersion)' == ''">4.0.0</HexalithFrontComposerVersion>
                  <HexalithCommonsVersion Condition="'$(Configuration)' == 'Release'">2.9.0</HexalithCommonsVersion>
                  <HexalithFrontComposerVersion Condition="'$(Configuration)' == 'Debug'">4.99.0</HexalithFrontComposerVersion>
                </PropertyGroup><ItemGroup>
                  <PackageVersion Include="Hexalith.EventStore.Contracts" Version="$(HexalithEventStoreVersion)" />
                  <PackageVersion Include="Hexalith.Commons.Http" Version="$(HexalithCommonsVersion)" />
                  <PackageVersion Include="Hexalith.FrontComposer.Shell" Version="$(HexalithFrontComposerVersion)" />
                </ItemGroup></Project>
                """);
            File.WriteAllText(Path.Combine(root, "Consumer.csproj"), """
                <Project><PropertyGroup>
                  <HexalithEventStoreVersion>3.8.0</HexalithEventStoreVersion>
                  <HexalithCommonsVersion>2.8.0</HexalithCommonsVersion>
                  <HexalithFrontComposerVersion>4.8.0</HexalithFrontComposerVersion>
                </PropertyGroup><Import Project="catalog.props" /></Project>
                """);

            IReadOnlyDictionary<string, string> packages = EffectivePackageVersions.ReadPackages(root, "Consumer.csproj");
            foreach ((string property, string package, string expected) in new[]
                     {
                         ("HexalithEventStoreVersion", "Hexalith.EventStore.Contracts", "3.8.0"),
                         ("HexalithCommonsVersion", "Hexalith.Commons.Http", "2.9.0"),
                         ("HexalithFrontComposerVersion", "Hexalith.FrontComposer.Shell", "4.8.0"),
                     })
            {
                EffectivePackageVersions.ReadProperty(root, property, "Consumer.csproj").ShouldBe(expected);
                packages[package].ShouldBe(expected);
            }

            EffectivePackageVersions.ReadProperty(root, "HexalithFrontComposerVersion", "Consumer.csproj", "Debug")
                .ShouldBe("4.99.0");
            EffectivePackageVersions.ReadPackages(root, "Consumer.csproj", "Debug")["Hexalith.FrontComposer.Shell"]
                .ShouldBe("4.99.0");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("%24%28UnresolvedVersion%29")]
    [InlineData("invalid-version")]
    [InlineData("3.8.0&#10;")]
    [InlineData("3.8.0&#13;&#10;")]
    public void MissingOrUnresolvedVersionsFailClosedForPropertiesAndPackages(string value)
    {
        string root = CreateFixture();
        try
        {
            File.WriteAllText(Path.Combine(root, "Consumer.csproj"), $"""
                <Project><PropertyGroup><HexalithEventStoreVersion>{value}</HexalithEventStoreVersion></PropertyGroup>
                <ItemGroup><PackageVersion Include="Hexalith.EventStore.Contracts" Version="$(HexalithEventStoreVersion)" /></ItemGroup></Project>
                """);

            Should.Throw<InvalidOperationException>(() => EffectivePackageVersions.ReadProperty(root, "HexalithEventStoreVersion", "Consumer.csproj"))
                .Message.ShouldContain("HexalithEventStoreVersion");
            Should.Throw<InvalidOperationException>(() => EffectivePackageVersions.ReadPackages(root, "Consumer.csproj"))
                .Message.ShouldContain("Hexalith.EventStore.Contracts");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingVersionProjectFailsWithItsIdentity()
    {
        string root = CreateFixture();
        try
        {
            Should.Throw<InvalidOperationException>(() => EffectivePackageVersions.ReadProperty(root, "HexalithEventStoreVersion", "Missing.csproj"))
                .Message.ShouldContain("Missing.csproj");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateFixture()
    {
        string root = Path.Combine(Path.GetTempPath(), $"parties-effective-versions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}

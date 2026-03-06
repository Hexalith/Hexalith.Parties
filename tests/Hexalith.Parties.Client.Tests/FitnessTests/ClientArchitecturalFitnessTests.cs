using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Parties.Client.Tests.FitnessTests;

public sealed class ClientArchitecturalFitnessTests
{
    [Fact]
    public void ClientAssembly_HasNoReferencesToServerProjectionsOrCommandApi()
    {
        Assembly clientAssembly = typeof(HttpPartiesCommandClient).Assembly;

        AssemblyName[] referencedAssemblies = clientAssembly.GetReferencedAssemblies();

        string[] forbiddenPrefixes =
        [
            "Hexalith.Parties.Server",
            "Hexalith.Parties.Projections",
            "Hexalith.Parties.CommandApi",
        ];

        List<string> violations = [];

        foreach (AssemblyName referenced in referencedAssemblies)
        {
            if (forbiddenPrefixes.Any(prefix =>
                referenced.Name!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add(referenced.Name!);
            }
        }

        violations.ShouldBeEmpty(
            $"Client assembly must not reference Server, Projections, or CommandApi. " +
            $"Found: {string.Join(", ", violations)}");
    }

    [Fact]
    public void ClientAssembly_HasNoReferencesToDaprOrMediatR()
    {
        Assembly clientAssembly = typeof(HttpPartiesCommandClient).Assembly;

        AssemblyName[] referencedAssemblies = clientAssembly.GetReferencedAssemblies();

        string[] forbiddenPrefixes =
        [
            "Dapr",
            "MediatR",
            "FluentValidation",
        ];

        List<string> violations = [];

        foreach (AssemblyName referenced in referencedAssemblies)
        {
            if (forbiddenPrefixes.Any(prefix =>
                referenced.Name!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add(referenced.Name!);
            }
        }

        violations.ShouldBeEmpty(
            $"Client assembly must not reference DAPR, MediatR, or FluentValidation. " +
            $"Found: {string.Join(", ", violations)}");
    }

    [Fact]
    public void ClientCsproj_HasNoForbiddenProjectReferences()
    {
        string testAssemblyDir = Path.GetDirectoryName(typeof(ClientArchitecturalFitnessTests).Assembly.Location)!;
        string repoRoot = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", "..", ".."));
        string clientCsprojPath = Path.Combine(repoRoot, "src", "Hexalith.Parties.Client", "Hexalith.Parties.Client.csproj");

        File.Exists(clientCsprojPath).ShouldBeTrue($"Client .csproj not found at {clientCsprojPath}");

        XDocument project = XDocument.Load(clientCsprojPath);

        List<string> declaredReferences =
        [
            .. project
                .Descendants()
                .Where(e => e.Name.LocalName is "ProjectReference" or "PackageReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!),
        ];

        string[] forbiddenReferences =
        [
            "Hexalith.Parties.Server",
            "Hexalith.Parties.Projections",
            "Hexalith.Parties.CommandApi",
            "Dapr",
            "MediatR",
            "FluentValidation",
        ];

        List<string> violations = [];

        foreach (string forbidden in forbiddenReferences)
        {
            if (declaredReferences.Any(reference => reference.Contains(forbidden, StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add(forbidden);
            }
        }

        violations.ShouldBeEmpty(
            $"Client .csproj must not reference forbidden packages. " +
            $"Found: {string.Join(", ", violations)}");
    }

    [Fact]
    public void ClientCsproj_ReferencesOnlyExpectedAbstractionPackages()
    {
        string testAssemblyDir = Path.GetDirectoryName(typeof(ClientArchitecturalFitnessTests).Assembly.Location)!;
        string repoRoot = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", "..", ".."));
        string clientCsprojPath = Path.Combine(repoRoot, "src", "Hexalith.Parties.Client", "Hexalith.Parties.Client.csproj");

        XDocument project = XDocument.Load(clientCsprojPath);

        string[] packageReferences =
        [
            .. project
                .Descendants()
                .Where(e => e.Name.LocalName == "PackageReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!),
        ];

        packageReferences.OrderBy(value => value).ShouldBe(
        [
            "Microsoft.Extensions.Http",
            "Microsoft.Extensions.Options",
        ]);
    }

    [Fact]
    public void IPartiesCommandClient_HasAllExpectedMethods()
    {
        Type interfaceType = typeof(Abstractions.IPartiesCommandClient);

        MethodInfo[] methods = interfaceType.GetMethods();

        string[] expectedMethods =
        [
            "CreatePartyAsync",
            "UpdatePersonDetailsAsync",
            "UpdateOrganizationDetailsAsync",
            "AddContactChannelAsync",
            "UpdateContactChannelAsync",
            "RemoveContactChannelAsync",
            "AddIdentifierAsync",
            "RemoveIdentifierAsync",
            "DeactivatePartyAsync",
            "ReactivatePartyAsync",
            "CreatePartyCompositeAsync",
            "UpdatePartyCompositeAsync",
            "SetIsNaturalPersonAsync",
        ];

        foreach (string expected in expectedMethods)
        {
            methods.ShouldContain(
                m => m.Name == expected,
                $"IPartiesCommandClient is missing method: {expected}");
        }

        methods.Length.ShouldBe(expectedMethods.Length,
            $"IPartiesCommandClient has {methods.Length} methods but expected {expectedMethods.Length}");
    }

    [Fact]
    public void IPartiesQueryClient_HasAllExpectedMethods()
    {
        Type interfaceType = typeof(Abstractions.IPartiesQueryClient);

        MethodInfo[] methods = interfaceType.GetMethods();

        string[] expectedMethods =
        [
            "GetPartyAsync",
            "ListPartiesAsync",
            "SearchPartiesAsync",
        ];

        foreach (string expected in expectedMethods)
        {
            methods.ShouldContain(
                m => m.Name == expected,
                $"IPartiesQueryClient is missing method: {expected}");
        }

        methods.Length.ShouldBe(expectedMethods.Length,
            $"IPartiesQueryClient has {methods.Length} methods but expected {expectedMethods.Length}");
    }

    [Fact]
    public void AllCommandMethods_ReturnTaskOfString()
    {
        Type interfaceType = typeof(Abstractions.IPartiesCommandClient);

        foreach (MethodInfo method in interfaceType.GetMethods())
        {
            method.ReturnType.ShouldBe(typeof(Task<string>),
                $"{method.Name} should return Task<string> but returns {method.ReturnType.Name}");
        }
    }

    [Fact]
    public void ClientCsproj_TransitiveDependenciesAreOnlySharedFrameworkPackages()
    {
        string testAssemblyDir = Path.GetDirectoryName(typeof(ClientArchitecturalFitnessTests).Assembly.Location)!;
        string repoRoot = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", "..", ".."));
        string assetsFilePath = Path.Combine(repoRoot, "src", "Hexalith.Parties.Client", "obj", "project.assets.json");

        File.Exists(assetsFilePath).ShouldBeTrue($"Client assets file not found at {assetsFilePath}");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(assetsFilePath));
        JsonElement targets = document.RootElement.GetProperty("targets");
        JsonProperty target = targets.EnumerateObject().Single(property => property.Name.StartsWith("net10.0", StringComparison.OrdinalIgnoreCase));

        HashSet<string> directPackageNames =
        [
            "Microsoft.Extensions.Http",
            "Microsoft.Extensions.Options",
            "MinVer",
        ];

        List<string> transitivePackageNames =
        [
            .. target.Value
                .EnumerateObject()
                .Where(property => property.Value.TryGetProperty("type", out JsonElement typeElement)
                    && string.Equals(typeElement.GetString(), "package", StringComparison.OrdinalIgnoreCase))
                .Select(property => property.Name.Split('/')[0])
                .Where(name => !directPackageNames.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
        ];

        transitivePackageNames.Count.ShouldBe(11,
            $"Expected the accepted Microsoft.Extensions transitive package set to remain stable. Found: {string.Join(", ", transitivePackageNames)}");

        List<string> violations =
        [
            .. transitivePackageNames.Where(name => !name.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase)),
        ];

        violations.ShouldBeEmpty(
            $"Client transitive packages must remain limited to Microsoft.Extensions shared framework packages. " +
            $"Found unexpected: {string.Join(", ", violations)}");
    }
}

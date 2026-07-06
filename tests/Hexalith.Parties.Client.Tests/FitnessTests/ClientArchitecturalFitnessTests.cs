using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.Parties.Client.Tests.FitnessTests;

public sealed class ClientArchitecturalFitnessTests
{
    [Fact]
    public void ClientAssembly_HasNoReferencesToServerProjectionsOrPartiesService()
    {
        Assembly clientAssembly = typeof(HttpPartiesCommandClient).Assembly;

        AssemblyName[] referencedAssemblies = clientAssembly.GetReferencedAssemblies();

        // Server/Projections roots match their entire subtree (e.g., a future
        // Hexalith.Parties.Server.Internal satellite must also be banned).
        // Bare "Hexalith.Parties" is exact-match-only because Hexalith.Parties.Contracts
        // is an allowed dependency.
        (string Name, bool AllowSubtree)[] forbiddenAssemblies =
        [
            ("Hexalith.Parties.Server", true),
            ("Hexalith.Parties.Projections", true),
            ("Hexalith.Parties", false),
        ];

        List<string> violations = [];

        foreach (AssemblyName referenced in referencedAssemblies)
        {
            if (forbiddenAssemblies.Any(entry => MatchesForbiddenName(referenced.Name, entry.Name, entry.AllowSubtree)))
            {
                violations.Add(referenced.Name!);
            }
        }

        violations.ShouldBeEmpty(
            $"Client assembly must not reference Server, Projections, or Parties service. " +
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
    public void ClientAssembly_ReferencesEventStoreContractsForGatewayBoundary()
    {
        Assembly clientAssembly = typeof(HttpPartiesCommandClient).Assembly;

        string[] referencedAssemblies =
        [
            .. clientAssembly.GetReferencedAssemblies()
                .Select(static assembly => assembly.Name)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Select(static name => name!),
        ];

        referencedAssemblies.ShouldContain("Hexalith.EventStore.Contracts");
        referencedAssemblies.ShouldNotContain("Hexalith.EventStore");
        referencedAssemblies.ShouldNotContain("Hexalith.EventStore.Server");
    }

    [Fact]
    public void ClientCsproj_HasNoForbiddenProjectReferences()
    {
        string repoRoot = LocateRepositoryRoot();
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

        // Server/Projections/Dapr/MediatR/FluentValidation match their entire subtree
        // (Dapr.Client, MediatR.Contracts, FluentValidation.AspNetCore, etc. are all banned).
        // Bare "Hexalith.Parties" is exact-match-only because Hexalith.Parties.Contracts is allowed.
        (string Name, bool AllowSubtree)[] forbiddenReferences =
        [
            ("Hexalith.Parties.Server", true),
            ("Hexalith.Parties.Projections", true),
            ("Hexalith.Parties", false),
            ("Dapr", true),
            ("MediatR", true),
            ("FluentValidation", true),
        ];

        List<string> violations = [];

        foreach ((string forbidden, bool allowSubtree) in forbiddenReferences)
        {
            if (declaredReferences.Any(reference => IsForbiddenReference(reference, forbidden, allowSubtree)))
            {
                violations.Add(forbidden);
            }
        }

        violations.ShouldBeEmpty(
            $"Client .csproj must not reference forbidden packages. " +
            $"Found: {string.Join(", ", violations)}");
    }

    [Fact]
    public void ClientCsproj_ReferencesEventStoreContractsWithoutServerProject()
    {
        string repoRoot = LocateRepositoryRoot();
        string clientCsprojPath = Path.Combine(repoRoot, "src", "Hexalith.Parties.Client", "Hexalith.Parties.Client.csproj");

        XDocument project = XDocument.Load(clientCsprojPath);

        string[] projectReferences =
        [
            .. project
                .Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Replace('\\', '/')),
        ];

        projectReferences.ShouldContain(reference =>
            reference.EndsWith(
                "Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj",
                StringComparison.OrdinalIgnoreCase));
        projectReferences.ShouldNotContain(reference =>
            reference.EndsWith(
                "Hexalith.EventStore/Hexalith.EventStore.csproj",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientCsproj_ReferencesOnlyExpectedAbstractionPackages()
    {
        string repoRoot = LocateRepositoryRoot();
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
            "Hexalith.Commons.Http",
            "Hexalith.EventStore.Contracts",
            "Microsoft.Extensions.Http",
            "Microsoft.Extensions.Options",
        ]);
    }

    [Fact]
    public void ClientSource_DoesNotContainRetiredPartiesRestRoutes()
    {
        string repoRoot = LocateRepositoryRoot();
        string sourceRoot = Path.Combine(repoRoot, "src", "Hexalith.Parties.Client");
        string source = string.Join(
            Environment.NewLine,
            Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        source.ShouldNotContain("api/v1/parties");
        source.ShouldNotContain("api/v1/admin");
        source.ShouldContain("api/v1/commands");
        source.ShouldContain("api/v1/queries");
    }

    [Fact]
    public void PartiesClientOptions_DocumentsBaseUrlAsEventStoreGateway()
    {
        string repoRoot = LocateRepositoryRoot();
        string optionsPath = Path.Combine(repoRoot, "src", "Hexalith.Parties.Client", "PartiesClientOptions.cs");
        string source = File.ReadAllText(optionsPath);

        source.ShouldContain("EventStore gateway base URL");
        source.ShouldNotContain("Parties service URL");
        source.ShouldNotContain("actor-host endpoint");
    }

    private static bool IsForbiddenReference(string reference, string forbiddenName, bool allowSubtree)
    {
        // A reference can be a project path ("..\..\Foo\Foo.csproj") or a bare package name ("Foo").
        // GetFileNameWithoutExtension on a bare package strips the last dot segment ("Foo.Bar" -> "Foo"),
        // so we only strip known project file extensions and then compare.
        string fileName = Path.GetFileName(reference);
        string normalized = StripProjectExtension(fileName);

        return MatchesForbiddenName(normalized, forbiddenName, allowSubtree)
            || MatchesForbiddenName(reference, forbiddenName, allowSubtree);
    }

    private static string StripProjectExtension(string value)
    {
        if (value.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return value[..^".csproj".Length];
        }

        if (value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return value[..^".dll".Length];
        }

        return value;
    }

    private static bool MatchesForbiddenName(string? candidate, string forbidden, bool allowSubtree)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        if (string.Equals(candidate, forbidden, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return allowSubtree
            && candidate.Length > forbidden.Length
            && candidate.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase)
            && candidate[forbidden.Length] == '.';
    }

    private static string LocateRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.Parties.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    [Fact]
    public void IPartiesCommandClient_HasAllExpectedMethods()
    {
        Type interfaceType = typeof(Abstractions.IPartiesCommandClient);

        MethodInfo[] methods = interfaceType.GetMethods();

        string[] expectedMethods =
        [
            "CreatePartyAsync",
            "CreatePartyWithResultAsync",
            "UpdatePersonDetailsAsync",
            "UpdatePersonDetailsWithResultAsync",
            "UpdateOrganizationDetailsAsync",
            "UpdateOrganizationDetailsWithResultAsync",
            "AddContactChannelAsync",
            "AddContactChannelWithResultAsync",
            "UpdateContactChannelAsync",
            "UpdateContactChannelWithResultAsync",
            "RemoveContactChannelAsync",
            "RemoveContactChannelWithResultAsync",
            "AddIdentifierAsync",
            "AddIdentifierWithResultAsync",
            "RemoveIdentifierAsync",
            "RemoveIdentifierWithResultAsync",
            "DeactivatePartyAsync",
            "DeactivatePartyWithResultAsync",
            "ReactivatePartyAsync",
            "ReactivatePartyWithResultAsync",
            "CreatePartyCompositeAsync",
            "CreatePartyCompositeWithResultAsync",
            "UpdatePartyCompositeAsync",
            "UpdatePartyCompositeWithResultAsync",
            "SetIsNaturalPersonAsync",
            "SetIsNaturalPersonWithResultAsync",
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
    public void IPartiesQueryClient_DoesNotExposeTemporalOrAdvancedSearchMethods()
    {
        Type interfaceType = typeof(Abstractions.IPartiesQueryClient);

        string[] methodNames = [.. interfaceType.GetMethods().Select(static method => method.Name)];

        methodNames.ShouldNotContain(name => name.Contains("Temporal", StringComparison.OrdinalIgnoreCase));
        methodNames.ShouldNotContain(name => name.Contains("NameAt", StringComparison.OrdinalIgnoreCase));
        methodNames.ShouldNotContain(name => name.Contains("Semantic", StringComparison.OrdinalIgnoreCase));
        methodNames.ShouldNotContain(name => name.Contains("Graph", StringComparison.OrdinalIgnoreCase));
        methodNames.ShouldNotContain(name => name.Contains("Hybrid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AllCommandMethods_ReturnExpectedTaskTypes()
    {
        Type interfaceType = typeof(Abstractions.IPartiesCommandClient);

        foreach (MethodInfo method in interfaceType.GetMethods())
        {
            Type expectedReturnType = method.Name.EndsWith("WithResultAsync", StringComparison.Ordinal)
                ? typeof(Task<Abstractions.PartiesCommandResult<Contracts.Models.PartyDetail>>)
                : typeof(Task<string>);

            method.ReturnType.ShouldBe(expectedReturnType,
                $"{method.Name} should return {expectedReturnType.Name} but returns {method.ReturnType.Name}");
        }
    }

    [Fact]
    public void ClientCsproj_TransitiveDependenciesAreOnlySharedFrameworkPackages()
    {
        string repoRoot = LocateRepositoryRoot();
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

        HashSet<string> allowedContractInfrastructurePackages =
        [
            "ByteAether.Ulid",
            "Dapr.Actors",
            "Dapr.Client",
            "Dapr.Common",
            "Dapr.Protos",
            "Google.Api.CommonProtos",
            "Google.Protobuf",
            "Grpc.Core.Api",
            "Grpc.Net.Client",
            "Grpc.Net.Common",
            "Hexalith.Commons.UniqueIds",
        ];

        List<string> violations =
        [
            .. transitivePackageNames.Where(name =>
                !name.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase)
                && !allowedContractInfrastructurePackages.Contains(name)),
        ];

        violations.ShouldBeEmpty(
            $"Client transitive packages must remain limited to Microsoft.Extensions shared framework packages plus the Dapr/Grpc/Protobuf actor-transport stack and identity infrastructure inherited through Hexalith.EventStore.Contracts. " +
            $"Found unexpected: {string.Join(", ", violations)}");
    }
}

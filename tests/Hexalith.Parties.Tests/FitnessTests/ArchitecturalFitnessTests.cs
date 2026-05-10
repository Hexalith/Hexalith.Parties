using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.Projections.Handlers;

using Microsoft.AspNetCore.Mvc;

using Shouldly;

namespace Hexalith.Parties.Tests.FitnessTests;

public sealed class ArchitecturalFitnessTests
{
    [Fact]
    public void PartiesAssembly_HasNoPublicRestControllerSurface()
    {
        Assembly partiesAssembly = typeof(Program).Assembly;

        List<string> violations = [];
        foreach (Type type in partiesAssembly.GetTypes().Where(t => t.Namespace?.StartsWith("Hexalith.Parties", StringComparison.Ordinal) == true))
        {
            if (type.GetCustomAttributes().Any(a => a.GetType().FullName == typeof(ApiControllerAttribute).FullName))
            {
                violations.Add($"{type.FullName} has [ApiController]");
            }

            if (typeof(ControllerBase).IsAssignableFrom(type))
            {
                violations.Add($"{type.FullName} derives from ControllerBase");
            }
        }

        violations.ShouldBeEmpty(
            "Hexalith.Parties is now an actor host; REST controllers must move behind EventStore-owned gateways.\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void PartiesAssembly_HasNoInProcessMcpToolSurface()
    {
        Assembly partiesAssembly = typeof(Program).Assembly;

        string[] forbiddenAttributeNames =
        [
            "ModelContextProtocol.Server.McpServerToolTypeAttribute",
            "ModelContextProtocol.Server.McpServerToolAttribute",
        ];

        List<string> violations = [];
        foreach (Type type in partiesAssembly.GetTypes().Where(t => t.Namespace?.StartsWith("Hexalith.Parties", StringComparison.Ordinal) == true))
        {
            foreach (CustomAttributeData attribute in type.CustomAttributes)
            {
                if (forbiddenAttributeNames.Contains(attribute.AttributeType.FullName, StringComparer.Ordinal))
                {
                    violations.Add($"{type.FullName} has {attribute.AttributeType.Name}");
                }
            }

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (CustomAttributeData attribute in method.CustomAttributes)
                {
                    if (forbiddenAttributeNames.Contains(attribute.AttributeType.FullName, StringComparer.Ordinal))
                    {
                        violations.Add($"{type.FullName}.{method.Name} has {attribute.AttributeType.Name}");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            "Hexalith.Parties must not host MCP tools in-process; Story 12.6 owns the new thin MCP host.\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void Program_SourceContainsOnlyActorHostMappingsAndDocumentedDaprInternalExceptions()
    {
        string source = ReadRepoFile("src", "Hexalith.Parties", "Program.cs");

        string[] forbiddenFragments =
        [
            "MapControllers",
            "MapMcp",
            "MapOpenApi",
            "UseSwaggerUI",
            "GdprWarningMiddleware",
            "MapGroup",
            "MapGet",
            "MapPost",
            "MapPut",
            "MapPatch",
            "MapDelete",
        ];

        List<string> violations = [.. forbiddenFragments.Where(fragment => source.Contains(fragment, StringComparison.Ordinal))];
        violations.ShouldBeEmpty(
            "Program.cs must not expose REST, MCP, OpenAPI, GDPR-header middleware, or public minimal API mappings: "
            + string.Join(", ", violations));

        source.ShouldContain("app.MapActorsHandlers()");
        source.ShouldContain("app.MapDefaultEndpoints()");
        source.ShouldContain("app.MapSubscribeHandler()");
        source.ShouldContain("app.MapTenantEventSubscription()");
        source.ShouldContain("DAPR sidecar-internal");
        source.ShouldContain("accesscontrol.parties.yaml");
    }

    [Fact]
    public void PartiesProject_RemovesRestOpenApiAndMcpHostPackages()
    {
        XDocument project = XDocument.Load(RepoPath("src", "Hexalith.Parties", "Hexalith.Parties.csproj"));

        string[] packageReferences =
        [
            .. project.Descendants()
                .Where(e => e.Name.LocalName == "PackageReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!),
        ];

        string[] forbiddenPackages =
        [
            "ModelContextProtocol.AspNetCore",
            "Microsoft.AspNetCore.OpenApi",
            "Swashbuckle.AspNetCore.SwaggerUI",
        ];

        List<string> violations = [.. forbiddenPackages.Where(package => packageReferences.Contains(package, StringComparer.OrdinalIgnoreCase))];
        violations.ShouldBeEmpty("Actor host project must not reference public REST/OpenAPI/MCP hosting packages: " + string.Join(", ", violations));
    }

    [Fact]
    public void PartiesSource_HasNoControllerOrMcpSurfaceMarkers()
    {
        string sourceRoot = Path.Combine(RepositoryRoot.Locate(), "src", "Hexalith.Parties");
        string[] sourceFiles = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);

        string[] forbiddenPatterns =
        [
            @"\[ApiController\]",
            @"\bControllerBase\b",
            @"\[Route\(",
            @"McpServerToolType",
            @"McpServerTool",
            @"AddMcpServer",
            @"WithToolsFromAssembly",
            @"MapMcp",
            @"MapControllers",
        ];

        List<string> violations = [];
        foreach (string file in sourceFiles)
        {
            string text = File.ReadAllText(file);
            foreach (string pattern in forbiddenPatterns)
            {
                if (Regex.IsMatch(text, pattern))
                {
                    violations.Add($"{Path.GetRelativePath(RepositoryRoot.Locate(), file)} contains {pattern}");
                }
            }
        }

        violations.ShouldBeEmpty("Forbidden public surface markers remain:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void PartiesAppHost_KeepsPartiesAppIdAndDedicatedDaprAccessControl()
    {
        string appHost = ReadRepoFile("src", "Hexalith.Parties.AppHost", "Program.cs");
        string partiesAccessControl = ReadRepoFile("src", "Hexalith.Parties.AppHost", "DaprComponents", "accesscontrol.parties.yaml");

        appHost.ShouldContain("AppId = \"parties\"");
        appHost.ShouldContain("accesscontrol.parties.yaml");
        partiesAccessControl.ShouldContain("defaultAction: deny");
        partiesAccessControl.ShouldContain("appId: eventstore");
        partiesAccessControl.ShouldContain("name: /process");
        partiesAccessControl.ShouldContain("httpVerb: ['POST']");
        partiesAccessControl.ShouldNotContain("appId: *");
        partiesAccessControl.ShouldNotContain("name: /**");
    }

    [Fact]
    public void ProjectionHandlers_HaveZeroDaprReferences()
    {
        Assembly projectionsAssembly = typeof(PartyDetailProjectionHandler).Assembly;

        Type[] handlerTypes = projectionsAssembly.GetTypes()
            .Where(t => t.Namespace == "Hexalith.Parties.Projections.Handlers")
            .ToArray();

        handlerTypes.ShouldNotBeEmpty("Expected projection handler types to exist");

        List<string> violations = [];

        foreach (Type handlerType in handlerTypes)
        {
            foreach (MethodInfo method in handlerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (IsDaprType(method.ReturnType))
                {
                    violations.Add($"{handlerType.Name}.{method.Name} returns DAPR type {method.ReturnType.FullName}");
                }

                foreach (ParameterInfo param in method.GetParameters())
                {
                    if (IsDaprType(param.ParameterType))
                    {
                        violations.Add($"{handlerType.Name}.{method.Name} param '{param.Name}' is DAPR type {param.ParameterType.FullName}");
                    }
                }

                foreach (Type localType in GetLocalVariableTypes(method))
                {
                    if (IsDaprType(localType))
                    {
                        violations.Add($"{handlerType.Name}.{method.Name} local variable is DAPR type {localType.FullName}");
                    }
                }
            }
        }

        violations.ShouldBeEmpty($"Projection handlers must not reference DAPR types. Violations:\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void ContractsProject_HasNoRuntimeDependenciesBeyondNetstandard()
    {
        Assembly contractsAssembly = typeof(Hexalith.Parties.Contracts.Commands.CreatePartyComposite).Assembly;

        AssemblyName[] referencedAssemblies = contractsAssembly.GetReferencedAssemblies();

        HashSet<string> allowedPrefixes =
        [
            "netstandard",
            "System",
            "Hexalith.EventStore.Contracts",
        ];

        List<string> violations = [];

        foreach (AssemblyName referenced in referencedAssemblies)
        {
            bool allowed = allowedPrefixes.Any(prefix =>
                referenced.Name!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            if (!allowed)
            {
                violations.Add(referenced.Name!);
            }
        }

        violations.ShouldBeEmpty(
            $"Contracts project should only depend on netstandard, System.*, and Hexalith.EventStore.Contracts. "
            + $"Found: {string.Join(", ", violations)}");
    }

    [Fact]
    public void ClientProject_HasNoReferencesToServerProjectionsOrPartiesService()
    {
        XDocument project = XDocument.Load(RepoPath("src", "Hexalith.Parties.Client", "Hexalith.Parties.Client.csproj"));

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
            "Hexalith.Parties",
        ];

        List<string> violations = [];

        foreach (string forbidden in forbiddenReferences)
        {
            if (declaredReferences.Any(reference => IsForbiddenClientReference(reference, forbidden)))
            {
                violations.Add(forbidden);
            }
        }

        violations.ShouldBeEmpty(
            "Client project must not reference Server, Projections, or Parties service. "
            + $"Found: {string.Join(", ", violations)}");
    }

    [Fact]
    public void PartiesProjectionActorsDoNotImplementEventStoreGenericProjectionContract()
    {
        string detailSource = ReadRepoFile("src", "Hexalith.Parties.Projections", "Actors", "PartyDetailProjectionActor.cs");
        string indexSource = ReadRepoFile("src", "Hexalith.Parties.Projections", "Actors", "PartyIndexProjectionActor.cs");

        Regex eventStoreContract = new(@"(?<!\w)(?:I?ProjectionActor)\b(?!\w)");
        Regex localPartyContract = new(@"\bIParty\w*ProjectionActor\b");

        bool DetectsEventStoreContract(string source)
        {
            string stripped = localPartyContract.Replace(source, string.Empty);
            return eventStoreContract.IsMatch(stripped);
        }

        DetectsEventStoreContract(detailSource).ShouldBeFalse(
            "PartyDetailProjectionActor must not implement EventStore's generic IProjectionActor contract.");
        DetectsEventStoreContract(indexSource).ShouldBeFalse(
            "PartyIndexProjectionActor must not implement EventStore's generic IProjectionActor contract.");
        detailSource.ShouldContain("IPartyDetailProjectionActor");
        indexSource.ShouldContain("IPartyIndexProjectionActor");
    }

    private static string ReadRepoFile(params string[] segments)
        => File.ReadAllText(RepoPath(segments));

    private static string RepoPath(params string[] segments)
        => Path.Combine([RepositoryRoot.Locate(), .. segments]);

    private static bool IsForbiddenClientReference(string reference, string forbiddenProjectName)
    {
        string fileName = Path.GetFileNameWithoutExtension(reference);
        return string.Equals(fileName, forbiddenProjectName, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<Type> GetLocalVariableTypes(MethodInfo method)
        => method
            .GetMethodBody()?
            .LocalVariables
            .Select(local => local.LocalType)
            .Where(type => type is not null)
            ?? [];

    private static bool IsDaprType(Type type)
    {
        Type checkType = Nullable.GetUnderlyingType(type) ?? type;

        if (checkType.IsGenericType)
        {
            return checkType.GetGenericArguments().Any(IsDaprType);
        }

        string? ns = checkType.Namespace;
        return ns is not null && ns.StartsWith("Dapr", StringComparison.Ordinal);
    }
}

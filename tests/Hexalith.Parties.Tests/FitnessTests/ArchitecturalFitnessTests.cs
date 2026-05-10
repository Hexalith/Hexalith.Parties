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

        // Story 12.2 Tasks/Subtasks: each retained sidecar-internal subscription mapping must be
        // documented with its route AND why it is not a client API.
        source.ShouldContain(
            "/dapr/subscribe",
            customMessage: "MapSubscribeHandler exception must name the route it exposes (POST /dapr/subscribe).");
        source.ShouldContain(
            "/tenants/events",
            customMessage: "MapTenantEventSubscription exception must name the route it exposes (POST /tenants/events).");
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

        // Wildcard guards must catch the canonical YAML quoted forms as well as the unquoted form.
        Regex wildcardAppId = new(@"appId:\s*['""]?\*['""]?");
        wildcardAppId.IsMatch(partiesAccessControl).ShouldBeFalse(
            "Parties access control must not allow wildcard appId (any quoted/unquoted form).");

        Regex wildcardOperationName = new(@"name:\s*['""]?/\*\*['""]?");
        wildcardOperationName.IsMatch(partiesAccessControl).ShouldBeFalse(
            "Parties access control must not expose a wildcard operation path (any quoted/unquoted form).");

        // Pub/sub event delivery is enforced by the pubsub component, not service-invocation ACL.
        // The YAML must document this explicitly so the Tenants -> Parties subscription route
        // (POST /tenants/events) is justified per Story 12.2 AC2.
        partiesAccessControl.ShouldContain(
            "pub/sub",
            customMessage: "Access-control YAML must document that pub/sub event delivery "
            + "(Tenants -> /tenants/events) is component-level, not service-invocation-level.");
    }

    [Fact]
    public void PartiesAssembly_DoesNotImplementEventStoreGatewayAuthorizationContracts()
    {
        string[] sourceFiles = EnumerateSourceFiles("src", "Hexalith.Parties");
        List<string> violations = [];

        // Match either single-base ":  ITenantValidator" or multi-base "..., ITenantValidator"
        // so a class declared as "Foo : BaseClass, ITenantValidator" cannot slip the gate.
        Regex tenantValidator = new(@"[:,]\s*(?:[\w\.]+\.)?ITenantValidator\b");
        Regex rbacValidator = new(@"[:,]\s*(?:[\w\.]+\.)?IRbacValidator\b");

        foreach (string file in sourceFiles)
        {
            // Strip line/block comments and string literals before matching so doc-comments,
            // raw-string templates, and incidental mentions cannot trigger false positives.
            string text = StripCommentsAndStringLiterals(File.ReadAllText(file));
            if (tenantValidator.IsMatch(text) || rbacValidator.IsMatch(text))
            {
                violations.Add(Path.GetRelativePath(RepositoryRoot.Locate(), file));
            }
        }

        violations.ShouldBeEmpty(
            "EventStore owns gateway tenant/RBAC validation; Hexalith.Parties must not implement those gateway contracts.\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void PartiesRequestPath_DoesNotUseTenantAccessServiceOrDenialTranslator()
    {
        string serviceRegistration = ReadRepoFile("src", "Hexalith.Parties", "Extensions", "PartiesServiceCollectionExtensions.cs");
        string domainInvoker = ReadRepoFile("src", "Hexalith.Parties", "Domain", "PartyDomainServiceInvoker.cs");
        string program = ReadRepoFile("src", "Hexalith.Parties", "Program.cs");

        // Gateway tenant/RBAC validators must never be wired by Parties; EventStore owns gateway
        // authorization and any Parties-side registration would split the boundary.
        StripCommentsAndStringLiterals(serviceRegistration).ShouldNotContain("ITenantValidator");
        StripCommentsAndStringLiterals(serviceRegistration).ShouldNotContain("IRbacValidator");

        // The legacy Parties tenant-access surfaces must stay out of the request path. The
        // ITenantAccessService registration in PartiesServiceCollectionExtensions.cs is permitted
        // and explicitly scoped to projection-side use (Story 12.3 AC4); see the comment beside
        // the registration. TenantAccessDenialTranslator was deleted as part of the same review.
        StripCommentsAndStringLiterals(domainInvoker).ShouldNotContain("ITenantAccessService");
        StripCommentsAndStringLiterals(domainInvoker).ShouldNotContain("TenantAccessDenialTranslator");
        StripCommentsAndStringLiterals(program).ShouldNotContain("ITenantAccessService");
        StripCommentsAndStringLiterals(program).ShouldNotContain("TenantAccessDenialTranslator");

        // Guard against TenantAccessDenialTranslator regressing anywhere in src/: it was deleted
        // as a request-path artifact and must not be reintroduced under any folder.
        string[] sourceFiles = EnumerateSourceFiles("src", "Hexalith.Parties");
        List<string> denialTranslatorViolations = [];
        foreach (string file in sourceFiles)
        {
            string text = StripCommentsAndStringLiterals(File.ReadAllText(file));
            if (text.Contains("TenantAccessDenialTranslator", StringComparison.Ordinal))
            {
                denialTranslatorViolations.Add(Path.GetRelativePath(RepositoryRoot.Locate(), file));
            }
        }

        denialTranslatorViolations.ShouldBeEmpty(
            "TenantAccessDenialTranslator was retired during Story 12.3; do not reintroduce it.\n"
            + string.Join("\n", denialTranslatorViolations));
    }

    [Fact]
    public void EventStoreGateway_AuthorizationBehaviorRunsBeforeValidationBehavior()
    {
        string? eventStoreRegistration = TryReadEventStoreFile(
            "src", "Hexalith.EventStore", "Extensions", "ServiceCollectionExtensions.cs");

        if (eventStoreRegistration is null)
        {
            // EventStore submodule is not initialised in this checkout (e.g., a CI lane that
            // builds Parties standalone). Skip the cross-repo source check rather than failing
            // for an environmental reason; the architectural boundary is still pinned by the
            // unit-level tests in the EventStore submodule itself.
            return;
        }

        int authorizationIndex = eventStoreRegistration.IndexOf(
            "cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>))",
            StringComparison.Ordinal);
        int validationIndex = eventStoreRegistration.IndexOf(
            "cfg.AddOpenBehavior(typeof(ValidationBehavior<,>))",
            StringComparison.Ordinal);

        authorizationIndex.ShouldBeGreaterThanOrEqualTo(0);
        validationIndex.ShouldBeGreaterThanOrEqualTo(0);
        authorizationIndex.ShouldBeLessThan(
            validationIndex,
            "EventStore gateway authorization must run before request validation so unauthorized invalid payloads are denied before Parties payload validation or actor/domain invocation.");
    }

    [Fact]
    public void EventStoreAggregateActor_TenantMismatchGuardPrecedesDomainInvocation()
    {
        string? aggregateActor = TryReadEventStoreFile(
            "src", "Hexalith.EventStore.Server", "Actors", "AggregateActor.cs");

        if (aggregateActor is null)
        {
            return;
        }

        int tenantValidationIndex = aggregateActor.IndexOf("tenantValidator.Validate(command.TenantId, Host.Id.GetId())", StringComparison.Ordinal);
        int domainInvocationIndex = aggregateActor.IndexOf(".InvokeAsync(command, currentState)", StringComparison.Ordinal);

        tenantValidationIndex.ShouldBeGreaterThanOrEqualTo(0);
        domainInvocationIndex.ShouldBeGreaterThanOrEqualTo(0);
        tenantValidationIndex.ShouldBeLessThan(
            domainInvocationIndex,
            "EventStore AggregateActor tenant mismatch defense must remain before domain actor invocation.");
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

    /// <summary>
    /// Reads a file from the Hexalith.EventStore submodule. Returns null when the submodule is
    /// not initialised (for CI lanes that build Parties standalone) so cross-repo source-text
    /// fitness checks can degrade gracefully instead of failing for environmental reasons.
    /// </summary>
    private static string? TryReadEventStoreFile(params string[] segments)
    {
        string path = RepoPath([.. new[] { "Hexalith.EventStore" }, .. segments]);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>
    /// Enumerates all .cs files under a repo-relative directory, excluding bin/, obj/, and
    /// generated outputs. Source-text fitness scans must use this enumeration so build artifacts
    /// (which contain copies of source through Razor codegen and similar) cannot trigger
    /// spurious violations or hide real ones.
    /// </summary>
    private static string[] EnumerateSourceFiles(params string[] segments)
    {
        string root = RepoPath(segments);
        return Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !ContainsSegment(path, "bin") && !ContainsSegment(path, "obj"))
            .ToArray();

        static bool ContainsSegment(string path, string segment)
        {
            string sep = Path.DirectorySeparatorChar.ToString();
            string altSep = Path.AltDirectorySeparatorChar.ToString();
            return path.Contains($"{sep}{segment}{sep}", StringComparison.Ordinal)
                || path.Contains($"{altSep}{segment}{altSep}", StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Strips C# line comments, block comments, and string literals from a source-text snippet
    /// so regex/contains checks operate on code structure only. Doc-comments, raw-string templates,
    /// and incidental string mentions are common false-positive sources for source-text fitness
    /// checks; this helper makes the matching robust without pulling in the full Roslyn dependency.
    /// </summary>
    private static string StripCommentsAndStringLiterals(string source)
    {
        var output = new System.Text.StringBuilder(source.Length);
        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];

            // Line comment
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            // Block comment
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                {
                    i++;
                }

                i = Math.Min(source.Length, i + 2);
                continue;
            }

            // Raw string literal (C# 11+): three or more " chars open, matching count closes.
            if (c == '"' && i + 2 < source.Length && source[i + 1] == '"' && source[i + 2] == '"')
            {
                int quoteCount = 0;
                while (i < source.Length && source[i] == '"')
                {
                    quoteCount++;
                    i++;
                }

                while (i < source.Length)
                {
                    if (source[i] == '"')
                    {
                        int run = 0;
                        while (i + run < source.Length && source[i + run] == '"')
                        {
                            run++;
                        }

                        if (run >= quoteCount)
                        {
                            i += run;
                            break;
                        }

                        i += run;
                    }
                    else
                    {
                        i++;
                    }
                }

                continue;
            }

            // Verbatim string literal: @"..."" (doubled quotes escape).
            if (c == '@' && i + 1 < source.Length && source[i + 1] == '"')
            {
                i += 2;
                while (i < source.Length)
                {
                    if (source[i] == '"' && i + 1 < source.Length && source[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }

                    if (source[i] == '"')
                    {
                        i++;
                        break;
                    }

                    i++;
                }

                continue;
            }

            // Regular string literal: "..." with backslash escapes.
            if (c == '"')
            {
                i++;
                while (i < source.Length && source[i] != '"')
                {
                    if (source[i] == '\\' && i + 1 < source.Length)
                    {
                        i += 2;
                        continue;
                    }

                    if (source[i] == '\n')
                    {
                        break;
                    }

                    i++;
                }

                if (i < source.Length)
                {
                    i++;
                }

                continue;
            }

            // Char literal: '\\'' / 'x'.
            if (c == '\'')
            {
                i++;
                while (i < source.Length && source[i] != '\'')
                {
                    if (source[i] == '\\' && i + 1 < source.Length)
                    {
                        i += 2;
                        continue;
                    }

                    if (source[i] == '\n')
                    {
                        break;
                    }

                    i++;
                }

                if (i < source.Length)
                {
                    i++;
                }

                continue;
            }

            output.Append(c);
            i++;
        }

        return output.ToString();
    }

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

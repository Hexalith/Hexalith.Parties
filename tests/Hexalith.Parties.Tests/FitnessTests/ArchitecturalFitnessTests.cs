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
    public void ServerTestProjects_DoNotRetainOldPartiesRestOrAdminAssertions()
    {
        string[] roots =
        [
            RepoPath("tests", "Hexalith.Parties.Tests"),
            RepoPath("tests", "Hexalith.Parties.IntegrationTests"),
        ];

        string[] forbiddenFragments =
        [
            "/api/v1/parties",
            "/api/v1/admin",
            "X-GDPR-Warning",
            "MapControllers",
            "MapMcp",
        ];

        List<string> violations = [];
        foreach (string root in roots)
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                         .Where(path => !ContainsSegment(path, "bin") && !ContainsSegment(path, "obj")))
            {
                if (Path.GetFileName(file).Equals(nameof(ArchitecturalFitnessTests) + ".cs", StringComparison.Ordinal))
                {
                    continue;
                }

                string text = File.ReadAllText(file);
                foreach (string fragment in forbiddenFragments)
                {
                    if (text.Contains(fragment, StringComparison.Ordinal))
                    {
                        violations.Add($"{Path.GetRelativePath(RepositoryRoot.Locate(), file)} contains {fragment}");
                    }
                }
            }
        }

        violations.ShouldBeEmpty(
            "Story 12.4 replaces the server-side REST/admin assertion surface with EventStore gateway tests. "
            + "Old REST/admin/GDPR-header assertions must not remain in server test projects.\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void RetiredServerFacingTestCoverage_IsDocumentedAndNotReintroduced()
    {
        List<string> violations = [];
        foreach (RetiredServerSurfaceCoverage row in RetiredServerSurfaceCoverageMatrix())
        {
            if (File.Exists(RepoPath(row.OldTestPath.Split('/'))))
            {
                violations.Add($"{row.OldTestPath} was reintroduced; replacement: {row.ReplacementOwner}.");
            }

            if (!string.IsNullOrWhiteSpace(row.ReplacementTestPath)
                && !File.Exists(RepoPath(row.ReplacementTestPath.Split('/'))))
            {
                violations.Add($"{row.OldTestPath} replacement path is missing: {row.ReplacementTestPath}.");
            }
        }

        violations.ShouldBeEmpty(
            "Story 12.4 retires old server-facing tests only when their replacement tier or future owner is documented.\n"
            + string.Join("\n", violations));
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
    }

    private static bool ContainsSegment(string path, string segment)
    {
        string sep = Path.DirectorySeparatorChar.ToString();
        string altSep = Path.AltDirectorySeparatorChar.ToString();
        return path.Contains($"{sep}{segment}{sep}", StringComparison.Ordinal)
            || path.Contains($"{altSep}{segment}{altSep}", StringComparison.Ordinal);
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

    private static RetiredServerSurfaceCoverage[] RetiredServerSurfaceCoverageMatrix() =>
    [
        Retired("tests/Hexalith.Parties.Tests/Controllers/AdminEndpointIntegrationTests.cs", "Tier-2 admin controller", "Retired transport; EventStore owns admin/public ingress.", "AC-12.4.1, AC-12.4.5, AC-12.4.8", "Architectural fitness and future EventStore admin tests", "tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs"),
        Retired("tests/Hexalith.Parties.Tests/Controllers/ConsentEndpointTests.cs", "Tier-2 command controller", "Consent domain behavior remains in aggregate/projection coverage; old response contract retired.", "AC-12.4.1, AC-12.4.4, AC-12.4.5", "Tier-1 domain/projection suites plus EventStore gateway smoke", "tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs"),
        Retired("tests/Hexalith.Parties.Tests/Controllers/CrossTenantIsolationTests.cs", "Tier-2 controller authorization", "Gateway tenant denial before Parties invocation.", "AC-12.4.1, AC-12.4.6", "EventStore gateway authorization tests", "tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs"),
        Retired("tests/Hexalith.Parties.Tests/Controllers/ErasureEndpointTests.cs", "Tier-2 command/query controller", "Erasure projection behavior retained without old controller response mapping.", "AC-12.4.1, AC-12.4.4, AC-12.4.5", "Tier-1 projection erasure coverage plus EventStore gateway smoke", "tests/Hexalith.Parties.Projections.Tests/Handlers/PartyIndexProjectionHandlerTests.cs"),
        Retired("tests/Hexalith.Parties.Tests/Controllers/KeyRotationEndpointTests.cs", "Tier-2 admin/security controller", "Security lifecycle belongs to security/domain tiers; old admin route retired.", "AC-12.4.1, AC-12.4.4, AC-12.4.5", "Security and architectural fitness suites", "tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs"),
        Retired("tests/Hexalith.Parties.Tests/Controllers/PartiesApiTestCollection.cs", "Tier-2 controller fixture", "Mutable controller fixture retired; gateway tests use deterministic per-test host.", "AC-12.4.1, AC-12.4.2", "EventStore gateway test factory", "tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs"),
        Retired("tests/Hexalith.Parties.Tests/Controllers/PartiesControllerProblemDetailsTests.cs", "Tier-2 controller error mapping", "EventStore owns validation response and rejection-before-invocation evidence.", "AC-12.4.1, AC-12.4.6", "EventStore gateway invalid-shape test", "tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs"),
        Retired("tests/Hexalith.Parties.Tests/Controllers/PartiesControllerTenantAuthorizationTests.cs", "Tier-2 controller authorization", "Gateway denies unauthorized tenants before command/query routing.", "AC-12.4.1, AC-12.4.6", "EventStore gateway authorization tests", "tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs"),
        Retired("tests/Hexalith.Parties.Tests/Controllers/PortabilityEndpointTests.cs", "Tier-2 GDPR controller", "Portability transport route retired; domain/projection data shape remains in Tier 1.", "AC-12.4.1, AC-12.4.4, AC-12.4.5", "Tier-1 contract/projection suites", "tests/Hexalith.Parties.Contracts.Tests/State/PartyStateTests.cs"),
        Retired("tests/Hexalith.Parties.Tests/Controllers/RestrictionEndpointTests.cs", "Tier-2 GDPR controller", "Restriction transport route retired; replacement behavior belongs to EventStore command contract.", "AC-12.4.1, AC-12.4.4, AC-12.4.5", "EventStore gateway smoke and future EventStore GDPR contract tests", "tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs"),
        Retired("tests/Hexalith.Parties.Tests/Controllers/StoryElevenThreeReviewPatchesTests.cs", "Tier-2 controller regression", "Old public-surface patch checks retired behind actor-host fitness.", "AC-12.4.1, AC-12.4.5, AC-12.4.8", "Architectural fitness", "tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs"),
        Retired("tests/Hexalith.Parties.Tests/Controllers/TemporalNameEndpointTests.cs", "Tier-2 query controller", "Temporal-name projection behavior remains outside old route contract.", "AC-12.4.1, AC-12.4.4", "Tier-1 projection suites plus EventStore query smoke", "tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs"),
        Retired("tests/Hexalith.Parties.Tests/Controllers/TenantActorIds.cs", "Tier-2 controller helper", "Controller-era tenant actor helper retired with controller fixture.", "AC-12.4.1, AC-12.4.8", "EventStore gateway deterministic test data", "tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs"),
        Retired("tests/Hexalith.Parties.Tests/Mcp/CreatePartyMcpToolTests.cs", "in-process MCP", "Future thin MCP host replacement is owned by Story 12.6.", "AC-12.4.5, AC-12.4.8", "Story 12.6 future MCP host tests", ""),
        Retired("tests/Hexalith.Parties.Tests/Mcp/DeletePartyMcpToolTests.cs", "in-process MCP", "Future thin MCP host replacement is owned by Story 12.6.", "AC-12.4.5, AC-12.4.8", "Story 12.6 future MCP host tests", ""),
        Retired("tests/Hexalith.Parties.Tests/Mcp/FindPartiesMcpToolTests.cs", "in-process MCP", "Future thin MCP host replacement is owned by Story 12.6.", "AC-12.4.5, AC-12.4.8", "Story 12.6 future MCP host tests", ""),
        Retired("tests/Hexalith.Parties.Tests/Mcp/GetPartyMcpToolTests.cs", "in-process MCP", "Future thin MCP host replacement is owned by Story 12.6.", "AC-12.4.5, AC-12.4.8", "Story 12.6 future MCP host tests", ""),
        Retired("tests/Hexalith.Parties.Tests/Mcp/GetPartyNameAtMcpToolTests.cs", "in-process MCP", "Future thin MCP host replacement is owned by Story 12.6.", "AC-12.4.5, AC-12.4.8", "Story 12.6 future MCP host tests", ""),
        Retired("tests/Hexalith.Parties.Tests/Mcp/McpSessionScope.cs", "in-process MCP helper", "Future thin MCP host replacement is owned by Story 12.6.", "AC-12.4.5, AC-12.4.8", "Story 12.6 future MCP host tests", ""),
        Retired("tests/Hexalith.Parties.Tests/Mcp/McpToolTenantAuthorizationTests.cs", "in-process MCP", "Future thin MCP host replacement is owned by Story 12.6.", "AC-12.4.5, AC-12.4.8", "Story 12.6 future MCP host tests", ""),
        Retired("tests/Hexalith.Parties.Tests/Mcp/McpToolTestServices.cs", "in-process MCP helper", "Future thin MCP host replacement is owned by Story 12.6.", "AC-12.4.5, AC-12.4.8", "Story 12.6 future MCP host tests", ""),
        Retired("tests/Hexalith.Parties.Tests/Mcp/UpdateAndDeletePartyMcpToolTests.cs", "in-process MCP", "Future thin MCP host replacement is owned by Story 12.6.", "AC-12.4.5, AC-12.4.8", "Story 12.6 future MCP host tests", ""),
        Retired("tests/Hexalith.Parties.Tests/Mcp/UpdatePartyMcpToolTests.cs", "in-process MCP", "Future thin MCP host replacement is owned by Story 12.6.", "AC-12.4.5, AC-12.4.8", "Story 12.6 future MCP host tests", ""),
        Retired("tests/Hexalith.Parties.IntegrationTests/Admin/AdminEndpointE2ETests.cs", "Tier-3 admin route", "Old admin route retired; EventStore owns future admin ingress.", "AC-12.4.3, AC-12.4.5", "Architectural fitness and future EventStore admin tests", "tests/Hexalith.Parties.Tests/FitnessTests/ArchitecturalFitnessTests.cs"),
        Retired("tests/Hexalith.Parties.IntegrationTests/PartyApiRoundTripIntegrationTests.cs", "Tier-3 Parties route", "Round-trip ingress moves to EventStore command/query gateway.", "AC-12.4.3, AC-12.4.5", "EventStore gateway routing tests", "tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs"),
        Retired("tests/Hexalith.Parties.IntegrationTests/Search/SemanticSearchE2ETests.cs", "Tier-3 search route", "Memories-backed search remains future EventStore query integration scope.", "AC-12.4.3, AC-12.4.4", "Future EventStore query integration tests", ""),
        Retired("tests/Hexalith.Parties.IntegrationTests/Search/TemporalNameE2ETests.cs", "Tier-3 query route", "Temporal-name query moves behind EventStore query gateway.", "AC-12.4.3, AC-12.4.4", "EventStore query smoke and future Tier-3 gateway tests", "tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs"),
        Retired("tests/Hexalith.Parties.IntegrationTests/Security/ConsentRestrictionE2ETests.cs", "Tier-3 security route", "Security/GDPR transport route retired; future EventStore contract tests own end-to-end ingress.", "AC-12.4.3, AC-12.4.4, AC-12.4.5", "Future EventStore security integration tests", ""),
        Retired("tests/Hexalith.Parties.IntegrationTests/Security/EncryptionE2ETests.cs", "Tier-3 security route", "Encryption evidence remains in security/domain tiers; old response contract retired.", "AC-12.4.3, AC-12.4.4, AC-12.4.5", "Security suites and future EventStore security integration tests", ""),
        Retired("tests/Hexalith.Parties.IntegrationTests/Security/ErasureE2ETests.cs", "Tier-3 security route", "Erasure projection behavior retained; old ingress contract retired.", "AC-12.4.3, AC-12.4.4, AC-12.4.5", "Tier-1 projection erasure and future EventStore security integration tests", "tests/Hexalith.Parties.Projections.Tests/Handlers/PartyIndexProjectionHandlerTests.cs"),
        Retired("tests/Hexalith.Parties.IntegrationTests/Security/KeyLifecycleE2ETests.cs", "Tier-3 security route", "Key lifecycle public ingress moves to EventStore/admin future coverage.", "AC-12.4.3, AC-12.4.4, AC-12.4.5", "Future EventStore security integration tests", ""),
        Retired("tests/Hexalith.Parties.IntegrationTests/Tenants/TenantIntegrationTestSeeder.cs", "Tier-3 REST seeder", "Seeder retired with old Parties HTTP client fixture.", "AC-12.4.3, AC-12.4.5", "EventStore gateway deterministic test data", "tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs"),
        Retired("tests/Hexalith.Parties.IntegrationTests/Tenants/TenantsBackedAccessE2ETests.cs", "Tier-3 tenant route", "Tenant denial remains in gateway and tenant projection tests.", "AC-12.4.3, AC-12.4.6", "EventStore gateway authorization and tenant access tests", "tests/Hexalith.Parties.Tests/Gateway/EventStoreGatewayRoutingTests.cs"),
    ];

    private static RetiredServerSurfaceCoverage Retired(
        string oldTestPath,
        string oldSurface,
        string retainedBehavior,
        string acceptanceCriteria,
        string replacementOwner,
        string replacementTestPath) =>
        new(oldTestPath, oldSurface, retainedBehavior, acceptanceCriteria, replacementOwner, replacementTestPath);

    private sealed record RetiredServerSurfaceCoverage(
        string OldTestPath,
        string OldSurface,
        string RetainedBehavior,
        string AcceptanceCriteria,
        string ReplacementOwner,
        string ReplacementTestPath);

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

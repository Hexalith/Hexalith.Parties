using System.Reflection;
using System.Xml.Linq;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.Parties.CommandApi.Mcp;
using Hexalith.Parties.Projections.Handlers;

using Shouldly;

namespace Hexalith.Parties.CommandApi.Tests.FitnessTests;

public sealed class ArchitecturalFitnessTests
{
    [Fact]
    public void McpNamespace_HasZeroReferencesToEventTypes()
    {
        Assembly mcpAssembly = typeof(GetPartyMcpTool).Assembly;

        Type[] mcpTypes = mcpAssembly.GetTypes()
            .Where(t => t.Namespace == "Hexalith.Parties.CommandApi.Mcp")
            .ToArray();

        mcpTypes.ShouldNotBeEmpty("Expected MCP types to exist in namespace");

        Type eventPayloadInterface = typeof(IEventPayload);
        Type rejectionEventInterface = typeof(IRejectionEvent);

        List<string> violations = [];

        foreach (Type mcpType in mcpTypes)
        {
            foreach (MethodInfo method in mcpType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (IsEventType(method.ReturnType, eventPayloadInterface, rejectionEventInterface))
                {
                    violations.Add($"{mcpType.Name}.{method.Name} returns event type {method.ReturnType.Name}");
                }

                foreach (ParameterInfo param in method.GetParameters())
                {
                    if (IsEventType(param.ParameterType, eventPayloadInterface, rejectionEventInterface))
                    {
                        violations.Add($"{mcpType.Name}.{method.Name} parameter '{param.Name}' is event type {param.ParameterType.Name}");
                    }
                }

                foreach (Type localType in GetLocalVariableTypes(method))
                {
                    if (IsEventType(localType, eventPayloadInterface, rejectionEventInterface))
                    {
                        violations.Add($"{mcpType.Name}.{method.Name} local variable is event type {localType.Name}");
                    }
                }
            }

            // Check fields
            foreach (FieldInfo field in mcpType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (IsEventType(field.FieldType, eventPayloadInterface, rejectionEventInterface))
                {
                    violations.Add($"{mcpType.Name}.{field.Name} is event type {field.FieldType.Name}");
                }
            }

            // Check properties
            foreach (PropertyInfo prop in mcpType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (IsEventType(prop.PropertyType, eventPayloadInterface, rejectionEventInterface))
                {
                    violations.Add($"{mcpType.Name}.{prop.Name} is event type {prop.PropertyType.Name}");
                }
            }
        }

        violations.ShouldBeEmpty($"MCP namespace must not reference event types. Violations:\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void McpNamespace_ReferencesOnlyCommandAndModelTypes()
    {
        Assembly mcpAssembly = typeof(GetPartyMcpTool).Assembly;

        Type[] mcpTypes = mcpAssembly.GetTypes()
            .Where(t => t.Namespace == "Hexalith.Parties.CommandApi.Mcp")
            .ToArray();

        HashSet<string> allowedNamespaces =
        [
            "Hexalith.Parties.Contracts.Commands",
            "Hexalith.Parties.Contracts.Models",
            "Hexalith.Parties.Contracts.ValueObjects",
            "Hexalith.Parties.Contracts.Search",
            "Hexalith.Parties.Projections.Abstractions",
            "Hexalith.Parties.Projections.Actors",
        ];

        List<string> violations = [];

        foreach (Type mcpType in mcpTypes)
        {
            foreach (MethodInfo method in mcpType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                CheckType(method.ReturnType, allowedNamespaces, violations, $"{mcpType.Name}.{method.Name} return type");

                foreach (ParameterInfo param in method.GetParameters())
                {
                    CheckType(param.ParameterType, allowedNamespaces, violations, $"{mcpType.Name}.{method.Name} param '{param.Name}'");
                }

                foreach (Type localType in GetLocalVariableTypes(method))
                {
                    CheckType(localType, allowedNamespaces, violations, $"{mcpType.Name}.{method.Name} local variable");
                }
            }

            foreach (FieldInfo field in mcpType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                CheckType(field.FieldType, allowedNamespaces, violations, $"{mcpType.Name}.{field.Name}");
            }

            foreach (PropertyInfo prop in mcpType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                CheckType(prop.PropertyType, allowedNamespaces, violations, $"{mcpType.Name}.{prop.Name}");
            }
        }

        violations.ShouldBeEmpty($"MCP namespace references forbidden Parties types:\n{string.Join("\n", violations)}");
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

            foreach (FieldInfo field in handlerType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (IsDaprType(field.FieldType))
                {
                    violations.Add($"{handlerType.Name}.{field.Name} is DAPR type {field.FieldType.FullName}");
                }
            }

            foreach (PropertyInfo prop in handlerType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (IsDaprType(prop.PropertyType))
                {
                    violations.Add($"{handlerType.Name}.{prop.Name} is DAPR type {prop.PropertyType.FullName}");
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
            $"Contracts project should only depend on netstandard, System.*, and Hexalith.EventStore.Contracts. " +
            $"Found: {string.Join(", ", violations)}");
    }

    [Fact]
    public void ClientProject_HasNoReferencesToServerProjectionsOrCommandApi()
    {
        // Verify declared client dependencies via project XML rather than raw substring matching.
        string testAssemblyDir = Path.GetDirectoryName(typeof(ArchitecturalFitnessTests).Assembly.Location)!;

        // Navigate from test output to repository root: bin/Debug/net10.0 -> tests/project -> root
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
            $"Client project must not reference Server, Projections, or CommandApi. " +
            $"Found: {string.Join(", ", violations)}");
    }

    private static IEnumerable<Type> GetLocalVariableTypes(MethodInfo method)
        => method
            .GetMethodBody()?
            .LocalVariables
            .Select(local => local.LocalType)
            .Where(type => type is not null)
            ?? [];

    private static bool IsEventType(Type type, Type eventPayloadInterface, Type rejectionEventInterface)
    {
        Type checkType = Nullable.GetUnderlyingType(type) ?? type;

        if (checkType.IsGenericType)
        {
            return checkType.GetGenericArguments().Any(arg => IsEventType(arg, eventPayloadInterface, rejectionEventInterface));
        }

        return eventPayloadInterface.IsAssignableFrom(checkType) || rejectionEventInterface.IsAssignableFrom(checkType);
    }

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

    private static void CheckType(Type type, HashSet<string> allowedNamespaces, List<string> violations, string context)
    {
        Type checkType = Nullable.GetUnderlyingType(type) ?? type;

        if (checkType.IsGenericType)
        {
            foreach (Type arg in checkType.GetGenericArguments())
            {
                CheckType(arg, allowedNamespaces, violations, context);
            }

            return;
        }

        string? ns = checkType.Namespace;
        if (ns is null || !ns.StartsWith("Hexalith.Parties.", StringComparison.Ordinal))
        {
            return;
        }

        // Skip types from the MCP namespace itself and from Search (internal helper)
        if (ns == "Hexalith.Parties.CommandApi.Mcp" || ns == "Hexalith.Parties.CommandApi.Search")
        {
            return;
        }

        if (!allowedNamespaces.Contains(ns))
        {
            violations.Add($"{context}: references {checkType.FullName} from forbidden namespace {ns}");
        }
    }
}

using System.Reflection;
using System.Runtime.CompilerServices;

using Hexalith.Parties.Contracts.Commands;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Package;

public sealed class ContractsPublicApiSnapshotTests
{
    private const string SnapshotRelativePath = "Package/ContractsPublicApiSnapshot.txt";

    [Fact]
    public void PublicContractSurface_MatchesCheckedInSnapshot()
    {
        string actual = string.Join(Environment.NewLine, BuildPublicApiSnapshot()) + Environment.NewLine;
        string snapshotPath = Path.Combine(FindProjectDirectory(), SnapshotRelativePath);

        if (string.Equals(Environment.GetEnvironmentVariable("UPDATE_CONTRACTS_API_SNAPSHOT"), "1", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            File.WriteAllText(snapshotPath, actual);
        }

        File.Exists(snapshotPath).ShouldBeTrue($"Missing public API snapshot at {snapshotPath}.");
        string expected = File.ReadAllText(snapshotPath);

        actual.ShouldBe(expected, "Public contract changes must be additive and intentional. Update the snapshot only with an explicit migration or additive-contract decision.");
    }

    private static IReadOnlyList<string> BuildPublicApiSnapshot()
    {
        Assembly assembly = typeof(CreateParty).Assembly;
        List<string> lines = [];

        foreach (Type type in assembly.GetExportedTypes().OrderBy(static type => type.FullName, StringComparer.Ordinal))
        {
            if (type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            {
                continue;
            }

            string typeKind = type.IsEnum ? "enum" :
                type.IsInterface ? "interface" :
                type.IsValueType ? "struct" :
                "class";
            string baseType = type.BaseType is null || type.BaseType == typeof(object)
                ? string.Empty
                : $" base:{FriendlyName(type.BaseType)}";
            string interfaces = string.Join(",", type.GetInterfaces().Select(FriendlyName).Order(StringComparer.Ordinal));
            lines.Add($"type {typeKind} {type.FullName}{baseType} interfaces:{interfaces}");

            if (type.IsEnum)
            {
                foreach (string name in Enum.GetNames(type).Order(StringComparer.Ordinal))
                {
                    object value = Enum.Parse(type, name);
                    lines.Add($"  enum {name}={(int)value}");
                }

                continue;
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                         .OrderBy(static constructor => string.Join(",", constructor.GetParameters().Select(parameter => parameter.Name)), StringComparer.Ordinal))
            {
                lines.Add($"  ctor ({string.Join(", ", constructor.GetParameters().Select(FormatParameter))})");
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .OrderBy(static property => property.Name, StringComparer.Ordinal))
            {
                string required = property.IsDefined(typeof(RequiredMemberAttribute), inherit: true) ? " required" : string.Empty;
                string setter = property.SetMethod is null ? " get" : " getset";
                lines.Add($"  property {FriendlyName(property.PropertyType)} {property.Name}{required}{setter}");
            }

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Where(static method => !method.IsSpecialName && !IsGeneratedRecordMethod(method))
                         .OrderBy(static method => method.Name, StringComparer.Ordinal))
            {
                lines.Add($"  method {FriendlyName(method.ReturnType)} {method.Name}({string.Join(", ", method.GetParameters().Select(FormatParameter))})");
            }
        }

        return lines;
    }

    private static string FormatParameter(ParameterInfo parameter)
        => $"{FriendlyName(parameter.ParameterType)} {parameter.Name}";

    private static bool IsGeneratedRecordMethod(MethodInfo method)
        => method.Name is "<Clone>$" or "Equals" or "GetHashCode" or "ToString";

    private static string FriendlyName(Type type)
    {
        if (type.IsGenericType)
        {
            string name = type.GetGenericTypeDefinition().FullName!;
            int tick = name.IndexOf('`', StringComparison.Ordinal);
            if (tick >= 0)
            {
                name = name[..tick];
            }

            return $"{name}<{string.Join(",", type.GetGenericArguments().Select(FriendlyName))}>";
        }

        return type.FullName ?? type.Name;
    }

    private static string FindProjectDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "tests", "Hexalith.Parties.Contracts.Tests");
            if (File.Exists(Path.Combine(candidate, "Hexalith.Parties.Contracts.Tests.csproj")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate Contracts test project directory.");
    }
}

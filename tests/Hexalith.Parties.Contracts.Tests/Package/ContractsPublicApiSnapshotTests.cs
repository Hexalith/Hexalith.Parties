using System.Reflection;
using System.Runtime.CompilerServices;

using Hexalith.Parties.Contracts.Commands;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.Package;

public sealed class ContractsPublicApiSnapshotTests
{
    private const string SnapshotRelativePath = "Package/ContractsPublicApiSnapshot.txt";
    private const string FrozenCoreLibraryIdentity = ", System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e";
    private const string CurrentCoreLibraryIdentity = ", System.Private.CoreLib, Version=11.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e";

    /// <summary>The two runtime-generated generic reference spellings preserve their frozen contract identity.</summary>
    [Fact]
    public void GenericByReferenceCoreLibraryIdentityRetainsTheFrozenSurface()
    {
        FriendlyName(typeof(DateTimeOffset?).MakeByRefType()).ShouldBe(
            "System.Nullable`1[[System.DateTimeOffset" + FrozenCoreLibraryIdentity + "]]&");
        FriendlyName(typeof(long?).MakeByRefType()).ShouldBe(
            "System.Nullable`1[[System.Int64" + FrozenCoreLibraryIdentity + "]]&");
        FriendlyName(typeof(long?)).ShouldBe("System.Nullable<System.Int64>");
        FriendlyName(typeof(long).MakeByRefType()).ShouldBe("System.Int64&");
    }

    /// <summary>Only the measured strong-named CoreLib 11 spelling is compatible with the frozen spelling.</summary>
    /// <param name="candidate">The qualified type spelling to compare.</param>
    /// <param name="compatible">Whether its complete signature must match the frozen signature.</param>
    [Theory]
    [MemberData(nameof(CoreLibraryCompatibilityCases))]
    public void GenericByReferenceIdentityRefusesUnqualifiedChanges(string candidate, bool compatible)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        string frozen = "System.Nullable`1[[System.Int64" + FrozenCoreLibraryIdentity + "]]&";
        string actual = CanonicalGenericByReferenceName(candidate);
        string.Equals(actual, frozen, StringComparison.Ordinal).ShouldBe(compatible);
    }

    /// <summary>Supplies historical/current successes and hostile framework, module and signature changes.</summary>
    public static TheoryData<string, bool> CoreLibraryCompatibilityCases()
    {
        string frozen = "System.Nullable`1[[System.Int64" + FrozenCoreLibraryIdentity + "]]&";
        string current = "System.Nullable`1[[System.Int64" + CurrentCoreLibraryIdentity + "]]&";
        return new()
        {
            { frozen, true },
            { current, true },
            { current.Replace("11.0.0.0", "12.0.0.0", StringComparison.Ordinal), false },
            { current.Replace("7cec85d7bea7798e", "0000000000000000", StringComparison.Ordinal), false },
            { current.Replace("Culture=neutral", "Culture=en-US", StringComparison.Ordinal), false },
            { current.Replace("System.Private.CoreLib", "Foreign.CoreLib", StringComparison.Ordinal), false },
            { current.Replace("System.Int64", "System.Int32", StringComparison.Ordinal), false },
            { current.Replace("System.Nullable`1", "Foreign.Nullable`1", StringComparison.Ordinal), false },
            { current.TrimEnd('&'), false },
            { current.Replace("System.Private.CoreLib", "Hexalith.Parties.Contracts", StringComparison.Ordinal), false },
        };
    }

    /// <summary>Framework compatibility cannot conceal a module-owned generic argument's assembly version.</summary>
    [Fact]
    public void GenericByReferenceIdentityPreservesModuleAssemblyVersions()
    {
        const string moduleIdentity = ", Hexalith.Parties.Contracts, Version=10.0.0.0, Culture=neutral, PublicKeyToken=null";
        string frozen = "Example.Pair`2[[System.Int64" + FrozenCoreLibraryIdentity
            + "],[Hexalith.Parties.Contracts.Models.PartyDetail" + moduleIdentity + "]]&";
        string current = frozen.Replace(FrozenCoreLibraryIdentity, CurrentCoreLibraryIdentity, StringComparison.Ordinal);
        CanonicalGenericByReferenceName(current).ShouldBe(frozen);

        string changedModule = current.Replace(moduleIdentity,
            moduleIdentity.Replace("10.0.0.0", "11.0.0.0", StringComparison.Ordinal), StringComparison.Ordinal);
        CanonicalGenericByReferenceName(changedModule).ShouldNotBe(frozen);
    }

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
        string expected = File.ReadAllText(snapshotPath).ReplaceLineEndings(Environment.NewLine);

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
#if HEXALITH_EVENTSTORE_CATALOG_API
                // The stable package surface excludes development-only EventStore schemas.
                // PartiesCatalogTests separately qualifies their source-selected shape and policy.
                if (type == typeof(Contracts.Routing.PartiesCatalogDeclarations)
                    && property.Name == nameof(Contracts.Routing.PartiesCatalogDeclarations.Operations))
                {
                    continue;
                }
#endif
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
        if (type.IsByRef && type.GetElementType()!.IsGenericType)
        {
            // FullName embeds the executing runtime's strong-named CoreLib identity here.
            // Preserve the frozen spelling for the independently measured NET11 identity;
            // every other assembly/version, element type and by-reference marker stays visible.
            return CanonicalGenericByReferenceName(type.FullName ?? type.Name);
        }

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

    private static string CanonicalGenericByReferenceName(string name)
        => name.Replace(CurrentCoreLibraryIdentity, FrozenCoreLibraryIdentity, StringComparison.Ordinal);

    private static string FindProjectDirectory([CallerFilePath] string sourceFile = "")
    {
        // SDK --artifacts-path places binaries outside the repository, and the
        // runner may change its working directory to the assembly directory.
        string sourceCandidate = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, ".."));
        if (File.Exists(Path.Combine(sourceCandidate, "Hexalith.Parties.Contracts.Tests.csproj")))
        {
            return sourceCandidate;
        }

        string currentCandidate = Path.Combine(Directory.GetCurrentDirectory(), "tests", "Hexalith.Parties.Contracts.Tests");
        if (File.Exists(Path.Combine(currentCandidate, "Hexalith.Parties.Contracts.Tests.csproj")))
        {
            return currentCandidate;
        }

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

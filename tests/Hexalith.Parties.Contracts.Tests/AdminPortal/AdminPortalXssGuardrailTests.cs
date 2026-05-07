// ATDD red-phase XSS guardrail scaffolds for Story 10.1 — Admin Portal.
// AC6: rendered party fields may contain user-supplied or AI-created data. The portal
// must output-encode every field via standard Razor binding and never use MarkupString,
// AddMarkupContent, raw HTML fragments, or untrusted-data JS interop bridges. These
// fitness checks scan the AdminPortal assembly for forbidden API usage; they remain
// skipped until the assembly exists in green phase.

using System.Linq;
using System.Reflection;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.AdminPortal;

/// <summary>
/// Story 10.1 — AC6 + Implementation Guardrails. Reflective scans that prevent the admin
/// portal from rendering party data through unsafe Razor seams.
/// </summary>
public sealed class AdminPortalXssGuardrailTests
{
    private const string AdminPortalAssemblyName = "Hexalith.Parties.AdminPortal";
    private const string MarkupStringFullName = "Microsoft.AspNetCore.Components.MarkupString";

    [Fact]
    public void AdminPortal_DoesNotDeclareMarkupStringFields()
    {
        // AC6: forbid any field/property typed as MarkupString — reduces the surface where a
        // future maintainer can accidentally bind unencoded party data into the rendered DOM.
        Assembly portal = LoadPortalAssembly();

        IEnumerable<MemberInfo> markupCarriers = portal.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Cast<MemberInfo>()
                .Concat(t.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)))
            .Where(IsMarkupStringMember);

        markupCarriers.ShouldBeEmpty(
            "AdminPortal must not declare MarkupString fields/properties for party data rendering.");
    }

    [Fact]
    public void AdminPortal_SourceDoesNotUseUnsafeRawRenderingApis()
    {
        string repoRoot = FindRepoRoot();
        string portalRoot = Path.Combine(repoRoot, "src", "Hexalith.Parties.AdminPortal");
        string[] sourceFiles = Directory.GetFiles(portalRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        string[] forbidden =
        [
            "MarkupString",
            "AddMarkupContent",
            "innerHTML",
            "AddContent(0, (MarkupString)",
        ];

        foreach (string file in sourceFiles)
        {
            string contents = File.ReadAllText(file);
            foreach (string token in forbidden)
            {
                contents.Contains(token, StringComparison.OrdinalIgnoreCase)
                    .ShouldBeFalse($"AdminPortal source file '{file}' must not use unsafe raw rendering token '{token}'.");
            }
        }
    }

    [Fact]
    public void AdminPortal_HasNoJsInteropBridgeForRawPartyHtml()
    {
        // Implementation Guardrails: do not pipe party values through JS interop scripts
        // that could reintroduce stored XSS by setting innerHTML or eval-equivalent paths.
        Assembly portal = LoadPortalAssembly();

        string[] forbiddenInteropMembers =
        [
            "innerHTML",
            "outerHTML",
            "eval",
            "setHtmlUnsafe",
        ];

        IEnumerable<string> stringConstants = portal.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string?)f.GetRawConstantValue() ?? string.Empty);

        foreach (string literal in stringConstants)
        {
            foreach (string forbidden in forbiddenInteropMembers)
            {
                literal.Contains(forbidden, StringComparison.Ordinal)
                    .ShouldBeFalse($"AdminPortal exposes a JS interop literal containing '{forbidden}'.");
            }
        }
    }

    private static bool IsMarkupStringMember(MemberInfo member)
    {
        Type? memberType = member switch
        {
            FieldInfo f => f.FieldType,
            PropertyInfo p => p.PropertyType,
            _ => null,
        };

        return memberType?.FullName == MarkupStringFullName;
    }

    private static Assembly LoadPortalAssembly()
    {
        Assembly? loaded = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, AdminPortalAssemblyName, StringComparison.Ordinal));

        return loaded ?? Assembly.Load(new AssemblyName(AdminPortalAssemblyName));
    }

    private static string FindRepoRoot()
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

        throw new InvalidOperationException("Could not locate repository root.");
    }
}

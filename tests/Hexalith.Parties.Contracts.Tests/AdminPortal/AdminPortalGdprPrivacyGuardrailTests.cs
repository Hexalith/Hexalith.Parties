// ATDD red-phase privacy and XSS guardrail scaffolds for Story 10.2.
// GDPR operation screens render untrusted party fields, consent purposes, processing
// summaries, ProblemDetails details, status text, and export metadata. These tests
// pin encoded rendering and safe storage/download behavior.

using System.Linq;
using System.Reflection;

using Shouldly;

namespace Hexalith.Parties.Contracts.Tests.AdminPortal;

/// <summary>
/// Story 10.2 — AC6, AC8, and AC9. The GDPR portal must never render backend or
/// user-created text through raw markup seams, and must not persist PII in storage keys,
/// logs, telemetry names, or export filenames.
/// </summary>
public sealed class AdminPortalGdprPrivacyGuardrailTests
{
    private const string AdminPortalAssemblyName = "Hexalith.Parties.AdminPortal";
    private const string MarkupStringFullName = "Microsoft.AspNetCore.Components.MarkupString";

    [Fact]
    public void GdprComponents_DoNotDeclareMarkupStringFieldsOrProperties()
    {
        Assembly portal = LoadPortalAssembly();

        IEnumerable<MemberInfo> markupMembers = portal.GetTypes()
            .Where(IsGdprType)
            .SelectMany(t => t.GetFields(AllMembers).Cast<MemberInfo>()
                .Concat(t.GetProperties(AllMembers)))
            .Where(IsMarkupStringMember);

        markupMembers.ShouldBeEmpty(
            "GDPR portal components must render party, consent, ProblemDetails, processing, and status text as encoded text.");
    }

    [Fact]
    public void GdprComponents_DoNotInvokeRawMarkupRenderApis()
    {
        string root = FindRepositoryRoot();
        IEnumerable<string> offenders = Directory.EnumerateFiles(
                Path.Combine(root, "src", "Hexalith.Parties.AdminPortal"),
                "*.*",
                SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => IsGdprSource(path))
            .Where(path =>
            {
                string text = File.ReadAllText(path);
                return text.Contains("MarkupString", StringComparison.Ordinal)
                    || text.Contains("AddMarkupContent", StringComparison.Ordinal)
                    || text.Contains("RenderTreeBuilder", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(root, path));

        offenders.ShouldBeEmpty(
            "GDPR portal components must not call AddMarkupContent or render MarkupString.");
    }

    [Fact]
    public void GdprComponents_DoNotExposeUnsafeJsInteropHtmlPaths()
    {
        Assembly portal = LoadPortalAssembly();

        string[] forbidden =
        [
            "innerHTML",
            "outerHTML",
            "eval",
            "setHtmlUnsafe",
            "downloadPreviewHtml",
        ];

        foreach (string literal in StringConstants(portal).Where(s => IsGdprLiteral(s)))
        {
            foreach (string forbiddenLiteral in forbidden)
            {
                literal.Contains(forbiddenLiteral, StringComparison.Ordinal)
                    .ShouldBeFalse($"GDPR portal exposes unsafe JS/HTML literal '{forbiddenLiteral}'.");
            }
        }
    }

    [Fact]
    public void GdprDownloadFilenameBuilder_UsesOnlyNonPiiIdentifiers()
    {
        Type filenameBuilder = LoadPortalAssembly().GetTypes()
            .FirstOrDefault(t => t.Name == "GdprExportFileNameBuilder")
            ?? throw new InvalidOperationException("AdminPortal must expose GdprExportFileNameBuilder.");

        MethodInfo method = filenameBuilder.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("GdprExportFileNameBuilder must expose static Build.");

        string[] parameterNames = method.GetParameters().Select(p => p.Name ?? string.Empty).ToArray();
        parameterNames.ShouldContain("partyId");
        parameterNames.ShouldNotContain("displayName");
        parameterNames.ShouldNotContain("email");
        parameterNames.ShouldNotContain("identifier");
        parameterNames.ShouldNotContain("purpose");
    }

    [Fact]
    public void GdprStorageAndTelemetryKeys_DoNotContainPiiFragments()
    {
        Assembly portal = LoadPortalAssembly();

        string[] forbiddenFragments =
        [
            "displayName",
            "sortName",
            "email",
            "phone",
            "contactValue",
            "identifierValue",
            "consentPurpose",
            "jwt",
            "claims",
            "membership",
            "problemDetails.detail",
        ];

        foreach (string literal in StringConstants(portal).Where(IsGdprLiteral))
        {
            foreach (string forbidden in forbiddenFragments)
            {
                literal.Contains(forbidden, StringComparison.OrdinalIgnoreCase)
                    .ShouldBeFalse($"GDPR portal literal '{literal}' may leak PII/sensitive data through '{forbidden}'.");
            }
        }
    }

    private static BindingFlags AllMembers =>
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static Assembly LoadPortalAssembly()
    {
        Assembly? loaded = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, AdminPortalAssemblyName, StringComparison.Ordinal));

        return loaded ?? Assembly.Load(new AssemblyName(AdminPortalAssemblyName));
    }

    private static bool IsGdprType(Type type)
        => type.FullName?.Contains("Gdpr", StringComparison.OrdinalIgnoreCase) == true
            || type.Name.Contains("Erasure", StringComparison.OrdinalIgnoreCase)
            || type.Name.Contains("Consent", StringComparison.OrdinalIgnoreCase)
            || type.Name.Contains("Restriction", StringComparison.OrdinalIgnoreCase)
            || type.Name.Contains("Portability", StringComparison.OrdinalIgnoreCase)
            || type.Name.Contains("Processing", StringComparison.OrdinalIgnoreCase)
            || type.Name.Contains("Dpo", StringComparison.OrdinalIgnoreCase);

    private static bool IsMarkupStringMember(MemberInfo member)
    {
        Type? memberType = member switch
        {
            FieldInfo field => field.FieldType,
            PropertyInfo property => property.PropertyType,
            _ => null,
        };

        return memberType?.FullName == MarkupStringFullName;
    }

    private static bool IsGdprSource(string path)
        => IsGdprLiteral(Path.GetFileNameWithoutExtension(path));

    private static IEnumerable<string> StringConstants(Assembly assembly)
        => assembly.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string?)f.GetRawConstantValue() ?? string.Empty);

    private static bool IsGdprLiteral(string value)
        => value.Contains("gdpr", StringComparison.OrdinalIgnoreCase)
            || value.Contains("erasure", StringComparison.OrdinalIgnoreCase)
            || value.Contains("consent", StringComparison.OrdinalIgnoreCase)
            || value.Contains("restriction", StringComparison.OrdinalIgnoreCase)
            || value.Contains("export", StringComparison.OrdinalIgnoreCase)
            || value.Contains("processing", StringComparison.OrdinalIgnoreCase)
            || value.Contains("dpo", StringComparison.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "Hexalith.Parties.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
